#if DEBUG
using System;
using System.IO;
using System.Text;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer
{
    /// <summary>Isolated disk regression. Never touches the live Config or active settings.</summary>
    internal static class BundledProfileBP12SelfTests
    {
        private static int checks;
        private static void Check(bool condition, string name)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("BP12 " + name);
        }

        internal static void Run()
        {
            checks = 0;
            string temp = Path.Combine(Path.GetTempPath(),
                "XPC_BP12_" + Guid.NewGuid().ToString("N"));
            string mod = Path.Combine(temp, "Mod");
            string presets = Path.Combine(mod, "Presets");
            try
            {
                Directory.CreateDirectory(presets);
                ProfileDocument longDoc = MakeSource(BundledProfileInstaller.LongId, "Long (x3)", 3f);
                ProfileDocument veryLongDoc = MakeSource(BundledProfileInstaller.VeryLongId, "Very Long (x6)", 6f);
                string longJson = ProfileJsonCodec.Serialize(longDoc);
                string veryJson = ProfileJsonCodec.Serialize(veryLongDoc);
                File.WriteAllText(Path.Combine(presets, "long.json"), longJson, new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(presets, "verylong.json"), veryJson, new UTF8Encoding(false));

                string config = Path.Combine(temp, "ConfigNew");
                ProfileFileRepository repo = Repository(config);
                BundledProfileInstaller.SeedReport first = Seed(mod, config, repo);
                Check(first.Success && first.Created == 2, "new user gets two");
                ProfileStorageResult firstLong = repo.Load(BundledProfileInstaller.LongId);
                Check(firstLong.Success && firstLong.Document.DisplayName == "Long (x3)", "long named");
                Check(repo.Load(BundledProfileInstaller.VeryLongId).Success, "very long created");
                string hash = firstLong.ContentHash;
                BundledProfileInstaller.SeedReport again = Seed(mod, config, repo);
                Check(again.Success && again.Created == 0 && again.AlreadyHandled == 2, "second visit no write");
                Check(repo.Load(BundledProfileInstaller.LongId).ContentHash == hash, "second visit bytes same");

                ProfileDocument edited = ProfileJsonCodec.Deserialize(ProfileJsonCodec.Serialize(firstLong.Document));
                edited.DisplayName = "My personal edit";
                ProfileStorageResult updated = repo.Update(edited, hash);
                Check(updated.Success, "external user edit succeeds");
                BundledProfileInstaller.SeedReport afterEdit = Seed(mod, config, repo);
                Check(afterEdit.Success && afterEdit.Created == 0, "existing edit no seed");
                Check(repo.Load(BundledProfileInstaller.LongId).Document.DisplayName == "My personal edit",
                    "user modified doc preserved");

                ProfileStorageResult del = repo.SoftDelete(BundledProfileInstaller.LongId, updated.ContentHash);
                Check(del.Success, "user delete succeeds");
                BundledProfileInstaller.SeedReport afterDelete = Seed(mod, config, repo);
                Check(afterDelete.Success && afterDelete.Created == 0, "deleted not recreated");
                Check(repo.Load(BundledProfileInstaller.LongId).Code == ProfileStorageCode.NotFound,
                    "deleted remains absent");

                string legacyConfig = Path.Combine(temp, "ConfigLegacy");
                ProfileFileRepository legacy = Repository(legacyConfig);
                ProfileStorageResult prior = legacy.Create(longDoc);
                Check(prior.Success, "legacy profile exists");
                Check(legacy.SoftDelete(BundledProfileInstaller.LongId, prior.ContentHash).Success,
                    "legacy goes to trash");
                BundledProfileInstaller.SeedReport legacyRun = Seed(mod, legacyConfig, legacy);
                Check(legacyRun.Success && legacyRun.PreviouslyRemoved == 1 && legacyRun.Created == 1,
                    "legacy deletion detected and other seeded");
                Check(legacy.Load(BundledProfileInstaller.LongId).Code == ProfileStorageCode.NotFound,
                    "legacy deletion not overridden");

                string oldConfig = Path.Combine(temp, "ConfigExisting");
                ProfileFileRepository existing = Repository(oldConfig);
                Check(existing.Create(longDoc).Success, "existing seeded before bundle");
                BundledProfileInstaller.SeedReport existingRun = Seed(mod, oldConfig, existing);
                Check(existingRun.Success && existingRun.Adopted == 1 && existingRun.Created == 1,
                    "existing GUID adopted, missing created");

                string crashConfig = Path.Combine(temp, "ConfigInterrupted");
                ProfileFileRepository crashed = Repository(crashConfig);
                BundledProfileInstaller.SeedReport interrupted = BundledProfileInstaller.EnsureAt(
                    mod, crashConfig, crashed, false, ProfileStorageFault.AfterTempWrite);
                Check(interrupted.Failed == 2 && interrupted.Created == 0, "injected failure leaves pending");
                BundledProfileInstaller.SeedReport recovered = Seed(mod, crashConfig, crashed);
                Check(recovered.Success && recovered.Created == 2, "pending retried without duplication");

                string invalidConfig = Path.Combine(temp, "ConfigInvalidSource");
                ProfileFileRepository invalidRepo = Repository(invalidConfig);
                File.WriteAllText(Path.Combine(presets, "long.json"), "{broken", new UTF8Encoding(false));
                BundledProfileInstaller.SeedReport invalid = Seed(mod, invalidConfig, invalidRepo);
                Check(invalid.Failed == 1 && invalid.Created == 1, "invalid source isolated");
                Check(invalidRepo.Load(BundledProfileInstaller.LongId).Code == ProfileStorageCode.NotFound,
                    "invalid source writes nothing");
                File.WriteAllText(Path.Combine(presets, "long.json"), longJson, new UTF8Encoding(false));
                BundledProfileInstaller.SeedReport repaired = Seed(mod, invalidConfig, invalidRepo);
                Check(repaired.Success && repaired.Created == 1 && repaired.AlreadyHandled == 1,
                    "source repair resumes once");

                string tamperedConfig = Path.Combine(temp, "ConfigTamperedState");
                string tamperedOwner = Path.Combine(tamperedConfig, "XpCustomizer");
                Directory.CreateDirectory(tamperedOwner);
                File.WriteAllText(Path.Combine(tamperedOwner, "BundledProfilesState.json"),
                    "corrupted!", new UTF8Encoding(false));
                ProfileFileRepository tamperedRepo = Repository(tamperedConfig);
                BundledProfileInstaller.SeedReport tampered = Seed(mod, tamperedConfig, tamperedRepo);
                Check(tampered.Failed == 1 && tampered.Created == 0, "tampered state fail closed");
                Check(tamperedRepo.List().Entries.Count == 0, "tampered state leaves profiles untouched");

                Log.Message("[XPC-BP12][BUNDLED_PROFILE_SELFTEST] status=PASS; checks=" +
                    checks + "; scenarios=NEW,REOPEN,EDIT,DELETE,LEGACY_TRASH,ADOPT," +
                    "INTERRUPTED_RETRY,INVALID_SOURCE,TAMPERED_STATE; isolatedRoot=True; " +
                    "liveConfigWrites=0; liveSettingsWrites=0; profileApply=0; build=" +
                    XpCustomizerMod.BuildId);
            }
            finally
            {
                try { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
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
    }
}
#endif
