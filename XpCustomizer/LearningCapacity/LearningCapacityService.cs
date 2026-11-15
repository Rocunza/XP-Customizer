using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.ConsciousnessLearning;

namespace XpCustomizer.LearningCapacity
{
    /// <summary>
    /// Calculates the full-rate daily learning capacity for a skill.
    ///
    /// V4 model:
    /// - User-selected Base Daily Capacity.
    /// - Optional explicit per-source capacity modifiers.
    /// - Optional Consciousness multiplier applied last.
    ///
    /// Gameplay uses the numeric path only. Breakdown objects and labels are
    /// created only when presentation code explicitly requests them.
    /// </summary>
    public static class LearningCapacityService
    {
        public const float VanillaDailyCap = 4000f;
        // RimWorld 1.6 nominal vanilla contributions to Global Learning Factor.
        // These are source-defined reference values, NOT a decomposition of a
        // Pawn's final stat (which can be changed by arbitrary other mods).
        // Keep this explicit to avoid modifying vanilla stat calculations.
        public const float SourceConversionFraction = 0.20f;
        public const float VanillaLfSlowLearner = -75f;
        public const float VanillaLfFastLearner = 75f;
        public const float VanillaLfTooSmart = 75f;
        public const float VanillaLfBlissLobotomy = -50f;
        public const float VanillaLfSlowStudy = -50f;
        public const float VanillaLfQuickStudy = 50f;
        public const float VanillaLfNeuralSupercharge = 25f;
        public const float VanillaLfRanchingCentral = 100f;
        public const float VanillaLfLearningAssistant = 20f;



        public static bool IsConfiguredToChangeDailyCap()
        {
            return IsConfiguredToChangeDailyCap(
                XpCustomizerMod.Settings);
        }


        internal static bool IsConfiguredToChangeDailyCap(
            XpCustomizerSettings settings)
        {
            return LearningCapacityActivationPolicy.IsConfiguredToChangeDailyCap(settings);
        }


        public static float GetDailyCap(
            SkillRecord skill)
        {
            if (skill == null ||
                skill.Pawn == null)
            {
                return VanillaDailyCap;
            }


            XpCustomizerSettings settings =
                XpCustomizerMod.Settings;


            if (settings == null)
            {
                return VanillaDailyCap;
            }


            float dynamicModifier;
            float rawDynamicCapacity;
            float dynamicCapacity;
            float consciousnessFactor;


            return LearningCapacityCalculator.CalculateDailyCap(
                skill,
                settings,
                null,
                out dynamicModifier,
                out rawDynamicCapacity,
                out dynamicCapacity,
                out consciousnessFactor);
        }


        public static LearningCapacityBreakdown GetBreakdown(
            SkillRecord skill)
        {
            return LearningCapacityBreakdownBuilder.GetBreakdown(skill);
        }


        internal static float CalculateFromComponents(
            float normalizedBaseCapacity,
            bool sourceModifiersEnabled,
            float sourceModifier,
            float consciousnessFactor,
            out float rawDynamicCapacity,
            out float dynamicCapacity)
        {
            return LearningCapacityCalculator.CalculateFromComponents(normalizedBaseCapacity, sourceModifiersEnabled, sourceModifier, consciousnessFactor, out rawDynamicCapacity, out dynamicCapacity);
        }


        public static float NormalizeBaseDailyCap(
            float value)
        {
            return LearningCapacityNormalization.NormalizeBaseDailyCap(value);
        }


        public static float GetV4SourcePercentagePoints(
            bool customOverride, float customFinalPercentagePoints,
            float vanillaLfPercentagePoints, float globalStrength, float otherVanillaFactor = 1f)
        {
            return LearningCapacityNormalization.GetV4SourcePercentagePoints(customOverride, customFinalPercentagePoints, vanillaLfPercentagePoints, globalStrength, otherVanillaFactor);
        }


        public static float GetEffectiveSourcePercentagePoints(
            float savedPercentagePoints, float globalStrength)
        {
            return LearningCapacityNormalization.GetEffectiveSourcePercentagePoints(savedPercentagePoints, globalStrength);
        }


        public static float NormalizeSourceStrength(float value)
        {
            return LearningCapacityNormalization.NormalizeSourceStrength(value);
        }


        public static float NormalizeSourcePercentagePoints(
            float value)
        {
            return LearningCapacityNormalization.NormalizeSourcePercentagePoints(value);
        }
    }
}
