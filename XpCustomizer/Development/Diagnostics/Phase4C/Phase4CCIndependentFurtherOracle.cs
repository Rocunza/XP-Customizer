#if DEBUG
using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.FurtherLearning;

namespace XpCustomizer
{
    /// <summary>
    /// R5-C1 read-only, independent breakdown of post-cap Further Learning.
    /// Gameplay FurtherLearningService and ConsciousnessFurtherLearningService
    /// are NOT used as oracles. The only live inputs are a pre-Learn snapshot,
    /// raw settings and vanilla Pawn stats. No Learn(), settings or Harmony writes.
    /// This is a partial diagnostics class, not a gameplay hook.
    /// </summary>
    internal static partial class Phase4CCLiveXpCapture
    {
        private static bool foreignFurtherHookPresent;
        private static string furtherHookOwnersAtStart = "NONE";

        private static void InspectFurtherHookOwners()
        {
            // Detect modifications on BOTH overloads: SkillRecord delegates to
            // Pawn, and another mod may patch either stage independently.
            MethodBase pawnMethod = AccessTools.Method(typeof(FurtherLearningService),
                "GetFactor", new[] { typeof(Pawn) });
            MethodBase recordMethod = AccessTools.Method(typeof(FurtherLearningService),
                "GetFactor", new[] { typeof(SkillRecord) });
            foreignFurtherHookPresent = HasForeignIntervalOwner(pawnMethod) ||
                HasForeignIntervalOwner(recordMethod);
            furtherHookOwnersAtStart = "pawn=" + (pawnMethod == null ? "MISSING" : PatchOwnerNames(pawnMethod)) +
                ",record=" + (recordMethod == null ? "MISSING" : PatchOwnerNames(recordMethod));
        }

        private sealed class FurtherProof
        {
            internal bool Ready;
            internal string SourceStatus = "NOT_EVALUATED";
            internal string Comparison = "NOT_VERIFIED";
            internal float BaseFactor;
            internal float ConsMultiplier = 1f;
            internal float MentalThreshold = float.NaN;
            internal float MthAdjustmentPp;
            internal string MthBand = "NOT_EVALUATED";
            internal float ExpectedFactor;
        }

        private static float IndependentMentalPp(double threshold, double strength)
        {
            double t = Bound(threshold, .35d, .17d, .50d);
            double normalized = t <= .35d
                ? (.35d - t) / .18d : -(t - .35d) / .15d;
            return (float)(10d * normalized * strength);
        }

        private static float IndependentFurtherFactor(float baseFactor,
            float consMultiplier, float mentalPp)
        {
            // Independently reconstruct (base + additive MTH pp) * Consciousness.
            // Floor only AFTER the multiplier, consistent with live gameplay.
            return (float)Math.Max(0d, ((double)baseFactor +
                (double)mentalPp / 100d) * (double)consMultiplier);
        }

        private static FurtherProof ComposeIndependentFurther(StartState b,
            XpCustomizerSettings s)
        {
            var proof = new FurtherProof();
            if (b == null || b.Pawn == null || s == null)
            {
                proof.SourceStatus = "MISSING_INPUT";
                return proof;
            }
            try
            {
                proof.BaseFactor = (float)Bound(s.furtherLearningBaseFactor, .20d,
                    XpCustomizerDefaults.FurtherLearningBaseMin,
                    XpCustomizerDefaults.FurtherLearningBaseMax);
                if (s.consciousnessAffectsLearning && s.consciousnessAffectsFurtherLearning &&
                    b.Pawn.health != null && b.Pawn.health.capacities != null)
                    proof.ConsMultiplier = IndependentFurtherCurve(
                        b.OracleConsciousness, s.consciousnessFurtherV2);
                proof.MthBand = "DISABLED_OR_INELIGIBLE";
                if (s.furtherLearningMentalThresholdEnabled &&
                    b.Pawn.RaceProps != null && b.Pawn.RaceProps.Humanlike &&
                    Math.Abs(s.furtherLearningMentalThresholdStrength) > 0.000001f)
                {
                    StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail("MentalBreakThreshold");
                    if (stat == null)
                    {
                        // Gameplay would skip the effect too, but absence of
                        // the expected stat cannot prove MTH's live behavior.
                        proof.SourceStatus = "MTH_STAT_MISSING";
                        return proof;
                    }
                    float statValue = b.Pawn.GetStatValue(stat);
                    if (!IsFinite(statValue))
                    {
                        proof.SourceStatus = "MTH_STAT_NONFINITE";
                        return proof;
                    }
                    proof.MentalThreshold = (float)Bound(statValue, .35d, .01d, .50d);
                    double strength = Bound(s.furtherLearningMentalThresholdStrength, 0d,
                        XpCustomizerDefaults.FurtherLearningMentalStrengthMin,
                        XpCustomizerDefaults.FurtherLearningMentalStrengthMax);
                    proof.MthAdjustmentPp = IndependentMentalPp(proof.MentalThreshold, strength);
                    proof.MthBand = proof.MthAdjustmentPp > .00001f ? "POSITIVE" :
                        proof.MthAdjustmentPp < -.00001f ? "NEGATIVE" : "NEUTRAL";
                }
                proof.ExpectedFactor = IndependentFurtherFactor(proof.BaseFactor,
                    proof.ConsMultiplier, proof.MthAdjustmentPp);
                proof.Ready = IsFinite(proof.BaseFactor) && IsFinite(proof.ConsMultiplier) &&
                    IsFinite(proof.MthAdjustmentPp) && IsFinite(proof.ExpectedFactor) &&
                    proof.ExpectedFactor >= 0f;
                proof.SourceStatus = proof.Ready ? "OK" : "NONFINITE_COMPONENT";
            }
            catch (Exception e)
            {
                proof.Ready = false;
                proof.SourceStatus = "SOURCE_EXCEPTION_" + Clean(e.GetType().Name);
            }
            return proof;
        }

        private static string PureFurtherComparison(FurtherProof proof,
            bool saturated, bool rateCaptured, float actualFactor, bool foreignHook)
        {
            if (proof == null || !proof.Ready || !saturated) return "NOT_VERIFIED";
            if (foreignHook) return "INCONCLUSIVE_FOREIGN_LEARN_RATE_OR_FURTHER_HOOK";
            if (!rateCaptured || !IsFinite(actualFactor))
                return "INCONCLUSIVE_PRODUCTION_READ";
            float tolerance = Math.Max(.0002f, Math.Abs(proof.ExpectedFactor) * .002f);
            return Math.Abs(proof.ExpectedFactor - actualFactor) <= tolerance
                ? "PASS_FURTHER_FACTOR" : "REVIEW_FURTHER_FACTOR_MISMATCH";
        }

        private static void CompareIndependentFurther(StartState b)
        {
            FurtherProof proof = b == null ? null : b.OracleFurtherProof;
            if (proof == null) return;
            proof.Comparison = PureFurtherComparison(proof, b.SaturatedBefore,
                b.RateCaptured, b.FurtherFactorBefore,
                b.OracleForeignRateHook || b.ForeignLearnHookAtStart ||
                foreignFurtherHookPresent);
        }

        private static void CheckFurtherPure(float actual, float expected, string name,
            ref int cases)
        {
            cases++;
            if (!IsFinite(actual) || Math.Abs(actual - expected) > .00025f)
                throw new InvalidOperationException("R5C1_FURTHER_" + name +
                    " expected=" + expected + ", actual=" + actual);
        }

        private static void CheckFurtherText(string actual, string expected, string name,
            ref int cases)
        {
            cases++;
            if (actual != expected)
                throw new InvalidOperationException("R5C1_FURTHER_" + name +
                    " expected=" + expected + ", actual=" + actual);
        }

        internal static void RunFurtherPureAudit() { VerifyIndependentFurtherSelfAudit(); }

        private static void VerifyIndependentFurtherSelfAudit()
        {
            // Synthetic only. No pawn/stat reads and no production calculator.
            int cases = 0;
            CheckFurtherPure(IndependentMentalPp(.17d, .60d), 6f, "MTH_POSITIVE60", ref cases);
            CheckFurtherPure(IndependentMentalPp(.35d, .60d), 0f, "MTH_NEUTRAL60", ref cases);
            CheckFurtherPure(IndependentMentalPp(.50d, .60d), -6f, "MTH_NEGATIVE60", ref cases);
            CheckFurtherPure(IndependentMentalPp(.01d, .60d), 6f, "MTH_LOW_CLAMP", ref cases);
            CheckFurtherPure(IndependentMentalPp(.90d, .60d), -6f, "MTH_HIGH_CLAMP", ref cases);
            CheckFurtherPure(IndependentMentalPp(.17d, 0d), 0f, "MTH_DISABLED", ref cases);
            CheckFurtherPure(IndependentMentalPp(.17d, 1d), 10f, "MTH_POSITIVE100", ref cases);
            CheckFurtherPure(IndependentMentalPp(.50d, 1d), -10f, "MTH_NEGATIVE100", ref cases);
            CheckFurtherPure(IndependentFurtherFactor(.20f, 1f, 6f), .26f, "POSITIVE_FINAL", ref cases);
            CheckFurtherPure(IndependentFurtherFactor(.20f, 1f, 0f), .20f, "NEUTRAL_FINAL", ref cases);
            CheckFurtherPure(IndependentFurtherFactor(.20f, 1f, -6f), .14f, "NEGATIVE_FINAL", ref cases);
            CheckFurtherPure(IndependentFurtherFactor(.20f, .80f, 6f), .208f, "LOW_CONS_POSITIVE", ref cases);
            CheckFurtherPure(IndependentFurtherFactor(.20f, 1.35f, -6f), .189f, "HIGH_CONS_NEGATIVE", ref cases);
            CheckFurtherPure(IndependentFurtherFactor(.20f, 1.235f, 6f), .3211f, "R5F1_CONS120_PLUS_WILLPOWER6", ref cases);
            CheckFurtherPure(IndependentFurtherFactor(0f, .80f, -10f), 0f, "ZERO_FLOOR", ref cases);
            CheckFurtherPure(IndependentFurtherFactor(.20f, 1f, -6f), .14f, "NOT_MTH_MULTIPLIER", ref cases);
            var profile = new ConsciousnessFurtherV2Settings();
            CheckFurtherPure(profile.HighAnchor, 1.20f, "D01_CONFIG_HIGH_ANCHOR120", ref cases);
            CheckFurtherPure(profile.HighFactor, 1.20f, "D01_CONFIG_HIGH_FACTOR120", ref cases);
            CheckFurtherPure(profile.HighCurve, 1f, "D01_CONFIG_LINEAR", ref cases);
            CheckFurtherPure(IndependentFurtherCurve(.80f, profile), .80f, "CONS_LOW_ANCHOR", ref cases);
            CheckFurtherPure(IndependentFurtherCurve(1f, profile), 1f, "CONS_NEUTRAL_ANCHOR", ref cases);
            CheckFurtherPure(IndependentFurtherCurve(1.20f, profile), 1.20f,
                "D01_CONS_HIGH_ANCHOR120", ref cases);
            CheckFurtherPure(IndependentFurtherCurve(1.35f, profile), 1.35f,
                "D01_CONS_135_NOT_ANCHOR", ref cases);
            var historicalProfile = new ConsciousnessFurtherV2Settings {
                HighAnchor = 1.35f, HighFactor = 1.35f, HighCurve = 1,
                HighCurvature = 1f, HighDiminishingLimit = 2f, HighSafetyLimit = 2f
            };
            CheckFurtherPure(historicalProfile.HighAnchor, 1.35f,
                "HISTORIC_CONFIG_HIGH_ANCHOR135", ref cases);
            CheckFurtherPure(historicalProfile.HighFactor, 1.35f,
                "HISTORIC_CONFIG_HIGH_FACTOR135", ref cases);
            CheckFurtherPure(IndependentFurtherCurve(1.35f, historicalProfile), 1.35f,
                "HISTORIC_CONS_EXPLICIT_135_ANCHOR", ref cases);
            var simulated = new FurtherProof { Ready = true, ExpectedFactor = .26f };
            CheckFurtherText(PureFurtherComparison(simulated, true, true, .26f, false),
                "PASS_FURTHER_FACTOR", "MATCH", ref cases);
            CheckFurtherText(PureFurtherComparison(simulated, true, true, .35f, false),
                "REVIEW_FURTHER_FACTOR_MISMATCH", "MISMATCH", ref cases);
            var newOrder = new FurtherProof { Ready = true, ExpectedFactor = .3211f };
            CheckFurtherText(PureFurtherComparison(newOrder, true, true, .307f, false),
                "REVIEW_FURTHER_FACTOR_MISMATCH", "OLD_FORMULA_MUST_NOT_PASS", ref cases);
            CheckFurtherText(PureFurtherComparison(simulated, true, true, .26f, true),
                "INCONCLUSIVE_FOREIGN_LEARN_RATE_OR_FURTHER_HOOK", "FOREIGN", ref cases);
            CheckFurtherText(PureFurtherComparison(simulated, false, true, .26f, false),
                "NOT_VERIFIED", "NOT_SATURATED", ref cases);
            CheckFurtherText(PureFurtherComparison(simulated, true, false, .26f, false),
                "INCONCLUSIVE_PRODUCTION_READ", "NO_READ", ref cases);
            CheckFurtherText(PureFurtherComparison(null, true, true, .26f, false),
                "NOT_VERIFIED", "NO_ORACLE", ref cases);
            Log.Message("[XPC-4CC][POSTCAP_FURTHER_AUDIT] status=PASS; cases=" + cases +
                "; syntheticOnly=True; oracle=RAW_BASE_PLUS_MTH_PP_TIMES_CONS_V2_R5F1" +
                "; productionFurtherCalculatorCalls=0; pawnWrites=0; settingsWrites=0" +
                "; revision=" + Revision + "; build=" + XpCustomizerMod.BuildId);
            Phase4CRegressionLedger.Record("4C-C-FURTHER-PURE", "PASS",
                "cases=" + cases + ",syntheticOnly=True");
        }
    }
}
#endif
