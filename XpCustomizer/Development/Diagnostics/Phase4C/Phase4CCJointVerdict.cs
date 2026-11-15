#if DEBUG
using System;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// T02 same-event, read-only joint evidence. Both classifiers MUST evaluate
    /// the SAME retained Sample. No gameplay calculations or Harmony ownership
    /// live in this file. Reserved post-cap samples are counted elsewhere.
    /// </summary>
    internal static partial class Phase4CCLiveXpCapture
    {
        private const string JointRevision = "4C-C-JOINT-VERDICT-T02-R1";

        // Pure outcome aggregation. A PASS on different events must never be
        // combined into a PASS. Non-PASS categories are deliberately conservative.
        private static string JointClassFromResults(string payout, string rate)
        {
            if (payout == "PASS_PAYOUT" && rate == "PASS_INDEPENDENT_RATE")
                return "JOINT_PASS";
            if (payout == "NOT_TARGET" || rate == "NOT_TARGET" ||
                IsJointSkip(payout) || IsJointSkip(rate))
                return "JOINT_SKIP";
            if (IsJointInconclusive(payout) || IsJointInconclusive(rate))
                return "JOINT_INCONCLUSIVE";
            if (IsJointReview(payout) || IsJointReview(rate))
                return "JOINT_REVIEW";
            // Unknown/new classification strings must never silently PASS.
            return "JOINT_INCONCLUSIVE";
        }

        private static bool IsJointSkip(string s)
        {
            return s != null && s.StartsWith("SKIP_", StringComparison.Ordinal);
        }
        private static bool IsJointReview(string s)
        {
            return s != null && s.StartsWith("REVIEW_", StringComparison.Ordinal);
        }
        private static bool IsJointInconclusive(string s)
        {
            return s != null && s.StartsWith("INCONCLUSIVE_", StringComparison.Ordinal);
        }
        private static string JointClass(Sample sample)
        {
            // Never take a PayoutClass from one Sample and a RateClass from another.
            return JointClassFromResults(PayoutClass(sample), IndependentRateClass(sample));
        }

        private static string JointCoverageStatus(int events, int passes, int reviews,
            int inconclusive)
        {
            if (reviews > 0) return "REVIEW";
            if (events == 0) return "NO_DATA";
            if (passes > 0 && passes == events) return "PASS_ALL_CAPTURED_ELIGIBLE";
            if (passes > 0) return "PARTIAL_JOINT_PASS";
            if (inconclusive > 0) return "INCONCLUSIVE_NO_JOINT_PASS";
            return "SKIP_ONLY_NOT_VERIFIED";
        }

        private static void AppendJointExamples(StringBuilder sb, List<Sample> candidates)
        {
            const int MaxRows = 16;
            int shown = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            // First show actual mismatches, then diverse same-event successes,
            // then examples that remain outside the validated source scope.
            for (int pass = 0; pass < 3 && shown < MaxRows; pass++)
            {
                foreach (Sample sample in candidates)
                {
                    string joint = JointClass(sample);
                    if (pass == 0 && joint != "JOINT_REVIEW") continue;
                    if (pass == 1 && joint != "JOINT_PASS") continue;
                    if (pass == 2 && (joint == "JOINT_PASS" || joint == "JOINT_REVIEW")) continue;
                    StartState b = sample.Before;
                    string key = b.PawnName + "|" + b.Job + "|" + b.Skill + "|" + joint;
                    if (joint != "JOINT_REVIEW" && !seen.Add(key)) continue;
                    sb.Append("\n[XPC-4CC][JOINT_SAMPLE] sampleIndex=")
                        .Append(samples.IndexOf(sample) + 1)
                        .Append("; pawn=").Append(b.PawnName)
                        .Append("; job=").Append(b.Job)
                        .Append("; category=").Append(b.Category)
                        .Append("; skill=").Append(b.Skill)
                        .Append("; inputXp=").Append(F(b.Input))
                        .Append("; expectedRate=").Append(b.OracleReady ? F(b.OracleRate) : "NA")
                        .Append("; observedRate=").Append(b.RateCaptured ? F(b.RateBefore) : "NA")
                        .Append("; payoutExpectedXp=").Append(b.RateCaptured ? F(b.Input * b.RateBefore) : "NA")
                        .Append("; actualXp=").Append(sample.Comparable ? F(sample.DeltaXp) : "LEVEL_TRANSITION")
                        .Append("; payoutClass=").Append(PayoutClass(sample))
                        .Append("; independentRateClass=").Append(IndependentRateClass(sample))
                        .Append("; jointClass=").Append(joint)
                        .Append("; attribution=").Append(OfficialBillEvidenceGrade(sample));
                    shown++;
                    if (shown >= MaxRows) break;
                }
            }
            if (candidates.Count > shown)
                sb.Append("\n[XPC-4CC][JOINT_DETAIL] representativeRows=").Append(shown)
                    .Append("; eligibleInputCandidateSamples=").Append(candidates.Count)
                    .Append("; truncated=True; regularSamplesOnly=True");
        }

        // Synthetic-only: no SkillRecord.Learn calls, settings mutations or
        // changes to the gameplay rate. Exercises classifiers, not just labels.
        private static void VerifyJointVerdictSelfAudit()
        {
            int cases = 0;
            var b = new StartState
            {
                Category = "WORK_CANDIDATE", Job = "ConstructFinishFrame",
                WorkKind = "CONSTRUCTION_CANDIDATE", Skill = "Construction",
                Input = 10f, RateCaptured = true, RateBefore = 0.7f,
                OracleReady = true, OracleRate = 0.7f
            };
            var s = new Sample { Before = b, Comparable = true, DeltaXp = 7f, DeltaDaily = 7f };
            RequireJoint(s, "JOINT_PASS", "WORK_SAME_EVENT", ref cases);

            // Two distinct events cannot be cross-joined even if one provides
            // Payout PASS and the other provides Independent Rate PASS.
            b.OracleRate = 0.9f;
            RequireJoint(s, "JOINT_REVIEW", "PAYOUT_ONLY_EVENT", ref cases);
            b.OracleRate = 0.7f;
            s.DeltaXp = 6f;
            RequireJoint(s, "JOINT_REVIEW", "RATE_ONLY_EVENT", ref cases);
            s.DeltaXp = 7f;
            RequireJoint(s, "JOINT_PASS", "RESTORED_BOTH", ref cases);

            b.Job = "DoBill"; b.WorkKind = "OTHER_WORK_CANDIDATE"; b.Skill = "Cooking";
            RequireJoint(s, "JOINT_SKIP", "UNVERIFIED_JOB_SKILL", ref cases);
            b.Category = "READING"; b.Skill = "Intellectual"; b.ReadingScope = true;
            RequireJoint(s, "JOINT_PASS", "READING_SAME_EVENT", ref cases);
            b.ReadingScope = false;
            RequireJoint(s, "JOINT_SKIP", "READING_SCOPE", ref cases);
            b.ReadingScope = true;
            s.Comparable = false;
            RequireJoint(s, "JOINT_INCONCLUSIVE", "LEVEL_TRANSITION", ref cases);
            s.Comparable = true;
            b.ForeignLearnHookAtStart = true;
            RequireJoint(s, "JOINT_INCONCLUSIVE", "FOREIGN_LEARN", ref cases);
            b.ForeignLearnHookAtStart = false;
            b.OracleForeignRateHook = true;
            RequireJoint(s, "JOINT_INCONCLUSIVE", "FOREIGN_RATE", ref cases);
            b.OracleForeignRateHook = false;
            b.Direct = true;
            RequireJoint(s, "JOINT_SKIP", "DIRECT", ref cases);
            b.Direct = false;
            b.IgnoreLearnRate = true;
            RequireJoint(s, "JOINT_SKIP", "IGNORE", ref cases);
            b.IgnoreLearnRate = false;
            b.TotallyDisabled = true; s.AfterDisabled = true;
            RequireJoint(s, "JOINT_SKIP", "DISABLED", ref cases);
            b.TotallyDisabled = false; s.AfterDisabled = false;
            b.Input = 0f;
            RequireJoint(s, "JOINT_SKIP", "ZERO_INPUT", ref cases);
            b.Input = 10f;
            // A future unknown status is never accepted as PASS.
            RequireJointPair("PASS_PAYOUT", "UNKNOWN_RATE", "JOINT_INCONCLUSIVE", "UNKNOWN", ref cases);
            RequireJointPair("PASS_PAYOUT", "PASS_INDEPENDENT_RATE", "JOINT_PASS", "PURE_PAIR", ref cases);
            var firstEvent = new Sample
            {
                Before = new StartState {
                    Category = "WORK_CANDIDATE", Job = "ConstructFinishFrame",
                    WorkKind = "CONSTRUCTION_CANDIDATE", Skill = "Construction",
                    Input = 10f, RateCaptured = true, RateBefore = .7f,
                    OracleReady = true, OracleRate = .9f
                }, Comparable = true, DeltaXp = 7f, DeltaDaily = 7f
            };
            var secondEvent = new Sample
            {
                Before = new StartState {
                    Category = "WORK_CANDIDATE", Job = "ConstructFinishFrame",
                    WorkKind = "CONSTRUCTION_CANDIDATE", Skill = "Construction",
                    Input = 10f, RateCaptured = true, RateBefore = .7f,
                    OracleReady = true, OracleRate = .7f
                }, Comparable = true, DeltaXp = 6f, DeltaDaily = 7f
            };
            RequireJoint(firstEvent, "JOINT_REVIEW", "DISTINCT_PAYOUT_ONLY", ref cases);
            RequireJoint(secondEvent, "JOINT_REVIEW", "DISTINCT_RATE_ONLY", ref cases);
            if (Object.ReferenceEquals(firstEvent, secondEvent) ||
                PayoutClass(firstEvent) != "PASS_PAYOUT" ||
                IndependentRateClass(secondEvent) != "PASS_INDEPENDENT_RATE" ||
                JointClass(firstEvent) == "JOINT_PASS" || JointClass(secondEvent) == "JOINT_PASS")
                throw new InvalidOperationException("4C-C T02 cross-event PASS promotion");
            cases++;
            RequireJointCoverage(0, 0, 0, 0, "NO_DATA", "NO_DATA", ref cases);
            RequireJointCoverage(3, 1, 0, 2, "PARTIAL_JOINT_PASS", "PARTIAL", ref cases);
            RequireJointCoverage(1, 0, 1, 0, "REVIEW", "REVIEW", ref cases);
            RequireJointCoverage(2, 0, 0, 0, "SKIP_ONLY_NOT_VERIFIED", "SKIPS", ref cases);
            Log.Message("[XPC-4CC][JOINT_VERDICT_AUDIT] status=PASS; cases=" + cases +
                "; schema=" + JointRevision + "; sameSampleRequired=True" +
                "; crossSamplePassBlocked=True; sourceUnverifiedNotPromoted=True" +
                "; reservedPostCapDoubleCountBlocked=REGULAR_ONLY" +
                "; syntheticOnly=True; xpWrites=0; build=" + XpCustomizerMod.BuildId);
        }

        private static void RequireJoint(Sample sample, string expected, string scenario, ref int cases)
        {
            string actual = JointClass(sample);
            if (actual != expected)
                throw new InvalidOperationException("4C-C T02 " + scenario +
                    " expected=" + expected + " actual=" + actual);
            cases++;
        }
        private static void RequireJointPair(string payout, string rate, string expected,
            string scenario, ref int cases)
        {
            string actual = JointClassFromResults(payout, rate);
            if (actual != expected)
                throw new InvalidOperationException("4C-C T02 " + scenario +
                    " expected=" + expected + " actual=" + actual);
            cases++;
        }
        private static void RequireJointCoverage(int events, int passes, int reviews,
            int inconclusive, string expected, string scenario, ref int cases)
        {
            string actual = JointCoverageStatus(events, passes, reviews, inconclusive);
            if (actual != expected)
                throw new InvalidOperationException("4C-C T02 " + scenario +
                    " expected=" + expected + " actual=" + actual);
            cases++;
        }
    }
}
#endif
