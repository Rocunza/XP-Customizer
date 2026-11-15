#if DEBUG
using System;
using System.IO;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer
{
    /// <summary>
    /// R6-P1-C2-A: isolated disk creation tests. No user Config, Scribe, GeneDef,
    /// XP cache, or live ModSettings writes. UI mouse input remains manual.
    /// </summary>
    internal static class ProfileCreationSelfTests
    {
        private static int checks;
        internal static void Run()
        {
            checks = 0;
            string testRoot = Path.Combine(Path.GetTempPath(), "XPC-R6P1C2A-Test-" +
                Guid.NewGuid().ToString("N"));
            XpCustomizerSettings live = XpCustomizerMod.Settings;
            object liveMap = live == null ? null : live.skillSpecificXpMultipliers;
            float liveCareer = live == null ? 0f : live.careerXpMultiplier;
            try
            {
                ProfileStoragePaths paths = ProfileStoragePaths.ForIsolatedTestRoot(testRoot);
                ProfileFileRepository store = new ProfileFileRepository(paths, false);
                ProfileCreationService creation = new ProfileCreationService(
                    delegate { return store; }, false);
                DateTime utc = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);
                string id1 = "9af41a5d-8bcf-41f2-b56d-20c2a0000001";
                string id2 = "9af41a5d-8bcf-41f2-b56d-20c2a0000002";
                string id3 = "9af41a5d-8bcf-41f2-b56d-20c2a0000003";
                string id4 = "9af41a5d-8bcf-41f2-b56d-20c2a0000004";
                XpCustomizerSettings sample = new XpCustomizerSettings();
                sample.careerXpMultiplier = 1.55f;
                sample.skillSpecificXpMultipliers["UnknownModSkill"] = 1.25f;
                sample.careerXpSplitLevels.Clear();

                string clean;
                Check(!ProfileCreationService.TryName(null, out clean) &&
                      !ProfileCreationService.TryName("   ", out clean) &&
                      !ProfileCreationService.TryName("a\nb", out clean) &&
                      !ProfileCreationService.TryName(new string('x', 81), out clean),
                    "invalid names rejected");
                Check(!ProfileCreationService.TryName("\uD800", out clean) &&
                      ProfileCreationService.TryName("  한글 / Sample #1  ", out clean) &&
                      clean == "한글 / Sample #1" &&
                      ProfileCreationService.TryName("Emoji \uD83D\uDE80", out clean),
                    "safe unicode and trimming");
                Check(!Directory.Exists(testRoot), "name tests do not touch disk");
                ProfileCreationResult empty = creation.CreateCore(sample, " ", "1.6.4871",
                    utc, id1, ProfileStorageFault.None);
                Check(empty.Code == ProfileCreationCode.InvalidName &&
                      !Directory.Exists(testRoot), "invalid name has zero file writes");
                ProfileCreationResult noSettings = creation.CreateCore(null, "Valid", "1.6.4871",
                    utc, id1, ProfileStorageFault.None);
                Check(noSettings.Code == ProfileCreationCode.InvalidSettings &&
                      !Directory.Exists(testRoot), "missing settings has zero file writes");
                sample.careerXpMultiplier = float.NaN;
                ProfileCreationResult invalidValue = creation.CreateCore(sample, "Valid",
                    "1.6.4871", utc, id1, ProfileStorageFault.None);
                Check(invalidValue.Code == ProfileCreationCode.InvalidSettings &&
                      !Directory.Exists(testRoot), "invalid candidate has zero file writes");
                sample.careerXpMultiplier = 1.55f;
                ProfileCreationResult badMeta = creation.CreateCore(sample, "Valid",
                    new string('1', 65), utc, id1, ProfileStorageFault.None);
                Check(badMeta.Code == ProfileCreationCode.InvalidProfile &&
                      !Directory.Exists(testRoot), "invalid metadata has zero file writes");

                ProfileCreationResult first = creation.CreateCore(sample, "  한글 / Sample #1  ",
                    "1.6.4871", utc, id1, ProfileStorageFault.None);
                Check(first.Success && first.ProfileId == id1, "explicit Create commits new GUID");
                ProfileStorageResult loaded = store.Load(id1);
                Check(loaded.Success && loaded.Document.DisplayName == "한글 / Sample #1" &&
                      loaded.Document.SourceBuildId == XpCustomizerMod.BuildId &&
                      loaded.Document.SourceGameVersion == "1.6.4871", "metadata persisted");
                Check(loaded.Document.CreatedUtc == utc && loaded.Document.UpdatedUtc == utc &&
                      loaded.Document.Settings != null, "timestamp/schema persisted");
                string oldHash = loaded.ContentHash;
                sample.careerXpMultiplier = 1.95f;
                sample.skillSpecificXpMultipliers["UnknownModSkill"] = 2.0f;
                ProfileStorageResult independent = store.Load(id1);
                Check(independent.Success && independent.ContentHash == oldHash &&
                    (float)independent.Document.Settings.Sections["career"]["careerXpMultiplier"] == 1.55f,
                    "saved snapshot remains independent after source mutation");

                // Identical display names do not overwrite; GUID is the real identity.
                ProfileCreationResult second = creation.CreateCore(sample, "한글 / Sample #1",
                    "1.6.4871", utc.AddSeconds(1), id2, ProfileStorageFault.None);
                Check(second.Success && store.List().Entries.Count == 2 &&
                    store.Load(id1).ContentHash == oldHash && store.Load(id2).Success,
                    "duplicate names, separate identities, no replacement");
                ProfileCreationResult collided = creation.CreateCore(sample, "colliding id",
                    "1.6.4871", utc.AddSeconds(2), id1, ProfileStorageFault.None);
                Check(collided.Code == ProfileCreationCode.StorageFailure &&
                    collided.StorageCode == ProfileStorageCode.AlreadyExists &&
                    store.Load(id1).ContentHash == oldHash, "existing GUID cannot be overwritten");

                ProfileCreationResult failedTemp = creation.CreateCore(sample, "fault1",
                    "1.6.4871", utc.AddSeconds(3), id3, ProfileStorageFault.AfterTempWrite);
                Check(failedTemp.Code == ProfileCreationCode.StorageFailure &&
                    failedTemp.StorageCode == ProfileStorageCode.InjectedFailure &&
                    store.Load(id3).Code == ProfileStorageCode.NotFound &&
                    store.Load(id1).ContentHash == oldHash, "temp-write fault preserves old profile");
                ProfileCreationResult failedCommit = creation.CreateCore(sample, "fault2",
                    "1.6.4871", utc.AddSeconds(4), id4, ProfileStorageFault.BeforeCommit);
                Check(failedCommit.Code == ProfileCreationCode.StorageFailure &&
                    failedCommit.StorageCode == ProfileStorageCode.InjectedFailure &&
                    store.Load(id4).Code == ProfileStorageCode.NotFound &&
                    store.List().Entries.Count == 2 &&
                    Directory.GetFiles(testRoot, ".stage-*.tmp").Length == 0,
                    "pre-commit failure preserves listing and cleans owned temp");

                Check(object.ReferenceEquals(XpCustomizerMod.Settings, live) &&
                    (live == null || (object.ReferenceEquals(liveMap, live.skillSpecificXpMultipliers) &&
                    live.careerXpMultiplier == liveCareer)), "live settings and nested references untouched");
                Log.Message("[XPC-R6P1C2A][PROFILE_CREATE_AUDIT] status=PASS; revision=" +
                    ProfileCreationService.Revision + "; checks=" + checks +
                    "; mode=CREATE_NEW_ONLY; fileSystem=ISOLATED_TEMP; unicodeName=PASS; " +
                    "duplicateName=GUID_SEPARATE; overwrite=REJECTED; " +
                    "faults=AFTER_TEMP_WRITE,BEFORE_COMMIT; originalPreserved=PASS; " +
                    "sourceSnapshot=DETACHED; liveSettingsWrites=0; geneDefWrites=0; " +
                    "scribeCalls=0; productionApply=NOT_EXERCISED_BY_THIS_TEST; realIMGUI=MANUAL; " +
                    "build=" + XpCustomizerMod.BuildId);
            }
            finally
            {
                if (Directory.Exists(testRoot))
                {
                    try
                    {
                        if ((File.GetAttributes(testRoot) & FileAttributes.ReparsePoint) == 0)
                            Directory.Delete(testRoot, true);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("[XPC-R6P1C2A][PROFILE_CREATE_AUDIT] status=CLEANUP_FAILED; " +
                            "exception=" + ex.GetType().Name + "; build=" + XpCustomizerMod.BuildId);
                    }
                }
            }
        }

        private static void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException("R6P1C2A: " + label);
            checks++;
        }
    }
}
#endif
