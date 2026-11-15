#if DEBUG
using System;
using System.IO;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer
{
    /// <summary>
    /// R6-P1-C2-B1 isolated-disk tests. No user Config, live XP settings,
    /// GeneDefs, Harmony hooks, UI mouse events or Scribe are mutated.
    /// </summary>
    internal static class ProfileRenameSelfTests
    {
        private static int checks;

        internal static void Run()
        {
            checks = 0;
            string root = Path.Combine(Path.GetTempPath(), "XPC-R6P1C2B1-Test-" +
                Guid.NewGuid().ToString("N"));
            bool oldBlocked = ProfileWriteSessionGate.IsBlocked;
            XpCustomizerSettings live = XpCustomizerMod.Settings;
            object liveMap = live == null ? null : live.skillSpecificXpMultipliers;
            float liveCareer = live == null ? 0f : live.careerXpMultiplier;
            try
            {
                Check(!oldBlocked, "no user recovery lock before tests");
                ProfileStoragePaths paths = ProfileStoragePaths.ForIsolatedTestRoot(root);
                ProfileFileRepository repo = new ProfileFileRepository(paths, false);
                ProfileCreationService creator = new ProfileCreationService(delegate { return repo; }, false);
                ProfileUpdateService updater = new ProfileUpdateService(delegate { return repo; }, false);
                XpCustomizerSettings sample = new XpCustomizerSettings();
                sample.careerXpMultiplier = 1.85f;
                sample.skillSpecificXpMultipliers["UnregisteredSkill"] = 1.23f;
                DateTime utc = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);
                string idA = "11ac19d5-8ef3-4235-a591-30c2b1000001";
                string idB = "11ac19d5-8ef3-4235-a591-30c2b1000002";
                Check(creator.CreateCore(sample, "Original A", "1.6.4871", utc,
                    idA, ProfileStorageFault.None).Success, "initial profile A created in temp");
                Check(creator.CreateCore(sample, "Other B", "1.6.4871", utc,
                    idB, ProfileStorageFault.None).Success, "initial profile B created in temp");
                ProfileStorageResult original = repo.Load(idA);
                ProfileStorageResult untouchedB = repo.Load(idB);
                Check(original.Success && untouchedB.Success, "initial profiles readable");
                string beforeJson = ProfileJsonCodec.Serialize(original.Document);
                string beforeHash = original.ContentHash;

                string normalized;
                Check(!ProfileNamePolicy.TryCanonical(null, out normalized) &&
                    !ProfileNamePolicy.TryCanonical("\r\n", out normalized) &&
                    !ProfileNamePolicy.TryCanonical(new string('Q', 81), out normalized) &&
                    !ProfileNamePolicy.TryCanonical("\uDC00", out normalized) &&
                    ProfileNamePolicy.TryCanonical("  한글 \uD83D\uDE80  ", out normalized) &&
                    normalized == "한글 \uD83D\uDE80", "shared name normalization");
                Check(ProfileCreationService.TryName("  Unicode  ", out normalized) &&
                    normalized == "Unicode", "create path uses shared policy");
                Check(updater.Rename(idA, beforeHash, " \n ").Code ==
                    ProfileRenameCode.InvalidName && repo.Load(idA).ContentHash == beforeHash,
                    "invalid name rejected without write");
                Check(updater.Rename(idA, "not a hash", "Renamed").Code ==
                    ProfileRenameCode.InvalidSelection && repo.Load(idA).ContentHash == beforeHash,
                    "invalid selection rejected without write");
                Check(!Directory.Exists(paths.Backups), "invalid inputs do not create backups");

                ProfileRenameResult updated = updater.Rename(idA, beforeHash, "  한글 \uD83D\uDE80  ");
                ProfileStorageResult after = repo.Load(idA);
                Check(updated.Success && after.Success && after.Document.DisplayName == "한글 \uD83D\uDE80",
                    "rename succeeds and trims Unicode name");
                Check(after.ContentHash != beforeHash && after.Document.ProfileId == idA &&
                    after.Document.CreatedUtc == original.Document.CreatedUtc &&
                    after.Document.UpdatedUtc > original.Document.UpdatedUtc,
                    "GUID, creation timestamp retained; modified time advances");
                Check(after.Document.SourceGameVersion == original.Document.SourceGameVersion &&
                    after.Document.SourceBuildId == original.Document.SourceBuildId,
                    "source metadata remains unchanged");

                // Prove ALL portable settings unchanged, not just a few representative floats.
                ProfileDocument compare = ProfileJsonCodec.Deserialize(
                    ProfileJsonCodec.Serialize(after.Document));
                compare.DisplayName = original.Document.DisplayName;
                compare.UpdatedUtc = original.Document.UpdatedUtc;
                Check(ProfileJsonCodec.Serialize(compare) == beforeJson,
                    "entire portable settings and all metadata except name/time unchanged");
                Check(repo.Load(idB).ContentHash == untouchedB.ContentHash &&
                    repo.Load(idB).Document.DisplayName == "Other B",
                    "unselected profile untouched");

                string[] backups = Directory.GetFiles(paths.Backups, idA + ".*.bak");
                Check(backups.Length == 1 && File.ReadAllText(backups[0]) == beforeJson,
                    "atomic replace retains original JSON backup");
                Check(Directory.GetFiles(root, ".stage-*.tmp").Length == 0,
                    "successful update cleans stage");
                Check(repo.List().Entries.Count == 2 &&
                    repo.Load(idA).Document.DisplayName == "한글 \uD83D\uDE80",
                    "list/restart-style reload has same GUID and updated name");
                Check(updater.Rename(idA, after.ContentHash, "  한글 \uD83D\uDE80  ").Code ==
                    ProfileRenameCode.NoChange && repo.Load(idA).ContentHash == after.ContentHash &&
                    Directory.GetFiles(paths.Backups, idA + ".*.bak").Length == 1,
                    "same canonical name is NoChange with zero writes");
                Check(updater.Rename(idA, beforeHash, "Stale").Code ==
                    ProfileRenameCode.Conflict && repo.Load(idA).ContentHash == after.ContentHash,
                    "old selected SHA blocks stale overwrite");

                ProfileRenameResult duplicate = updater.Rename(idA, after.ContentHash, "Other B");
                ProfileStorageResult duplicateLoaded = repo.Load(idA);
                Check(duplicate.Success && duplicateLoaded.Document.DisplayName == "Other B" &&
                    duplicateLoaded.Document.ProfileId != repo.Load(idB).Document.ProfileId,
                    "duplicate display names allowed but IDs are different");
                Check(repo.Load(idB).ContentHash == untouchedB.ContentHash,
                    "duplicate name never touches the other GUID file");
                string holdHash = duplicateLoaded.ContentHash;
                int backupsBeforeFault = Directory.GetFiles(paths.Backups, idA + ".*.bak").Length;
                ProfileRenameResult tempFault = updater.RenameCore(idA, holdHash, "Injected 1",
                    ProfileStorageFault.AfterTempWrite);
                Check(tempFault.StorageCode == ProfileStorageCode.InjectedFailure &&
                    repo.Load(idA).ContentHash == holdHash,
                    "temp-write fault preserves original");
                ProfileRenameResult replaceFault = updater.RenameCore(idA, holdHash, "Injected 2",
                    ProfileStorageFault.BeforeReplace);
                Check(replaceFault.StorageCode == ProfileStorageCode.InjectedFailure &&
                    repo.Load(idA).ContentHash == holdHash &&
                    Directory.GetFiles(paths.Backups, idA + ".*.bak").Length == backupsBeforeFault,
                    "pre-replace fault preserves original and no new backup");
                Check(Directory.GetFiles(root, ".stage-*.tmp").Length == 0 &&
                    !ProfileWriteSessionGate.IsBlocked, "injected faults cleanup, no recovery lock");

                Check(updater.Rename("invalid", holdHash, "X").Code ==
                    ProfileRenameCode.InvalidSelection, "unsafe ID refused");
                Check(updater.Rename(idA, holdHash, new string('z', 81)).Code ==
                    ProfileRenameCode.InvalidName, "too-long rename refused");
                Check(updater.Rename(idA, holdHash, "Other B").Code ==
                    ProfileRenameCode.NoChange, "unchanged name still no write");

                ProfileWriteSessionGate.Observe(ProfileStorageCode.RecoveryRequired);
                Check(ProfileWriteSessionGate.IsBlocked, "recovery status triggers session lock");
                Check(updater.Rename(idA, holdHash, "Should Not Write").Code ==
                    ProfileRenameCode.WriteLocked && repo.Load(idA).ContentHash == holdHash,
                    "rename blocked while recovery required");
                Check(creator.CreateCore(sample, "Should Not Create", "1.6.4871", utc,
                    "11ac19d5-8ef3-4235-a591-30c2b1000003", ProfileStorageFault.None).StorageCode ==
                    ProfileStorageCode.RecoveryRequired && repo.List().Entries.Count == 2,
                    "create also blocked while recovery required");
                ProfileWriteSessionGate.ResetForIsolatedTests();
                Check(!ProfileWriteSessionGate.IsBlocked, "test-only session gate reset");
                Check(object.ReferenceEquals(XpCustomizerMod.Settings, live) &&
                    (live == null || (object.ReferenceEquals(liveMap, live.skillSpecificXpMultipliers) &&
                    live.careerXpMultiplier == liveCareer)),
                    "live settings and nested references unchanged");

                Log.Message("[XPC-R6P1C2B1][PROFILE_RENAME_AUDIT] status=PASS; revision=" +
                    ProfileUpdateService.Revision + "; checks=" + checks +
                    "; testRoot=ISOLATED_TEMP; identity=KEEP; allSettings=UNCHANGED; " +
                    "backup=PASS; staleHash=CONFLICT; noChange=ZERO_WRITE; " +
                    "failures=AFTER_TEMP_WRITE,BEFORE_REPLACE; sessionRecoveryLock=PASS; " +
                    "userConfigWrites=0; liveSettingsWrites=0; geneDefWrites=0; scribeCalls=0; " +
                    "productionApply=NOT_EXERCISED_BY_THIS_TEST; realIMGUI=MANUAL; build=" + XpCustomizerMod.BuildId);
            }
            finally
            {
                // The self-test is isolated; never leave its deliberate recovery lock set.
                // A pre-existing lock is treated as an error instead of being cleared.
                if (!oldBlocked) ProfileWriteSessionGate.ResetForIsolatedTests();
                if (Directory.Exists(root))
                {
                    try
                    {
                        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) == 0)
                            Directory.Delete(root, true);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("[XPC-R6P1C2B1][PROFILE_RENAME_AUDIT] " +
                            "status=CLEANUP_FAILED; exception=" + ex.GetType().Name +
                            "; build=" + XpCustomizerMod.BuildId);
                    }
                }
            }
        }

        private static void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("R6P1C2B1: " + label);
            checks++;
        }
    }
}
#endif
