#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;
using XpCustomizer.LearningCapacity;

namespace XpCustomizer
{
    /// <summary>
    /// LC1: detached pure calculation, source-detector null safety, public API,
    /// and presentation-only accumulation. Does not create a live Pawn or
    /// execute Harmony, modify XpCustomizerMod.Settings, or write any files.
    /// </summary>
    internal static class LearningCapacitySplitSelfTests
    {
        private const float Tolerance = 0.001f;
        private static int checks;

        private static void Check(bool valid, string name)
        {
            checks++;
            if (!valid)
                throw new InvalidOperationException("LC1_SPLIT:" + name);
        }

        private static void Near(float actual, float expected, string name)
        {
            Check(!float.IsNaN(actual) &&
                !float.IsInfinity(actual) &&
                Math.Abs(actual - expected) <= Tolerance,
                name + " expected=" + expected + "; actual=" + actual);
        }

        private static void CheckMethod(Type type, string methodName,
            BindingFlags flags, Type returnType, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(methodName, flags,
                null, parameters, null);
            Check(method != null && method.ReturnType == returnType,
                type.Name + "." + methodName + "_CONTRACT");
        }

        internal static void Run()
        {
            checks = 0;
            // A newer refactor must not fail this LC1 math/API test solely
            // because the project BuildId advanced. Keep basic identity
            // validation; exact package identity is checked via build logs.
            Check(!string.IsNullOrEmpty(XpCustomizerMod.BuildId) &&
                XpCustomizerMod.BuildId.StartsWith("XPC-", StringComparison.Ordinal),
                "BUILD_ID_FAMILY");
            Type facade = typeof(LearningCapacityService);
            Check(facade.FullName ==
                "XpCustomizer.LearningCapacity.LearningCapacityService",
                "FACADE_TYPE_IDENTITY");
            FieldInfo[] constants = facade.GetFields(
                BindingFlags.Public | BindingFlags.Static);
            int literalCount = 0;
            foreach (FieldInfo f in constants)
                if (f.IsLiteral) literalCount++;
            Check(literalCount == 11, "FACADE_CONST_COUNT_11");
            Check(LearningCapacityService.VanillaDailyCap == 4000f &&
                LearningCapacityService.SourceConversionFraction == 0.20f,
                "VANILLA_AND_SOURCE_CONVERSION_CONST");
            Check(LearningCapacityService.VanillaLfSlowLearner == -75f &&
                LearningCapacityService.VanillaLfFastLearner == 75f &&
                LearningCapacityService.VanillaLfTooSmart == 75f &&
                LearningCapacityService.VanillaLfBlissLobotomy == -50f &&
                LearningCapacityService.VanillaLfSlowStudy == -50f &&
                LearningCapacityService.VanillaLfQuickStudy == 50f &&
                LearningCapacityService.VanillaLfNeuralSupercharge == 25f &&
                LearningCapacityService.VanillaLfRanchingCentral == 100f &&
                LearningCapacityService.VanillaLfLearningAssistant == 20f,
                "ALL_NINE_VANILLA_SOURCE_CONSTANTS");

            const BindingFlags pub = BindingFlags.Public | BindingFlags.Static;
            const BindingFlags internalFlags = BindingFlags.NonPublic | BindingFlags.Static;
            CheckMethod(facade, "IsConfiguredToChangeDailyCap", pub,
                typeof(bool), Type.EmptyTypes);
            CheckMethod(facade, "IsConfiguredToChangeDailyCap", internalFlags,
                typeof(bool), typeof(XpCustomizerSettings));
            CheckMethod(facade, "GetDailyCap", pub,
                typeof(float), typeof(SkillRecord));
            CheckMethod(facade, "GetBreakdown", pub,
                typeof(LearningCapacityBreakdown), typeof(SkillRecord));
            CheckMethod(facade, "CalculateFromComponents", internalFlags,
                typeof(float), typeof(float), typeof(bool), typeof(float),
                typeof(float), typeof(float).MakeByRefType(),
                typeof(float).MakeByRefType());
            CheckMethod(facade, "NormalizeBaseDailyCap", pub,
                typeof(float), typeof(float));
            CheckMethod(facade, "NormalizeSourceStrength", pub,
                typeof(float), typeof(float));
            CheckMethod(facade, "NormalizeSourcePercentagePoints", pub,
                typeof(float), typeof(float));
            CheckMethod(facade, "GetEffectiveSourcePercentagePoints", pub,
                typeof(float), typeof(float), typeof(float));
            CheckMethod(facade, "GetV4SourcePercentagePoints", pub,
                typeof(float), typeof(bool), typeof(float), typeof(float),
                typeof(float), typeof(float));

            Type[] helpers = new Type[] {
                typeof(LearningCapacityActivationPolicy),
                typeof(LearningCapacityCalculator),
                typeof(LearningCapacitySourceEvaluator),
                typeof(LearningCapacitySourceAccumulator),
                typeof(LearningCapacitySourceDetector),
                typeof(LearningCapacityBreakdownBuilder),
                typeof(LearningCapacityNormalization)
            };
            foreach (Type helper in helpers)
            {
                Check(helper.IsAbstract && helper.IsSealed &&
                    helper.IsNotPublic, helper.Name + "_STATIC_INTERNAL");
            }
            CheckMethod(typeof(Patch_SkillRecord_LearningSaturatedToday),
                "Postfix", pub, typeof(void), typeof(SkillRecord),
                typeof(bool).MakeByRefType());
            CheckMethod(typeof(LearningCapacitySourceEvaluator),
                "GetSourceCapacityModifier", internalFlags,
                typeof(float), typeof(Pawn), typeof(SkillDef),
                typeof(XpCustomizerSettings),
                typeof(List<LearningCapacityContribution>));
            CheckMethod(typeof(LearningCapacityCalculator),
                "CalculateDailyCap", internalFlags, typeof(float),
                typeof(SkillRecord), typeof(XpCustomizerSettings),
                typeof(List<LearningCapacityContribution>),
                typeof(float).MakeByRefType(), typeof(float).MakeByRefType(),
                typeof(float).MakeByRefType(), typeof(float).MakeByRefType());

            Near(LearningCapacityService.GetDailyCap(null), 4000f,
                "NULL_SKILL_VANILLA_CAP");
            LearningCapacityBreakdown empty =
                LearningCapacityService.GetBreakdown(null);
            Check(empty != null && empty.Contributions != null &&
                empty.Contributions.Count == 0, "NULL_SKILL_EMPTY_BREAKDOWN");
            Near(empty.BaseCapacity, 4000f, "NULL_SKILL_BASE_4000");
            Near(empty.FinalCapacity, 4000f, "NULL_SKILL_FINAL_4000");
            Near(empty.ConsciousnessFactor, 1f, "NULL_SKILL_CONS_NEUTRAL");
            Check(!LearningCapacityService.IsConfiguredToChangeDailyCap(null),
                "NULL_SETTINGS_VANILLA_GATE");
            Check(!LearningCapacityActivationPolicy.IsConfiguredToChangeDailyCap(null),
                "NULL_SETTINGS_HELPER_GATE");

            float[] bases = { 0f, 101f, 4000f, 8000f };
            float[] fractions = { -1.2f, -0.25f, 0f, 0.005f, 0.25f, 1.0f };
            float[] consciousness = { 0f, 0.8f, 1f, 1.35f, 1.5f };
            foreach (float b in bases)
            foreach (float fraction in fractions)
            foreach (float cons in consciousness)
            foreach (bool enabled in new bool[] { false, true })
            {
                float rawFacade, middleFacade, rawHelper, middleHelper;
                float viaFacade = LearningCapacityService.CalculateFromComponents(
                    b, enabled, fraction, cons, out rawFacade, out middleFacade);
                float viaHelper = LearningCapacityCalculator.CalculateFromComponents(
                    b, enabled, fraction, cons, out rawHelper, out middleHelper);
                Near(viaFacade, viaHelper, "TWO_ROUND_FINAL_PARITY");
                Near(rawFacade, rawHelper, "TWO_ROUND_RAW_PARITY");
                Near(middleFacade, middleHelper, "TWO_ROUND_MIDDLE_PARITY");
            }
            float raw, middle;
            Near(LearningCapacityService.CalculateFromComponents(
                101f, true, 0.005f, 1.5f, out raw, out middle),
                153f, "ROUND_TWICE_101");
            Near(middle, 102f, "INTERMEDIATE_102");
            Near(LearningCapacityService.CalculateFromComponents(
                4000f, false, 0.5f, 1f, out raw, out middle),
                4000f, "SOURCE_MASTER_OFF");
            Near(LearningCapacityService.CalculateFromComponents(
                4000f, true, -1.2f, 1f, out raw, out middle),
                0f, "NEGATIVE_RAW_CLAMP_AT_ZERO");
            Near(LearningCapacityService.CalculateFromComponents(
                4000f, true, 0.15f, 1f, out raw, out middle),
                4600f, "PLUS_15PP");

            float[] strengths = { -1f, 0f, 0.5f, 1f, 2f, 3f, float.NaN };
            float[] sourceValues = { -200f, -75f, -15f, 0f, 20f, 75f, 200f };
            foreach (float str in strengths)
            {
                Near(LearningCapacityService.NormalizeSourceStrength(str),
                    LearningCapacityNormalization.NormalizeSourceStrength(str),
                    "STRENGTH_FACADE_TO_CORE");
                foreach (float pp in sourceValues)
                {
                    Near(LearningCapacityService.GetEffectiveSourcePercentagePoints(pp, str),
                        LearningCapacityNormalization.GetEffectiveSourcePercentagePoints(pp, str),
                        "LEGACY_V3_PREVIEW_PARITY");
                }
            }
            foreach (float pp in sourceValues)
            {
                Near(LearningCapacityService.NormalizeSourcePercentagePoints(pp),
                    LearningCapacityNormalization.NormalizeSourcePercentagePoints(pp),
                    "SOURCE_CLAMP_PARITY");
            }
            foreach (float b in new float[] { -1f, 0f, 4000f, 9999999f, 10000000f })
            {
                Near(LearningCapacityService.NormalizeBaseDailyCap(b),
                    LearningCapacityNormalization.NormalizeBaseDailyCap(b),
                    "BASE_CLAMP_PARITY");
            }
            foreach (float lf in new float[] { -75f, -50f, 20f, 25f, 50f, 75f, 100f })
            foreach (float strength in new float[] { 0f, 0.5f, 1f, 2f })
            foreach (float factor in new float[] { 0.5f, 1f })
            foreach (bool custom in new bool[] { false, true })
            {
                float throughFacade = LearningCapacityService.GetV4SourcePercentagePoints(
                    custom, 17f, lf, strength, factor);
                float throughHelper = LearningCapacityNormalization.GetV4SourcePercentagePoints(
                    custom, 17f, lf, strength, factor);
                Near(throughFacade, throughHelper, "V4_MANUAL_AUTO_PARITY");
            }
            Near(LearningCapacityService.GetV4SourcePercentagePoints(
                false, 0f, 75f, 1f, 0.5f), 7.5f,
                "SLOW_STUDY_AUTO_FACTOR");
            Near(LearningCapacityService.GetV4SourcePercentagePoints(
                true, 17f, 75f, 2f, 0.5f), 17f,
                "MANUAL_IGNORES_SLOW_STUDY_AND_STRENGTH");
            Near(LearningCapacityService.GetV4SourcePercentagePoints(
                false, 0f, -50f, 1f), -10f,
                "SLOW_STUDY_SELF_NO_FACTOR");

            // No actual Pawn or Def database is required for these checks.
            Check(!LearningCapacitySourceDetector.HasActiveTrait(null, "SlowLearner"),
                "NULL_PAWN_TRAIT_INACTIVE");
            Check(!LearningCapacitySourceDetector.HasActiveGene(null, "Learning_Fast"),
                "NULL_PAWN_GENE_INACTIVE");
            Check(!LearningCapacitySourceDetector.HasActiveHediff(null, "SomeHediff"),
                "NULL_PAWN_HEDIFF_INACTIVE");
            Check(!LearningCapacitySourceDetector.HasActivePrecept(null, "Ranching_Central"),
                "NULL_PAWN_PRECEPT_INACTIVE");
            Check(LearningCapacitySourceDetector.GetActiveTraitLabel(null, "SlowLearner") ==
                "SlowLearner", "NULL_PAWN_TRAIT_LABEL_FALLBACK");
            XpCustomizerSettings isolated = new XpCustomizerSettings();
            Near(LearningCapacitySourceEvaluator.GetSourceCapacityModifier(
                null, null, isolated, null), 0f,
                "NULL_PAWN_EVALUATOR_ZERO");
            float total = 7f;
            LearningCapacitySourceAccumulator.AddTraitModifier(
                null, "SlowLearner", 15f, null, ref total);
            Near(total, 7f, "NULL_PAWN_ACCUMULATOR_NOT_MUTATED");

            List<LearningCapacityContribution> rows =
                new List<LearningCapacityContribution>();
            LearningCapacityBreakdownBuilder.AddCapacityContribution(rows, "Fast", 0.15f);
            LearningCapacityBreakdownBuilder.AddCapacityContribution(rows, "Fast", 0.05f);
            LearningCapacityBreakdownBuilder.AddCapacityContribution(rows, "Slow", -0.05f);
            Check(rows.Count == 2 && rows[0].Label == "Fast" &&
                rows[1].Label == "Slow", "BREAKDOWN_DUPLICATE_MERGE_ORDER");
            Near(rows[0].Modifier, 0.20f, "BREAKDOWN_DUPLICATE_MERGE_VALUE");
            Near(rows[1].Modifier, -0.05f, "BREAKDOWN_SEPARATE_ROW_VALUE");
            LearningCapacityBreakdownBuilder.AddCapacityContribution(rows, "", 1f);
            LearningCapacityBreakdownBuilder.AddCapacityContribution(rows, "Fast", 0f);
            Check(rows.Count == 2, "BREAKDOWN_EMPTY_AND_ZERO_IGNORED");

            Log.Message("[XPC-LC1][LEARNING_CAPACITY_SPLIT_AUDIT] status=PASS; " +
                "checks=" + checks + "; helpers=7; facadePublicApis=8; " +
                "facadeInternalApis=2; facadeConstants=11; " +
                "scope=DETACHED_PURE_ARITHMETIC_AND_REFLECTION; " +
                "buildIdPolicy=NONEMPTY_XPC_PREFIX; " +
                "sourceOrder=STATIC_ORIGINAL_PRESERVED; " +
                "sourceDetectorLivePawn=NOT_RUN; realHarmony=NOT_RUN; " +
                "realTooltip=NOT_RUN; externalMods=NOT_RUN; " +
                "diskScribe=NOT_RUN; realGameWrites=0; " +
                "build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
