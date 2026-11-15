using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.ConsciousnessLearning;

namespace XpCustomizer.LearningCapacity
{
    /// <summary>
    /// Settings-only neutral activation gate; no live Pawn access.
    /// LC1 preserves existing numerical and gameplay semantics.
    /// </summary>
    internal static class LearningCapacityActivationPolicy
    {
        internal static bool IsConfiguredToChangeDailyCap(
            XpCustomizerSettings settings)
        {
            if (settings == null)
            {
                return false;
            }


            if (!Mathf.Approximately(
                    LearningCapacityNormalization.NormalizeBaseDailyCap(
                        settings.learningCapacityBaseDailyCap),
                    LearningCapacityService.VanillaDailyCap))
            {
                return true;
            }


            if (settings.learningCapacitySourceModifiersEnabled &&
                LearningCapacityActivationPolicy.HasAnyConfiguredSourceModifier(settings))
            {
                return true;
            }


            // The Consciousness-only gate uses the same selected source as
            // the gameplay capacity factor. Legacy strength keys never gate
            // an active Anchor V2 profile (and vice versa).
            return ConsciousnessCurveDispatcher.HasConfiguredCapacityEffect(
                settings);
        }


        internal static bool HasAnyConfiguredSourceModifier(XpCustomizerSettings s)
        {
            // Do not bypass a manual override when the Auto slider is 0%.
            // A nonzero Auto formula can modify capacity only when strength > 0.
            bool autoActive = LearningCapacityNormalization.NormalizeSourceStrength(s.learningCapacitySourceStrength) > 0f;
            return
                (s.learningCapacityOverrideSlowLearner
                    ? !Mathf.Approximately(s.learningCapacityCustomSlowLearner, 0f)
                    : autoActive) ||
                (s.learningCapacityOverrideFastLearner
                    ? !Mathf.Approximately(s.learningCapacityCustomFastLearner, 0f)
                    : autoActive) ||
                (s.learningCapacityOverrideTooSmart
                    ? !Mathf.Approximately(s.learningCapacityCustomTooSmart, 0f)
                    : autoActive) ||
                (s.learningCapacityOverrideBlissLobotomy
                    ? !Mathf.Approximately(s.learningCapacityCustomBlissLobotomy, 0f)
                    : autoActive) ||
                (s.learningCapacityOverrideSlowStudy
                    ? !Mathf.Approximately(s.learningCapacityCustomSlowStudy, 0f)
                    : autoActive) ||
                (s.learningCapacityOverrideQuickStudy
                    ? !Mathf.Approximately(s.learningCapacityCustomQuickStudy, 0f)
                    : autoActive) ||
                (s.learningCapacityOverrideNeuralSupercharge
                    ? !Mathf.Approximately(s.learningCapacityCustomNeuralSupercharge, 0f)
                    : autoActive) ||
                (s.learningCapacityOverrideRanchingCentral
                    ? !Mathf.Approximately(s.learningCapacityCustomRanchingCentral, 0f)
                    : autoActive) ||
                (s.learningCapacityOverrideLearningAssistant
                    ? !Mathf.Approximately(s.learningCapacityCustomLearningAssistant, 0f)
                    : autoActive);
        }
    }
}
