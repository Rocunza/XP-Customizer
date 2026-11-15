#if DEBUG
using System;
using RimWorld;
using Verse;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.FurtherLearning;
using XpCustomizer.Gws;
using XpCustomizer.LearningCapacity;

namespace XpCustomizer
{
    /// <summary>
    /// R3 passive real-pawn Learn payout audit. The EXPECTED XP is derived from
    /// the pre-call LearnRateFactor snapshot, NOT from calling Learn a second time.
    /// This tests final rate -> XP/daily payout, not the independent correctness
    /// of RimWorld's native rate (passion, stats) or all Harmony rate postfixes.
    /// Separate 4C-B and RND1 audits test their selected rate/curve components.
    /// </summary>
    internal static partial class Phase4CCLiveXpCapture
    {
        private static void SnapshotPayoutContext(StartState b)
        {
            b.ForeignLearnHookAtStart = learnForeignHookPresent;
            b.FastLearningBefore = DebugSettings.fastLearning;
            b.RateCaptureReason = "NOT_ELIGIBLE";
            if (b.Direct || b.IgnoreLearnRate || b.TotallyDisabled || b.FastLearningBefore)
                return;
            try
            {
                // The observer reads, but does not change, any gameplay state.
                // Pre-Learn rate preserves the original saturation threshold.
                b.SaturatedBefore = b.Record.LearningSaturatedToday;
                b.RateBefore = b.Record.LearnRateFactor(false);
                b.DailyCapBefore = LearningCapacityService.GetDailyCap(b.Record);
                b.ConsciousnessFactorBefore = ConsciousnessLearningService.GetFactor(b.Pawn);
                b.FurtherFactorBefore = b.SaturatedBefore
                    ? FurtherLearningService.GetFactor(b.Record) : FurtherLearningService.VanillaFactor;
                b.GwsFactorBefore = GwsLearningService.GetFactor(b.Record);
                b.RateCaptured = IsFinite(b.RateBefore) && b.RateBefore >= 0f;
                b.RateCaptureReason = b.RateCaptured ? "OK" : "INVALID_RATE";
            }
            catch (Exception ex)
            {
                // A failed diagnostic read must not suppress the real Learn call.
                b.RateCaptured = false;
                b.RateCaptureReason = "READ_EXCEPTION_" + Clean(ex.GetType().Name);
            }
        }

        private static bool IsFinite(float x)
        {
            return !float.IsNaN(x) && !float.IsInfinity(x);
        }

        private static float PayoutTolerance(float existing, float expected)
        {
            // Absolute floor accommodates small float operations. The current
            // accumulator's ULP makes high-level XP less precise; very tiny
            // expected gains are marked inconclusive instead of falsely PASS.
            return Math.Max(0.005f, Math.Abs(expected) * 0.005f +
                Math.Abs(existing) * 2.3841858e-7f);
        }

        // B1-B1: STOP-sealed same-Sample Bill admission; all other paths unchanged.
        private static string PayoutClass(Sample s)
        {
            return PayoutClassCore(s, CanApproveBillForOfficial(s));
        }

        private static string PayoutClassCore(Sample s, bool approvedBill)
        {
            StartState b = s.Before;
            bool work = b.Category == "WORK_CANDIDATE";
            bool reading = b.Category == "READING";
            if (!work && !reading) return "NOT_TARGET";
            if (!IsFinite(b.Input) || b.Input <= 0f) return "SKIP_NONPOSITIVE_INPUT";
            if (work && !ExpectedWorkSkill(b) &&
                !(approvedBill && InBillAuthorizationScope(b))) return "SKIP_JOB_SKILL_UNVERIFIED";
            if (reading && !b.ReadingScope) return "SKIP_READING_SCOPE_UNCERTAIN";
            if (b.Direct) return "SKIP_DIRECT_SOURCE";
            if (b.IgnoreLearnRate) return "SKIP_IGNORE_LEARN_RATE";
            if (b.FastLearningBefore) return "SKIP_DEBUG_FAST_LEARNING";
            if (b.TotallyDisabled != s.AfterDisabled) return "INCONCLUSIVE_DISABLED_STATE_CHANGED";
            if (b.TotallyDisabled) return "SKIP_DISABLED_SKILL";
            if (!s.Comparable) return "INCONCLUSIVE_LEVEL_TRANSITION";
            if (b.ForeignLearnHookAtStart) return "INCONCLUSIVE_FOREIGN_LEARN_HOOK";
            if (!b.RateCaptured) return "INCONCLUSIVE_RATE_CAPTURE";
            if (!IsFinite(b.Xp) || !IsFinite(b.Daily))
                return "INCONCLUSIVE_NONFINITE_BASE";
            if (!IsFinite(s.DeltaXp) || !IsFinite(s.DeltaDaily))
                return "REVIEW_NONFINITE_DELTA";
            float expected = b.Input * b.RateBefore;
            if (!IsFinite(expected) || expected < 0f) return "INCONCLUSIVE_EXPECTED_NONFINITE";
            if (expected == 0f) return "INCONCLUSIVE_ZERO_EXPECTED_RATE";
            if (expected <= PayoutTolerance(b.Xp, expected) * 2f && b.Xp > 32768f)
                return "INCONCLUSIVE_FLOAT_PRECISION";
            if (Math.Abs(s.DeltaXp - expected) > PayoutTolerance(b.Xp, expected))
                return "REVIEW_XP_PAYOUT_MISMATCH";
            if (Math.Abs(s.DeltaDaily - expected) > PayoutTolerance(b.Daily, expected))
                return "REVIEW_DAILY_PAYOUT_MISMATCH";
            return "PASS_PAYOUT";
        }

        // Pure classifier audit; no gameplay objects, Learn calls or settings edits.
        private static void VerifyLivePayoutClassifierSelfAudit()
        {
            var b = new StartState {
                Category = "WORK_CANDIDATE", WorkKind = "CONSTRUCTION_CANDIDATE",
                Skill = "Construction", Input = 10f, RateBefore = 0.7f,
                RateCaptured = true, ReadingScope = false
            };
            var s = new Sample { Before = b, Comparable = true, DeltaXp = 7f, DeltaDaily = 7f };
            RequirePayout(s, "PASS_PAYOUT", "WORK");
            b.Category = "READING"; b.Skill = "Intellectual"; b.ReadingScope = true;
            RequirePayout(s, "PASS_PAYOUT", "READING");
            b.TotallyDisabled = true; s.AfterDisabled = true;
            RequirePayout(s, "SKIP_DISABLED_SKILL", "CHILD");
            b.TotallyDisabled = false; s.AfterDisabled = false;
            b.ReadingScope = false;
            RequirePayout(s, "SKIP_READING_SCOPE_UNCERTAIN", "READING_SCOPE");
            b.Category = "WORK_CANDIDATE"; b.Skill = "Social";
            RequirePayout(s, "SKIP_JOB_SKILL_UNVERIFIED", "WORK_SKILL");
            b.Skill = "Construction"; s.Comparable = false;
            RequirePayout(s, "INCONCLUSIVE_LEVEL_TRANSITION", "LEVEL");
            s.Comparable = true; b.ForeignLearnHookAtStart = true;
            RequirePayout(s, "INCONCLUSIVE_FOREIGN_LEARN_HOOK", "FOREIGN");
            b.ForeignLearnHookAtStart = false; s.DeltaXp = 6f;
            RequirePayout(s, "REVIEW_XP_PAYOUT_MISMATCH", "BAD_XP");
            s.DeltaXp = 7f; s.DeltaDaily = 6f;
            RequirePayout(s, "REVIEW_DAILY_PAYOUT_MISMATCH", "BAD_DAILY");
            s.DeltaDaily = 7f; b.Direct = true;
            RequirePayout(s, "SKIP_DIRECT_SOURCE", "DIRECT");
            b.Direct = false; b.IgnoreLearnRate = true;
            RequirePayout(s, "SKIP_IGNORE_LEARN_RATE", "IGNORE");
            b.IgnoreLearnRate = false; b.FastLearningBefore = true;
            RequirePayout(s, "SKIP_DEBUG_FAST_LEARNING", "FAST");
            b.FastLearningBefore = false; s.DeltaDaily = float.NaN;
            RequirePayout(s, "REVIEW_NONFINITE_DELTA", "NONFINITE");
            Log.Message("[XPC-4CC][PAYOUT_CLASSIFIER_AUDIT] status=PASS; cases=13" +
                "; noRealPawnLearnCalls=True; noSettingsWrites=True; revision=" + Revision);
        }

        private static void RequirePayout(Sample s, string expected, string scenario)
        {
            string actual = PayoutClass(s);
            if (actual != expected)
                throw new InvalidOperationException("4C-C R3 " + scenario +
                    " expected=" + expected + " got=" + actual);
        }
    }
}
#endif
