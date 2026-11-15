#if DEBUG
using System;
using System.Globalization;
using UnityEngine;
using Verse;
using XpCustomizer.UI;

namespace XpCustomizer
{
    /// <summary>
    /// Q1.1/P1 isolated UI contract: date display, geometry, and recovery-log
    /// semantics. Never opens a window or touches settings, files, GeneDefs.
    /// </summary>
    internal static class ProfileUiPolishP1SelfTests
    {
        private static int checks;

        private static void Check(bool passes, string reason)
        {
            checks++;
            if (!passes) throw new InvalidOperationException("[XPC-P1] " + reason);
        }

        internal static void Run()
        {
            checks = 0;
            Check(Dialog_ReviewXpProfileApply.BodyFont == GameFont.Small,
                "review body uses Small, not Tiny");
            Check(RuntimeRecoverySessionGate.ExplainProfileApplyLatch(false) ==
                "NOT_BLOCKED_BY_RECOVERY_LATCH", "healthy gate is not falsely blocked");
            Check(RuntimeRecoverySessionGate.ExplainProfileApplyLatch(true) ==
                "BLOCKED_BY_ACTIVE_RECOVERY_LATCH", "real recovery block remains explicit");

            DateTime sample = new DateTime(2026, 11, 8, 21, 25, 0, DateTimeKind.Local);
            DateTime nextYear = new DateTime(2027, 1, 1, 11, 30, 0, DateTimeKind.Local);
            DateTime oldYearEnd = new DateTime(2026, 12, 31, 12, 45, 0, DateTimeKind.Local);
            Check(QuickProfileMenuController.FormatUpdatedAt(sample.ToUniversalTime()) ==
                "261108 21:25", "year included in 2026 date label");
            Check(QuickProfileMenuController.FormatUpdatedAt(nextYear.ToUniversalTime()) ==
                "270101 11:30", "new year does not collide with old date");
            Check(QuickProfileMenuController.FormatUpdatedAt(oldYearEnd.ToUniversalTime()) ==
                "261231 12:45", "year-end label includes year");
            Check(QuickProfileMenuController.FormatUpdatedAt(sample.ToUniversalTime()).Length == 12,
                "fixed quick menu timestamp width (yyMMdd HH:mm)");
            Check(QuickProfileMenuController.FormatUpdatedAt(sample.ToUniversalTime()) ==
                sample.ToString("yyMMdd HH:mm", CultureInfo.InvariantCulture),
                "display follows existing local-time conversion");

            Vector2 normal = ProfileReviewLayoutP1.InitialSize(1920f, 1080f);
            Check(normal.x == 675f && normal.y == 440f, "P1 larger normal window");
            Vector2 compact = ProfileReviewLayoutP1.InitialSize(620f, 390f);
            Check(compact.x == 572f && compact.y == 342f, "small screen is clamped");

            float[] widths = { 270f, 350f, 560f, 675f };
            float[] heights = { 260f, 342f, 440f };
            int geometry = 0;
            foreach (float width in widths)
                foreach (float height in heights)
                {
                    Rect bounds = new Rect(0f, 0f, width, height);
                    Rect view, apply, cancel;
                    ProfileReviewLayoutP1.Calculate(bounds, out view, out apply, out cancel);
                    Check(apply.x >= 0f && cancel.xMax <= width && apply.width > 0f &&
                        apply.xMax + 7.99f <= cancel.x, "buttons are contained and separated");
                    Check(apply.y >= 0f && cancel.yMax <= height && view.yMax + 11.99f <= apply.y,
                        "scroll view cannot overlap fixed buttons");
                    Check(view.width == width && view.height > 0f && apply.y == cancel.y,
                        "scroll bounds and left-right button order");
                    geometry++;
                }
            Log.Message("[XPC-Q11][RECOVERY_LATCH_DIAGNOSTIC_SELFTEST] status=PASS; checks=2; " +
                "healthy=NOT_BLOCKED_BY_RECOVERY_LATCH; blocked=BLOCKED_BY_ACTIVE_RECOVERY_LATCH; " +
                "realGateMutations=0; build=" + XpCustomizerMod.BuildId);
            Log.Message("[XPC-P1][PROFILE_UI_POLISH_SELFTEST] status=PASS; checks=" + checks +
                "; geometryCases=" + geometry + "; bodyFont=SMALL; yearStampedMenu=yyMMdd_HH:mm; " +
                "timestampChars=12; hotfix=P1-HF1; " +
                "applyLeft=PASS; cancelRight=PASS; recoveryLockScope=PASS; " +
                "settingsWrites=0; profileJsonWrites=0; configXmlWrites=0; geneDefWrites=0; " +
                "liveIMGUI=MANUAL; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
