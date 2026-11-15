#if DEBUG
using System;
using System.IO;
using System.Text;
using Verse;
using XpCustomizer.Profiles;
using XpCustomizer.UI;

namespace XpCustomizer
{
    /// <summary>
    /// Tests the C1 view model against a disposable folder. No real Config access,
    /// no IMGUI event simulation, and no game settings or GeneDef writes.
    /// </summary>
    internal static class ProfileManagementUiSelfTests
    {
        private const string Revision = "R6P1C1-READONLY-PROFILES-20261106";
        private static int checks;

        internal static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "XPC-R6P1C1-Test-" +
                Guid.NewGuid().ToString("N"));
            bool cleanup = false;
            checks = 0;
            try
            {
                ProfileStoragePaths paths = ProfileStoragePaths.ForIsolatedTestRoot(root);
                ProfileFileRepository store = new ProfileFileRepository(paths, false);
                int factoryCalls = 0;
                ProfileManagementUiState model = new ProfileManagementUiState(delegate
                {
                    factoryCalls++;
                    return store;
                });
                Check(!Directory.Exists(root), "root absent before visiting browser");
                model.EnsureLoaded();
                Check(!Directory.Exists(root), "initial read does not create folders");
                Check(model.VisibleEntries.Count == 0 && model.ListingCode == ProfileStorageCode.Ok,
                    "empty read-only listing");
                Check(factoryCalls == 1 && model.ActiveListReads == 1,
                    "lazy repository; one list on first visit");
                for (int i = 0; i < 100; i++)
                {
                    model.EnsureLoaded();
                    int unusedCount = model.VisibleEntries.Count;
                    string unusedStatus = model.Status;
                    Check(unusedCount == 0 && unusedStatus != null, "cached view " + i);
                }
                Check(model.ActiveListReads == 1 && model.LoadReads == 0 && factoryCalls == 1,
                    "no repeated I/O on cached redraw");

                string id1 = "6ea5ce43-497e-4364-8f0b-91cc1b600001";
                string id2 = "6ea5ce43-497e-4364-8f0b-91cc1b600002";
                string bad = "6ea5ce43-497e-4364-8f0b-91cc1b600003";
                DateTime time = new DateTime(2026, 11, 6, 8, 0, 0, DateTimeKind.Utc);
                XpCustomizerSettings settings = new XpCustomizerSettings();
                settings.careerXpMultiplier = 1.35f;
                ProfileDocument doc1 = ProfileMapper.Export(settings, "검증 프로필 A", "1.6.4871", time, id1);
                ProfileDocument doc2 = ProfileMapper.Export(settings, "검증 프로필 A", "1.6.4871", time, id2);
                Check(store.Create(doc1).Success && store.Create(doc2).Success,
                    "disposable fixture with duplicate names and unique IDs");
                File.WriteAllText(paths.FileForId(bad), "{broken", new UTF8Encoding(false));
                model.Refresh();
                Check(model.VisibleEntries.Count == 3, "two valid + one invalid");
                Check(model.ActiveListReads == 2 && factoryCalls == 1, "only explicit Refresh lists again");
                int i1 = IndexOf(model, id1);
                int i2 = IndexOf(model, id2);
                int invalid = FindInvalid(model);
                Check(i1 >= 0 && i2 >= 0 && i1 != i2 && invalid >= 0,
                    "two duplicate display names have distinct valid rows");
                string before = store.Load(id1).ContentHash;
                Check(model.Select(i1) == ProfileStorageCode.Ok &&
                    model.Selected != null && model.Selected.ProfileId == id1 &&
                    model.SelectedHash == before && model.LoadReads == 1,
                    "select performs one fresh Load without applying");
                Check(model.Select(invalid) == ProfileStorageCode.InvalidFormat &&
                    model.HasErrorSelection && model.Selected == null && model.LoadReads == 1,
                    "damaged file shown without attempting to load unsafe filename");
                Check(model.Select(i2) == ProfileStorageCode.Ok && model.Selected.ProfileId == id2,
                    "other healthy profile selectable after damaged row");
                Check(model.Select(-1) == ProfileStorageCode.NotFound && model.Selected == null,
                    "out-of-range row rejected");
                Check(store.Load(id1).ContentHash == before, "browser never altered profile content");

                // An external editor changed the file after listing: stale selection
                // must not silently accept updated content or mark a stale hash current.
                ProfileStorageResult loaded = store.Load(id1);
                ProfileDocument changed = ProfileJsonCodec.Deserialize(ProfileJsonCodec.Serialize(loaded.Document));
                changed.DisplayName = "외부 편집됨";
                Check(store.Update(changed, loaded.ContentHash).Success, "fixture external update");
                Check(model.Select(i1) == ProfileStorageCode.Conflict &&
                    model.Selected == null && model.HasErrorSelection,
                    "stale listing shows conflict; never writes");
                model.Refresh();
                i1 = IndexOf(model, id1);
                Check(i1 >= 0 && model.Select(i1) == ProfileStorageCode.Ok &&
                    model.Selected.DisplayName == "외부 편집됨", "refresh resolves stale hash");

                // Trash is only listed and previewed. No Restore is available in C1.
                ProfileStorageResult item2 = store.Load(id2);
                Check(store.SoftDelete(id2, item2.ContentHash).Success,
                    "fixture creates a deleted item in disposable trash");
                model.SetTrashView(true);
                Check(model.InTrash && model.TrashListReads == 1 &&
                    model.VisibleEntries.Count == 1 && model.Selected == null,
                    "lazy trash list and selected state cleared");
                int loadsBeforeTrash = model.LoadReads;
                Check(model.Select(0) == ProfileStorageCode.Ok && model.Selected != null &&
                    model.Selected.ProfileId == id2 && model.LoadReads == loadsBeforeTrash,
                    "trash selection uses cached read-only snapshot");
                Check(store.ListTrash().Entries.Count == 1,
                    "trash selection does not restore deleted file");
                model.SetTrashView(false);
                Check(!model.InTrash && model.Selected == null && model.ActiveListReads == 3,
                    "return to existing active listing does not rescan each frame");
                model.Refresh();
                Check(model.VisibleEntries.Count == 2, "explicit active refresh sees deleted profile gone");
                Check(File.Exists(paths.FileForId(bad)), "corrupt file never rewritten or deleted");

                ProfileManagementUiState failure = new ProfileManagementUiState(delegate
                { throw new IOException("simulated path error"); });
                failure.EnsureLoaded();
                Check(failure.ListingCode == ProfileStorageCode.IoError &&
                    failure.VisibleEntries.Count == 0 && failure.Status.Contains("Unable"),
                    "listing error is never displayed as normal Empty");
                Check(ProfileManagementUiState.Explain(ProfileStorageCode.UnsupportedVersion).Contains("Unsupported") &&
                    ProfileManagementUiState.Explain(ProfileStorageCode.RecoveryRequired).Contains("recovery"),
                    "important disk errors receive explicit explanations");
                cleanup = true;
                Log.Message("[XPC-R6P1C1][PROFILE_UI_AUDIT] status=PASS; revision=" + Revision +
                    "; checks=" + checks + "; getterPolls=100; cacheNoRepeatIo=PASS; " +
                    "validAndCorruptRows=PASS; staleHashConflict=REJECTED; trashPreview=READ_ONLY; " +
                    "testRoot=ISOLATED_TEMP; userConfigWrites=0; liveSettingsWrites=0; " +
                    "geneDefWrites=0; scribeCalls=0; liveIMGUI=NOT_TESTED; " +
                    "productionApply=NOT_EXERCISED_BY_THIS_TEST; build=" + XpCustomizerMod.BuildId);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    try { Directory.Delete(root, true); }
                    catch (Exception ex)
                    { Log.Warning("[XPC-R6P1C1][PROFILE_UI_AUDIT] tempCleanup=FAILED; error=" + ex.GetType().Name); }
                }
                if (!cleanup)
                    Log.Error("[XPC-R6P1C1][PROFILE_UI_AUDIT] status=FAIL; stage=READONLY_MODEL; build=" +
                        XpCustomizerMod.BuildId);
            }
        }

        private static int IndexOf(ProfileManagementUiState model, string id)
        {
            for (int i = 0; i < model.VisibleEntries.Count; i++)
            {
                ProfileStorageResult result = model.VisibleEntries[i].Result;
                if (result.Success && result.Document.ProfileId == id) return i;
            }
            return -1;
        }

        private static int FindInvalid(ProfileManagementUiState model)
        {
            for (int i = 0; i < model.VisibleEntries.Count; i++)
                if (!model.VisibleEntries[i].Result.Success) return i;
            return -1;
        }

        private static void Check(bool condition, string stage)
        {
            if (!condition) throw new InvalidOperationException("R6P1C1 UI self-test: " + stage);
            checks++;
        }
    }
}
#endif
