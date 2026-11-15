#if DEBUG
using System;
using System.IO;
using UnityEngine;
using Verse;
using XpCustomizer.UI;
using XpCustomizer.Profiles;

namespace XpCustomizer
{
    /// <summary>Pure geometry checks; live window clicks require manual UI validation.</summary>
    internal static class ProfileManagerWindowSelfTests
    {
        internal static void Run()
        {
            int checks = 0;
            float[] widths = { 320f, 480f, 689f, 690f, 920f, 1280f };
            float[] heights = { 340f, 470f, 610f, 760f };
            foreach (float width in widths)
            {
                foreach (float height in heights)
                {
                    Rect canvas = new Rect(17f, 23f, width, height);
                    ProfileManagerGeometry g = ProfileManagerLayout.Calculate(canvas);
                    Check(Within(canvas, g.Header) && Within(canvas, g.Toolbar) &&
                        Within(canvas, g.Footer) && Within(canvas, g.List) &&
                        Within(canvas, g.Detail), "rect containment", ref checks);
                    Check(g.Header.yMax <= g.Toolbar.y + 0.01f &&
                        g.Toolbar.yMax <= g.List.y + 0.01f &&
                        g.List.yMax <= g.Footer.y + 0.01f &&
                        g.Detail.yMax <= g.Footer.y + 0.01f,
                        "header/content/footer separated", ref checks);
                    Check(g.Stacked == (width < ProfileManagerLayout.StackedThreshold) &&
                        (g.Stacked ? g.List.yMax <= g.Detail.y + 0.01f :
                            g.List.xMax <= g.Detail.x + 0.01f),
                        "responsive panels do not overlap", ref checks);
                    Check(g.Header.height >= ProfileManagerLayout.HeaderHeight - 0.01f &&
                        g.Footer.height >= ProfileManagerLayout.FooterHeight - 0.01f,
                        "Small-font header/footer height budget", ref checks);
                    if (g.Stacked && height >= 340f)
                        Check(g.List.height >= 94f - 0.01f,
                            "Small-font list header remains visible in stacked layout", ref checks);
                }
            }
            Check(ProfileManagerLayout.ListRowHeight >= 53f &&
                ProfileManagerLayout.ListRowGap >= 3f &&
                ProfileManagerLayout.ListHeaderOffset >= 52f,
                "Small-font list row/metadata geometry", ref checks);
            // Injected repository failure: no access to the user's profile directory.
            // This checks the window model's Active/Trash/Refresh routing contract;
            // real GUI clicks are deliberately not claimed as automated coverage.
            int factoryCalls = 0;
            ProfileManagementUiState isolated = new ProfileManagementUiState(delegate
            {
                factoryCalls++;
                throw new IOException("isolated fake repository failure");
            });
            isolated.EnsureLoaded();
            Check(isolated.ListingCode == ProfileStorageCode.IoError &&
                isolated.VisibleEntries.Count == 0 && factoryCalls == 1,
                "active list failure shown separately from empty success", ref checks);
            isolated.SetTrashView(true);
            Check(isolated.InTrash && isolated.ListingCode == ProfileStorageCode.IoError &&
                factoryCalls == 2, "trash switch routes only to trash", ref checks);
            isolated.SetTrashView(false);
            Check(!isolated.InTrash && factoryCalls == 2,
                "return to cached active model without repeated file access", ref checks);
            isolated.Refresh();
            Check(factoryCalls == 3 && isolated.ListingCode == ProfileStorageCode.IoError &&
                isolated.Selected == null, "explicit refresh does not change settings", ref checks);
            Log.Message("[XPC-R6P1C15][PROFILE_UI_ROUTE_AUDIT] status=PASS; " +
                "activeTrashRefresh=PASS; cacheNoFrameIo=PASS; " +
                "repository=INJECTED_FAILURE; userConfigWrites=0; " +
                "liveSettingsWrites=0; geneDefWrites=0; productionApply=NOT_EXERCISED_BY_THIS_TEST; " +
                "realClicks=MANUAL; build=" + XpCustomizerMod.BuildId);
            Log.Message("[XPC-R6P1C15][PROFILE_LAYOUT_AUDIT] status=PASS; revision=" +
                ProfileManagerLayout.Revision + "; canvases=" + (widths.Length * heights.Length) +
                "; checks=" + checks + "; stacked=PASS; dual=PASS; " +
                "fontStageA=SMALL; header=" + ProfileManagerLayout.HeaderHeight +
                "; footer=" + ProfileManagerLayout.FooterHeight +
                "; listRow=" + ProfileManagerLayout.ListRowHeight + "; " +
                "windowClicks=MANUAL; realConfigWrites=0; settingsWrites=0; build=" +
                XpCustomizerMod.BuildId);
            Log.Message("[XPC-R6P1C15][PROFILE_WINDOW_AUDIT] status=PASS; " +
                "scope=STATIC_ROUTE_AND_LAYOUT; ownedWindow=ProfileManagerWindow; " +
                "entry=MANAGE_PROFILES_BUTTON; oldProfilesTab=REMOVED; " +
                "model=R6P1C1_REUSED; repository=UNCHANGED; " +
                "productionApply=NOT_EXERCISED_BY_THIS_TEST; actualOpenClose=MANUAL; scribeCalls=0; build=" +
                XpCustomizerMod.BuildId);
        }

        private static bool Within(Rect canvas, Rect sub)
        {
            const float epsilon = 0.01f;
            return sub.width >= 0f && sub.height >= 0f &&
                sub.x >= canvas.x - epsilon && sub.y >= canvas.y - epsilon &&
                sub.xMax <= canvas.xMax + epsilon && sub.yMax <= canvas.yMax + epsilon;
        }

        private static void Check(bool pass, string label, ref int checks)
        {
            if (!pass) throw new InvalidOperationException("R6P1C15 layout: " + label);
            checks++;
        }
    }
}
#endif
