using System;
using System.Globalization;
using UnityEngine;
using Verse;
using XpCustomizer.ConsciousnessLearning;

namespace XpCustomizer.UI
{
    /// <summary>
    /// Single-source Anchor V2 editor for independent Speed and Capacity.
    /// GWS-style mode choices are placed directly below each Low/High toggle.
    /// No additional Harmony patches are introduced.
    /// </summary>
    internal static partial class ConsciousnessAnchorV2SettingsUI
    {
        internal const string Revision = "CONS-ANCHOR-V2-NUMERIC-P4BR6-R1-20261103";

        // S06 file-layout identity; emitted on startup and checked in DEBUG.
        internal const string SplitStage = "XPC-S06-CONS-ANCHOR-V2-UI-3PART-20261111-R1";

        internal static void DrawProfile(Listing_Standard listing,
            XpCustomizerSettings settings, bool speed)
        {
            ConsAnchorV2ProfileSettings profile = speed
                ? settings.consciousnessAnchorSpeed : settings.consciousnessAnchorCapacity;
            if (profile == null)
            {
                profile = new ConsAnchorV2ProfileSettings();
                if (speed) settings.consciousnessAnchorSpeed = profile;
                else settings.consciousnessAnchorCapacity = profile;
            }
            ProfileInputBuffers inputs = speed ? speedInputs : capacityInputs;
            listing.Gap(4f);
            SettingsUIHelpers.DrawReferenceText(listing,
                "XPC_Cons_AnchorIntro".Translate().ToString());
            listing.Gap(6f);
            DrawLowHighTitle(listing, "XPC_Cons_LowTitle".Translate().ToString());
            bool lowEnabled = profile.LowEnabled;
            SettingsUIHelpers.DrawRightAdjacentToggle(
                listing, "XPC_Cons_LowEnabled".Translate().ToString(), ref lowEnabled);
            profile.LowEnabled = lowEnabled;
            if (lowEnabled)
            {
                DrawCurveModes(listing, ref profile.LowCurve);
                DrawNumber(listing, "XPC_Cons_LowAnchor".Translate().ToString(),
                    ref profile.LowAnchor, inputs.LowAnchor,
                    5f, 95f, ConsciousnessAnchorCurveService.DefaultLowAnchor, true);
                DrawNumber(listing, "XPC_Cons_LowFactor".Translate().ToString(),
                    ref profile.LowFactor, inputs.LowFactor,
                    0f, 1f, ConsciousnessAnchorCurveService.DefaultLowFactor, false);
                if (profile.LowCurve != (int)ConsAnchorCurveMode.Linear)
                    DrawCurvature(listing, "XPC_Curve_LowCurvature".Translate().ToString(), ref profile.LowCurvature);
                DrawLowPreview(listing, settings, speed);
            }

            listing.Gap(10f);
            DrawLowHighTitle(listing, "XPC_Cons_HighTitle".Translate().ToString());
            bool highEnabled = profile.HighEnabled;
            SettingsUIHelpers.DrawRightAdjacentToggle(
                listing, "XPC_Cons_HighEnabled".Translate().ToString(), ref highEnabled);
            profile.HighEnabled = highEnabled;
            if (highEnabled)
            {
                DrawCurveModes(listing, ref profile.HighCurve);
                DrawNumber(listing, "XPC_Cons_HighAnchor".Translate().ToString(),
                    ref profile.HighAnchor, inputs.HighAnchor,
                    101f, 1000f, ConsciousnessAnchorCurveService.DefaultHighAnchor, true);
                DrawNumber(listing, "XPC_Cons_HighFactor".Translate().ToString(),
                    ref profile.HighFactor, inputs.HighFactor,
                    1f, 19.99f, ConsciousnessAnchorCurveService.DefaultHighFactor, false);
                if (profile.HighCurve != (int)ConsAnchorCurveMode.Linear)
                    DrawCurvature(listing, "XPC_Curve_HighCurvature".Translate().ToString(), ref profile.HighCurvature);
                if (profile.HighCurve == (int)ConsAnchorCurveMode.Diminishing)
                {
                    DrawNumber(listing, "XPC_Cons_DiminishingLimit".Translate().ToString(),
                        ref profile.HighDiminishingLimit, inputs.DiminishingLimit,
                        Mathf.Min(20f, profile.HighFactor + 0.01f), 20f,
                        ConsciousnessAnchorCurveService.DefaultHighDiminishingLimit, false);
                    SettingsUIHelpers.DrawReferenceText(listing,
                        "XPC_Cons_AnchorLimitHelp".Translate().ToString());
                }
                else
                {
                    DrawNumber(listing, "XPC_Cons_SafetyCap".Translate().ToString(),
                        ref profile.HighSafetyLimit, inputs.SafetyLimit,
                        profile.HighFactor, 20f,
                        ConsciousnessAnchorCurveService.DefaultHighSafetyLimit, false);
                    SettingsUIHelpers.DrawReferenceText(listing,
                        "XPC_Cons_AnchorSafetyHelp".Translate().ToString());
                }
                DrawHighPreview(listing, settings, speed);
            }

            // Only the current profile is normalized. The sibling profile
            // and Further Learning settings cannot be mutated by this editor.
            profile.Normalize();
        }

        // Subsections stay one visual level below the three primary Cons
        // features. Use a local style rather than mutating Text.CurFontStyle.
        private static void DrawLowHighTitle(Listing_Standard listing, string title)
        {
            Rect row = listing.GetRect(34f);
            GameFont previousFont = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                GUIStyle style = new GUIStyle(Text.CurFontStyle);
                style.fontSize = 19;
                style.alignment = TextAnchor.MiddleLeft;
                GUI.Label(row, title, style);
            }
            finally
            {
                Text.Font = previousFont;
            }
        }

        private static void DrawCurveModes(Listing_Standard listing, ref int selected)
        {
            Rect row = listing.GetRect(32f);
            const float gap = 6f;
            float w = (row.width - gap * 2f) / 3f;
            DrawCurveButton(new Rect(row.x, row.y, w, row.height),
                "XPC_Curve_Diminishing".Translate().ToString(), (int)ConsAnchorCurveMode.Diminishing, ref selected);
            DrawCurveButton(new Rect(row.x + w + gap, row.y, w, row.height),
                "XPC_Curve_Linear".Translate().ToString(), (int)ConsAnchorCurveMode.Linear, ref selected);
            DrawCurveButton(new Rect(row.x + (w + gap) * 2f, row.y, w, row.height),
                "XPC_Curve_Accelerating".Translate().ToString(), (int)ConsAnchorCurveMode.Accelerating, ref selected);
        }

        private static void DrawCurveButton(Rect rect, string title, int choice, ref int selected)
        {
            if (Widgets.ButtonText(rect, (selected == choice ? "✓ " : "") + title))
                selected = choice;
        }

        private static void DrawCurvature(Listing_Standard listing, string label,
            ref float amount)
        {
            Rect row = listing.GetRect(27f);
            Widgets.Label(new Rect(row.x, row.y, row.width - 120f, row.height), label);
            Widgets.Label(new Rect(row.xMax - 118f, row.y, 66f, row.height),
                Mathf.RoundToInt(amount * 100f) + "%");
            Rect reset = new Rect(row.xMax - 50f, row.y, 50f, 26f);
            if (Widgets.ButtonText(reset, "100%")) amount = 1f;
            float before = amount * 100f;
            float percent = Widgets.HorizontalSlider(listing.GetRect(26f),
                before, 0f, 200f, false, null, null, null, 5f);
            // Preserve precise imported values until the user drags this bar.
            if (!Mathf.Approximately(percent, before))
                amount = Mathf.Clamp(Mathf.Round(percent / 5f) * 5f / 100f, 0f, 2f);
            SettingsUIHelpers.DrawReferenceText(listing,
                "XPC_Curve_CurvatureHelp".Translate().ToString());
        }

    }
}
