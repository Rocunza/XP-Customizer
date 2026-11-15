using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace XpCustomizer.LearningCapacity
{
    /// <summary>
    /// Stateless settings normalization and V4 source conversion.
    /// LC1 preserves existing numerical and gameplay semantics.
    /// </summary>
    internal static class LearningCapacityNormalization
    {
        internal static float NormalizeBaseDailyCap(
            float value)
        {
            return Mathf.Round(
                Mathf.Clamp(
                    value,
                    XpCustomizerDefaults
                        .LearningCapacityBaseDailyCapMin,
                    XpCustomizerDefaults
                        .LearningCapacityBaseDailyCapMax));
        }


        internal static float GetV4SourcePercentagePoints(
            bool customOverride, float customFinalPercentagePoints,
            float vanillaLfPercentagePoints, float globalStrength, float otherVanillaFactor = 1f)
        {
            if (customOverride)
                return LearningCapacityNormalization.NormalizeSourcePercentagePoints(customFinalPercentagePoints);
            return LearningCapacityNormalization.NormalizeSourcePercentagePoints(
                vanillaLfPercentagePoints * otherVanillaFactor * LearningCapacityService.SourceConversionFraction *
                LearningCapacityNormalization.NormalizeSourceStrength(globalStrength));
        }


        internal static float GetEffectiveSourcePercentagePoints(
            float savedPercentagePoints, float globalStrength)
        {
            return LearningCapacityNormalization.NormalizeSourcePercentagePoints(
                LearningCapacityNormalization.NormalizeSourcePercentagePoints(savedPercentagePoints) *
                LearningCapacityNormalization.NormalizeSourceStrength(globalStrength));
        }


        internal static float NormalizeSourceStrength(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return XpCustomizerDefaults.LearningCapacitySourceStrength;
            }
            return Mathf.Clamp(value,
                XpCustomizerDefaults.LearningCapacitySourceStrengthMin,
                XpCustomizerDefaults.LearningCapacitySourceStrengthMax);
        }


        internal static float NormalizeSourcePercentagePoints(
            float value)
        {
            return Mathf.Clamp(
                value,
                XpCustomizerDefaults
                    .LearningCapacityCustomMin,
                XpCustomizerDefaults
                    .LearningCapacityCustomMax);
        }
    }
}
