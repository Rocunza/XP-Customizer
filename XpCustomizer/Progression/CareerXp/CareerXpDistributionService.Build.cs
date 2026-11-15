using System;
using System.Collections.Generic;

namespace XpCustomizer.Progression
{
    internal static partial class CareerXpDistributionService
    {

        // Authoritative game/UI curve computation. Original Build body is unchanged.

        public static CareerXpDistribution Build(
            float totalMultiplier,
            bool useCustomDistribution,
            IList<int> splitLevels,
            IList<float> stageWeights,
            int roundingStep = XpRoundingExact,
            IList<float> lockedStageShares = null)
        {
            float normalizedMultiplier =
                NormalizeTotalMultiplier(
                    totalMultiplier);

            int[] normalizedSplits =
                NormalizeSplitLevels(
                    splitLevels);

            int[] boundaries =
                BuildBoundaries(
                    normalizedSplits);

            int stageCount =
                boundaries.Length - 1;

            float[] normalizedStageWeights =
                ResolveStageWeights(
                    stageCount,
                    useCustomDistribution,
                    stageWeights);


            int targetTotalXp =
                (int)Math.Round(
                    VanillaTotalXp *
                    normalizedMultiplier,
                    MidpointRounding.AwayFromZero);

            // Keep at least one XP available for every level-up.
            if (targetTotalXp < LevelUpCount)
            {
                targetTotalXp = LevelUpCount;
            }


            int normalizedRoundingStep =
                NormalizeRoundingStep(
                    roundingStep);

            int effectiveRoundingStep =
                GetEffectiveRoundingStep(
                    normalizedRoundingStep,
                    targetTotalXp);

            int totalXp;

            if (normalizedRoundingStep == XpRoundingExact)
            {
                totalXp =
                    targetTotalXp;
            }
            else
            {
                totalXp =
                    (int)Math.Round(
                        targetTotalXp /
                        (double)effectiveRoundingStep,
                        MidpointRounding.AwayFromZero) *
                    effectiveRoundingStep;

                int minimumRoundedTotal =
                    effectiveRoundingStep *
                    LevelUpCount;

                if (totalXp < minimumRoundedTotal)
                {
                    totalXp =
                        minimumRoundedTotal;
                }
            }


            // Build one effective score per individual level-up.
            //
            // This avoids nested rounding between stage allocation and
            // level allocation. With every stage weight at 1.00x, the
            // exact vanilla relative curve is preserved regardless of
            // where the user places stage boundaries.
            double[] levelScores =
                new double[LevelUpCount];

            for (int stageIndex = 0;
                 stageIndex < stageCount;
                 stageIndex++)
            {
                int startLevel =
                    boundaries[stageIndex];

                int endLevelExclusive =
                    boundaries[stageIndex + 1];

                double stageWeight =
                    normalizedStageWeights[stageIndex];

                for (int level = startLevel;
                     level < endLevelExclusive;
                     level++)
                {
                    levelScores[level] =
                        VanillaRequirements[level] *
                        stageWeight;
                }
            }


            int allocationUnit =
                normalizedRoundingStep == XpRoundingExact
                    ? 1
                    : effectiveRoundingStep;

            int totalUnits =
                totalXp /
                allocationUnit;

            // No locks: preserve the original allocation bit-for-bit.
            // Locked allocations are stage-first, then level-within-stage.
            int[] levelUnits =
                HasActiveShareLocks(stageCount, useCustomDistribution,
                    lockedStageShares)
                    ? AllocateShareLockedLevels(
                        totalUnits, boundaries, levelScores, lockedStageShares)
                    : CareerXpAllocator.AllocateIntegerTotal(
                        totalUnits, levelScores);

            int[] minimumLevelUnits =
                new int[LevelUpCount];

            for (int level = 0;
                 level < LevelUpCount;
                 level++)
            {
                minimumLevelUnits[level] = 1;
            }

            CareerXpAllocator.EnsureMinimumAllocations(
                levelUnits,
                minimumLevelUnits);


            int[] levelXp =
                new int[LevelUpCount];

            float[] requirements =
                new float[LevelUpCount];

            for (int level = 0;
                 level < LevelUpCount;
                 level++)
            {
                levelXp[level] =
                    levelUnits[level] *
                    allocationUnit;

                requirements[level] =
                    levelXp[level];
            }


            float[] stageXp =
                new float[stageCount];

            float[] stageShares =
                new float[stageCount];

            for (int stageIndex = 0;
                 stageIndex < stageCount;
                 stageIndex++)
            {
                int startLevel =
                    boundaries[stageIndex];

                int endLevelExclusive =
                    boundaries[stageIndex + 1];

                int currentStageXp = 0;

                for (int level = startLevel;
                     level < endLevelExclusive;
                     level++)
                {
                    currentStageXp +=
                        levelXp[level];
                }

                stageXp[stageIndex] =
                    currentStageXp;

                stageShares[stageIndex] =
                    totalXp > 0
                        ? currentStageXp /
                          (float)totalXp
                        : 0f;
            }


            return new CareerXpDistribution(
                totalXp,
                requirements,
                boundaries,
                normalizedStageWeights,
                stageXp,
                stageShares,
                normalizedRoundingStep,
                effectiveRoundingStep);
        }
    }
}
