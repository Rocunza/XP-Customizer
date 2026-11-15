using System;
using System.Globalization;
using UnityEngine;
using Verse;
using XpCustomizer.FurtherLearning;
using XpCustomizer.ConsciousnessLearning;

namespace XpCustomizer.UI
{
    /// <summary>
    /// Separate post-cap Cons profile editor. Cons speed/capacity retain their
    /// own profiles. Numbers use the same staged editing policy as Cons R6:
    /// partial input stays visible, bounds are applied on commit only.
    /// </summary>
    internal static partial class ConsciousnessFurtherLearningSettingsUI
    {
        internal const string Revision = "CONS-FURTHER-PREVIEW-CP1-R1-20261114";

        internal static void Draw(Listing_Standard listing)
        {
            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            if (settings == null) return;
            if (settings.consciousnessFurtherV2 == null)
                settings.consciousnessFurtherV2 = new ConsciousnessFurtherV2Settings();
            ConsciousnessFurtherV2Settings p = settings.consciousnessFurtherV2;

            SettingsUIHelpers.DrawReferenceText(listing,
                "XPC_Cons_FurtherIntro".Translate().ToString());
            listing.Gap(6f);
            DrawPreviewBaseSelector(listing, settings);
            listing.Gap(8f);
            DrawTitle(listing, "XPC_Cons_LowTitle".Translate().ToString());
            bool lowEnabled = p.LowEnabled;
            SettingsUIHelpers.DrawRightAdjacentToggle(
                listing, "XPC_Cons_LowEnabled".Translate().ToString(), ref lowEnabled);
            p.LowEnabled = lowEnabled;
            if (!lowEnabled)
            {
                lowAnchorInput.Reset();
                lowFactorInput.Reset();
            }
            if (lowEnabled)
            {
                DrawCurveModes(listing, ref p.LowCurve);
                DrawNumber(listing, "XPC_Cons_LowAnchor".Translate().ToString(), ref p.LowAnchor,
                    lowAnchorInput, 5f, 95f, ConsciousnessFurtherV2Settings.DefaultLowAnchor, true);
                DrawNumber(listing, "XPC_Cons_LowFactor".Translate().ToString(), ref p.LowFactor,
                    lowFactorInput, 0f, 1f, ConsciousnessFurtherV2Settings.DefaultLowFactor, false);
                if (p.LowCurve != (int)ConsAnchorCurveMode.Linear)
                    DrawCurvature(listing, "XPC_Curve_LowCurvature".Translate().ToString(), ref p.LowCurvature);
                DrawPreview(listing, settings, p, false);
            }

            listing.Gap(12f);
            DrawTitle(listing, "XPC_Cons_HighTitle".Translate().ToString());
            bool highEnabled = p.HighEnabled;
            SettingsUIHelpers.DrawRightAdjacentToggle(
                listing, "XPC_Cons_HighEnabled".Translate().ToString(), ref highEnabled);
            p.HighEnabled = highEnabled;
            if (!highEnabled)
            {
                highAnchorInput.Reset();
                highFactorInput.Reset();
                highLimitInput.Reset();
                safetyInput.Reset();
            }
            if (highEnabled)
            {
                DrawCurveModes(listing, ref p.HighCurve);
                DrawNumber(listing, "XPC_Cons_HighAnchor".Translate().ToString(), ref p.HighAnchor,
                    highAnchorInput, 101f, 1000f, ConsciousnessFurtherV2Settings.DefaultHighAnchor, true);
                DrawNumber(listing, "XPC_Cons_HighFactor".Translate().ToString(), ref p.HighFactor,
                    highFactorInput, 1f, 19.99f, ConsciousnessFurtherV2Settings.DefaultHighFactor, false);
                if (p.HighCurve != (int)ConsAnchorCurveMode.Linear)
                    DrawCurvature(listing, "XPC_Curve_HighCurvature".Translate().ToString(), ref p.HighCurvature);
                if (p.HighCurve == (int)ConsAnchorCurveMode.Diminishing)
                {
                    DrawNumber(listing, "XPC_Cons_DiminishingLimit".Translate().ToString(), ref p.HighDiminishingLimit,
                        highLimitInput, Mathf.Min(20f, p.HighFactor + 0.01f), 20f,
                        ConsciousnessFurtherV2Settings.DefaultHighLimit, false);
                    SettingsUIHelpers.DrawReferenceText(listing,
                        "XPC_Cons_FurtherLimitHelp".Translate().ToString());
                }
                else
                {
                    DrawNumber(listing, "XPC_Cons_SafetyCap".Translate().ToString(), ref p.HighSafetyLimit,
                        safetyInput, p.HighFactor, 20f,
                        ConsciousnessFurtherV2Settings.DefaultSafetyCap, false);
                    SettingsUIHelpers.DrawReferenceText(listing,
                        "XPC_Cons_FurtherSafetyHelp".Translate().ToString());
                }
                DrawPreview(listing, settings, p, true);
            }
            // External values are normalized but half-typed text is never
            // overwritten. This only touches the dedicated Further profile.
            p.Normalize();
        }

        private static void DrawTitle(Listing_Standard listing, string title)
        {
            Rect row = listing.GetRect(34f);
            GameFont previous = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                GUIStyle style = new GUIStyle(Text.CurFontStyle);
                style.fontSize = 19;
                style.alignment = TextAnchor.MiddleLeft;
                GUI.Label(row, title, style);
            }
            finally { Text.Font = previous; }
        }

        private static void DrawCurveModes(Listing_Standard listing, ref int selected)
        {
            Rect row = listing.GetRect(32f);
            const float gap = 6f;
            float w = (row.width - gap * 2f) / 3f;
            DrawChoice(new Rect(row.x, row.y, w, row.height), "XPC_Curve_Diminishing".Translate().ToString(),
                (int)ConsAnchorCurveMode.Diminishing, ref selected);
            DrawChoice(new Rect(row.x + w + gap, row.y, w, row.height), "XPC_Curve_Linear".Translate().ToString(),
                (int)ConsAnchorCurveMode.Linear, ref selected);
            DrawChoice(new Rect(row.x + 2f * (w + gap), row.y, w, row.height),
                "XPC_Curve_Accelerating".Translate().ToString(), (int)ConsAnchorCurveMode.Accelerating, ref selected);
        }

        private static void DrawChoice(Rect rect, string title, int mode, ref int selected)
        {
            if (Widgets.ButtonText(rect, (selected == mode ? "✓ " : "") + title)) selected = mode;
        }

        private static void DrawCurvature(Listing_Standard listing, string label, ref float value)
        {
            Rect row = listing.GetRect(27f);
            Widgets.Label(new Rect(row.x, row.y, row.width - 120f, row.height), label);
            Widgets.Label(new Rect(row.xMax - 118f, row.y, 66f, row.height),
                Mathf.RoundToInt(value * 100f) + "%");
            if (Widgets.ButtonText(new Rect(row.xMax - 50f, row.y, 50f, 26f), "100%")) value = 1f;
            float before = value * 100f;
            float percent = Widgets.HorizontalSlider(listing.GetRect(26f),
                before, 0f, 200f, false, null, null, null, 5f);
            if (!Mathf.Approximately(percent, before))
                value = Mathf.Clamp(Mathf.Round(percent / 5f) * 5f / 100f, 0f, 2f);
            SettingsUIHelpers.DrawReferenceText(listing,
                "XPC_Curve_FurtherCurvatureHelp".Translate().ToString());
        }

        internal static void NotifySettingsExternallyChanged()
        {
            lowAnchorInput.Reset(); lowFactorInput.Reset();
            highAnchorInput.Reset(); highFactorInput.Reset();
            highLimitInput.Reset(); safetyInput.Reset();
            ResetPreviewToCurrent();
        }
    }
}
