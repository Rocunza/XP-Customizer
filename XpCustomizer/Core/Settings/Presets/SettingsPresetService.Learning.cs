using Verse;

namespace XpCustomizer
{
    internal static partial class SettingsPresetService
    {
        // ================================================================
        // Learning - combined Vanilla preset
        // ================================================================


        public static void ResetLearningToVanilla()
        {
            ResetLearningCapacityToVanilla();
            ResetFurtherLearningToVanilla();
            ResetConsciousnessLearningToVanilla();
        }


        // ================================================================
        // Dynamic Learning Capacity
        // ================================================================


        public static void ResetLearningCapacityToVanilla()
        {
            XpCustomizerSettings settings =
                XpCustomizerMod.Settings;

            if (settings == null)
            {
                return;
            }


            settings.learningCapacityBaseDailyCap =
                XpCustomizerDefaults.LearningCapacityBaseDailyCap;

            settings.learningCapacitySourceModifiersEnabled =
                XpCustomizerDefaults.LearningCapacitySourceModifiersEnabled;
            settings.learningCapacitySourceStrength =
                XpCustomizerDefaults.LearningCapacitySourceStrength;


            // Also clear source values so enabling source customization
            // after pressing the Vanilla reset remains neutral.
            settings.learningCapacityCustomSlowLearner = 0f;
            settings.learningCapacityCustomFastLearner = 0f;
            settings.learningCapacityCustomTooSmart = 0f;

            settings.learningCapacityCustomBlissLobotomy = 0f;

            settings.learningCapacityCustomSlowStudy = 0f;
            settings.learningCapacityCustomQuickStudy = 0f;

            settings.learningCapacityCustomNeuralSupercharge = 0f;
            settings.learningCapacityCustomLearningAssistant = 0f;
            settings.learningCapacityCustomRanchingCentral = 0f;

            settings.learningCapacityOverrideSlowLearner = false;
            settings.learningCapacityOverrideFastLearner = false;
            settings.learningCapacityOverrideTooSmart = false;
            settings.learningCapacityOverrideBlissLobotomy = false;
            settings.learningCapacityOverrideSlowStudy = false;
            settings.learningCapacityOverrideQuickStudy = false;
            settings.learningCapacityOverrideNeuralSupercharge = false;
            settings.learningCapacityOverrideRanchingCentral = false;
            settings.learningCapacityOverrideLearningAssistant = false;
        }


        // ================================================================
        // Further Learning
        // ================================================================


        public static void ResetFurtherLearningToVanilla()
        {
            XpCustomizerSettings s = XpCustomizerMod.Settings;
            if (s == null) return;
            ApplyFurtherLearningBaseline(s,
                XpCustomizerDefaults.FurtherLearningMentalVanillaStrength);
            Log.Message("[XP Customizer][MTH-DEFAULT60-R1] action=FurtherLearning-Vanilla; strength=100%; enabled=False; gameplayNeutral=True");
        }

        // Shared by the per-tab button (whose Vanilla caption is preserved)
        // and the full Mod Defaults reset. Both restore the stored 60% strength.
        internal static void ResetFurtherLearningToModDefaults()
        {
            XpCustomizerSettings s = XpCustomizerMod.Settings;
            if (s == null) return;
            ResetFurtherLearningToModDefaults(s);
            Log.Message("[XPC-FLR1][FURTHER_LEARNING_RESET] route=SECTION_OR_GLOBAL; policy=MOD_DEFAULTS" +
                "; strength=" + s.furtherLearningMentalThresholdStrength.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) +
                "; enabled=" + s.furtherLearningMentalThresholdEnabled +
                "; build=" + XpCustomizerMod.BuildId);
        }

        // Detached overload makes the actual 60% reset route testable without
        // changing live settings, Scribe, GeneDefs or other mod state.
        internal static void ResetFurtherLearningToModDefaults(XpCustomizerSettings settings)
        {
            if (settings == null) return;
            ApplyFurtherLearningBaseline(settings,
                XpCustomizerDefaults.FurtherLearningMentalStrength);
        }

        // Pure detached setter shared by the internal Vanilla (100%) route
        // and the section/global Mod Defaults (60%) routes.
        internal static void ApplyFurtherLearningBaseline(
            XpCustomizerSettings settings, float mentalStrength)
        {
            if (settings == null) return;

            settings.furtherLearningBaseFactor =
                XpCustomizerDefaults.FurtherLearningBase;
            settings.furtherLearningMentalThresholdEnabled =
                XpCustomizerDefaults.FurtherLearningMentalThresholdEnabled;
            settings.furtherLearningMentalThresholdStrength = mentalStrength;
            settings.furtherLearningTraitModifiersEnabled =
                XpCustomizerDefaults.FurtherLearningTraitModifiersEnabled;

            // Legacy trait fields are vanilla-neutral and default to zero.
            settings.furtherLearningVolatileModifier = 0f;
            settings.furtherLearningNervousModifier = 0f;
            settings.furtherLearningSteadfastModifier = 0f;
            settings.furtherLearningIronWilledModifier = 0f;
            settings.furtherLearningSlothfulModifier = 0f;
            settings.furtherLearningLazyModifier = 0f;
            settings.furtherLearningHardWorkerModifier = 0f;
            settings.furtherLearningIndustriousModifier = 0f;
            settings.furtherLearningNeuroticModifier = 0f;
            settings.furtherLearningVeryNeuroticModifier = 0f;
        }
    }
}
