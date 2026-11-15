#if DEBUG
using System;
using System.Collections.Generic;
using XpCustomizer.Progression;

namespace XpCustomizer
{
    /// <summary>
    /// Development-time regression tests for the career-XP distribution core.
    ///
    /// These tests intentionally exercise only deterministic calculation code.
    /// They do not touch RimWorld state and therefore catch curve/allocation
    /// regressions before an in-game manual test is needed.
    /// </summary>
    internal static class CareerXpSelfTests
    {
        private const float FloatTolerance = 0.01f;


        public static void Run()
        {
            TestVanillaCurveIdentity();
            TestRecommendedStageTotals();
            TestBoundaryNormalization();
            TestBoundariesDoNotChangeAllOneCurve();
            TestCustomStageWeightRedistributesButConservesTotal();
            TestRoundingSteps();
            TestLowTotalRoundingFallback();
            TestMultiplierNormalization();
            TestVanillaReferenceColumnsAcrossCustomBoundaries();
            TestShareLockConservesTotalAndRemainder();
            TestShareLockBackwardCompatibility();
            TestShareLockGameplayCache();
            Verse.Log.Message(
                "[XP Customizer] DEBUG stage Share Lock tests passed (SL-1): " +
                "locked target, residual weights, rounding, OFF and gameplay cache.");
            Verse.Log.Message(
                "[XP Customizer] DEBUG Vanilla stage comparison passed (UI-6C): " +
                "custom boundaries, reference shares/XP and current-to-Vanilla ratios.");
        }


        // ================================================================
        // Optional stage share locks (SL-1)
        // ================================================================

        private static void TestShareLockConservesTotalAndRemainder()
        {
            int[] splits = CareerXpDistributionService.GetRecommendedSplitLevels();
            float[] weights = { 1f, 1f, 1f, 3f, 5f, 1f };
            float[] locks = { -1f, -1f, -1f, -1f, -1f, 0.5f };

            foreach (int rounding in new int[] {
                CareerXpDistributionService.XpRoundingExact,
                CareerXpDistributionService.XpRounding100,
                CareerXpDistributionService.XpRounding500,
                CareerXpDistributionService.XpRounding1000 })
            {
                CareerXpDistribution d = CareerXpDistributionService.Build(
                    5f, true, splits, weights, rounding, locks);
                AssertNear(d.TotalXp, 1325000f, 0.01f, "lock total");
                AssertNear(d.GetStageShare(5), 0.5f, 0.001f,
                    "level 19-20 locked 50%");
                float sum = 0;
                for (int i = 0; i < d.StageCount; i++)
                    sum += d.GetStageXp(i);
                AssertNear(sum, d.TotalXp, 0.01f, "lock stages conserve XP");
                for (int level = 0; level < 20; level++)
                    AssertTrue(d.GetRequirement(level) >= 1f,
                        "locked allocations never zero a level");
            }

            CareerXpDistribution before = CareerXpDistributionService.Build(
                5f, true, splits, weights, 500, locks);
            weights[0] = 100f;
            CareerXpDistribution after = CareerXpDistributionService.Build(
                5f, true, splits, weights, 500, locks);
            AssertNear(after.GetStageShare(5), 0.5f, 0.001f,
                "locked stage unaffected by other weight edits");
            AssertTrue(after.GetStageXp(0) > before.GetStageXp(0),
                "unlocked weights redistribute remainder");

            float[] multiLocks = { 0.2f, -1f, -1f, -1f, -1f, 0.5f };
            CareerXpDistribution multi = CareerXpDistributionService.Build(
                5f, true, splits, weights, 500, multiLocks);
            AssertNear(multi.GetStageShare(0), 0.2f, 0.001f,
                "first independently locked share");
            AssertNear(multi.GetStageShare(5), 0.5f, 0.001f,
                "second independently locked share");
        }

        private static void TestShareLockBackwardCompatibility()
        {
            int[] splits = CareerXpDistributionService.GetRecommendedSplitLevels();
            float[] weights = { 1f, 2f, 3f, 4f, 5f, 6f };
            float[] noLocks = { -1f, -1f, -1f, -1f, -1f, -1f };
            float[] lockLast = { -1f, -1f, -1f, -1f, -1f, 0.5f };
            CareerXpDistribution original = CareerXpDistributionService.Build(
                2f, true, splits, weights, 1000);
            CareerXpDistribution unlocked = CareerXpDistributionService.Build(
                2f, true, splits, weights, 1000, noLocks);
            CareerXpDistribution off = CareerXpDistributionService.Build(
                2f, false, splits, weights, 1000, lockLast);
            CareerXpDistribution vanillaMode = CareerXpDistributionService.Build(
                2f, false, splits, weights, 1000);
            for (int i = 0; i < 20; i++)
            {
                AssertNear(original.GetRequirement(i),
                    unlocked.GetRequirement(i), 0f, "no-lock unchanged level " + i);
                AssertNear(vanillaMode.GetRequirement(i),
                    off.GetRequirement(i), 0f, "distribution OFF unchanged level " + i);
            }
        }

        private static void TestShareLockGameplayCache()
        {
            XpCustomizerSettings settings = new XpCustomizerSettings();
            settings.careerXpMultiplier = 5f;
            settings.careerXpUseCustomDistribution = true;
            settings.careerXpStageWeights = new List<float> {1f,1f,1f,1f,1f,1f};
            settings.careerXpStageLockedShares =
                new List<float> {-1f,-1f,-1f,-1f,-1f,-1f};
            try
            {
                float baseline = XpCurve.GetRequirement(19, settings);
                settings.careerXpStageLockedShares[5] = 0.5f;
                float modified = XpCurve.GetRequirement(19, settings);
                AssertNear(modified, XpCurve.GetTotalXp(settings) * 0.5f,
                    1f, "runtime cache rechecks lock change");
                AssertTrue(modified > baseline,
                    "locked requirement must change after cache invalidation");
                settings.careerXpStageLockedShares[5] = -1f;
                AssertNear(XpCurve.GetRequirement(19, settings), baseline,
                    0f, "unlock restores original distribution");
            }
            finally { XpCurve.InvalidateCache(); }
        }

        // ================================================================
        // Progression UI: Vanilla reference columns
        // ================================================================

        private static void TestVanillaReferenceColumnsAcrossCustomBoundaries()
        {
            // The reference columns must use the selected stage boundaries,
            // but must not inherit current weights, total multiplier or rounding.
            int[] customSplits = { 4, 11, 18 };
            float[] customWeights = { 1f, 2f, 4f, 19f };

            CareerXpDistribution distribution =
                CareerXpDistributionService.Build(
                    5f,
                    true,
                    customSplits,
                    customWeights,
                    CareerXpDistributionService.XpRounding500);

            float totalVanillaXp = 0f;

            for (int stage = 0; stage < distribution.StageCount; stage++)
            {
                int startLevel = distribution.GetStageStartLevel(stage);
                int endLevel = distribution.GetStageEndLevel(stage);

                float expectedVanillaXp = 0f;
                for (int level = startLevel; level < endLevel; level++)
                {
                    expectedVanillaXp +=
                        CareerXpDistributionService.GetVanillaRequirement(level);
                }

                float vanillaXp =
                    CareerXpDistributionService.GetVanillaXpForRange(
                        startLevel, endLevel);

                float vanillaShare =
                    CareerXpDistributionService.GetVanillaShareForRange(
                        startLevel, endLevel);

                AssertNear(vanillaXp, expectedVanillaXp, FloatTolerance,
                    "vanilla reference XP with custom boundaries at stage " + stage);

                AssertNear(vanillaShare,
                    expectedVanillaXp / CareerXpDistributionService.VanillaTotalXp,
                    0.00001f,
                    "vanilla reference share with custom boundaries at stage " + stage);

                // UI shows current / vanilla in both comparison columns.
                float xpRatio = distribution.GetStageXp(stage) / vanillaXp;
                float shareRatio = distribution.GetStageShare(stage) / vanillaShare;
                float totalRatio = distribution.TotalXp /
                    CareerXpDistributionService.VanillaTotalXp;

                AssertNear(xpRatio, shareRatio * totalRatio, 0.001f,
                    "vanilla reference ratios at stage " + stage);

                totalVanillaXp += vanillaXp;
            }

            AssertNear(totalVanillaXp,
                CareerXpDistributionService.VanillaTotalXp,
                FloatTolerance, "vanilla references still sum to vanilla total");
        }


        // ================================================================
        // Vanilla identity / known totals
        // ================================================================

        private static void TestVanillaCurveIdentity()
        {
            CareerXpDistribution distribution =
                CareerXpDistributionService.BuildRecommended(
                    totalMultiplier: 1f,
                    useCustomDistribution: false,
                    stageWeights: null,
                    roundingStep: CareerXpDistributionService.XpRoundingExact);

            AssertNear(
                distribution.TotalXp,
                CareerXpDistributionService.VanillaTotalXp,
                FloatTolerance,
                "vanilla exact total");

            float summedRequirements = 0f;

            for (int level = 0;
                 level < CareerXpDistributionService.LevelUpCount;
                 level++)
            {
                float expected =
                    CareerXpDistributionService.GetVanillaRequirement(level);

                float actual =
                    distribution.GetRequirement(level);

                AssertNear(
                    actual,
                    expected,
                    FloatTolerance,
                    "vanilla exact requirement level " + level.ToString());

                AssertTrue(
                    actual >= 1f,
                    "vanilla exact requirement must remain positive at level " +
                    level.ToString());

                summedRequirements += actual;
            }

            AssertNear(
                summedRequirements,
                distribution.TotalXp,
                FloatTolerance,
                "vanilla exact requirement sum");
        }


        private static void TestRecommendedStageTotals()
        {
            CareerXpDistribution distribution =
                CareerXpDistributionService.BuildRecommended(
                    totalMultiplier: 1f,
                    useCustomDistribution: false,
                    stageWeights: null,
                    roundingStep: CareerXpDistributionService.XpRoundingExact);

            int[] expectedStarts =
            {
                0, 3, 7, 14, 17, 19
            };

            int[] expectedEnds =
            {
                3, 7, 14, 17, 19, 20
            };

            float[] expectedStageXp =
            {
                6000f,
                22000f,
                87000f,
                66000f,
                54000f,
                30000f
            };

            AssertTrue(
                distribution.StageCount == expectedStageXp.Length,
                "recommended stage count expected " +
                expectedStageXp.Length.ToString() + " but got " +
                distribution.StageCount.ToString());

            float stageTotal = 0f;
            float shareTotal = 0f;

            for (int stage = 0;
                 stage < distribution.StageCount;
                 stage++)
            {
                AssertTrue(
                    distribution.GetStageStartLevel(stage) == expectedStarts[stage],
                    "recommended stage " + stage.ToString() +
                    " start boundary mismatch");

                AssertTrue(
                    distribution.GetStageEndLevel(stage) == expectedEnds[stage],
                    "recommended stage " + stage.ToString() +
                    " end boundary mismatch");

                AssertNear(
                    distribution.GetStageXp(stage),
                    expectedStageXp[stage],
                    FloatTolerance,
                    "recommended stage " + stage.ToString() + " XP");

                AssertNear(
                    distribution.GetStageWeight(stage),
                    1f,
                    FloatTolerance,
                    "recommended stage " + stage.ToString() + " weight");

                stageTotal +=
                    distribution.GetStageXp(stage);

                shareTotal +=
                    distribution.GetStageShare(stage);
            }

            AssertNear(
                stageTotal,
                distribution.TotalXp,
                FloatTolerance,
                "recommended stage XP sum");

            AssertNear(
                shareTotal,
                1f,
                0.0001f,
                "recommended stage share sum");
        }


        // ================================================================
        // Dynamic boundaries
        // ================================================================

        private static void TestBoundaryNormalization()
        {
            int[] normalized =
                CareerXpDistributionService.NormalizeSplitLevels(
                    new List<int>
                    {
                        19,
                        3,
                        7,
                        7,
                        0,
                        20,
                        -5,
                        14,
                        17,
                        21
                    });

            int[] expected =
            {
                3, 7, 14, 17, 19
            };

            AssertTrue(
                normalized.Length == expected.Length,
                "split normalization length mismatch");

            for (int i = 0;
                 i < expected.Length;
                 i++)
            {
                AssertTrue(
                    normalized[i] == expected[i],
                    "split normalization mismatch at index " + i.ToString());
            }

            int[] empty =
                CareerXpDistributionService.NormalizeSplitLevels(null);

            AssertTrue(
                empty.Length == 0,
                "null split list must normalize to one 0-20 stage");
        }


        private static void TestBoundariesDoNotChangeAllOneCurve()
        {
            List<int> everyLevelBoundary =
                new List<int>();

            for (int level = 1;
                 level < CareerXpDistributionService.MaxLevel;
                 level++)
            {
                everyLevelBoundary.Add(level);
            }

            CareerXpDistribution singleStage =
                CareerXpDistributionService.Build(
                    totalMultiplier: 1f,
                    useCustomDistribution: true,
                    splitLevels: new int[0],
                    stageWeights: new float[] { 1f },
                    roundingStep: CareerXpDistributionService.XpRoundingExact);

            CareerXpDistribution twentyStages =
                CareerXpDistributionService.Build(
                    totalMultiplier: 1f,
                    useCustomDistribution: true,
                    splitLevels: everyLevelBoundary,
                    stageWeights: BuildConstantWeights(20, 1f),
                    roundingStep: CareerXpDistributionService.XpRoundingExact);

            AssertTrue(
                singleStage.StageCount == 1,
                "empty split list must create exactly one 0-20 stage");

            AssertTrue(
                twentyStages.StageCount == 20,
                "1..19 split list must create twenty one-level stages");

            for (int level = 0;
                 level < CareerXpDistributionService.LevelUpCount;
                 level++)
            {
                float vanilla =
                    CareerXpDistributionService.GetVanillaRequirement(level);

                AssertNear(
                    singleStage.GetRequirement(level),
                    vanilla,
                    FloatTolerance,
                    "single-stage all-one curve level " + level.ToString());

                AssertNear(
                    twentyStages.GetRequirement(level),
                    vanilla,
                    FloatTolerance,
                    "twenty-stage all-one curve level " + level.ToString());
            }
        }


        // ================================================================
        // Custom weights / total conservation
        // ================================================================

        private static void TestCustomStageWeightRedistributesButConservesTotal()
        {
            float[] weights =
            {
                1f, 1f, 1f, 1f, 1f, 3f
            };

            CareerXpDistribution distribution =
                CareerXpDistributionService.BuildRecommended(
                    totalMultiplier: 5f,
                    useCustomDistribution: true,
                    stageWeights: weights,
                    roundingStep: CareerXpDistributionService.XpRoundingExact);

            AssertNear(
                distribution.TotalXp,
                1325000f,
                FloatTolerance,
                "custom 5x exact total conservation");

            AssertNear(
                SumRequirements(distribution),
                distribution.TotalXp,
                FloatTolerance,
                "custom 5x exact requirement sum");

            AssertNear(
                SumStageXp(distribution),
                distribution.TotalXp,
                FloatTolerance,
                "custom 5x exact stage sum");

            AssertNear(
                SumStageShares(distribution),
                1f,
                0.0001f,
                "custom 5x exact stage share sum");

            AssertNear(
                distribution.GetRequirement(19),
                366923f,
                1f,
                "custom 5x final stage known regression value");

            AssertTrue(
                distribution.GetRequirement(19) > 150000f,
                "3x final-stage weight must raise level 19-20 above the " +
                "all-one 5x value of 150000 XP");

            AssertAllRequirementsPositive(
                distribution,
                "custom 5x exact");
        }


        // ================================================================
        // Rounding / quantization
        // ================================================================

        private static void TestRoundingSteps()
        {
            int[] steps =
            {
                CareerXpDistributionService.XpRounding100,
                CareerXpDistributionService.XpRounding500,
                CareerXpDistributionService.XpRounding1000
            };

            for (int i = 0;
                 i < steps.Length;
                 i++)
            {
                int step = steps[i];

                CareerXpDistribution distribution =
                    CareerXpDistributionService.BuildRecommended(
                        totalMultiplier: 5f,
                        useCustomDistribution: true,
                        stageWeights: new float[]
                        {
                            1f, 1f, 1f, 1f, 1f, 3f
                        },
                        roundingStep: step);

                AssertTrue(
                    distribution.RequestedRoundingStep == step,
                    "requested rounding step mismatch for " + step.ToString());

                AssertTrue(
                    distribution.EffectiveRoundingStep == step,
                    "5x total should not require rounding fallback for " +
                    step.ToString());

                AssertTrue(
                    !distribution.UsesRoundingFallback,
                    "5x total unexpectedly used rounding fallback for " +
                    step.ToString());

                AssertAllRequirementsPositive(
                    distribution,
                    "rounding " + step.ToString());

                AssertAllRequirementsDivisibleBy(
                    distribution,
                    step,
                    "rounding " + step.ToString());

                AssertNear(
                    SumRequirements(distribution),
                    distribution.TotalXp,
                    FloatTolerance,
                    "rounded requirement sum " + step.ToString());

                AssertTrue(
                    ((int)distribution.TotalXp) % step == 0,
                    "rounded total must be divisible by effective step " +
                    step.ToString());
            }


            CareerXpDistribution known100 =
                CareerXpDistributionService.BuildRecommended(
                    totalMultiplier: 5f,
                    useCustomDistribution: true,
                    stageWeights: new float[]
                    {
                        1f, 1f, 1f, 1f, 1f, 3f
                    },
                    roundingStep: CareerXpDistributionService.XpRounding100);

            AssertNear(
                known100.GetRequirement(19),
                366900f,
                FloatTolerance,
                "100-XP rounding final-stage known regression value");
        }


        private static void TestLowTotalRoundingFallback()
        {
            CareerXpDistribution distribution =
                CareerXpDistributionService.BuildRecommended(
                    totalMultiplier: CareerXpDistributionService.MinTotalMultiplier,
                    useCustomDistribution: false,
                    stageWeights: null,
                    roundingStep: CareerXpDistributionService.XpRounding1000);

            AssertTrue(
                distribution.RequestedRoundingStep ==
                CareerXpDistributionService.XpRounding1000,
                "low-total fallback must preserve requested 1000-XP step");

            AssertTrue(
                distribution.EffectiveRoundingStep ==
                CareerXpDistributionService.XpRounding100,
                "0.01x total with requested 1000-XP rounding must fall back " +
                "to 100 XP");

            AssertTrue(
                distribution.UsesRoundingFallback,
                "low-total 1000-XP request must report rounding fallback");

            AssertNear(
                distribution.TotalXp,
                2700f,
                FloatTolerance,
                "0.01x rounded fallback total");

            AssertAllRequirementsPositive(
                distribution,
                "low-total rounding fallback");

            AssertAllRequirementsDivisibleBy(
                distribution,
                CareerXpDistributionService.XpRounding100,
                "low-total rounding fallback");

            AssertNear(
                SumRequirements(distribution),
                distribution.TotalXp,
                FloatTolerance,
                "low-total fallback requirement sum");
        }


        // ================================================================
        // Input normalization
        // ================================================================

        private static void TestMultiplierNormalization()
        {
            AssertNear(
                CareerXpDistributionService.NormalizeTotalMultiplier(-50f),
                CareerXpDistributionService.MinTotalMultiplier,
                FloatTolerance,
                "total multiplier lower clamp");

            AssertNear(
                CareerXpDistributionService.NormalizeTotalMultiplier(500f),
                CareerXpDistributionService.MaxTotalMultiplier,
                FloatTolerance,
                "total multiplier upper clamp");

            AssertNear(
                CareerXpDistributionService.NormalizeTotalMultiplier(float.NaN),
                1f,
                FloatTolerance,
                "total multiplier NaN fallback");

            AssertNear(
                CareerXpDistributionService.NormalizeStageWeight(0f),
                CareerXpDistributionService.MinStageWeight,
                FloatTolerance,
                "stage weight lower clamp");

            AssertNear(
                CareerXpDistributionService.NormalizeStageWeight(1000f),
                CareerXpDistributionService.MaxStageWeight,
                FloatTolerance,
                "stage weight upper clamp");

            AssertTrue(
                CareerXpDistributionService.NormalizeRoundingStep(123) ==
                CareerXpDistributionService.XpRoundingExact,
                "unsupported rounding step must normalize to Exact");
        }


        // ================================================================
        // Helpers
        // ================================================================

        private static float[] BuildConstantWeights(
            int count,
            float value)
        {
            float[] result =
                new float[count];

            for (int i = 0;
                 i < count;
                 i++)
            {
                result[i] = value;
            }

            return result;
        }


        private static float SumRequirements(
            CareerXpDistribution distribution)
        {
            float total = 0f;

            for (int level = 0;
                 level < CareerXpDistributionService.LevelUpCount;
                 level++)
            {
                total +=
                    distribution.GetRequirement(level);
            }

            return total;
        }


        private static float SumStageXp(
            CareerXpDistribution distribution)
        {
            float total = 0f;

            for (int stage = 0;
                 stage < distribution.StageCount;
                 stage++)
            {
                total +=
                    distribution.GetStageXp(stage);
            }

            return total;
        }


        private static float SumStageShares(
            CareerXpDistribution distribution)
        {
            float total = 0f;

            for (int stage = 0;
                 stage < distribution.StageCount;
                 stage++)
            {
                total +=
                    distribution.GetStageShare(stage);
            }

            return total;
        }


        private static void AssertAllRequirementsPositive(
            CareerXpDistribution distribution,
            string label)
        {
            for (int level = 0;
                 level < CareerXpDistributionService.LevelUpCount;
                 level++)
            {
                float value =
                    distribution.GetRequirement(level);

                AssertTrue(
                    value > 0f,
                    label + " produced non-positive XP at level " +
                    level.ToString() + ": " + value.ToString("0.###"));
            }
        }


        private static void AssertAllRequirementsDivisibleBy(
            CareerXpDistribution distribution,
            int step,
            string label)
        {
            for (int level = 0;
                 level < CareerXpDistributionService.LevelUpCount;
                 level++)
            {
                int value =
                    (int)distribution.GetRequirement(level);

                AssertTrue(
                    value % step == 0,
                    label + " produced level " + level.ToString() +
                    " requirement " + value.ToString() +
                    " which is not divisible by " + step.ToString());
            }
        }


        private static void AssertNear(
            float actual,
            float expected,
            float tolerance,
            string label)
        {
            if (Math.Abs(actual - expected) > tolerance)
            {
                Fail(
                    label + " expected " + expected.ToString("0.###") +
                    " but got " + actual.ToString("0.###") +
                    " (tolerance " + tolerance.ToString("0.###") + ").");
            }
        }


        private static void AssertTrue(
            bool condition,
            string message)
        {
            if (!condition)
            {
                Fail(message);
            }
        }


        private static void Fail(
            string message)
        {
            throw new InvalidOperationException(
                "[XP Customizer] DEBUG self-test failed: " + message);
        }
    }
}
#endif
