using UnityEngine;
using Verse;
using XpCustomizer.LearningCapacity;

namespace XpCustomizer.UI
{
    // LCUI1: same CLR type; original method bodies retained without behavioral edits.
    internal static partial class LearningCapacitySettingsUI
    {

        private static void DrawSourceModifierToggle(
            Listing_Standard listing)
        {
            bool enabled =
                XpCustomizerMod.Settings
                    .learningCapacitySourceModifiersEnabled;


            SettingsUIHelpers.DrawRightAdjacentToggle(
                listing, "XPC_Learning_SourceToggle".Translate().ToString(), ref enabled);


            XpCustomizerMod.Settings
                .learningCapacitySourceModifiersEnabled =
                enabled;


            SettingsUIHelpers.DrawReferenceText(
                listing,
                enabled
                    ? "XPC_Learning_SourceOn".Translate().ToString()
                    : "XPC_Learning_SourceOff".Translate().ToString());
        }


        private static void DrawSourceStrength(Listing_Standard listing)
        {
            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            float strength = LearningCapacityService.NormalizeSourceStrength(
                settings.learningCapacitySourceStrength);
            float conversion = LearningCapacityService.SourceConversionFraction * strength;
            listing.Label(string.Format(
                "XPC_Learning_SourceConversion".Translate().ToString(),
                (conversion * 100f).ToString("0.##"),
                Mathf.RoundToInt(strength * 100f)));
            strength = listing.Slider(strength,
                XpCustomizerDefaults.LearningCapacitySourceStrengthMin,
                XpCustomizerDefaults.LearningCapacitySourceStrengthMax);
            settings.learningCapacitySourceStrength =
                Mathf.Round(strength * 100f) / 100f;
            SettingsUIHelpers.DrawReferenceText(listing,
                "XPC_Learning_SourceStrengthRange".Translate().ToString());
        }


        private static void DrawSourceModifiers(Listing_Standard listing)
        {
            SettingsUIHelpers.DrawDescription(listing,
                "XPC_Learning_SourceAutoDescription".Translate().ToString());
            SettingsUIHelpers.DrawReferenceText(listing,
                "XPC_Learning_SourceCustomDescription".Translate().ToString());

            if (listing.ButtonText("XPC_Learning_SourceResetAuto".Translate().ToString()))
            {
                XpCustomizerSettings s = XpCustomizerMod.Settings;
                s.learningCapacityOverrideSlowLearner = false;
                s.learningCapacityOverrideFastLearner = false;
                s.learningCapacityOverrideTooSmart = false;
                s.learningCapacityOverrideBlissLobotomy = false;
                s.learningCapacityOverrideSlowStudy = false;
                s.learningCapacityOverrideQuickStudy = false;
                s.learningCapacityOverrideNeuralSupercharge = false;
                s.learningCapacityOverrideRanchingCentral = false;
                s.learningCapacityOverrideLearningAssistant = false;
                ResetInputBuffers();
            }
            listing.Gap(8f);
            DrawSourceColumnHeaders(listing);
            listing.Gap(4f);

            DrawGroupTitle(listing, "XPC_Learning_SourceGroupTraits".Translate().ToString());
            DrawSourceValue(listing, "XPC_Learning_SourceSlowLearner", LearningCapacityService.VanillaLfSlowLearner,
                ref XpCustomizerMod.Settings.learningCapacityOverrideSlowLearner,
                ref XpCustomizerMod.Settings.learningCapacityCustomSlowLearner, slowLearnerInput);
            DrawSourceValue(listing, "XPC_Learning_SourceFastLearner", LearningCapacityService.VanillaLfFastLearner,
                ref XpCustomizerMod.Settings.learningCapacityOverrideFastLearner,
                ref XpCustomizerMod.Settings.learningCapacityCustomFastLearner, fastLearnerInput);
            DrawSourceValue(listing, "XPC_Learning_SourceTooSmart", LearningCapacityService.VanillaLfTooSmart,
                ref XpCustomizerMod.Settings.learningCapacityOverrideTooSmart,
                ref XpCustomizerMod.Settings.learningCapacityCustomTooSmart, tooSmartInput);
            listing.Gap(8f);
            DrawGroupTitle(listing, "XPC_Learning_SourceGroupAnomaly".Translate().ToString());
            DrawSourceValue(listing, "XPC_Learning_SourceBlissLobotomy", LearningCapacityService.VanillaLfBlissLobotomy,
                ref XpCustomizerMod.Settings.learningCapacityOverrideBlissLobotomy,
                ref XpCustomizerMod.Settings.learningCapacityCustomBlissLobotomy, blissLobotomyInput);
            listing.Gap(8f);
            DrawGroupTitle(listing, "XPC_Learning_SourceGroupBiotech".Translate().ToString());
            DrawSourceValueWithKind(listing, "XPC_Learning_SourceSlowStudy", LearningCapacityService.VanillaLfSlowStudy,
                ref XpCustomizerMod.Settings.learningCapacityOverrideSlowStudy,
                ref XpCustomizerMod.Settings.learningCapacityCustomSlowStudy, slowStudyInput,
                SourceDescriptionKind.SlowStudy);
            DrawSourceValue(listing, "XPC_Learning_SourceQuickStudy", LearningCapacityService.VanillaLfQuickStudy,
                ref XpCustomizerMod.Settings.learningCapacityOverrideQuickStudy,
                ref XpCustomizerMod.Settings.learningCapacityCustomQuickStudy, quickStudyInput);
            listing.Gap(8f);
            DrawGroupTitle(listing, "XPC_Learning_SourceGroupIdeology".Translate().ToString());
            DrawSourceValue(listing, "XPC_Learning_SourceNeuralSupercharge", LearningCapacityService.VanillaLfNeuralSupercharge,
                ref XpCustomizerMod.Settings.learningCapacityOverrideNeuralSupercharge,
                ref XpCustomizerMod.Settings.learningCapacityCustomNeuralSupercharge, neuralSuperchargeInput);
            DrawSourceValueWithKind(listing, "XPC_Learning_SourceRanchingCentral", LearningCapacityService.VanillaLfRanchingCentral,
                ref XpCustomizerMod.Settings.learningCapacityOverrideRanchingCentral,
                ref XpCustomizerMod.Settings.learningCapacityCustomRanchingCentral, ranchingCentralInput,
                SourceDescriptionKind.RanchingAnimals);
            listing.Gap(8f);
            DrawGroupTitle(listing, "XPC_Learning_SourceGroupRoyalty".Translate().ToString());
            DrawSourceValue(listing, "XPC_Learning_SourceLearningAssistant", LearningCapacityService.VanillaLfLearningAssistant,
                ref XpCustomizerMod.Settings.learningCapacityOverrideLearningAssistant,
                ref XpCustomizerMod.Settings.learningCapacityCustomLearningAssistant, learningAssistantInput);
            listing.Gap(8f);
        }


        private static void DrawGroupTitle(
            Listing_Standard listing,
            string title)
        {
            GameFont oldFont =
                Text.Font;

            Text.Font =
                GameFont.Small;

            listing.Label(
                title);

            Text.Font =
                oldFont;
        }
    }
}
