using System;
using System.Collections.Generic;

namespace XpCustomizer.Progression
{
    /// <summary>
    /// Gameplay-facing facade for the career XP curve.
    ///
    /// CareerXpDistributionService owns the actual curve math.
    /// This class adds a small cache so RimWorld can query level-up XP
    /// repeatedly without rebuilding the full 0 -> 20 distribution every time.
    ///
    /// UI preview and gameplay therefore share the same calculator.
    /// </summary>
    public static class XpCurve
    {
        private static CareerXpDistribution cachedDistribution;

        private static float cachedMultiplier =
            float.NaN;

        private static bool cachedUseCustomDistribution;

        private static int cachedRoundingStep =
            int.MinValue;

        private static int[] cachedSplitLevels =
            new int[0];

        private static float[] cachedStageWeights =
            new float[0];

        private static float[] cachedLockedShares =
            new float[0];


        // ================================================================
        // Public API
        // ================================================================

        public static float NormalizeMultiplier(
            float multiplier)
        {
            return CareerXpDistributionService
                .NormalizeTotalMultiplier(
                    multiplier);
        }


        public static float GetRequirement(
            int startingLevel,
            XpCustomizerSettings settings)
        {
            if (startingLevel < CareerXpDistributionService.MinLevel ||
                startingLevel >= CareerXpDistributionService.MaxLevel)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startingLevel));
            }

            return GetDistribution(
                    settings)
                .GetRequirement(
                    startingLevel);
        }


        public static float GetTotalXp(
            XpCustomizerSettings settings)
        {
            return GetDistribution(
                    settings)
                .TotalXp;
        }


        internal static CareerXpDistribution GetDistribution(
            XpCustomizerSettings settings)
        {
            if (settings == null)
            {
                return CareerXpDistributionService.Build(
                    1f,
                    false,
                    null,
                    null,
                    CareerXpDistributionService.XpRoundingExact);
            }

            if (!CacheMatches(
                    settings))
            {
                RebuildCache(
                    settings);
            }

            return cachedDistribution;
        }


        public static void InvalidateCache()
        {
            cachedDistribution =
                null;

            cachedMultiplier =
                float.NaN;

            cachedRoundingStep =
                int.MinValue;

            cachedSplitLevels =
                new int[0];

            cachedStageWeights =
                new float[0];

            cachedLockedShares =
                new float[0];
        }


        // ================================================================
        // Cache
        // ================================================================

        private static bool CacheMatches(
            XpCustomizerSettings settings)
        {
            if (cachedDistribution == null)
            {
                return false;
            }


            float multiplier =
                CareerXpDistributionService.NormalizeTotalMultiplier(
                    settings.careerXpMultiplier);

            if (Math.Abs(
                    cachedMultiplier -
                    multiplier) > 0.000001f)
            {
                return false;
            }


            if (cachedUseCustomDistribution !=
                settings.careerXpUseCustomDistribution)
            {
                return false;
            }


            int roundingStep =
                CareerXpDistributionService.NormalizeRoundingStep(
                    settings.careerXpRoundingStep);

            if (cachedRoundingStep !=
                roundingStep)
            {
                return false;
            }


            // Split layout and stage weights cannot affect gameplay while
            // custom distribution is disabled. Ignore them in that state so
            // editing preview boundaries does not cause needless cache rebuilds.
            if (!settings.careerXpUseCustomDistribution)
            {
                return true;
            }


            if (!IntSequenceMatches(
                    settings.careerXpSplitLevels,
                    cachedSplitLevels))
            {
                return false;
            }


            if (!FloatSequenceMatches(
                    settings.careerXpStageWeights,
                    cachedStageWeights))
            {
                return false;
            }

            if (!FloatSequenceMatches(
                    settings.careerXpStageLockedShares,
                    cachedLockedShares))
            {
                return false;
            }


            return true;
        }


        private static void RebuildCache(
            XpCustomizerSettings settings)
        {
            cachedMultiplier =
                CareerXpDistributionService.NormalizeTotalMultiplier(
                    settings.careerXpMultiplier);

            cachedUseCustomDistribution =
                settings.careerXpUseCustomDistribution;

            cachedRoundingStep =
                CareerXpDistributionService.NormalizeRoundingStep(
                    settings.careerXpRoundingStep);


            cachedDistribution =
                CareerXpDistributionService.Build(
                    cachedMultiplier,
                    cachedUseCustomDistribution,
                    settings.careerXpSplitLevels,
                    settings.careerXpStageWeights,
                    cachedRoundingStep,
                    settings.careerXpStageLockedShares);


            if (cachedUseCustomDistribution)
            {
                cachedSplitLevels =
                    CopyInts(
                        settings.careerXpSplitLevels);

                cachedStageWeights =
                    CopyFloats(
                        settings.careerXpStageWeights);

                cachedLockedShares =
                    CopyFloats(settings.careerXpStageLockedShares);
            }
            else
            {
                cachedSplitLevels =
                    new int[0];

                cachedStageWeights =
                    new float[0];

                cachedLockedShares =
                    new float[0];
            }
        }


        private static bool IntSequenceMatches(
            IList<int> values,
            int[] cachedValues)
        {
            int count =
                values != null
                    ? values.Count
                    : 0;

            if (cachedValues == null ||
                cachedValues.Length != count)
            {
                return false;
            }

            for (int i = 0;
                 i < count;
                 i++)
            {
                if (values[i] !=
                    cachedValues[i])
                {
                    return false;
                }
            }

            return true;
        }


        private static bool FloatSequenceMatches(
            IList<float> values,
            float[] cachedValues)
        {
            int count =
                values != null
                    ? values.Count
                    : 0;

            if (cachedValues == null ||
                cachedValues.Length != count)
            {
                return false;
            }

            for (int i = 0;
                 i < count;
                 i++)
            {
                if (Math.Abs(
                        values[i] -
                        cachedValues[i]) > 0.000001f)
                {
                    return false;
                }
            }

            return true;
        }


        private static int[] CopyInts(
            IList<int> values)
        {
            int count =
                values != null
                    ? values.Count
                    : 0;

            int[] copy =
                new int[count];

            for (int i = 0;
                 i < count;
                 i++)
            {
                copy[i] =
                    values[i];
            }

            return copy;
        }


        private static float[] CopyFloats(
            IList<float> values)
        {
            int count =
                values != null
                    ? values.Count
                    : 0;

            float[] copy =
                new float[count];

            for (int i = 0;
                 i < count;
                 i++)
            {
                copy[i] =
                    values[i];
            }

            return copy;
        }
    }
}
