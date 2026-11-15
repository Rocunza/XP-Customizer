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
        // ================================================================
        // Learning Capacity V2 migration / integrity
        // ================================================================

        private void NormalizeLearningCapacitySettings()
        {
            // Only V0/V1 require a legacy Scaled/Custom conversion.
            // V2 -> V3 must NEVER re-run that migration or overwrite edits.
            if (learningCapacityDataVersion < 2)
            {
                MigrateLegacyLearningCapacitySettings();
            }

            // V4 migration. Existing V2/V3 custom DLC values stay effective
            // rather than silently being replaced by new Auto formulas.
            // Convert V3 scaled values once, then enable manual source mode.
            // A fully neutral legacy setup stays neutral/Auto by default.
            if (learningCapacityDataVersion < 4 &&
                (learningCapacitySourceModifiersEnabled || HasLegacySourceValues()))
            {
                learningCapacityCustomSlowLearner =
                    LearningCapacity.LearningCapacityService.GetEffectiveSourcePercentagePoints(
                        learningCapacityCustomSlowLearner, learningCapacitySourceStrength);
                learningCapacityOverrideSlowLearner = true;
                learningCapacityCustomFastLearner =
                    LearningCapacity.LearningCapacityService.GetEffectiveSourcePercentagePoints(
                        learningCapacityCustomFastLearner, learningCapacitySourceStrength);
                learningCapacityOverrideFastLearner = true;
                learningCapacityCustomTooSmart =
                    LearningCapacity.LearningCapacityService.GetEffectiveSourcePercentagePoints(
                        learningCapacityCustomTooSmart, learningCapacitySourceStrength);
                learningCapacityOverrideTooSmart = true;
                learningCapacityCustomBlissLobotomy =
                    LearningCapacity.LearningCapacityService.GetEffectiveSourcePercentagePoints(
                        learningCapacityCustomBlissLobotomy, learningCapacitySourceStrength);
                learningCapacityOverrideBlissLobotomy = true;
                learningCapacityCustomSlowStudy =
                    LearningCapacity.LearningCapacityService.GetEffectiveSourcePercentagePoints(
                        learningCapacityCustomSlowStudy, learningCapacitySourceStrength);
                learningCapacityOverrideSlowStudy = true;
                learningCapacityCustomQuickStudy =
                    LearningCapacity.LearningCapacityService.GetEffectiveSourcePercentagePoints(
                        learningCapacityCustomQuickStudy, learningCapacitySourceStrength);
                learningCapacityOverrideQuickStudy = true;
                learningCapacityCustomNeuralSupercharge =
                    LearningCapacity.LearningCapacityService.GetEffectiveSourcePercentagePoints(
                        learningCapacityCustomNeuralSupercharge, learningCapacitySourceStrength);
                learningCapacityOverrideNeuralSupercharge = true;
                learningCapacityCustomRanchingCentral =
                    LearningCapacity.LearningCapacityService.GetEffectiveSourcePercentagePoints(
                        learningCapacityCustomRanchingCentral, learningCapacitySourceStrength);
                learningCapacityOverrideRanchingCentral = true;
                learningCapacityCustomLearningAssistant =
                    LearningCapacity.LearningCapacityService.GetEffectiveSourcePercentagePoints(
                        learningCapacityCustomLearningAssistant, learningCapacitySourceStrength);
                learningCapacityOverrideLearningAssistant = true;
            }

            learningCapacitySourceStrength =
                LearningCapacity.LearningCapacityService.NormalizeSourceStrength(
                    learningCapacitySourceStrength);


            learningCapacityBaseDailyCap =
                Clamp(
                    learningCapacityBaseDailyCap,
                    XpCustomizerDefaults.LearningCapacityBaseDailyCapMin,
                    XpCustomizerDefaults.LearningCapacityBaseDailyCapMax);


            learningCapacityCustomSlowLearner =
                NormalizeLearningCapacitySourceValue(
                    learningCapacityCustomSlowLearner);

            learningCapacityCustomFastLearner =
                NormalizeLearningCapacitySourceValue(
                    learningCapacityCustomFastLearner);

            learningCapacityCustomTooSmart =
                NormalizeLearningCapacitySourceValue(
                    learningCapacityCustomTooSmart);

            learningCapacityCustomBlissLobotomy =
                NormalizeLearningCapacitySourceValue(
                    learningCapacityCustomBlissLobotomy);

            learningCapacityCustomSlowStudy =
                NormalizeLearningCapacitySourceValue(
                    learningCapacityCustomSlowStudy);

            learningCapacityCustomQuickStudy =
                NormalizeLearningCapacitySourceValue(
                    learningCapacityCustomQuickStudy);

            learningCapacityCustomNeuralSupercharge =
                NormalizeLearningCapacitySourceValue(
                    learningCapacityCustomNeuralSupercharge);

            learningCapacityCustomLearningAssistant =
                NormalizeLearningCapacitySourceValue(
                    learningCapacityCustomLearningAssistant);

            learningCapacityCustomRanchingCentral =
                NormalizeLearningCapacitySourceValue(
                    learningCapacityCustomRanchingCentral);


            learningCapacityDataVersion =
                CurrentLearningCapacityDataVersion;
        }


        private bool HasLegacySourceValues()
        {
            return
                !UnityEngine.Mathf.Approximately(learningCapacityCustomSlowLearner, 0f) ||
                !UnityEngine.Mathf.Approximately(learningCapacityCustomFastLearner, 0f) ||
                !UnityEngine.Mathf.Approximately(learningCapacityCustomTooSmart, 0f) ||
                !UnityEngine.Mathf.Approximately(learningCapacityCustomBlissLobotomy, 0f) ||
                !UnityEngine.Mathf.Approximately(learningCapacityCustomSlowStudy, 0f) ||
                !UnityEngine.Mathf.Approximately(learningCapacityCustomQuickStudy, 0f) ||
                !UnityEngine.Mathf.Approximately(learningCapacityCustomNeuralSupercharge, 0f) ||
                !UnityEngine.Mathf.Approximately(learningCapacityCustomRanchingCentral, 0f) ||
                !UnityEngine.Mathf.Approximately(learningCapacityCustomLearningAssistant, 0f);
        }

        private void MigrateLegacyLearningCapacitySettings()
        {
            learningCapacityBaseDailyCap =
                XpCustomizerDefaults.LearningCapacityBaseDailyCap;


            if (legacyLearningCapacityUseCustomValues)
            {
                // Legacy Custom mode already stored direct percentage-point
                // values in the same per-source fields used by V2.
                learningCapacitySourceModifiersEnabled = true;
                return;
            }


            float conversion =
                Clamp(
                    legacyLearningCapacityBonusConversion,
                    0f,
                    2f);


            if (conversion <= 0f)
            {
                learningCapacitySourceModifiersEnabled = false;
                return;
            }


            // Legacy Scaled mode converted these vanilla learning-source
            // modifiers into capacity modifiers. Preserve the known built-in
            // sources as direct V2 percentage-point values.
            learningCapacitySourceModifiersEnabled = true;

            learningCapacityCustomSlowLearner = -75f * conversion;
            learningCapacityCustomFastLearner = 75f * conversion;
            learningCapacityCustomTooSmart = 75f * conversion;

            learningCapacityCustomBlissLobotomy = -50f * conversion;

            learningCapacityCustomSlowStudy = -50f * conversion;
            learningCapacityCustomQuickStudy = 50f * conversion;

            learningCapacityCustomNeuralSupercharge = 25f * conversion;
            learningCapacityCustomLearningAssistant = 20f * conversion;
            learningCapacityCustomRanchingCentral = 100f * conversion;
        }


        private static float NormalizeLearningCapacitySourceValue(
            float value)
        {
            return Clamp(
                value,
                XpCustomizerDefaults.LearningCapacityCustomMin,
                XpCustomizerDefaults.LearningCapacityCustomMax);
        }


        private static float Clamp(
            float value,
            float min,
            float max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }


    }
}
