using System.Collections.Generic;
using Verse;
using XpCustomizer.Progression;
using XpCustomizer.FurtherLearning;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.Decay;
using XpCustomizer.Skilltrainer;
using XpCustomizer.BookXp;
using XpCustomizer.Gws;


namespace XpCustomizer
{
    public partial class XpCustomizerSettings
    {
        private void ExposeLearningCapacityData()
        {
            // ============================================================
            // Daily Learning Capacity
            // ============================================================

            Scribe_Values.Look(
                ref learningCapacityDataVersion,
                "learningCapacityDataVersion",
                0);

            Scribe_Values.Look(
                ref learningCapacityBaseDailyCap,
                "learningCapacityBaseDailyCap",
                XpCustomizerDefaults.LearningCapacityBaseDailyCap);

            Scribe_Values.Look(
                ref learningCapacitySourceModifiersEnabled,
                "learningCapacitySourceModifiersEnabled",
                XpCustomizerDefaults.LearningCapacitySourceModifiersEnabled);

            Scribe_Values.Look(
                ref learningCapacitySourceStrength,
                "learningCapacitySourceStrength",
                XpCustomizerDefaults.LearningCapacitySourceStrength);


            // Legacy V1 keys are retained for one-way migration.
            Scribe_Values.Look(
                ref legacyLearningCapacityUseCustomValues,
                "learningCapacityUseCustomValues",
                false);

            Scribe_Values.Look(
                ref legacyLearningCapacityBonusConversion,
                "learningCapacityBonusConversion",
                0f);


            // ------------------------------------------------------------
            // Per-source Learning Capacity values
            // ------------------------------------------------------------

            Scribe_Values.Look(
                ref learningCapacityCustomSlowLearner,
                "learningCapacityCustomSlowLearner",
                XpCustomizerDefaults.SlowLearnerCapacity);

            Scribe_Values.Look(
                ref learningCapacityCustomFastLearner,
                "learningCapacityCustomFastLearner",
                XpCustomizerDefaults.FastLearnerCapacity);

            Scribe_Values.Look(
                ref learningCapacityCustomTooSmart,
                "learningCapacityCustomTooSmart",
                XpCustomizerDefaults.TooSmartCapacity);

            Scribe_Values.Look(
                ref learningCapacityCustomBlissLobotomy,
                "learningCapacityCustomBlissLobotomy",
                XpCustomizerDefaults.BlissLobotomyCapacity);

            Scribe_Values.Look(
                ref learningCapacityCustomSlowStudy,
                "learningCapacityCustomSlowStudy",
                XpCustomizerDefaults.SlowStudyCapacity);

            Scribe_Values.Look(
                ref learningCapacityCustomQuickStudy,
                "learningCapacityCustomQuickStudy",
                XpCustomizerDefaults.QuickStudyCapacity);

            Scribe_Values.Look(
                ref learningCapacityCustomNeuralSupercharge,
                "learningCapacityCustomNeuralSupercharge",
                XpCustomizerDefaults.NeuralSuperchargeCapacity);

            Scribe_Values.Look(
                ref learningCapacityCustomLearningAssistant,
                "learningCapacityCustomLearningAssistant",
                XpCustomizerDefaults.LearningAssistantCapacity);

            Scribe_Values.Look(
                ref learningCapacityCustomRanchingCentral,
                "learningCapacityCustomRanchingCentral",
                XpCustomizerDefaults.RanchingCentralCapacity);


            // V4 per-source Auto/Custom modes. Previous versions have no
            // such keys; PostLoadInit migrates their old values safely.
            Scribe_Values.Look(ref learningCapacityOverrideSlowLearner, "learningCapacityOverrideSlowLearner", false);
            Scribe_Values.Look(ref learningCapacityOverrideFastLearner, "learningCapacityOverrideFastLearner", false);
            Scribe_Values.Look(ref learningCapacityOverrideTooSmart, "learningCapacityOverrideTooSmart", false);
            Scribe_Values.Look(ref learningCapacityOverrideBlissLobotomy, "learningCapacityOverrideBlissLobotomy", false);
            Scribe_Values.Look(ref learningCapacityOverrideSlowStudy, "learningCapacityOverrideSlowStudy", false);
            Scribe_Values.Look(ref learningCapacityOverrideQuickStudy, "learningCapacityOverrideQuickStudy", false);
            Scribe_Values.Look(ref learningCapacityOverrideNeuralSupercharge, "learningCapacityOverrideNeuralSupercharge", false);
            Scribe_Values.Look(ref learningCapacityOverrideRanchingCentral, "learningCapacityOverrideRanchingCentral", false);
            Scribe_Values.Look(ref learningCapacityOverrideLearningAssistant, "learningCapacityOverrideLearningAssistant", false);


        }

        private void ExposeFurtherLearningData()
        {
            // ============================================================
            // Further Learning
            // ============================================================

            Scribe_Values.Look(
                ref furtherLearningBaseFactor,
                "furtherLearningBaseFactor",
                XpCustomizerDefaults.FurtherLearningBase);


            Scribe_Values.Look(ref furtherLearningMentalSettingsVersion,
                "furtherLearningMentalSettingsVersion", 0);
            Scribe_Values.Look(ref furtherLearningMentalThresholdEnabled,
                "furtherLearningMentalThresholdEnabled",
                XpCustomizerDefaults.FurtherLearningMentalThresholdEnabled);
            Scribe_Values.Look(ref furtherLearningMentalThresholdStrength,
                "furtherLearningMentalThresholdStrength",
                XpCustomizerDefaults.FurtherLearningMentalStrength);

            // The version marker distinguishes an old config with no switch
            // from a new config where the switch is intentionally OFF.
            Scribe_Values.Look(
                ref furtherLearningTraitSettingsVersion,
                "furtherLearningTraitSettingsVersion",
                0);

            Scribe_Values.Look(
                ref furtherLearningTraitModifiersEnabled,
                "furtherLearningTraitModifiersEnabled",
                XpCustomizerDefaults.FurtherLearningTraitModifiersEnabled);


            // Nerves

            Scribe_Values.Look(
                ref furtherLearningVolatileModifier,
                "furtherLearningVolatileModifier",
                XpCustomizerDefaults.VolatileFurtherLearning);

            Scribe_Values.Look(
                ref furtherLearningNervousModifier,
                "furtherLearningNervousModifier",
                XpCustomizerDefaults.NervousFurtherLearning);

            Scribe_Values.Look(
                ref furtherLearningSteadfastModifier,
                "furtherLearningSteadfastModifier",
                XpCustomizerDefaults.SteadfastFurtherLearning);

            Scribe_Values.Look(
                ref furtherLearningIronWilledModifier,
                "furtherLearningIronWilledModifier",
                XpCustomizerDefaults.IronWilledFurtherLearning);


            // Industriousness

            Scribe_Values.Look(
                ref furtherLearningSlothfulModifier,
                "furtherLearningSlothfulModifier",
                XpCustomizerDefaults.SlothfulFurtherLearning);

            Scribe_Values.Look(
                ref furtherLearningLazyModifier,
                "furtherLearningLazyModifier",
                XpCustomizerDefaults.LazyFurtherLearning);

            Scribe_Values.Look(
                ref furtherLearningHardWorkerModifier,
                "furtherLearningHardWorkerModifier",
                XpCustomizerDefaults.HardWorkerFurtherLearning);

            Scribe_Values.Look(
                ref furtherLearningIndustriousModifier,
                "furtherLearningIndustriousModifier",
                XpCustomizerDefaults.IndustriousFurtherLearning);


            // Neurotic drive

            Scribe_Values.Look(
                ref furtherLearningNeuroticModifier,
                "furtherLearningNeuroticModifier",
                XpCustomizerDefaults.NeuroticFurtherLearning);

            Scribe_Values.Look(
                ref furtherLearningVeryNeuroticModifier,
                "furtherLearningVeryNeuroticModifier",
                XpCustomizerDefaults.VeryNeuroticFurtherLearning);


        }

    }
}
