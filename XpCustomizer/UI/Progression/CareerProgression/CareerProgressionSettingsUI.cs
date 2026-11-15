using System.Collections.Generic;
using UnityEngine;
using Verse;
using XpCustomizer.Progression;

namespace XpCustomizer.UI
{
    /// <summary>
    /// Draws and manages the Career Progression section.
    ///
    /// Responsibilities:
    /// - Total career XP multiplier direct input
    /// - Stage-based XP distribution settings
    /// - Dynamic stage-weight input state
    /// - Stage share / XP result table
    /// - Boundary-editor integration
    /// - Per-level XP rounding selection
    ///
    /// Gameplay calculations remain in XpCustomizer.Progression.
    /// </summary>
    internal static partial class CareerProgressionSettingsUI
    {
        // Exact vanilla-reference gold used by DecaySettingsUI and BookXpSettingsUI.
        // Presentation only: never changes the distribution values.
        private static readonly Color VanillaColor =
            new Color(0.96f, 0.82f, 0.48f, 1f);

        private static readonly NumericInputState totalMultiplierInput =
            new NumericInputState();

        private static readonly List<NumericInputState> stageWeightInputs =
            new List<NumericInputState>();

        private static readonly List<NumericInputState> stageLockedShareInputs =
            new List<NumericInputState>();

#if DEBUG
        // L3-B1: presentation-only version probe, once on first Career XP draw.
        private static bool l3B1TranslationLogged;
#endif


        public static void Draw(
            Listing_Standard listing)
        {
            DrawCareerProgression(
                listing);
        }


        private static void DrawCareerProgression(
            Listing_Standard listing)
        {
#if DEBUG
            if (!l3B1TranslationLogged)
            {
                l3B1TranslationLogged = true;
                Log.Message("[XPC-L10N-L3B1][KEYED_CAREER] " +
                    "version=XPC-L10N-L3B1-CAREER-20261112-R1; phase=B1; keys=25; " +
                    "sampleKey=XPC_L3B_TotalMultiplierTitle; sample=" +
                    "XPC_L3B_TotalMultiplierTitle".Translate().ToString() +
                    "; build=" + XpCustomizerMod.BuildId);
            }
#endif
            XpCustomizerSettings settings =
                XpCustomizerMod.Settings;


            float multiplier =
                settings.careerXpMultiplier;


            if (!float.IsNaN(
                    totalMultiplierInput.lastValue) &&
                !Mathf.Approximately(
                    multiplier,
                    totalMultiplierInput.lastValue))
            {
                totalMultiplierInput.buffer = null;
            }


            multiplier =
                DrawTotalMultiplierInput(
                    listing,
                    multiplier,
                    totalMultiplierInput);


            settings.careerXpMultiplier =
                CareerXpDistributionService.NormalizeTotalMultiplier(
                    multiplier);

            totalMultiplierInput.lastValue =
                settings.careerXpMultiplier;


            SettingsUIHelpers.DrawDescription(
                listing,
                "XPC_L3B_TotalMultiplierDescription".Translate().ToString());

            VanillaReferenceAccentUI.DrawProgressionRange(listing,
                "XPC_L3F3_CareerRangeMuted", "XPC_L3F3_CareerRangeVanilla",
                "XPC_L3B_TotalMultiplierRange");

            listing.Gap(6f);


            VanillaReferenceAccentUI.DrawVanillaLabel(listing,
                string.Format("XPC_L3B_VanillaTotalXp".Translate().ToString(),
                    CareerXpDistributionService.VanillaTotalXp.ToString("N0")));

            listing.Label(
                string.Format("XPC_L3B_CustomTotalXp".Translate().ToString(),
                    XpCurve.GetTotalXp(settings).ToString("N0")));


            listing.Gap(12f);

            SkillSpecificXpSettingsUI.Draw(listing, settings);

            // Distinguish XP total/scaling from XP stage distribution.
            listing.Gap(8f);
            listing.GapLine();
            listing.Gap(8f);

            DrawCareerDistribution(
                listing,
                settings);
        }


        private static float DrawTotalMultiplierInput(
            Listing_Standard listing,
            float value,
            NumericInputState inputState)
        {
            // Give the main setting the same visual rank as XP distribution.
            // The description stays on its own line for localization safety.
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Text.Font = GameFont.Medium;
            string label = "XPC_L3B_TotalMultiplierTitle".Translate().ToString();
            NumericRowLayout.Placement geometry = NumericRowLayout.Allocate(
                listing, label, 100f, 24f, 40f, 30f);
            Rect titleRect = geometry.Label;
            Rect fieldRect = geometry.Field;
            Rect suffixRect = geometry.Suffix;

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(titleRect, label);

            // Numeric entry must use the standard font / existing buffer.
            Text.Font = oldFont;

            ProfileNumericFocus.Draw(
                fieldRect,
                ref value,
                inputState,
                CareerXpDistributionService.MinTotalMultiplier,
                CareerXpDistributionService.MaxTotalMultiplier);

            Widgets.Label(suffixRect, "x");

            Text.Anchor = oldAnchor;

            return Mathf.Clamp(
                value,
                CareerXpDistributionService.MinTotalMultiplier,
                CareerXpDistributionService.MaxTotalMultiplier);
        }
    }
}
