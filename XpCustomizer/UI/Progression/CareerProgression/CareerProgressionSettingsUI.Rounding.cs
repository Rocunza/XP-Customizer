using System.Collections.Generic;
using UnityEngine;
using Verse;
using XpCustomizer.Progression;

namespace XpCustomizer.UI
{
    // CPS1: same CLR type, original method bodies moved without behavioral changes.
    internal static partial class CareerProgressionSettingsUI
    {

        private static void DrawXpRoundingControls(
            Listing_Standard listing,
            XpCustomizerSettings settings)
        {
            settings.careerXpRoundingStep =
                CareerXpDistributionService.NormalizeRoundingStep(
                    settings.careerXpRoundingStep);


            listing.Label(
                "XPC_L3B_RoundingTitle".Translate().ToString());

            Rect row =
                listing.GetRect(30f);

            const float gap = 8f;

            float buttonWidth =
                (row.width -
                 (gap * 3f)) /
                4f;


            DrawRoundingButton(
                new Rect(
                    row.x,
                    row.y,
                    buttonWidth,
                    row.height),
                settings,
                CareerXpDistributionService.XpRoundingExact,
                "XPC_L3B_RoundingExact".Translate().ToString());

            DrawRoundingButton(
                new Rect(
                    row.x +
                    buttonWidth +
                    gap,
                    row.y,
                    buttonWidth,
                    row.height),
                settings,
                CareerXpDistributionService.XpRounding100,
                "100 XP");

            DrawRoundingButton(
                new Rect(
                    row.x +
                    ((buttonWidth + gap) * 2f),
                    row.y,
                    buttonWidth,
                    row.height),
                settings,
                CareerXpDistributionService.XpRounding500,
                "500 XP");

            DrawRoundingButton(
                new Rect(
                    row.x +
                    ((buttonWidth + gap) * 3f),
                    row.y,
                    buttonWidth,
                    row.height),
                settings,
                CareerXpDistributionService.XpRounding1000,
                "1,000 XP");

            SettingsUIHelpers.DrawReferenceText(
                listing,
                "XPC_L3B_RoundingDescription".Translate().ToString());


            int requestedRoundingStep =
                CareerXpDistributionService.NormalizeRoundingStep(
                    settings.careerXpRoundingStep);

            int targetTotalXp =
                Mathf.RoundToInt(
                    CareerXpDistributionService.VanillaTotalXp *
                    CareerXpDistributionService.NormalizeTotalMultiplier(
                        settings.careerXpMultiplier));

            int effectiveRoundingStep =
                CareerXpDistributionService.GetEffectiveRoundingStep(
                    requestedRoundingStep,
                    targetTotalXp);

            if (requestedRoundingStep !=
                    CareerXpDistributionService.XpRoundingExact &&
                effectiveRoundingStep !=
                    requestedRoundingStep)
            {
                SettingsUIHelpers.DrawReferenceText(
                    listing,
                    string.Format("XPC_L3B_RoundingFallback".Translate().ToString(),
                        GetLocalizedRoundingStepLabel(requestedRoundingStep),
                        GetLocalizedRoundingStepLabel(effectiveRoundingStep)));
            }
        }


        private static void DrawRoundingButton(
            Rect rect,
            XpCustomizerSettings settings,
            int roundingStep,
            string label)
        {
            bool selected =
                settings.careerXpRoundingStep ==
                roundingStep;

            string buttonLabel =
                selected
                    ? "[" + label + "]"
                    : label;


            if (Widgets.ButtonText(
                    rect,
                    buttonLabel))
            {
                settings.careerXpRoundingStep =
                    roundingStep;
            }
        }


        // Preserve CPS1's English-only formatting oracle; translate at the UI boundary.
        private static string GetLocalizedRoundingStepLabel(int roundingStep)
        {
            return roundingStep == CareerXpDistributionService.XpRoundingExact
                ? "XPC_L3B_RoundingExact".Translate().ToString()
                : GetRoundingStepLabel(roundingStep);
        }


        private static string GetRoundingStepLabel(
            int roundingStep)
        {
            switch (roundingStep)
            {
                case CareerXpDistributionService.XpRounding100:
                    return "100 XP";

                case CareerXpDistributionService.XpRounding500:
                    return "500 XP";

                case CareerXpDistributionService.XpRounding1000:
                    return "1,000 XP";

                default:
                    return "Exact";
            }
        }
    }
}
