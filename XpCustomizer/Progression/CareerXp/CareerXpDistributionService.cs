using System;
using System.Collections.Generic;

namespace XpCustomizer.Progression
{
    /// <summary>
    /// Calculates a career-XP curve from four independent ideas:
    ///
    /// 1. Total XP multiplier
    ///    Controls the complete 0 -> 20 XP total.
    ///
    /// 2. Stage boundaries
    ///    Split levels define user-editable progression stages.
    ///    Level 0 and level 20 are always fixed endpoints.
    ///
    /// 3. Stage weights
    ///    Redistribute the fixed total XP between stages while preserving
    ///    RimWorld's vanilla relative XP curve inside each stage.
    ///
    /// 4. XP rounding
    ///    Optionally quantizes every individual level-up requirement to a
    ///    clean 100 / 500 / 1000 XP step while keeping the curve and total
    ///    as close as mathematically possible.
    ///
    /// Recommended starting stages:
    /// 0 -> 3 -> 7 -> 14 -> 17 -> 19 -> 20
    ///
    /// This service is the authoritative career-XP calculator.
    /// Both the settings UI and gameplay consume the same calculated curve.
    /// </summary>
    internal static partial class CareerXpDistributionService
    {
        public const float VanillaTotalXp = 265000f;

        public const float MinTotalMultiplier = 0.01f;
        public const float MaxTotalMultiplier = 100f;

        public const float MinStageWeight = 0.01f;
        public const float MaxStageWeight = 100f;

        public const int XpRoundingExact = 0;
        public const int XpRounding100 = 100;
        public const int XpRounding500 = 500;
        public const int XpRounding1000 = 1000;

        public const int MinLevel = 0;
        public const int MaxLevel = 20;
        public const int LevelUpCount = 20;

        public const int RecommendedStageCount = 6;


        private static readonly float[] VanillaRequirements =
        {
             1000f,  2000f,  3000f,  4000f,  5000f,
             6000f,  7000f,  8000f,  9000f, 10000f,
            12000f, 14000f, 16000f, 18000f, 20000f,
            22000f, 24000f, 26000f, 28000f, 30000f
        };


        // Only interior split levels are stored here.
        // Level 0 and level 20 are fixed endpoints and are added automatically.
        private static readonly int[] RecommendedSplitLevels =
        {
            3,
            7,
            14,
            17,
            19
        };

        // Existing public facade and vanilla reference helpers.

        public static float NormalizeTotalMultiplier(
            float multiplier)
        {
            if (float.IsNaN(multiplier) ||
                float.IsInfinity(multiplier))
            {
                return 1f;
            }

            if (multiplier < MinTotalMultiplier)
            {
                return MinTotalMultiplier;
            }

            if (multiplier > MaxTotalMultiplier)
            {
                return MaxTotalMultiplier;
            }

            return multiplier;
        }


        public static float GetVanillaRequirement(
            int startingLevel)
        {
            ValidateStartingLevel(
                startingLevel);

            return VanillaRequirements[startingLevel];
        }


        public static float GetVanillaXpForRange(
            int startLevel,
            int endLevelExclusive)
        {
            ValidateStageRange(
                startLevel,
                endLevelExclusive);

            float total = 0f;

            for (int level = startLevel;
                 level < endLevelExclusive;
                 level++)
            {
                total +=
                    VanillaRequirements[level];
            }

            return total;
        }


        public static float GetVanillaShareForRange(
            int startLevel,
            int endLevelExclusive)
        {
            return
                GetVanillaXpForRange(
                    startLevel,
                    endLevelExclusive) /
                VanillaTotalXp;
        }


        public static CareerXpDistribution BuildRecommended(
            float totalMultiplier,
            bool useCustomDistribution,
            IList<float> stageWeights,
            int roundingStep = XpRoundingExact)
        {
            return Build(
                totalMultiplier,
                useCustomDistribution,
                RecommendedSplitLevels,
                stageWeights,
                roundingStep);
        }


        private static void ValidateStartingLevel(
            int startingLevel)
        {
            if (startingLevel < MinLevel ||
                startingLevel >= MaxLevel)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startingLevel));
            }
        }


        private static void ValidateStageRange(
            int startLevel,
            int endLevelExclusive)
        {
            if (startLevel < MinLevel ||
                startLevel >= MaxLevel)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startLevel));
            }

            if (endLevelExclusive <= startLevel ||
                endLevelExclusive > MaxLevel)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(endLevelExclusive));
            }
        }
    }
}
