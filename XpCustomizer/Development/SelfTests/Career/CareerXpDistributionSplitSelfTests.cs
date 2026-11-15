#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using XpCustomizer.Progression;

namespace XpCustomizer
{
    /// <summary>
    /// CXD1 move-only audit. Read-only reflection and deterministic calculation
    /// against known vanilla anchors and distinct input-path equivalences.
    /// Does NOT patch Harmony, mutate settings, persist XML or invoke live XP.
    /// The original 20 method bodies are checked byte-for-byte by an external
    /// source audit; compiled code cannot inspect its own original source.
    /// </summary>
    internal static class CareerXpDistributionSplitSelfTests
    {
        private const BindingFlags DeclaredStatic = BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static int checks;
        private static int vectors;
        private static int levelComparisons;

        private static void Check(bool valid, string name)
        {
            checks++;
            if (!valid)
                throw new InvalidOperationException("CXD1_DISTRIBUTION_SPLIT:" + name);
        }

        private static MethodInfo CheckMethod(Type owner, string name,
            Type resultType, bool isPublic, params Type[] parameters)
        {
            MethodInfo method = owner.GetMethod(name, DeclaredStatic,
                null, parameters, null);
            Check(method != null && method.DeclaringType == owner &&
                method.IsStatic && method.IsPublic == isPublic &&
                method.ReturnType == resultType, "METHOD_" + name);
            return method;
        }

        private static void CompareAllLevels(CareerXpDistribution left,
            CareerXpDistribution right, string name)
        {
            Check(left != null && right != null, name + "_NOT_NULL");
            Check(left.TotalXp == right.TotalXp, name + "_TOTAL_XP");
            Check(left.StageCount == right.StageCount, name + "_STAGE_COUNT");
            Check(left.RequestedRoundingStep == right.RequestedRoundingStep &&
                left.EffectiveRoundingStep == right.EffectiveRoundingStep,
                name + "_ROUNDING_STEPS");
            for (int level = 0; level < CareerXpDistributionService.LevelUpCount; level++)
            {
                Check(left.GetRequirement(level) == right.GetRequirement(level),
                    name + "_LEVEL_" + level);
                levelComparisons++;
            }
            for (int stage = 0; stage < left.StageCount; stage++)
            {
                Check(left.GetStageStartLevel(stage) == right.GetStageStartLevel(stage) &&
                    left.GetStageEndLevel(stage) == right.GetStageEndLevel(stage),
                    name + "_STAGE_BOUNDARY_" + stage);
                Check(left.GetStageXp(stage) == right.GetStageXp(stage) &&
                    left.GetStageShare(stage) == right.GetStageShare(stage),
                    name + "_STAGE_TOTAL_" + stage);
            }
            vectors++;
        }

        private static void CheckConservation(CareerXpDistribution distribution,
            string name)
        {
            Check(distribution != null && distribution.StageCount > 0,
                name + "_VALID_RESULT");
            double sum = 0d;
            for (int level = 0; level < CareerXpDistributionService.LevelUpCount; level++)
            {
                float xp = distribution.GetRequirement(level);
                Check(xp >= 1f && xp == (int)xp, name + "_POSITIVE_INTEGER_" + level);
                sum += xp;
            }
            Check(sum == distribution.TotalXp, name + "_CONSERVES_LEVEL_TOTAL");
            double stageSum = 0d;
            for (int stage = 0; stage < distribution.StageCount; stage++)
                stageSum += distribution.GetStageXp(stage);
            Check(stageSum == distribution.TotalXp, name + "_CONSERVES_STAGE_TOTAL");
        }

        private static void CheckVanillaGolden()
        {
            float[] vanilla = {
                1000f, 2000f, 3000f, 4000f, 5000f,
                6000f, 7000f, 8000f, 9000f, 10000f,
                12000f, 14000f, 16000f, 18000f, 20000f,
                22000f, 24000f, 26000f, 28000f, 30000f
            };
            Check(CareerXpDistributionService.VanillaTotalXp == 265000f,
                "VANILLA_TOTAL_CONSTANT");
            Check(CareerXpDistributionService.MinLevel == 0 &&
                CareerXpDistributionService.MaxLevel == 20 &&
                CareerXpDistributionService.LevelUpCount == 20,
                "LEVEL_LIMITS");
            CareerXpDistribution distribution = CareerXpDistributionService.Build(
                1f, false, null, null);
            Check(distribution.TotalXp == 265000f, "VANILLA_GOLDEN_TOTAL");
            for (int level = 0; level < 20; level++)
            {
                Check(CareerXpDistributionService.GetVanillaRequirement(level) == vanilla[level],
                    "REFERENCE_LEVEL_" + level);
                Check(distribution.GetRequirement(level) == vanilla[level],
                    "BUILT_LEVEL_" + level);
            }
            CheckConservation(distribution, "VANILLA_GOLDEN");
            int[] expectedBoundaries = { 0, 3, 7, 14, 17, 19, 20 };
            int[] boundaries = CareerXpDistributionService.GetRecommendedBoundaries();
            Check(boundaries.Length == expectedBoundaries.Length,
                "RECOMMENDED_BOUNDARY_COUNT");
            for (int i = 0; i < boundaries.Length; i++)
                Check(boundaries[i] == expectedBoundaries[i],
                    "RECOMMENDED_BOUNDARY_" + i);
            int[] first = CareerXpDistributionService.GetRecommendedSplitLevels();
            int[] second = CareerXpDistributionService.GetRecommendedSplitLevels();
            Check(!Object.ReferenceEquals(first, second),
                "RECOMMENDED_SPLITS_RETURN_CLONES");
            first[0] = 18;
            Check(second[0] == 3 &&
                CareerXpDistributionService.GetRecommendedSplitLevels()[0] == 3,
                "RECOMMENDED_SPLITS_NOT_MUTATED");
        }

        private static void CheckSignatures()
        {
            Type owner = typeof(CareerXpDistributionService);
            Check(owner.FullName == "XpCustomizer.Progression.CareerXpDistributionService",
                "ORIGINAL_FULL_NAME");
            Check(owner.IsNotPublic && owner.IsAbstract && owner.IsSealed,
                "ONE_INTERNAL_STATIC_CLR_TYPE");
            Check(owner.Assembly == typeof(XpCustomizerMod).Assembly &&
                typeof(CareerXpAllocator).Assembly == owner.Assembly &&
                typeof(CareerXpDistribution).Assembly == owner.Assembly,
                "COLLABORATORS_SAME_ASSEMBLY");
            Check(!string.IsNullOrEmpty(XpCustomizerMod.BuildId) &&
                XpCustomizerMod.BuildId.StartsWith("XPC-", StringComparison.Ordinal),
                "BUILD_ID_FAMILY");
            FieldInfo vanilla = owner.GetField("VanillaRequirements", DeclaredStatic);
            FieldInfo recommended = owner.GetField("RecommendedSplitLevels", DeclaredStatic);
            Check(vanilla != null && vanilla.DeclaringType == owner &&
                vanilla.IsPrivate && vanilla.IsStatic && vanilla.IsInitOnly &&
                vanilla.FieldType == typeof(float[]), "VANILLA_FIELD_SAME_OWNER");
            Check(recommended != null && recommended.DeclaringType == owner &&
                recommended.IsPrivate && recommended.IsStatic && recommended.IsInitOnly &&
                recommended.FieldType == typeof(int[]), "RECOMMENDED_FIELD_SAME_OWNER");
            int instanceFreeFields = 0;
            foreach (FieldInfo field in owner.GetFields(DeclaredStatic))
                if (!field.IsLiteral) instanceFreeFields++;
            Check(instanceFreeFields == 2, "ONLY_ORIGINAL_2_ARRAY_FIELDS");
            Check(owner.GetMethods(DeclaredStatic).Length == 20,
                "TWENTY_ORIGINAL_METHODS");
            Type f = typeof(float);
            Type i = typeof(int);
            Type b = typeof(bool);
            Type ia = typeof(int[]);
            Type fa = typeof(float[]);
            Type listI = typeof(IList<int>);
            Type listF = typeof(IList<float>);
            Type result = typeof(CareerXpDistribution);
            CheckMethod(owner, "NormalizeTotalMultiplier", f, true, f);
            CheckMethod(owner, "GetVanillaRequirement", f, true, i);
            CheckMethod(owner, "GetVanillaXpForRange", f, true, i, i);
            CheckMethod(owner, "GetVanillaShareForRange", f, true, i, i);
            MethodInfo recommendedBuild = CheckMethod(owner, "BuildRecommended", result,
                true, f, b, listF, i);
            CheckMethod(owner, "ValidateStartingLevel", typeof(void), false, i);
            CheckMethod(owner, "ValidateStageRange", typeof(void), false, i, i);
            CheckMethod(owner, "NormalizeStageWeight", f, true, f);
            CheckMethod(owner, "GetRecommendedSplitLevels", ia, true);
            CheckMethod(owner, "GetRecommendedBoundaries", ia, true);
            CheckMethod(owner, "NormalizeSplitLevels", ia, true, listI);
            CheckMethod(owner, "BuildBoundaries", ia, false, listI);
            CheckMethod(owner, "ResolveStageWeights", fa, false, i, b, listF);
            CheckMethod(owner, "NormalizeRoundingStep", i, true, i);
            CheckMethod(owner, "GetEffectiveRoundingStep", i, true, i, i);
            MethodInfo build = CheckMethod(owner, "Build", result, true,
                f, b, listI, listF, i, listF);
            CheckMethod(owner, "NormalizeLockedStageShare", f, true, f);
            CheckMethod(owner, "HasActiveShareLocks", b, false, i, b, listF);
            CheckMethod(owner, "CountLocked", i, false, i, listF);
            CheckMethod(owner, "AllocateShareLockedLevels", ia, false,
                i, ia, typeof(double[]), listF);
            ParameterInfo[] buildParams = build.GetParameters();
            ParameterInfo[] recommendedParams = recommendedBuild.GetParameters();
            Check(buildParams.Length == 6 &&
                buildParams[4].IsOptional &&
                (int)buildParams[4].DefaultValue == CareerXpDistributionService.XpRoundingExact &&
                buildParams[5].IsOptional && buildParams[5].DefaultValue == null,
                "BUILD_OPTIONAL_PARAMETERS");
            Check(recommendedParams.Length == 4 &&
                recommendedParams[3].IsOptional &&
                (int)recommendedParams[3].DefaultValue == CareerXpDistributionService.XpRoundingExact,
                "RECOMMENDED_OPTIONAL_PARAMETERS");
        }

        private static void CheckPureVectors()
        {
            Check(CareerXpDistributionService.GetEffectiveRoundingStep(1000, 2650) == 100,
                "LOW_TOTAL_1000_FALLBACK_100");
            Check(CareerXpDistributionService.GetEffectiveRoundingStep(500, 10000) == 500,
                "BOUNDARY_500_ACCEPTED");
            Check(CareerXpDistributionService.NormalizeLockedStageShare(-1f) == -1f,
                "LOCK_UNLOCK_SENTINEL");
            Check(CareerXpDistributionService.NormalizeLockedStageShare(0f) == 0f,
                "ZERO_IS_LOCKED");
            Check(CareerXpDistributionService.NormalizeLockedStageShare(float.NaN) == -1f,
                "NAN_IS_UNLOCKED");
            Check(CareerXpDistributionService.NormalizeStageWeight(1000f) == 100f,
                "STAGE_WEIGHT_MAX");
            float[] multipliers = { 0.01f, 0.33f, 1f, 5f };
            int[] rounding = { 0, 100, 500, 1000 };
            IList<int>[] splitCases = {
                null,
                new int[] { 3, 7, 14, 17, 19 },
                new int[] { 14, 3, 14, 0, 20, 7 }
            };
            for (int mi = 0; mi < multipliers.Length; mi++)
                for (int ri = 0; ri < rounding.Length; ri++)
                    for (int si = 0; si < splitCases.Length; si++)
                    {
                        string tag = "M" + mi + "R" + ri + "S" + si;
                        IList<int> splits = splitCases[si];
                        float[] ones = { 1f, 1f, 1f, 1f, 1f, 1f };
                        float[] unlocked = { -1f, -1f, -1f, -1f, -1f, -1f };
                        CareerXpDistribution vanilla = CareerXpDistributionService.Build(
                            multipliers[mi], false, splits, ones, rounding[ri]);
                        CareerXpDistribution customOnes = CareerXpDistributionService.Build(
                            multipliers[mi], true, splits, ones, rounding[ri]);
                        CareerXpDistribution explicitUnlocked = CareerXpDistributionService.Build(
                            multipliers[mi], true, splits, ones, rounding[ri], unlocked);
                        CareerXpDistribution disabledWithLock = CareerXpDistributionService.Build(
                            multipliers[mi], false, splits, ones, rounding[ri],
                            new float[] { 0.2f, -1f, -1f, -1f, -1f, 0.5f });
                        CompareAllLevels(vanilla, customOnes, tag + "_ONE_WEIGHT_PARITY");
                        CompareAllLevels(customOnes, explicitUnlocked,
                            tag + "_UNLOCKED_PATH_PARITY");
                        CompareAllLevels(vanilla, disabledWithLock,
                            tag + "_DISABLED_LOCK_IGNORED");
                        CheckConservation(vanilla, tag + "_VANILLA_CONSERVATION");
                        CheckConservation(explicitUnlocked, tag + "_UNLOCKED_CONSERVATION");
                        if (si == 1)
                        {
                            CareerXpDistribution recommended =
                                CareerXpDistributionService.BuildRecommended(
                                    multipliers[mi], true, ones, rounding[ri]);
                            CompareAllLevels(recommended, customOnes,
                                tag + "_RECOMMENDED_PARITY");
                        }
                    }
            float[] weights = { 1f, 1f, 2f, 4f, 3f, 1f };
            float[] locks = { -1f, -1f, -1f, -1f, -1f, 0.5f };
            float[] allLocks = { 0.1f, 0.2f, 0.2f, 0.1f, 0.1f, 0.3f };
            for (int i = 0; i < rounding.Length; i++)
            {
                CareerXpDistribution unlocked = CareerXpDistributionService.Build(
                    5f, true, CareerXpDistributionService.GetRecommendedSplitLevels(),
                    weights, rounding[i]);
                CareerXpDistribution locked = CareerXpDistributionService.Build(
                    5f, true, CareerXpDistributionService.GetRecommendedSplitLevels(),
                    weights, rounding[i], locks);
                CareerXpDistribution corrupted = CareerXpDistributionService.Build(
                    5f, true, CareerXpDistributionService.GetRecommendedSplitLevels(),
                    weights, rounding[i], allLocks);
                CheckConservation(unlocked, "WEIGHTED_UNL_" + i);
                CheckConservation(locked, "WEIGHTED_LOCK_" + i);
                CheckConservation(corrupted, "ALL_LOCK_DEFENSIVE_" + i);
                Check(Math.Abs(locked.GetStageShare(5) - 0.5f) < 0.001f,
                    "LAST_STAGE_HALF_" + i);
            }
        }

        internal static void Run()
        {
            checks = 0;
            vectors = 0;
            levelComparisons = 0;
            CheckSignatures();
            CheckVanillaGolden();
            CheckPureVectors();
            Log.Message("[XPC-CXD1][CAREER_XP_DISTRIBUTION_SPLIT_AUDIT] " +
                "status=PASS; checks=" + checks +
                "; type=ONE_INTERNAL_STATIC_PARTIAL_CLR_TYPE" +
                "; methods=20_RETAINED; privateArrays=2_SAME_OWNER" +
                "; golden=VANILLA_LEVELS_20; vectors=" + vectors +
                "; levelComparisons=" + levelComparisons +
                "; locks=NULL_UNLOCKED_ZERO_LAST_ALL_LOCKED" +
                "; rounding=EXACT_100_500_1000_WITH_FALLBACK" +
                "; perLevelParity=ALL_0_TO_19; sourceMoveParity=EXTERNAL_SHA_AUDIT" +
                "; livePawnXp=NOT_RUN; realHarmonyOrder=NOT_RUN" +
                "; actualProfileApply=NOT_RUN; diskScribe=NOT_RUN" +
                "; realSettingsWrites=0; profileIO=0; xmlWrites=0" +
                "; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
