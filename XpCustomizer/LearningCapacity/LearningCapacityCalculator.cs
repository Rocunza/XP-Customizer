using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.ConsciousnessLearning;

namespace XpCustomizer.LearningCapacity
{
    /// <summary>
    /// Hot-path daily capacity calculation with original two-round sequence.
    /// LC1 preserves existing numerical and gameplay semantics.
    /// </summary>
    internal static class LearningCapacityCalculator
    {
        internal static float CalculateDailyCap(
            SkillRecord skill,
            XpCustomizerSettings settings,
            List<LearningCapacityContribution> contributions,
            out float dynamicModifier,
            out float rawDynamicCapacity,
            out float dynamicCapacity,
            out float consciousnessFactor)
        {
            float baseCapacity =
                LearningCapacityNormalization.NormalizeBaseDailyCap(
                    settings.learningCapacityBaseDailyCap);


            if (settings.learningCapacitySourceModifiersEnabled)
            {
                dynamicModifier =
                    LearningCapacitySourceEvaluator.GetSourceCapacityModifier(
                        skill.Pawn,
                        skill.def,
                        settings,
                        contributions);
            }
            else
            {
                dynamicModifier = 0f;
            }


            consciousnessFactor =
                ConsciousnessCapacityService.GetFactor(
                    skill.Pawn);


            return LearningCapacityCalculator.CalculateFromComponents(
                baseCapacity,
                settings.learningCapacitySourceModifiersEnabled,
                dynamicModifier,
                consciousnessFactor,
                out rawDynamicCapacity,
                out dynamicCapacity);
        }


        internal static float CalculateFromComponents(
            float normalizedBaseCapacity,
            bool sourceModifiersEnabled,
            float sourceModifier,
            float consciousnessFactor,
            out float rawDynamicCapacity,
            out float dynamicCapacity)
        {
            float effectiveModifier =
                sourceModifiersEnabled
                    ? sourceModifier
                    : 0f;


            rawDynamicCapacity =
                normalizedBaseCapacity *
                (1f + effectiveModifier);


            dynamicCapacity =
                Mathf.Round(
                    Mathf.Max(
                        rawDynamicCapacity,
                        0f));


            return Mathf.Round(
                Mathf.Max(
                    dynamicCapacity *
                    consciousnessFactor,
                    0f));
        }
    }
}
