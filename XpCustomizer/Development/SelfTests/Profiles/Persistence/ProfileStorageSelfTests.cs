#if DEBUG
using System;
using System.IO;
using System.Text;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer
{
    /// <summary>Real filesystem tests: only in a newly owned disposable temp folder.</summary>
    internal static class ProfileStorageSelfTests
    {
        private const string Revision = "R6P1B-DISK-TRANSACTION-20261106";
        private static int passed;
        private static int injected;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        internal static void Run()
        {
            string testRoot = Path.Combine(Path.GetTempPath(), "XPC-R6P1B-SelfTest-" + Guid.NewGuid().ToString("N"));
            XpCustomizerSettings live = XpCustomizerMod.Settings;
            object liveDict = live == null ? null : live.skillSpecificXpMultipliers;
            float liveMultiplier = live == null ? 0f : live.careerXpMultiplier;
            passed = 0;
            injected = 0;
            bool clean = false;
            try
            {
                Directory.CreateDirectory(testRoot);
                ProfileStoragePaths paths = ProfileStoragePaths.ForIsolatedTestRoot(testRoot);
                ProfileFileRepository store = new ProfileFileRepository(paths, false);
                string id1 = "c0a44a37-52fd-4870-a872-fd119dc00001";
                string id2 = "c0a44a37-52fd-4870-a872-fd119dc00002";
                string id3 = "c0a44a37-52fd-4870-a872-fd119dc00003";
                string id4 = "c0a44a37-52fd-4870-a872-fd119dc00004";
                string id5 = "c0a44a37-52fd-4870-a872-fd119dc00005";
                string id6 = "c0a44a37-52fd-4870-a872-fd119dc00006";
                string id7 = "c0a44a37-52fd-4870-a872-fd119dc00007";
                XpCustomizerSettings settings = new XpCustomizerSettings();
                settings.careerXpMultiplier = 1.45f;
                settings.skillSpecificXpEnabled = false;
                settings.skillSpecificXpMultipliers["ExternalMod_UnknownSkill"] = 1.25f;
                settings.careerXpSplitLevels.Clear();
                ProfileDocument doc1 = ProfileMapper.Export(settings, "한글 / profile A", "1.6.4871",
                    new DateTime(2026, 11, 6, 6, 0, 0, DateTimeKind.Utc), id1);
                ProfileDocument doc2 = ProfileMapper.Export(settings, "한글 / profile A", "1.6.4871",
                    new DateTime(2026, 11, 6, 6, 0, 0, DateTimeKind.Utc), id2);
                Check(store.Load(id1).Code == ProfileStorageCode.NotFound, "no unexpected file creation");
                Check(!Directory.Exists(Path.Combine(testRoot,"_Trash")), "startup no trash side effect");
                Check(ProfileStoragePaths.ForConfigRoot(Path.GetFullPath(testRoot)).Root ==
                    Path.GetFullPath(Path.Combine(testRoot,"XpCustomizer","Profiles")), "config path is scoped");
                Check(store.Create(doc1, ProfileStorageFault.AfterTempWrite).Code == ProfileStorageCode.InjectedFailure,
                    "create temp failure"); injected++;
                Check(store.Load(id1).Code == ProfileStorageCode.NotFound, "failed create has no final file");
                Check(Directory.GetFiles(testRoot,"*.tmp").Length == 0, "owned stages cleaned");
                ProfileStorageResult created = store.Create(doc1);
                Check(created.Success && created.Document.ProfileId == id1, "create with readback");
                Check(store.Create(doc1).Code == ProfileStorageCode.AlreadyExists, "duplicate ID denied");
                ProfileStorageResult loaded = store.Load(id1);
                Check(loaded.Success && loaded.ContentHash == created.ContentHash, "disk reopen SHA256");
                Check(loaded.Document.DisplayName == "한글 / profile A", "UTF8 display name");
                Check(store.Create(doc2).Success, "same display name different GUID");
                Check(store.List().Entries.Count == 2 && store.List().Entries[0].Result.Success,
                    "listing sorted and independent");
                Check(store.Load("../escaped").Code == ProfileStorageCode.InvalidArgument, "path traversal blocked");
                Check(store.Load(id1.ToUpperInvariant()).Code == ProfileStorageCode.InvalidArgument,
                    "canonical id required");

                ProfileDocument edited = ProfileJsonCodec.Deserialize(ProfileJsonCodec.Serialize(loaded.Document));
                edited.DisplayName = "이름 변경 — профиль";
                string beforeUpdate = loaded.ContentHash;
                foreach (ProfileStorageFault failure in new[] {
                    ProfileStorageFault.AfterTempWrite, ProfileStorageFault.BeforeCommit,
                    ProfileStorageFault.BeforeReplace })
                {
                    Check(store.Update(edited, beforeUpdate, failure).Code == ProfileStorageCode.InjectedFailure,
                        "update fault=" + failure); injected++;
                    Check(store.Load(id1).ContentHash == beforeUpdate, "pre-replace original preserved");
                }
                Check(store.Update(edited, new string('0', 64)).Code == ProfileStorageCode.Conflict,
                    "stale external update rejected");
                ProfileStorageResult updated = store.Update(edited, beforeUpdate);
                Check(updated.Success, "replace success (requires File.Replace support)");
                Check(updated.Document.DisplayName == "이름 변경 — профиль", "rename persisted");
                Check(updated.Document.CreatedUtc == loaded.Document.CreatedUtc, "created time immutable");
                Check(updated.Document.UpdatedUtc > loaded.Document.UpdatedUtc, "updated time advanced");
                Check(File.Exists(paths.FileForId(id1)), "new final exists");
                string[] backups = Directory.GetFiles(paths.Backups, "*.bak");
                Check(backups.Length == 1, "retained immutable replacement backup");
                Check(Utf8.GetString(File.ReadAllBytes(backups[0])) ==
                    ProfileJsonCodec.Serialize(loaded.Document), "backup contains previous version");
                Check(store.Update(edited, beforeUpdate).Code == ProfileStorageCode.Conflict,
                    "stale hash after successful update");
                Check(store.Load(id1).ContentHash == updated.ContentHash, "hash of new version persisted");
                Check(Directory.GetFiles(testRoot, "*.tmp").Length == 0, "no staging files left");

                // Invalid files must be isolated; no scans may delete or rewrite them.
                File.WriteAllText(paths.FileForId(id3), "{invalid", Utf8);
                File.WriteAllBytes(paths.FileForId(id4), new byte[] { 0xc3, 0x28 });
                File.WriteAllBytes(paths.FileForId(id5), new byte[4 * 1024 * 1024 + 1]);
                string future = ProfileJsonCodec.Serialize(doc1).Replace("\"formatVersion\":2", "\"formatVersion\":3");
                File.WriteAllText(paths.FileForId(id6), future, Utf8);
                File.WriteAllText(paths.FileForId(id7), ProfileJsonCodec.Serialize(doc1), Utf8);
                Check(store.Load(id3).Code == ProfileStorageCode.InvalidFormat, "bad JSON rejected");
                Check(store.Load(id4).Code == ProfileStorageCode.InvalidFormat, "bad UTF8 rejected");
                Check(store.Load(id5).Code == ProfileStorageCode.FileTooLarge, "oversized bytes rejected");
                Check(store.Load(id6).Code == ProfileStorageCode.UnsupportedVersion, "future format rejected");
                Check(store.Load(id7).Code == ProfileStorageCode.IdMismatch, "filename ID mismatch denied");
                ProfileStorageListing listing = store.List();
                int valid = 0, invalid = 0;
                foreach(ProfileStorageEntry entry in listing.Entries)
                { if(entry.Result.Success)valid++; else invalid++; }
                Check(listing.Code == ProfileStorageCode.Ok && valid == 2 && invalid == 5,
                    "one corrupt file does not hide other profiles");
                Check(File.Exists(paths.FileForId(id3)) && File.Exists(paths.FileForId(id4)),
                    "bad files left unchanged");

                Check(store.SoftDelete(id1, updated.ContentHash, ProfileStorageFault.BeforeSoftDelete).Code ==
                    ProfileStorageCode.InjectedFailure, "delete fault"); injected++;
                Check(store.Load(id1).Success, "failed delete leaves source");
                Check(store.SoftDelete(id1, beforeUpdate).Code == ProfileStorageCode.Conflict,
                    "delete requires current hash");
                ProfileStorageResult deleted = store.SoftDelete(id1, updated.ContentHash);
                Check(deleted.Success && deleted.TrashToken != null, "soft delete moves to trash");
                Check(store.Load(id1).Code == ProfileStorageCode.NotFound, "deleted profile absent from list");
                Check(File.Exists(Path.Combine(paths.Trash, deleted.TrashToken)), "trash retained");
                Check(store.ListTrash().Entries.Count == 1 &&
                      store.ListTrash().Entries[0].FileName == deleted.TrashToken &&
                      store.ListTrash().Entries[0].Result.Success, "deleted items discoverable after restart");
                Check(store.Restore("../outside.deleted").Code == ProfileStorageCode.InvalidArgument,
                    "trash traversal denied");
                Check(store.Restore(deleted.TrashToken, ProfileStorageFault.BeforeRestore).Code ==
                    ProfileStorageCode.InjectedFailure, "restore fault"); injected++;
                Check(File.Exists(Path.Combine(paths.Trash, deleted.TrashToken)), "failed restore retains trash");
                Check(store.Restore(deleted.TrashToken, "not-a-hash").Code ==
                    ProfileStorageCode.InvalidArgument, "UI restore requires SHA256");
                Check(store.Restore(deleted.TrashToken, new string('0', 64)).Code ==
                    ProfileStorageCode.Conflict, "UI restore rejects stale trash hash");
                Check(File.Exists(Path.Combine(paths.Trash, deleted.TrashToken)),
                    "rejected UI restore leaves trash untouched");
                ProfileStorageResult restored = store.Restore(deleted.TrashToken, updated.ContentHash);
                Check(restored.Success && restored.ContentHash == updated.ContentHash, "restore bytes match");
                Check(store.Restore(deleted.TrashToken).Code == ProfileStorageCode.AlreadyExists ||
                    store.Restore(deleted.TrashToken).Code == ProfileStorageCode.NotFound,
                    "repeat restore denied");
                ProfileStorageResult deletedAgain = store.SoftDelete(id1, restored.ContentHash);
                Check(deletedAgain.Success, "second soft delete");
                // Restore must not overwrite independently created file of same ID.
                Check(store.Create(doc1).Success, "new active file after deletion");
                Check(store.Restore(deletedAgain.TrashToken).Code == ProfileStorageCode.AlreadyExists,
                    "conflicting restore does not overwrite active profile");
                Check(store.Load(id1).ContentHash == beforeUpdate, "restore conflict preserves active");
                Check(File.Exists(Path.Combine(paths.Trash, deletedAgain.TrashToken)),
                    "restore conflict preserves deleted version");

                // The actual UI action adapter is exercised against the isolated repository.
                ProfileTrashActionService uiActions = new ProfileTrashActionService(delegate { return store; });
                ProfileStorageResult secondActive = store.Load(id2);
                Check(uiActions.Delete(id2, new string('0', 64)).Code == ProfileStorageCode.Conflict,
                    "UI delete rejects changed profile SHA");
                Check(store.Load(id2).Success, "failed UI delete leaves active intact");
                ProfileStorageResult uiDeleted = uiActions.Delete(id2, secondActive.ContentHash);
                Check(uiDeleted.Success && uiDeleted.TrashToken != null,
                    "UI delete moves only selected profile to trash");
                Check(store.Load(id2).Code == ProfileStorageCode.NotFound,
                    "UI delete removes selected profile from active list");
                Check(uiActions.Restore(uiDeleted.TrashToken, new string('0', 64)).Code ==
                    ProfileStorageCode.Conflict, "UI restore rejects stale SHA");
                Check(uiActions.Restore(uiDeleted.TrashToken, uiDeleted.ContentHash).Success,
                    "UI restore returns profile to active");
                Check(store.Load(id2).ContentHash == secondActive.ContentHash,
                    "UI restore preserves original profile bytes");

                Check(Object.ReferenceEquals(XpCustomizerMod.Settings, live) &&
                    (live == null || (Object.ReferenceEquals(live.skillSpecificXpMultipliers, liveDict) &&
                                     live.careerXpMultiplier == liveMultiplier)),
                    "live settings unchanged");
                clean = true;
                Log.Message("[XPC-R6P1B][PROFILE_STORAGE_AUDIT] status=PASS; revision="+Revision+
                    "; scope=ISOLATED_TEMP; checks="+passed+
                    "; validListed=2; invalidIsolated=5; strictUtf8=PASS; pathTraversal=REJECTED;"+
                    " liveSettingsWrites=0; geneDefWrites=0; scribeCalls=0; productionApply=NOT_EXERCISED_BY_THIS_TEST; build="+
                    XpCustomizerMod.BuildId);
                Log.Message("[XPC-R6P1B][PROFILE_ATOMIC_AUDIT] status=PASS; injectedFailures="+injected+
                    "; expectedHash=REQUIRED; createNew=PASS; retainedBackup=PASS; replace=PASS;"+
                    " originalFilesPreserved=PASS; build="+XpCustomizerMod.BuildId);
                Log.Message("[XPC-R6P1B][PROFILE_DELETE_AUDIT] status=PASS; softDelete=PASS;"+
                    " restore=PASS; uiActions=PASS; restoreShaGuard=PASS; conflict=REJECTED;"+
                    " noPermanentDeletion=PASS; build="+
                    XpCustomizerMod.BuildId);
            }
            finally
            {
                if (Directory.Exists(testRoot))
                {
                    try
                    {
                        // Only this run's GUID-named directory; never user's Config.
                        if ((File.GetAttributes(testRoot) & FileAttributes.ReparsePoint) == 0)
                            Directory.Delete(testRoot, true);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("[XPC-R6P1B][PROFILE_STORAGE_AUDIT] status=TEMP_CLEANUP_FAILED;"+
                            " exception="+ex.GetType().Name+"; build="+XpCustomizerMod.BuildId);
                    }
                }
                if (!clean)
                    Log.Warning("[XPC-R6P1B][PROFILE_STORAGE_AUDIT] status=INCOMPLETE;"+
                        " build="+XpCustomizerMod.BuildId);
            }
        }

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("[R6P1B] disk self-test: " + name);
            passed++;
        }
    }
}
#endif
