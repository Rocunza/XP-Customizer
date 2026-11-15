using UnityEngine;
using Verse;
using XpCustomizer.LearningCapacity;

namespace XpCustomizer.UI
{
    /// <summary>
    /// Draws Daily Learning Capacity settings.
    ///
    /// Responsibilities:
    /// - Direct Base Daily Capacity input.
    /// - Optional source-modifier master switch.
    /// - V4 Auto vanilla-LF conversion with independent final-value overrides.
    /// - Live effective and one-source capacity preview (Cons = 100%).
    /// - Section-level Vanilla reset.
    ///
    /// Gameplay calculations do not belong here.
    /// </summary>
    internal static partial class LearningCapacitySettingsUI
    {
        private const string UiRevision = "LC-V4-AUTO-FORMULA-OVERRIDE-R1-20261101";
        private static bool revisionLogged;

        private static readonly NumericInputState baseDailyCapInput =
            new NumericInputState();

        private static readonly NumericInputState slowLearnerInput =
            new NumericInputState();

        private static readonly NumericInputState fastLearnerInput =
            new NumericInputState();

        private static readonly NumericInputState tooSmartInput =
            new NumericInputState();

        private static readonly NumericInputState blissLobotomyInput =
            new NumericInputState();

        private static readonly NumericInputState slowStudyInput =
            new NumericInputState();

        private static readonly NumericInputState quickStudyInput =
            new NumericInputState();

        private static readonly NumericInputState neuralSuperchargeInput =
            new NumericInputState();

        private static readonly NumericInputState learningAssistantInput =
            new NumericInputState();

        private static readonly NumericInputState ranchingCentralInput =
            new NumericInputState();


        // ================================================================
        // Main draw
        // ================================================================

        public static void Draw(
            Listing_Standard listing)
        {
            if (!revisionLogged)
            {
                revisionLogged = true;
                Log.Message("[XP Customizer][Learning Capacity UI] UI_REVISION=" +
                    UiRevision + "; preview=single-source; sourceMode=Auto/Custom; gameplay=V4");
            }

            SettingsUIHelpers.DrawSectionTitle(
                listing,
                "XPC_Learning_DailyCapTitle".Translate().ToString());

            SettingsUIHelpers.DrawDescription(
                listing,
                "XPC_Learning_DailyCapDescription".Translate().ToString());

            listing.Gap(6f);


            DrawBaseDailyCapacity(
                listing);

            listing.Gap(12f);


            DrawSourceModifierToggle(
                listing);


            if (XpCustomizerMod.Settings
                .learningCapacitySourceModifiersEnabled)
            {
                listing.Gap(10f);
                DrawSourceStrength(listing);
                listing.Gap(10f);
                DrawSourceModifiers(listing);
            }


            listing.Gap(14f);

            DrawVanillaResetButton(
                listing);

            listing.Gap(20f);
        }


        private static void DrawVanillaResetButton(
            Listing_Standard listing)
        {
            if (listing.ButtonText(
                    "XPC_Learning_DailyCapReset".Translate().ToString()))
            {
                SettingsPresetService
                    .ResetLearningCapacityToVanilla();

                ResetInputBuffers();
            }
        }
    }
}
