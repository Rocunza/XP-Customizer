#if DEBUG
using System;
using System.IO;
using System.Text;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer
{
    /// <summary>
    /// BP4 isolated transaction and backward-compatibility tests. Creates no
    /// live profile or XML files; requires no map, pawn, or GUI interactions.
    /// </summary>
    internal static class BundledProfileBP4SelfTests
    {
        private static int checks;
        private static void Check(bool expected, string name)
        {
            checks++;
            if (!expected) throw new InvalidOperationException("BP4 " + name);
        }

        internal static void Run()
        {
            checks = 0;
            string root = Path.Combine(Path.GetTempPath(),
                "XPC_BP4_" + Guid.NewGuid().ToString("N"));
            string mod = Path.Combine(root, "Mod");
            string presets = Path.Combine(mod, "Presets");
            try
            {
                Directory.CreateDirectory(presets);
                ProfileDocument longDoc = MakeSource(BundledProfileInstaller.LongId, "Long (x3)", 3f);
                ProfileDocument veryDoc = MakeSource(BundledProfileInstaller.VeryLongId, "Very Long (x6)", 6f);
                string longJson = ProfileJsonCodec.Serialize(longDoc);
                File.WriteAllText(Path.Combine(presets, "long.json"), longJson,
                    new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(presets, "verylong.json"),
                    ProfileJsonCodec.Serialize(veryDoc), new UTF8Encoding(false));

                // Direct Reset before any profile menu: first install + idempotence.
                string freshConfig = Path.Combine(root, "Fresh");
                ProfileFileRepository fresh = Repository(freshConfig);
                BundledProfileInstaller.RestoreReport first = Restore(mod, freshConfig, fresh);
                Check(first.Success && first.Created == 2, "reset-first-open creates 2");
                Check(fresh.Load(BundledProfileInstaller.LongId).Success, "first Long ID intact");
                Check(fresh.Load(BundledProfileInstaller.VeryLongId).Success,
                    "first Very Long ID intact");
                BundledProfileInstaller.RestoreReport again = Restore(mod, freshConfig, fresh);
                Check(again.Success && again.Created == 0 && again.Preserved == 2,
                    "repeat reset is idempotent");
                Check(File.ReadAllText(Marker(freshConfig)).Contains("\"schema\":2"),
                    "new installs write schema2");

                // User modifications survive Reset. Deleted originals remain in Trash.
                string config = Path.Combine(root, "Edited");
                ProfileFileRepository repo = Repository(config);
                BundledProfileInstaller.SeedReport seed = Seed(mod, config, repo);
                Check(seed.Success && seed.Created == 2, "BP12 first-open still works");
                ProfileStorageResult originalLong = repo.Load(BundledProfileInstaller.LongId);
                ProfileStorageResult originalVery = repo.Load(BundledProfileInstaller.VeryLongId);
                ProfileDocument edited = ProfileJsonCodec.Deserialize(
                    ProfileJsonCodec.Serialize(originalVery.Document));
                edited.DisplayName = "My very long";
                ProfileStorageResult saved = repo.Update(edited, originalVery.ContentHash);
                Check(saved.Success, "personal edit succeeds");
                Check(Restore(mod, config, repo).Preserved == 2, "reset preserves both existing");
                Check(repo.Load(BundledProfileInstaller.VeryLongId).ContentHash == saved.ContentHash,
                    "user edit bytes unchanged");
                Check(repo.SoftDelete(BundledProfileInstaller.LongId,
                    originalLong.ContentHash).Success, "original Long deleted to trash");
                int trashBefore = repo.ListTrash().Entries.Count;
                Check(trashBefore == 1, "trash contains deleted Long");
                Check(Seed(mod, config, repo).Created == 0, "menu cannot resurrect deletion");
                BundledProfileInstaller.RestoreReport restored = Restore(mod, config, repo);
                Check(restored.Success && restored.Created == 1 && restored.Preserved == 1,
                    "reset recreates only missing Long");
                Check(repo.ListTrash().Entries.Count == trashBefore, "trash preserved after reset");
                ProfileStorageListing current = repo.List();
                Check(current.Code == ProfileStorageCode.Ok && current.Entries.Count == 2,
                    "exactly 2 active profiles");
                string recreatedId = FindByName(current, "Long (x3)");
                Check(recreatedId != null && recreatedId != BundledProfileInstaller.LongId,
                    "deleted Long gets a new GUID");
                Check(repo.Load(BundledProfileInstaller.VeryLongId).ContentHash == saved.ContentHash,
                    "unrelated user edit remains unchanged");
                Check(Restore(mod, config, repo).Created == 0, "reset twice creates no duplicate");
                Check(Seed(mod, config, repo).Created == 0, "menu still cannot duplicate restored");
                ProfileStorageResult recreated = repo.Load(recreatedId);
                Check(recreated.Success, "recreated profile can be loaded");
                Check(repo.SoftDelete(recreatedId, recreated.ContentHash).Success,
                    "recreated copy is deletable");
                Check(Seed(mod, config, repo).Created == 0,
                    "deleted restored copy does not auto-revive");
                BundledProfileInstaller.RestoreReport recreatedAgain = Restore(mod, config, repo);
                Check(recreatedAgain.Created == 1 && recreatedAgain.Preserved == 1,
                    "subsequent explicit reset regenerates one");
                Check(repo.List().Entries.Count == 2, "repeated deletion/reset remains bounded");

                // Existing schema1 BP12 journal migrates without resurrecting on menu open.
                string legacyConfig = Path.Combine(root, "LegacyV1");
                ProfileFileRepository legacy = Repository(legacyConfig);
                Check(legacy.Create(longDoc).Success, "legacy Long present");
                Check(legacy.Create(veryDoc).Success, "legacy Very Long present");
                ProfileStorageResult legacyLong = legacy.Load(BundledProfileInstaller.LongId);
                Check(legacy.SoftDelete(BundledProfileInstaller.LongId,
                    legacyLong.ContentHash).Success, "legacy Long deleted");
                Directory.CreateDirectory(Path.GetDirectoryName(Marker(legacyConfig)));
                File.WriteAllText(Marker(legacyConfig),
                    "{\"schema\":1,\"long\":\"D\",\"veryLong\":\"D\"}\n",
                    new UTF8Encoding(false));
                Check(Seed(mod, legacyConfig, legacy).Created == 0,
                    "schema1 D does not revive deleted profile on menu");
                BundledProfileInstaller.RestoreReport legacyRestore = Restore(mod, legacyConfig, legacy);
                Check(legacyRestore.Success && legacyRestore.Created == 1 &&
                    legacyRestore.Preserved == 1, "legacy D restored via reset");
                Check(File.ReadAllText(Marker(legacyConfig)).Contains("\"schema\":2"),
                    "schema1 migrated to schema2");
                Check(legacy.ListTrash().Entries.Count == 1, "legacy trash intact");
                Check(legacy.List().Entries.Count == 2, "legacy migration no duplicate");

                string legacyBoth = Path.Combine(root, "LegacyBothPresent");
                ProfileFileRepository bothRepo = Repository(legacyBoth);
                Check(bothRepo.Create(longDoc).Success && bothRepo.Create(veryDoc).Success,
                    "legacy complete set exists");
                Directory.CreateDirectory(Path.GetDirectoryName(Marker(legacyBoth)));
                File.WriteAllText(Marker(legacyBoth),
                    "{\"schema\":1,\"long\":\"D\",\"veryLong\":\"D\"}\n",
                    new UTF8Encoding(false));
                string existingLongHash = bothRepo.Load(BundledProfileInstaller.LongId).ContentHash;
                BundledProfileInstaller.RestoreReport bothResult = Restore(mod, legacyBoth, bothRepo);
                Check(bothResult.Success && bothResult.Created == 0 && bothResult.Preserved == 2,
                    "legacy complete set preserved");
                Check(File.ReadAllText(Marker(legacyBoth)).Contains("\"schema\":2"),
                    "v1 state migrates even when both profiles exist");
                Check(bothRepo.Load(BundledProfileInstaller.LongId).ContentHash == existingLongHash,
                    "v1 migration never rewrites existing profile");

                // A reset failure keeps R intent; next menu cannot finish/reset it.
                string interruptedConfig = Path.Combine(root, "Interrupted");
                ProfileFileRepository interruptedRepo = Repository(interruptedConfig);
                BundledProfileInstaller.RestoreReport failure = BundledProfileInstaller.RestoreAt(
                    mod, interruptedConfig, interruptedRepo, false,
                    ProfileStorageFault.AfterTempWrite);
                Check(failure.Failed == 2 && failure.Created == 0,
                    "failure leaves two reset intents");
                BundledProfileStateStore.State inProgress =
                    BundledProfileStateStore.Read(Marker(interruptedConfig));
                Check(inProgress.Long == 'R' && inProgress.VeryLong == 'R',
                    "reset-specific intent persisted");
                BundledProfileInstaller.SeedReport menu = Seed(mod, interruptedConfig, interruptedRepo);
                Check(menu.Created == 0 && menu.AlreadyHandled == 2,
                    "menu does not resume reset intent");
                Check(interruptedRepo.List().Entries.Count == 0,
                    "menu leaves incomplete reset untouched");
                BundledProfileInstaller.RestoreReport recovered = Restore(mod,
                    interruptedConfig, interruptedRepo);
                Check(recovered.Success && recovered.Created == 2,
                    "next confirmed reset resumes intents");
                Check(interruptedRepo.List().Entries.Count == 2, "interrupted retry no duplicates");

                // Simulate a process exit AFTER Create but BEFORE journal R -> D.
                string postCreateConfig = Path.Combine(root, "InterruptedAfterCreate");
                ProfileFileRepository postCreateRepo = Repository(postCreateConfig);
                BundledProfileInstaller.RestoreAt(mod, postCreateConfig, postCreateRepo,
                    false, ProfileStorageFault.AfterTempWrite);
                Check(BundledProfileStateStore.Read(Marker(postCreateConfig)).Long == 'R',
                    "post-create simulation prepared R");
                Check(postCreateRepo.Create(longDoc).Success,
                    "synthetic crash leaves a finished Long copy");
                string postCreateHash = postCreateRepo.Load(BundledProfileInstaller.LongId).ContentHash;
                BundledProfileInstaller.RestoreReport postCreateRecovery = Restore(mod,
                    postCreateConfig, postCreateRepo);
                Check(postCreateRecovery.Success && postCreateRecovery.Created == 1 &&
                    postCreateRecovery.Preserved == 1,
                    "post-create crash adopts Long and creates only Very Long");
                Check(postCreateRepo.Load(BundledProfileInstaller.LongId).ContentHash ==
                    postCreateHash, "post-create crash adoption has no overwrite");
                Check(postCreateRepo.List().Entries.Count == 2,
                    "post-create crash recovers exactly two profiles");

                // Source corruption must not overwrite existing profile/other preset.
                string invalidConfig = Path.Combine(root, "InvalidSource");
                ProfileFileRepository invalidRepo = Repository(invalidConfig);
                Check(Seed(mod, invalidConfig, invalidRepo).Created == 2, "invalid test seeded");
                ProfileStorageResult invalidLong = invalidRepo.Load(BundledProfileInstaller.LongId);
                Check(invalidRepo.SoftDelete(BundledProfileInstaller.LongId,
                    invalidLong.ContentHash).Success, "invalid test deleted Long");
                File.WriteAllText(Path.Combine(presets, "long.json"), "{broken",
                    new UTF8Encoding(false));
                BundledProfileInstaller.RestoreReport invalid = Restore(mod, invalidConfig, invalidRepo);
                Check(invalid.Failed == 1 && invalid.Created == 0 && invalid.Preserved == 1,
                    "corrupt bundled JSON isolated");
                Check(invalidRepo.List().Entries.Count == 1,
                    "corrupt source does not mutate personal files");
                File.WriteAllText(Path.Combine(presets, "long.json"), longJson,
                    new UTF8Encoding(false));
                Check(Restore(mod, invalidConfig, invalidRepo).Created == 1,
                    "repaired source restored on next reset");

                // Never erase or regenerate a corrupt/newer journal.
                string tamperConfig = Path.Combine(root, "Tampered");
                Directory.CreateDirectory(Path.GetDirectoryName(Marker(tamperConfig)));
                File.WriteAllText(Marker(tamperConfig), "{\"schema\":999}",
                    new UTF8Encoding(false));
                ProfileFileRepository tamperedRepo = Repository(tamperConfig);
                BundledProfileInstaller.RestoreReport denied = Restore(mod,
                    tamperConfig, tamperedRepo);
                Check(denied.Failed > 0 && denied.Created == 0,
                    "unknown future state fails closed");
                Check(tamperedRepo.List().Entries.Count == 0, "tampered state no file writes");
                Check(File.ReadAllText(Marker(tamperConfig)) == "{\"schema\":999}",
                    "tampered state not overwritten");

                Log.Message("[XPC-BP4][BUNDLED_RESTORE_SELFTEST] status=PASS; checks=" +
                    checks + "; scenarios=INITIAL_RESET,REPEAT,EDIT,DELETE,TRASH," +
                    "REDELETE,SCHEMA1_MIGRATION,INTERRUPTED_R,POSTCREATE_CRASH,INVALID_SOURCE,FUTURE_STATE; " +
                    "isolatedRoot=True; liveConfigWrites=0; liveSettingsWrites=0; " +
                    "autoApply=NEVER; build=" + XpCustomizerMod.BuildId);
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static string FindByName(ProfileStorageListing list, string name)
        {
            foreach (ProfileStorageEntry entry in list.Entries)
                if (entry.Result.Success && entry.Result.Document.DisplayName == name)
                    return entry.Result.Document.ProfileId;
            return null;
        }

        private static ProfileDocument MakeSource(string id, string name, float multiplier)
        {
            ProfileDocument doc = ProfileMapper.Export(new XpCustomizerSettings(),
                name, "1.6.4871", DateTime.UtcNow, id);
            doc.Settings.Sections["career"]["careerXpMultiplier"] = multiplier;
            ProfileJsonCodec.Validate(doc);
            return doc;
        }

        private static ProfileFileRepository Repository(string config)
        { return new ProfileFileRepository(ProfileStoragePaths.ForConfigRoot(config), false); }
        private static BundledProfileInstaller.SeedReport Seed(string mod, string config,
            ProfileFileRepository repo)
        { return BundledProfileInstaller.EnsureAt(mod, config, repo, false); }
        private static BundledProfileInstaller.RestoreReport Restore(string mod, string config,
            ProfileFileRepository repo)
        { return BundledProfileInstaller.RestoreAt(mod, config, repo, false); }
        private static string Marker(string config)
        { return Path.Combine(config, "XpCustomizer", "BundledProfilesState.json"); }
    }
}
#endif
