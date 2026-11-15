#if DEBUG
using System;
using System.IO;
using System.Text;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer
{
    /// <summary>All permanent-delete tests use a unique disposable test root, NEVER live Config.</summary>
    internal static class ProfileTrashPurgeSelfTests
    {
        private static int checks;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("R6P1C2D purge: " + label);
            checks++;
        }

        private static ProfileDocument Make(string id, string name)
        {
            XpCustomizerSettings detached = new XpCustomizerSettings();
            detached.careerXpMultiplier = 1.56f;
            return ProfileMapper.Export(detached, name, "1.6.4871",
                new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc), id);
        }

        private static ProfileStorageResult ToTrash(ProfileFileRepository repository,
            ProfileDocument document)
        {
            ProfileStorageResult created = repository.Create(document);
            Check(created.Success, "test file create");
            ProfileStorageResult deleted = repository.SoftDelete(document.ProfileId, created.ContentHash);
            Check(deleted.Success && deleted.TrashToken != null, "test soft delete");
            return deleted;
        }

        internal static void Run()
        {
            string dir = Path.Combine(Path.GetTempPath(), "XPC-R6P1C2D-Isolated-" +
                Guid.NewGuid().ToString("N"));
            checks = 0;
            bool finished = false;
            XpCustomizerSettings live = XpCustomizerMod.Settings;
            float originalMultiplier = live == null ? 0f : live.careerXpMultiplier;
            object originalDict = live == null ? null : live.skillSpecificXpMultipliers;
            try
            {
                Directory.CreateDirectory(dir);
                ProfileStoragePaths paths = ProfileStoragePaths.ForIsolatedTestRoot(dir);
                ProfileFileRepository repository = new ProfileFileRepository(paths, false);
                ProfileTrashActionService actions = new ProfileTrashActionService(delegate { return repository; });
                string id1 = "c3a44a37-52fd-4870-a872-fd119dc00001";
                string id2 = "c3a44a37-52fd-4870-a872-fd119dc00002";
                string id3 = "c3a44a37-52fd-4870-a872-fd119dc00003";
                string id4 = "c3a44a37-52fd-4870-a872-fd119dc00004";
                ProfileDocument d1 = Make(id1, "same name");
                ProfileDocument d2 = Make(id2, "same name");
                ProfileDocument d3 = Make(id3, "sample 3");
                ProfileDocument d4 = Make(id4, "sample 4");
                ProfileTrashPurgeResult empty = actions.PrepareEmptyTrash();
                Check(empty.State == ProfileTrashPurgeState.NoOp && empty.Requested == 0,
                    "empty trash no-op");
                Check(!Directory.Exists(paths.Trash), "no folder created for empty list");
                Check(actions.EmptyTrash(null).State == ProfileTrashPurgeState.Rejected,
                    "cannot confirm null plan");
                ProfileStorageResult t1 = ToTrash(repository, d1);
                ProfileStorageResult t2 = ToTrash(repository, d2);
                ProfileStorageResult t3 = ToTrash(repository, d3);
                string sentinelXml = Path.Combine(dir, "Mod_XpCustomizer.xml");
                File.WriteAllText(sentinelXml, "do-not-write-config", Utf8);
                string activeJson = paths.FileForId(id4);
                Check(repository.Create(d4).Success, "one active file");
                byte[] activeBefore = File.ReadAllBytes(activeJson);
                string ignored = Path.Combine(paths.Trash, "ignore.txt");
                File.WriteAllText(ignored, "not a trash profile", Utf8);
                Directory.CreateDirectory(paths.Backups);
                string backup = Path.Combine(paths.Backups, "do-not-delete.bak");
                File.WriteAllText(backup, "unchanged-backup", Utf8);
                string otherMod = Path.Combine(dir, "another-mod.txt");
                File.WriteAllText(otherMod, "unchanged-other-mod", Utf8);
                ProfileTrashPurgeResult ready = actions.PrepareEmptyTrash();
                Check(ready.State == ProfileTrashPurgeState.Ready && ready.Plan.Count == 3 &&
                    ready.Plan.Fingerprint.Length == 64, "3-entry approved snapshot");
                Check(File.Exists(Path.Combine(paths.Trash, t1.TrashToken)) &&
                    File.Exists(Path.Combine(paths.Trash, t2.TrashToken)), "prepare read only");

                // A newly trashed profile invalidates the previously approved set.
                ProfileStorageResult newEntry = repository.SoftDelete(id4,
                    repository.Load(id4).ContentHash);
                Check(newEntry.Success, "external addition for conflict");
                Check(actions.EmptyTrash(ready.Plan).State == ProfileTrashPurgeState.Rejected,
                    "new file conflicts with frozen approval");
                Check(repository.ListTrash().Entries.Count == 4, "conflict deleted nothing");
                Check(repository.Restore(newEntry.TrashToken, newEntry.ContentHash).Success,
                    "restore extra test entry");
                ready = actions.PrepareEmptyTrash();

                // Modified JSON is a conflict, even when its name/token is unchanged.
                string firstFile = Path.Combine(paths.Trash, t1.TrashToken);
                byte[] original = File.ReadAllBytes(firstFile);
                File.WriteAllText(firstFile, "{bad-json", Utf8);
                Check(actions.EmptyTrash(ready.Plan).State == ProfileTrashPurgeState.Rejected,
                    "invalid changed bytes cannot be purged");
                Check(actions.PrepareEmptyTrash().State == ProfileTrashPurgeState.Rejected,
                    "invalid JSON blocks entire trash");
                File.WriteAllBytes(firstFile, original);
                ready = actions.PrepareEmptyTrash();
                Check(ready.State == ProfileTrashPurgeState.Ready, "valid restored after mismatch");
                // Valid JSON with different bytes must also be rejected (SHA collision policy).
                File.WriteAllText(firstFile,
                    Utf8.GetString(original).Replace("same name", "modified name"), Utf8);
                Check(actions.EmptyTrash(ready.Plan).State == ProfileTrashPurgeState.Rejected,
                    "valid JSON with stale SHA rejected without delete");
                File.WriteAllBytes(firstFile, original);
                ready = actions.PrepareEmptyTrash();
                string raw = Utf8.GetString(original);
                string future = raw.Replace("\"formatVersion\":2", "\"formatVersion\":3");
                Check(future != raw, "future-version fixture contains correct schema field");
                File.WriteAllText(firstFile, future, Utf8);
                Check(actions.PrepareEmptyTrash().State == ProfileTrashPurgeState.Rejected,
                    "future schema blocks entire trash purge");
                File.WriteAllBytes(firstFile, original);
                ready = actions.PrepareEmptyTrash();
                File.WriteAllBytes(firstFile, File.ReadAllBytes(Path.Combine(paths.Trash, t2.TrashToken)));
                Check(actions.PrepareEmptyTrash().State == ProfileTrashPurgeState.Rejected,
                    "profile ID mismatch blocks entire purge");
                File.WriteAllBytes(firstFile, original);
                ready = actions.PrepareEmptyTrash();
                File.WriteAllText(Path.Combine(paths.Trash, "invalid.deleted"), "bad", Utf8);
                Check(actions.PrepareEmptyTrash().State == ProfileTrashPurgeState.Rejected,
                    "invalid trash token blocks purge");
                File.Delete(Path.Combine(paths.Trash, "invalid.deleted"));
                ready = actions.PrepareEmptyTrash();
                Check(ready.State == ProfileTrashPurgeState.Ready, "valid after invalid token removed");

                // Fault before the first delete is a safe rejection.
                ProfileTrashPurgeResult failFirst = actions.EmptyTrash(ready.Plan, injectBeforeIndex: 0);
                Check(failFirst.State == ProfileTrashPurgeState.Rejected &&
                    failFirst.DeletedVerified == 0 && !ProfileWriteSessionGate.IsBlocked,
                    "before-first failure is no-write");
                Check(repository.ListTrash().Entries.Count == 3, "before-first preserves all");
                ProfileTrashPurgeResult partial = actions.EmptyTrash(ready.Plan, injectBeforeIndex: 1);
                Check(partial.State == ProfileTrashPurgeState.Partial &&
                    partial.DeletedVerified == 1 && partial.Code == ProfileStorageCode.RecoveryRequired,
                    "mid-delete failure is partial, not complete");
                Check(ProfileWriteSessionGate.IsBlocked, "partial latches session write lock");
                Check(actions.PrepareEmptyTrash().State == ProfileTrashPurgeState.Rejected &&
                    actions.Restore(t2.TrashToken, t2.ContentHash).Code == ProfileStorageCode.RecoveryRequired,
                    "session lock blocks new actions");
                ProfileWriteSessionGate.ResetForIsolatedTests();
                Check(repository.ListTrash().Entries.Count == 2, "two files remain after partial");

                // Simulate a post-delete failure before absence verification.
                ProfileTrashPurgeResult after = actions.PrepareEmptyTrash();
                Check(after.State == ProfileTrashPurgeState.Ready, "second snapshot prepared");
                ProfileTrashPurgeResult uncertain = actions.EmptyTrash(after.Plan,
                    injectAfterDeleteIndex: 0);
                Check(uncertain.State == ProfileTrashPurgeState.Uncertain &&
                    uncertain.Remaining == -1 && ProfileWriteSessionGate.IsBlocked,
                    "post-delete uncertainty latches and never claims success");
                ProfileWriteSessionGate.ResetForIsolatedTests();

                ProfileTrashPurgeResult finalReady = actions.PrepareEmptyTrash();
                Check(finalReady.State == ProfileTrashPurgeState.Ready && finalReady.Plan.Count == 1,
                    "remaining one candidate only");
                ProfileTrashPurgeResult success = actions.EmptyTrash(finalReady.Plan);
                Check(success.IsComplete && success.DeletedVerified == 1 && success.Remaining == 0,
                    "last candidate completely purged");
                Check(repository.ListTrash().Entries.Count == 0 &&
                    actions.PrepareEmptyTrash().State == ProfileTrashPurgeState.NoOp,
                    "second purge is read-only no-op");
                Check(File.Exists(ignored) && File.ReadAllText(ignored, Utf8) == "not a trash profile",
                    "unrelated trash file preserved");
                Check(File.Exists(sentinelXml) &&
                    File.ReadAllText(sentinelXml, Utf8) == "do-not-write-config",
                    "XML sentinel preserved");
                Check(File.Exists(backup) && File.ReadAllText(backup, Utf8) == "unchanged-backup",
                    "backup preserved");
                Check(File.Exists(otherMod) && File.ReadAllText(otherMod, Utf8) == "unchanged-other-mod",
                    "other mod sentinel preserved");
                Check(File.Exists(activeJson) &&
                    BytesEqual(activeBefore, File.ReadAllBytes(activeJson)),
                    "active profile remains identical");
                Check(!ProfileWriteSessionGate.IsBlocked, "successful purge does not latch");
                Check(Object.ReferenceEquals(XpCustomizerMod.Settings, live) &&
                    (live == null || (live.careerXpMultiplier == originalMultiplier &&
                        Object.ReferenceEquals(live.skillSpecificXpMultipliers, originalDict))),
                    "live XP settings and dictionary identity preserved");
                finished = true;
                Log.Message("[XPC-R6P1C2D][TRASH_PURGE_SELFTEST] status=PASS; checks=" + checks +
                    "; isolatedRoot=True; emptyNoOp=PASS; snapshotConflict=PASS; invalidFilesRejected=PASS; " +
                    "partialStop=PASS; uncertainStop=PASS; sessionLock=PASS; activeXmlPreserved=PASS; " +
                    "liveConfigWrites=0; productionApply=NOT_EXERCISED_BY_THIS_TEST; build=" + XpCustomizerMod.BuildId);
            }
            catch (Exception ex)
            {
                Log.Error("[XPC-R6P1C2D][TRASH_PURGE_SELFTEST] status=FAIL; checks=" + checks +
                    "; exception=" + ex.GetType().Name + "; build=" + XpCustomizerMod.BuildId);
                throw;
            }
            finally
            {
                ProfileWriteSessionGate.ResetForIsolatedTests();
                if (finished)
                {
                    try { Directory.Delete(dir, true); }
                    catch (Exception) { /* isolated test only; cleanup failure cannot touch user config */ }
                }
            }
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
#endif
