#if DEBUG
using System;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// R5-B diagnostic-only daily cap oracle. Composes cap from raw settings,
    /// active pawn sources and an independent V2 Consciousness curve evaluator.
    /// Never calls the production daily-cap, source, or consciousness services
    /// while constructing its expectation; those are read only for comparison.
    /// Vanilla/foreign pass-through never asserts a saturation PASS.
    /// </summary>
    internal static class Phase4CCIndependentDailyCapOracle
    {
        internal const string Schema = "4C-C-DAILYCAP-ORACLE-R5B1-20261106";

        internal sealed class Result
        {
            internal bool Ready;
            internal float Cap;
            internal float SourcePp;
            internal float Consciousness;
            internal float CapacityFactor;
            internal bool? PredictedSaturated;
            internal string ThresholdPath;
            internal string CapComparison;
            internal string SaturationComparison;
            internal string Error;
        }

        private static bool Finite(float x) { return !float.IsNaN(x) && !float.IsInfinity(x); }
        private static float Clip(float x, float lo, float hi) { return Mathf.Clamp(x, lo, hi); }
        private static bool Near(float x, float y) { return Math.Abs(x - y) < 0.01f; }

        // This path intentionally does not depend on
        // LearningCapacityService.GetDailyCap/GetBreakdown/GetV4SourcePercentagePoints,
        // ConsciousnessCapacityService.GetFactor, or source-def helper methods.
        internal static Result Compose(SkillRecord record, XpCustomizerSettings settings)
        {
            var r = new Result { Ready = false, Error = "MISSING_INPUT" };
            try
            {
                if (record == null || record.Pawn == null || record.def == null)
                    return r;
                if (settings == null)
                {
                    r.Cap = 4000f; r.Consciousness = 1f;
                    r.CapacityFactor = 1f; r.ThresholdPath = "VANILLA_PASSTHROUGH";
                    r.Ready = true; r.Error = "NONE";
                    return r;
                }
                Pawn pawn = record.Pawn;
                float baseCap = Mathf.Round(Clip(settings.learningCapacityBaseDailyCap, 0f, 9999999f));
                if (!Finite(baseCap)) { r.Error = "INVALID_BASE_CAP"; return r; }
                float pp = 0f;
                if (settings.learningCapacitySourceModifiersEnabled)
                    pp = CalculateActiveSourcePp(pawn, record.def, settings);
                r.SourcePp = pp;
                r.Consciousness = pawn.health != null && pawn.health.capacities != null
                    ? pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) : 1f;
                float cons = !settings.consciousnessAffectsLearning ||
                    !settings.consciousnessAffectsDailyCapacity || pawn.health == null ||
                    pawn.health.capacities == null ? 1f :
                    Phase4CCLiveXpCapture.IndependentCons(
                        r.Consciousness, settings.consciousnessAnchorCapacity);
                r.CapacityFactor = cons;
                if (!Finite(pp) || !Finite(cons)) { r.Error = "INVALID_SOURCE_OR_CONS"; return r; }
                // Match the two distinct Mathf.Round stages in gameplay.
                float afterSources = Mathf.Round(Mathf.Max(0f, baseCap * (1f + pp / 100f)));
                r.Cap = Mathf.Round(Mathf.Max(0f, afterSources * cons));
                if (!Finite(r.Cap)) { r.Error = "INVALID_COMPOSED_CAP"; return r; }
                r.ThresholdPath = r.Cap <= 0f ? "ZERO_ALWAYS_SATURATED" :
                    Mathf.Approximately(r.Cap, 4000f) ? "VANILLA_PASSTHROUGH" :
                    "DYNAMIC_STRICT_GREATER";
                r.Ready = true;
                r.Error = "NONE";
            }
            catch (Exception e)
            {
                r.Ready = false;
                r.Error = "ORACLE_EXCEPTION_" + e.GetType().Name;
            }
            return r;
        }

        /// <summary>
        /// Expected state is null whenever vanilla/foreign result must be
        /// preserved. A cap of zero is always saturated only when our
        /// non-neutral Postfix is active; Compose flags the zero-cap path.
        /// </summary>
        internal static bool? PredictedState(float cap, float daily, bool foreign)
        {
            if (foreign || !Finite(cap) || !Finite(daily)) return null;
            if (cap <= 0f) return true;
            if (Mathf.Approximately(cap, 4000f)) return null;
            return daily > cap;
        }

        internal static void Compare(Result r, float daily, float productionCap,
            bool engineSaturated, bool foreignSaturationPatch)
        {
            if (!r.Ready)
            {
                r.CapComparison = "INCONCLUSIVE_ORACLE_INPUT";
                r.SaturationComparison = "INCONCLUSIVE_ORACLE_INPUT";
                return;
            }
            r.CapComparison = Finite(productionCap) && Near(r.Cap, productionCap)
                ? "PASS_CAP" : "REVIEW_CAP_MISMATCH";
            r.PredictedSaturated = PredictedState(r.Cap, daily, foreignSaturationPatch);
            r.SaturationComparison = !r.PredictedSaturated.HasValue ?
                (foreignSaturationPatch ? "INCONCLUSIVE_FOREIGN_PATCH" :
                 r.ThresholdPath == "VANILLA_PASSTHROUGH" ?
                 "INCONCLUSIVE_VANILLA_PASSTHROUGH" : "INCONCLUSIVE_INVALID_INPUT") :
                r.PredictedSaturated.Value == engineSaturated ?
                "PASS_THRESHOLD" : "REVIEW_THRESHOLD_MISMATCH";
        }

        private static float ManualOrAuto(bool manual, float manualPp, float lfPp,
            float strength, float otherFactor)
        {
            float raw = manual ? manualPp : lfPp * otherFactor * .20f * strength;
            return Clip(raw, -100f, 100f);
        }

        private static float CalculateActiveSourcePp(Pawn pawn, SkillDef skill,
            XpCustomizerSettings s)
        {
            float strength = Finite(s.learningCapacitySourceStrength) ?
                Clip(s.learningCapacitySourceStrength, 0f, 2f) : 1f;
            bool slowStudy = ActiveGene(pawn, "Learning_Slow");
            float other = slowStudy && !s.learningCapacityOverrideSlowStudy ? .5f : 1f;
            float total = 0f;
            if (ActiveTrait(pawn, "SlowLearner")) total += ManualOrAuto(
                s.learningCapacityOverrideSlowLearner, s.learningCapacityCustomSlowLearner, -75f, strength, other);
            if (ActiveTrait(pawn, "FastLearner")) total += ManualOrAuto(
                s.learningCapacityOverrideFastLearner, s.learningCapacityCustomFastLearner, 75f, strength, other);
            if (ActiveTrait(pawn, "TooSmart")) total += ManualOrAuto(
                s.learningCapacityOverrideTooSmart, s.learningCapacityCustomTooSmart, 75f, strength, other);
            if (ActiveHediff(pawn, "BlissLobotomy")) total += ManualOrAuto(
                s.learningCapacityOverrideBlissLobotomy, s.learningCapacityCustomBlissLobotomy, -50f, strength, other);
            if (slowStudy) total += ManualOrAuto(
                s.learningCapacityOverrideSlowStudy, s.learningCapacityCustomSlowStudy, -50f, strength, 1f);
            if (ActiveGene(pawn, "Learning_Fast")) total += ManualOrAuto(
                s.learningCapacityOverrideQuickStudy, s.learningCapacityCustomQuickStudy, 50f, strength, other);
            if (ActiveHediff(pawn, "NeuralSupercharge")) total += ManualOrAuto(
                s.learningCapacityOverrideNeuralSupercharge, s.learningCapacityCustomNeuralSupercharge, 25f, strength, other);
            if (skill == SkillDefOf.Animals && ActivePrecept(pawn, "Ranching_Central"))
                total += ManualOrAuto(s.learningCapacityOverrideRanchingCentral,
                    s.learningCapacityCustomRanchingCentral, 100f, strength, 1f);
            if (ActiveHediff(pawn, "LearningAssistant")) total += ManualOrAuto(
                s.learningCapacityOverrideLearningAssistant, s.learningCapacityCustomLearningAssistant, 20f, strength, other);
            return total;
        }

        private static bool ActiveTrait(Pawn pawn, string id)
        {
            if (pawn.story == null || pawn.story.traits == null) return false;
            foreach (Trait t in pawn.story.traits.allTraits)
                if (t != null && !t.Suppressed && t.def != null && t.def.defName == id) return true;
            return false;
        }
        private static bool ActiveGene(Pawn pawn, string id)
        {
            if (!ModsConfig.BiotechActive || pawn.genes == null) return false;
            foreach (Gene g in pawn.genes.GenesListForReading)
                if (g != null && g.Active && g.def != null && g.def.defName == id) return true;
            return false;
        }
        private static bool ActiveHediff(Pawn pawn, string id)
        {
            if (pawn.health == null || pawn.health.hediffSet == null) return false;
            foreach (Hediff h in pawn.health.hediffSet.hediffs)
                if (h != null && h.CurStage != null && h.def != null && h.def.defName == id) return true;
            return false;
        }
        private static bool ActivePrecept(Pawn pawn, string id)
        {
            if (!ModsConfig.IdeologyActive || pawn.Ideo == null) return false;
            foreach (Precept p in pawn.Ideo.PreceptsListForReading)
                if (p != null && p.def != null && p.def.defName == id) return true;
            return false;
        }

        private static int auditRuns;
        internal static void RunSyntheticAudit()
        {
            int checks = 0;
            try
            {
                // Boundary and rounding checks are on synthetic scalar inputs.
                Assert(PredictedState(0f, 0f, false) == true, "ZERO", ref checks);
                Assert(PredictedState(5000f, 5000f, false) == false, "EQUAL_NOT_SAT", ref checks);
                Assert(PredictedState(5000f, 5000.1f, false) == true, "OVER_CAP", ref checks);
                Assert(PredictedState(5000f, 4999.9f, false) == false, "UNDER_CAP", ref checks);
                Assert(!PredictedState(4000f, 9000f, false).HasValue, "VANILLA_PASSTHROUGH", ref checks);
                Assert(!PredictedState(5000f, 9000f, true).HasValue, "FOREIGN_PASSTHROUGH", ref checks);
                Assert(!PredictedState(5000f, float.NaN, false).HasValue, "INVALID_DAILY", ref checks);
                Assert(Near(ManualOrAuto(false, 90f, 75f, 1f, 1f), 15f), "FAST_AUTO", ref checks);
                Assert(Near(ManualOrAuto(false, 90f, 75f, 1f, .5f), 7.5f), "SLOW_STUDY_PROJECT", ref checks);
                Assert(Near(ManualOrAuto(true, -25f, 75f, 2f, .5f), -25f), "MANUAL_IS_FINAL", ref checks);
                Assert(Near(ManualOrAuto(false, 0f, -75f, 2f, 1f), -30f), "DOUBLE_STRENGTH", ref checks);
                Assert(Near(ManualOrAuto(true, 500f, 75f, 0f, 1f), 100f), "MANUAL_CLAMP", ref checks);
                Assert(Near(PureCap(4000f, 0f, 1f), 4000f), "CAP_DEFAULT", ref checks);
                Assert(Near(PureCap(4000f, 15f, 1f), 4600f), "CAP_FAST", ref checks);
                Assert(Near(PureCap(4000f, -15f, 1f), 3400f), "CAP_SLOW", ref checks);
                Assert(Near(PureCap(0f, 40f, 1f), 0f), "CAP_ZERO", ref checks);
                Assert(Near(PureCap(4000f, 0f, 1.1f), 4400f), "CAP_CONS_HIGH", ref checks);
                Assert(Near(PureCap(4000f, 0f, .5f), 2000f), "CAP_CONS_LOW", ref checks);
                Assert(Near(PureCap(1000f, -100f, 1.5f), 0f), "CAP_SOURCE_ZERO", ref checks);
                Assert(Near(PureCap(4000f, 15f, 0f), 0f), "CAP_CONS_ZERO", ref checks);
                // 101*1.005 rounds to 102, then x1.5 =>153;
                // one final round of 101*1.005*1.5 would yield 152.
                Assert(Near(PureCap(101f, .5f, 1.5f), 153f), "TWO_STAGE_ROUND", ref checks);
                Log.Message("[XPC-4CC][DAILYCAP_ORACLE_AUDIT] status=PASS; cases=" + checks +
                    "; syntheticOnly=True; productionCapCalled=False; pawnWrites=0; settingsWrites=0" +
                    "; schema=" + Schema + "; build=" + XpCustomizerMod.BuildId);
                Phase4CRegressionLedger.Record("4C-C-DAILYCAP-PURE", "PASS", "cases=" + checks + ",syntheticOnly=True");
            }
            catch (Exception ex)
            {
                Phase4CRegressionLedger.Record("4C-C-DAILYCAP-PURE", "REVIEW", "FAILED_" + ex.GetType().Name + "_" + ex.Message);
                Log.Error("[XPC-4CC][DAILYCAP_ORACLE_AUDIT] status=REVIEW; case=" + checks + "; exception=" + ex);
            }
            auditRuns++;
        }

        private static float PureCap(float baseCap, float sourcePp, float cons)
        {
            float first = Mathf.Round(Mathf.Max(0f, baseCap * (1f + sourcePp / 100f)));
            return Mathf.Round(Mathf.Max(0f, first * cons));
        }
        private static void Assert(bool yes, string name, ref int checks)
        {
            checks++;
            if (!yes) throw new InvalidOperationException("R5B_" + name);
        }
    }
}
#endif
