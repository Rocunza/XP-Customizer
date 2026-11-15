#if DEBUG
using System;
using UnityEngine;
using Verse;
using XpCustomizer.UI;

namespace XpCustomizer
{
    internal static class BiotechUiSelfTests
    {
        internal static void Run()
        {
            int v;
            Assert(!BiotechSettingsUI.TryParseInteger("-", out v), "unfinished minus");
            Assert(!BiotechSettingsUI.TryParseInteger("+", out v), "unfinished plus");
            Assert(!BiotechSettingsUI.TryParseInteger("", out v), "empty edit");
            Assert(!BiotechSettingsUI.TryParseInteger("1.5", out v), "fractional edit");
            Assert(!BiotechSettingsUI.TryParseInteger("abc", out v), "malformed edit");
            Assert(BiotechSettingsUI.TryParseInteger("-12", out v) && v == -12,
                "negative aptitude");
            Assert(BiotechSettingsUI.TryParseInteger("+4", out v) && v == 4,
                "explicit positive aptitude");
            Assert(BiotechSettingsUI.ResolveIntegerCommit("-12", -8, -20, 0) == -12,
                "valid aptitude commit");
            Assert(BiotechSettingsUI.ResolveIntegerCommit("-99", -8, -20, 0) == -20,
                "aptitude lower bound");
            Assert(BiotechSettingsUI.ResolveIntegerCommit("3", -8, -20, 0) == 0,
                "awful aptitude upper bound");
            Assert(BiotechSettingsUI.ResolveIntegerCommit("25", 8, 0, 20) == 20,
                "great aptitude upper bound");
            Assert(BiotechSettingsUI.ResolveIntegerCommit("-1", 1, 0, 10) == 0,
                "complexity lower bound");
            Assert(BiotechSettingsUI.ResolveIntegerCommit("15", 1, 0, 10) == 10,
                "complexity upper bound");
            Assert(BiotechSettingsUI.ResolveIntegerCommit("-9", 2, -5, 5) == -5,
                "metabolism lower bound");
            Assert(BiotechSettingsUI.ResolveIntegerCommit("8", -1, -5, 5) == 5,
                "metabolism upper bound");
            Assert(BiotechSettingsUI.ResolveIntegerCommit("-", -8, -20, 0) == -8,
                "partial edit restores last valid");
            Assert(BiotechSettingsUI.ResolveIntegerCommit("garbage", 2, -5, 5) == 2,
                "malformed edit restores last valid");
            Assert(BiotechSettingsUI.ResolveIntegerCommit("", 1, 0, 10) == 1,
                "empty edit restores last valid");
            // Keep this test UI-only: GeneDef mutations and vanilla restore
            // are verified separately by the existing gameplay audit.
            Log.Message("[XP Customizer][BIOTECH-UI-R1] numeric pure tests PASS; cases=18; " +
                "signed/partial/invalid/commit/bounds; inputFocus+GeneDefs runtime NOT tested");
        }

        private static void Assert(bool condition, string reason)
        {
            if (!condition)
                throw new InvalidOperationException(
                    "[XP Customizer][BIOTECH-UI-R1] test failed: " + reason);
        }
    }
}
#endif
