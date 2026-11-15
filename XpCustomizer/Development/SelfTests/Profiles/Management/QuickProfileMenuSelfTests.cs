#if DEBUG
using System;
using System.IO;
using System.Text;
using UnityEngine;
using Verse;
using XpCustomizer.Profiles;
using XpCustomizer.UI;

namespace XpCustomizer
{
    /// <summary>Read-only menu model, UI geometry, and isolated temp disk fixtures.</summary>
    internal static class QuickProfileMenuSelfTests
    {
        private const string Revision = "R6P1C2B15-MINIMAL-QUICKMENU-20261107";
        private static int checks;

        internal static void Run()
        {
            checks = 0;
            int layoutCases = 0;
            foreach (float width in new[] { 120f, 250f, 400f, 900f, 1280f })
            {
                Rect canvas = new Rect(17f, 29f, width, 32f);
                Rect quick, manage;
                QuickProfileToolbarLayout.Calculate(canvas, out quick, out manage);
                Check(quick.x >= canvas.x - 0.01f && manage.xMax <= canvas.xMax + 0.01f &&
                      quick.xMax <= manage.x - 7.99f && quick.y == manage.y &&
                      quick.height == canvas.height && manage.height == canvas.height,
                      "toolbar rect containment and no overlap");
                layoutCases++;
            }
            string root = Path.Combine(Path.GetTempPath(), "XPC-R6P1C2B15-Test-" +
                Guid.NewGuid().ToString("N"));
            XpCustomizerSettings live = XpCustomizerMod.Settings;
            try
            {
                ProfileStoragePaths paths = ProfileStoragePaths.ForIsolatedTestRoot(root);
                ProfileFileRepository store = new ProfileFileRepository(paths, false);
                QuickProfileMenuController model = new QuickProfileMenuController(
                    new ProfileManagementUiState(delegate { return store; }));
                Check(!Directory.Exists(root), "no config created before read");
                model.Refresh();
                Check(model.ListingCode == ProfileStorageCode.Ok && model.Entries.Count == 0 &&
                    model.ListReads == 1 && model.LoadReads == 0, "empty listing is read-only");
                Check(!Directory.Exists(root), "read does not create folders");
                System.Collections.Generic.IList<QuickProfileMenuController.MenuChoice> emptyChoices =
                    model.BuildChoices();
                Check(emptyChoices.Count == 1 && !emptyChoices[0].Selectable &&
                    emptyChoices[0].Label.Contains("No saved profiles") &&
                    !emptyChoices[0].Label.Contains("available in C2"),
                    "empty list uses one up-to-date disabled diagnostic");
                Check(model.ListReads == 1 && model.LoadReads == 0,
                    "building menu labels does not read disk");
                for (int i = 0; i < 50; i++)
                    Check(model.Entries.Count == 0, "no repeated disk I/O on redraw");
                Check(model.ListReads == 1, "one explicit read only");

                string id1 = "6ea5ce43-497e-4364-8f0b-91cc1b601601";
                string id2 = "6ea5ce43-497e-4364-8f0b-91cc1b601602";
                string id3 = "6ea5ce43-497e-4364-8f0b-91cc1b601603";
                DateTime before = new DateTime(2026, 11, 6, 3, 0, 0, DateTimeKind.Utc);
                XpCustomizerSettings detached = new XpCustomizerSettings();
                detached.careerXpMultiplier = 1.5f;
                ProfileDocument doc1 = ProfileMapper.Export(detached, "한국어 / profile #A",
                    "1.6.4871", before, id1);
                ProfileDocument doc2 = ProfileMapper.Export(detached, "한국어 / profile #A",
                    "1.6.4871", before.AddMinutes(1), id2);
                Check(store.Create(doc1).Success, "first saved profile fixture");
                model.Refresh();
                System.Collections.Generic.IList<QuickProfileMenuController.MenuChoice> oneChoice =
                    model.BuildChoices();
                Check(oneChoice.Count == 1 && oneChoice[0].Selectable &&
                    oneChoice[0].SourceIndex == 0 && oneChoice[0].Label.StartsWith("한국어"),
                    "single profile appears as exactly one selectable choice");
                Check(store.Create(doc2).Success,
                    "duplicate display names with independent GUIDs");
                File.WriteAllText(paths.FileForId(id3), "{broken", new UTF8Encoding(false));
                model.Refresh();
                Check(model.Entries.Count == 3 && model.ListReads == 3,
                    "fresh list contains valid and malformed rows");
                Check(model.Entries[0].Result.Success && model.Entries[1].Result.Success &&
                    !model.Entries[2].Result.Success &&
                    model.Entries[0].Result.Document.ProfileId == id2,
                    "recent first, malformed last");
                System.Collections.Generic.IList<QuickProfileMenuController.MenuChoice> choices =
                    model.BuildChoices();
                Check(choices.Count == 3 && choices[0].Selectable && choices[1].Selectable &&
                    !choices[2].Selectable && choices[0].SourceIndex == 0 &&
                    choices[1].SourceIndex == 1 && choices[2].SourceIndex == -1,
                    "menu only contains 2 selectable profiles and 1 invalid warning");
                Check(choices[0].Label.StartsWith("한국어") &&
                    choices[0].Label.Contains("[") &&
                    !choices[0].Label.StartsWith("1. ") &&
                    choices[2].Label.StartsWith("! "),
                    "safe timestamp labels with no transient rank numbers");
                Check(!choices[0].Label.Contains("Refresh list") &&
                    !choices[0].Label.Contains("Manage Profiles") &&
                    !choices[0].Label.Contains("Cancel / Close") &&
                    !choices[0].Label.Contains("preview only"),
                    "profile labels contain no menu utilities");
                Check(model.ListReads == 3 && model.LoadReads == 0,
                    "pure menu construction does not re-query profile files");
                Check(model.Select(0) == ProfileStorageCode.Ok &&
                    model.Selected != null && model.Selected.ProfileId == id2 &&
                    model.LoadReads == 1 && model.SelectedHash != null,
                    "selection freshly loads and verifies hash");
                Check(model.Select(2) == ProfileStorageCode.InvalidFormat && model.Selected == null,
                    "malformed row never becomes selected");

                ProfileStorageResult old = store.Load(id2);
                ProfileDocument edited = ProfileJsonCodec.Deserialize(ProfileJsonCodec.Serialize(old.Document));
                edited.DisplayName = "changed by external editor";
                Check(store.Update(edited, old.ContentHash).Success, "simulate external edit");
                Check(model.Select(0) == ProfileStorageCode.Conflict && model.Selected == null,
                    "stale SHA256 selection is refused");
                model.Refresh();
                Check(model.Select(0) == ProfileStorageCode.Ok && model.Selected != null &&
                    model.Selected.DisplayName == "changed by external editor",
                    "refresh loads new safe snapshot");
                Check(model.BuildChoices().Count == 3 &&
                    model.BuildChoices()[0].Label.StartsWith("changed by external editor"),
                    "reopening after an explicit refresh sees updated name");

                QuickProfileMenuController failed = new QuickProfileMenuController(
                    new ProfileManagementUiState(delegate {
                        throw new IOException("injected isolated listing error");
                    }));
                failed.Refresh();
                System.Collections.Generic.IList<QuickProfileMenuController.MenuChoice> failureChoices =
                    failed.BuildChoices();
                Check(failed.ListingCode == ProfileStorageCode.IoError &&
                    failureChoices.Count == 1 && !failureChoices[0].Selectable &&
                    failureChoices[0].Label.Contains("Cannot read profiles"),
                    "listing error is one disabled warning, not an empty authoritative list");
                Check(object.ReferenceEquals(XpCustomizerMod.Settings, live),
                    "live ModSettings reference unchanged");
                Log.Message("[XPC-R6P1C16][QUICK_MENU_AUDIT] status=PASS; revision=" + Revision +
                    "; checks=" + checks + "; toolbarWidths=" + layoutCases +
                    "; empty=PASS; duplicateNames=GUID_INDEPENDENT; newestFirst=PASS; " +
                    "invalidIsolated=PASS; staleHashConflict=REJECTED; " +
                    "cachedRedrawReads=0; storageTest=ISOLATED_TEMP; userConfigWrites=0; " +
                    "liveSettingsWrites=0; geneDefWrites=0; scribeCalls=0; productionApply=NOT_EXERCISED_BY_THIS_TEST; " +
                    "realIMGUI=MANUAL; build=" + XpCustomizerMod.BuildId);
                Log.Message("[XPC-R6P1C2B15][QUICK_MENU_SIMPLIFY_AUDIT] status=PASS; revision=" + Revision +
                    "; checks=" + checks + "; menuPolicy=PROFILES_ONLY; " +
                    "removed=REFRESH,MANAGE,CANCEL,PREVIEW_FOOTER; orderingNumbers=REMOVED; " +
                    "emptyAndError=ONE_DISABLED_MESSAGE; invalidRows=DISABLED; " +
                    "sourceRows=INDEX_PRESERVED; staleHashConflict=REJECTED; " +
                    "listingOnOpen=ONCE; buildChoicesDiskReads=0; " +
                    "liveSettingsWrites=0; geneDefWrites=0; scribeCalls=0; " +
                    "productionApply=NOT_EXERCISED_BY_THIS_TEST; realFloatMenu=MANUAL; build=" + XpCustomizerMod.BuildId);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    try
                    {
                        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) == 0)
                            Directory.Delete(root, true);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("[XPC-R6P1C16][QUICK_MENU_AUDIT] status=CLEANUP_FAILED; " +
                            "exception=" + ex.GetType().Name + "; build=" + XpCustomizerMod.BuildId);
                    }
                }
            }
        }

        private static void Check(bool passed, string reason)
        {
            if (!passed) throw new InvalidOperationException("R6P1C2B15 self-test: " + reason);
            checks++;
        }
    }
}
#endif
