#if DEBUG
using System;
using System.IO;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer
{
    internal static class ProfileOverwritePreflightSelfTests
    {
        private static int checks;
        private static void Check(bool condition, string label)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("R6P1C2B2A PREFLIGHT " + label);
        }
        private static ProfileEditorReadinessResult State(ProfileEditorReadinessCode code)
        {
            return new ProfileEditorReadinessResult(code, "TEST", 15, 0, 0);
        }
        internal static void Run()
        {
            checks = 0;
            string root = Path.Combine(Path.GetTempPath(), "XPC-R6P1C2B2A-" +
                Guid.NewGuid().ToString("N"));
            bool oldLock = ProfileWriteSessionGate.IsBlocked;
            XpCustomizerSettings live = XpCustomizerMod.Settings;
            try
            {
                Check(!oldLock, "preexisting session recovery lock absent");
                ProfileStoragePaths paths = ProfileStoragePaths.ForIsolatedTestRoot(root);
                ProfileFileRepository repo = new ProfileFileRepository(paths, false);
                ProfileCreationService creator = new ProfileCreationService(delegate { return repo; }, false);
                XpCustomizerSettings original = new XpCustomizerSettings();
                original.careerXpMultiplier = 1.2f;
                string id = "22bc29d5-8ef3-4235-a591-30c2b2000001";
                DateTime utc = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);
                Check(creator.CreateCore(original, "Target", "1.6.4871", utc, id,
                    ProfileStorageFault.None).Success, "temp target created");
                ProfileStorageResult before = repo.Load(id);
                Check(before.Success, "temp target loaded");
                XpCustomizerSettings edited = SettingsSnapshotService.CloneIndependent(original);
                edited.careerXpMultiplier = 2.25f;
                var ready = new ProfileOverwritePreflightService(
                    delegate { return repo; },
                    delegate { return State(ProfileEditorReadinessCode.Ready); }, false);
                ProfileOverwritePreflightResult accepted = ready.Check(before.Document,
                    before.ContentHash, edited);
                Check(accepted.Success && accepted.Candidate != null &&
                    accepted.ContentDiffers, "read-only candidate prepared and differs");
                Check(accepted.Candidate.ProfileId == before.Document.ProfileId &&
                    accepted.Candidate.CreatedUtc == before.Document.CreatedUtc &&
                    accepted.Candidate.DisplayName == before.Document.DisplayName,
                    "metadata identity preserved");
                Check(accepted.Candidate.UpdatedUtc > before.Document.UpdatedUtc &&
                    accepted.Candidate.Settings != before.Document.Settings,
                    "new modified time and detached data");
                Check(repo.Load(id).ContentHash == before.ContentHash &&
                    !Directory.Exists(paths.Backups),
                    "preflight never writes original or backup");
                Check(edited.careerXpMultiplier == 2.25f &&
                    original.careerXpMultiplier == 1.2f,
                    "live input objects unchanged");
                ProfileOverwritePreflightResult same = ready.Check(before.Document,
                    before.ContentHash, original);
                Check(same.Success && !same.ContentDiffers, "identical portable settings detected");
                var pending = new ProfileOverwritePreflightService(
                    delegate { return repo; },
                    delegate { return State(ProfileEditorReadinessCode.PendingEdit); }, false);
                Check(pending.Check(before.Document, before.ContentHash, edited).Code ==
                    ProfileOverwritePreflightCode.PendingEdit, "focused number blocks preflight");
                var uncertain = new ProfileOverwritePreflightService(
                    delegate { return repo; },
                    delegate { return State(ProfileEditorReadinessCode.Uncertain); }, false);
                Check(uncertain.Check(before.Document, before.ContentHash, edited).Code ==
                    ProfileOverwritePreflightCode.Uncertain, "untracked focus blocks preflight");
                Check(ready.Check(before.Document, "bad-hash", edited).Code ==
                    ProfileOverwritePreflightCode.InvalidSelection,
                    "bad SHA rejected without I/O");
                Check(ready.Check(before.Document,
                    new string('0', 64), edited).Code == ProfileOverwritePreflightCode.Conflict,
                    "stale SHA rejected");
                Check(ready.Check(null, before.ContentHash, edited).Code ==
                    ProfileOverwritePreflightCode.InvalidSelection, "null selection rejected");
                XpCustomizerSettings invalid = SettingsSnapshotService.CloneIndependent(edited);
                invalid.careerXpMultiplier = float.NaN;
                Check(ready.Check(before.Document, before.ContentHash, invalid).Code ==
                    ProfileOverwritePreflightCode.InvalidSettings,
                    "invalid settings rejected without changing profile");
                ProfileWriteSessionGate.Observe(ProfileStorageCode.RecoveryRequired);
                Check(ready.Check(before.Document, before.ContentHash, edited).Code ==
                    ProfileOverwritePreflightCode.RecoveryRequired,
                    "existing session write lock respected");
                ProfileWriteSessionGate.ResetForIsolatedTests();
                Check(repo.Load(id).ContentHash == before.ContentHash &&
                    Directory.GetFiles(paths.Root, "*.json").Length == 1 &&
                    !Directory.Exists(paths.Backups),
                    "all preflight cases have zero profile writes");
                Check(object.ReferenceEquals(XpCustomizerMod.Settings, live),
                    "live settings reference never replaced");
                Log.Message("[XPC-R6P1C2B2A][PROFILE_OVERWRITE_PREFLIGHT_AUDIT] status=PASS; " +
                    "revision=" + ProfileOverwritePreflightService.Revision +
                    "; checks=" + checks + "; storage=ISOLATED_TEMP; " +
                    "ready=DETACHED_CANDIDATE_ONLY; pending=BLOCKED; uncertain=BLOCKED; " +
                    "staleHash=CONFLICT; sessionLock=BLOCKED; candidateMutation=NONE; " +
                    "fileWritesByPreflight=0; userConfigWrites=0; liveSettingsWrites=0; " +
                    "geneDefWrites=0; scribeCalls=0; productionApply=NOT_EXERCISED_BY_THIS_TEST; realIMGUI=MANUAL; build=" +
                    XpCustomizerMod.BuildId);
            }
            finally
            {
                if (!oldLock) ProfileWriteSessionGate.ResetForIsolatedTests();
                if (Directory.Exists(root))
                {
                    try
                    {
                        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) == 0)
                            Directory.Delete(root, true);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }
    }
}
#endif
