#if DEBUG
using System;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// T04-B1-B0: Stop-time, read-only, one-decision-per-sample BILL math preview.
    /// Absolutely no official JOINT/Payout/IndependentRate promotion and no
    /// gameplay Harmony registration or mutation. Fail-closed on patch drift.
    /// </summary>
    internal static partial class Phase4CCLiveXpCapture
    {
        private const string BillMathRevision = "4C-C-B1B1-OFFICIAL-CROSSCHECK-R1-20261115";
        private const int MaxBillMathExamples = 12;
        private static readonly Dictionary<Sample, SourceAuthorizationEvidence> billMathAdmissions =
            new Dictionary<Sample, SourceAuthorizationEvidence>();
        private static bool billMathPreviewFinalized;
        private static bool billMathPreviewInvalid;
        private static string billMathStopPatchAudit = "NOT_RUN";
        private static int billMathPolicyErrors;
        private static int billMathExpectedEntries;

        private static void ResetBillMathPreviewSession()
        {
            ResetOfficialBillSession(); // One session, one official seal; no stale admission.
            billMathAdmissions.Clear();
            billMathPreviewFinalized = false;
            billMathPreviewInvalid = false;
            billMathStopPatchAudit = "NOT_RUN";
            billMathPolicyErrors = 0;
            billMathExpectedEntries = 0;
        }

        // Only once, after RemoveHooks and before BuildReport's very first
        // classifier run. Both virtual oracles receive the SAME cached decision.
        private static void FinalizeBillMathPreviewSession()
        {
            ResetBillMathPreviewSession();
            try
            {
                billMathStopPatchAudit = CheckSourceAuthorizationEndPatches();
                bool stable = string.Equals(billMathStopPatchAudit, "PASS", StringComparison.Ordinal);
                // Failure is visible and blocks every Bill admission, even when
                // the original B1-A shadow classifier had yielded a candidate.
                if (!stable) billMathPreviewInvalid = true;
                foreach (Sample sample in samples)
                {
                    if (sample == null || !InBillAuthorizationScope(sample.Before)) continue;
                    billMathExpectedEntries++;
                    SourceAuthorizationEvidence result;
                    try
                    {
                        result = AuthorizeBillSourceShadow(sample.Before, sample.Before.SourceProof);
                        if (result.Admissible && !stable)
                            result = SourceAuth("INSUFFICIENT_SESSION_PATCH_DRIFT", billMathStopPatchAudit);
                    }
                    catch (Exception ex)
                    {
                        billMathPolicyErrors++;
                        result = SourceAuth("INSUFFICIENT_POLICY_ERROR", ex.GetType().Name);
                    }
                    billMathAdmissions.Add(sample, result); // Duplicate identity => global fail-closed.
                }
                if (billMathPolicyErrors != 0) billMathPreviewInvalid = true;
                if (billMathAdmissions.Count != billMathExpectedEntries)
                    billMathPreviewInvalid = true;
            }
            catch (Exception ex)
            {
                billMathPreviewInvalid = true;
                billMathPolicyErrors++;
                billMathStopPatchAudit = "FINALIZE_ERROR_" + ex.GetType().Name;
            }
            if (billMathPreviewInvalid)
            {
                // No previously admitted sample may survive a partial finalize.
                var keys = new List<Sample>(billMathAdmissions.Keys);
                foreach (Sample sample in keys)
                    billMathAdmissions[sample] = SourceAuth("INSUFFICIENT_SESSION_AUDIT_ERROR",
                        billMathStopPatchAudit);
            }
            billMathPreviewFinalized = true;
        }

        // Read-only cached outcome. Never recalculates Harmony/pawn context.
        private static SourceAuthorizationEvidence FinalizedBillAuthorization(Sample sample)
        {
            if (!billMathPreviewFinalized || billMathPreviewInvalid)
                return SourceAuth("INSUFFICIENT_SESSION_UNFINALIZED", "NO_COMPLETE_CACHE");
            SourceAuthorizationEvidence result;
            if (sample == null || !billMathAdmissions.TryGetValue(sample, out result) || result == null)
                return SourceAuth("INSUFFICIENT_SAMPLE_NOT_CAPTURED", "NOT_REGULAR_BILL_SAMPLE");
            return result;
        }

        private static bool CanPreviewBillMath(Sample sample)
        {
            return billMathPreviewFinalized && !billMathPreviewInvalid &&
                string.Equals(billMathStopPatchAudit, "PASS", StringComparison.Ordinal) &&
                sample != null && InBillAuthorizationScope(sample.Before) &&
                FinalizedBillAuthorization(sample).Admissible;
        }

        // Read-only after the official admission seal; no patch reads, auth recalculation,
        // XP mutation, previewInvalid writes, or reclassification with different gates.
        private static void AppendBillMathPreviewReport(StringBuilder sb)
        {
            int eligible = 0, admitted = 0, tick = 0, finish = 0;
            int blocked = 0, insufficient = 0, foreign = 0;
            int payoutPass = 0, ratePass = 0, wouldJointPass = 0;
            int wouldReview = 0, wouldInconclusive = 0, wouldSkip = 0;
            int admittedButMathFailed = 0, wrapperDifferences = 0, examples = 0;
            var patterns = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (Sample sample in samples)
            {
                if (sample == null || !InBillAuthorizationScope(sample.Before)) continue;
                eligible++;
                SourceAuthorizationEvidence result = FinalizedBillAuthorization(sample);
                if (!CanApproveBillForOfficial(sample))
                {
                    if (result.Status == "FOREIGN_PATCH") foreign++;
                    else if (result.Status.StartsWith("INSUFFICIENT", StringComparison.Ordinal)) insufficient++;
                    else blocked++;
                    continue;
                }
                OfficialBillResult math;
                if (!officialBillResults.TryGetValue(sample, out math))
                {
                    wrapperDifferences++;
                    sb.Append("\n[XPC-4CC][B1B_MATH_ERROR] schema=").Append(BillMathRevision)
                        .Append("; reason=SEALED_SAMPLE_MISSING; sampleIndex=")
                        .Append(samples.IndexOf(sample) + 1);
                    continue;
                }
                admitted++;
                if (result.Status == "SHADOW_ADMISSIBLE_TICK") tick++;
                if (result.Status == "SHADOW_ADMISSIBLE_FINISH_NESTED") finish++;
                if (math.Payout == "PASS_PAYOUT") payoutPass++;
                if (math.Rate == "PASS_INDEPENDENT_RATE") ratePass++;
                if (math.Joint == "JOINT_PASS") wouldJointPass++;
                else
                {
                    admittedButMathFailed++;
                    if (math.Joint == "JOINT_REVIEW") wouldReview++;
                    else if (math.Joint == "JOINT_INCONCLUSIVE") wouldInconclusive++;
                    else wouldSkip++;
                }
                string actualPayout = PayoutClass(sample);
                string actualRate = IndependentRateClass(sample);
                string actualJoint = JointClassFromResults(actualPayout, actualRate);
                if (actualPayout != math.Payout || actualRate != math.Rate ||
                    actualJoint != math.Joint) wrapperDifferences++;
                string key = sample.Before.Skill + "/" + math.SourceRoute + "/" + math.Joint;
                int count; patterns.TryGetValue(key, out count); patterns[key] = count + 1;
                if (examples++ < MaxBillMathExamples)
                    sb.Append("\n[XPC-4CC][B1B_MATH_SAMPLE] schema=").Append(BillMathRevision)
                        .Append("; sampleIndex=").Append(samples.IndexOf(sample) + 1)
                        .Append("; skill=").Append(sample.Before.Skill)
                        .Append("; job=").Append(sample.Before.Job)
                        .Append("; route=").Append(math.SourceRoute)
                        .Append("; auth=").Append(math.SourceStatus)
                        .Append("; payout=").Append(math.Payout)
                        .Append("; rate=").Append(math.Rate)
                        .Append("; wouldJoint=").Append(math.Joint)
                        .Append("; actualJoint=").Append(actualJoint)
                        .Append("; legacyJoint=").Append(math.LegacyJoint)
                        .Append("; actualJointPromoted=")
                        .Append(math.LegacyJoint == "JOINT_SKIP" && actualJoint == "JOINT_PASS");
            }
            foreach (var entry in patterns)
                sb.Append("\n[XPC-4CC][B1B_MATH_PATTERN] schema=").Append(BillMathRevision)
                    .Append("; key=").Append(entry.Key).Append("; count=").Append(entry.Value);
            bool invariant = officialBillSeal == OfficialBillSealed &&
                billMathPreviewFinalized && !billMathPreviewInvalid &&
                eligible == billMathExpectedEntries && eligible == billMathAdmissions.Count &&
                eligible == admitted + blocked + insufficient + foreign &&
                admitted == tick + finish && admitted == officialBillResults.Count &&
                admitted == wouldJointPass + admittedButMathFailed &&
                admittedButMathFailed == wouldReview + wouldInconclusive + wouldSkip &&
                payoutPass >= wouldJointPass && ratePass >= wouldJointPass &&
                wrapperDifferences == 0 && billMathPolicyErrors == 0;
            sb.Append("\n[XPC-4CC][B1B_MATH_PREVIEW] schema=").Append(BillMathRevision)
                .Append("; mode=OFFICIAL_CROSSCHECK; scope=SAME_RETAINED_REGULAR_BILL_SAMPLE")
                .Append("; eligible=").Append(eligible)
                .Append("; admitted=").Append(admitted)
                .Append("; admittedTick=").Append(tick)
                .Append("; admittedFinishNested=").Append(finish)
                .Append("; wouldPayoutPass=").Append(payoutPass)
                .Append("; wouldRatePass=").Append(ratePass)
                .Append("; wouldJointPass=").Append(wouldJointPass)
                .Append("; wouldReview=").Append(wouldReview)
                .Append("; wouldInconclusive=").Append(wouldInconclusive)
                .Append("; wouldSkip=").Append(wouldSkip)
                .Append("; admittedButMathFailed=").Append(admittedButMathFailed)
                .Append("; blocked=").Append(blocked)
                .Append("; insufficient=").Append(insufficient)
                .Append("; foreign=").Append(foreign)
                .Append("; sourceError=").Append(billMathPolicyErrors)
                .Append("; patchAudit=").Append(billMathStopPatchAudit)
                .Append("; officialSeal=").Append(officialBillSeal)
                .Append("; wrapperDifferences=").Append(wrapperDifferences)
                .Append("; invariant=").Append(invariant ? "PASS" : "FAIL")
                .Append("; actualJointPromotions=").Append(OfficialBillPromotionCount())
                .Append("; gameplayWrites=0; newHarmonyPatches=0")
                .Append("; legacyExpectedWorkSkill=UNCHANGED; postCapSelection=UNCHANGED");
            if (!invariant)
                Log.Error("[XPC-4CC][B1B_MATH_REPORT_ERROR] sealed crosscheck mismatch; build=" +
                    XpCustomizerMod.BuildId);
        }

        // Synthetic cases preserve baseline classification when no session is sealed.
        private static void VerifyBillMathPreviewSelfAudit()
        {
            int checks = 0;
            var before = new StartState { RegularSlot = true, Category = "WORK_CANDIDATE",
                WorkKind = "OTHER_WORK_CANDIDATE", Job = "DoBill", Skill = "Crafting",
                Input = 10f, RateBefore = .7f, RateCaptured = true, OracleReady = true,
                OracleRate = .7f, RateCaptureReason = "OK" };
            var sample = new Sample { Before = before, Comparable = true,
                DeltaXp = 7f, DeltaDaily = 7f };
            BillMathRequire(PayoutClass(sample) == "SKIP_JOB_SKILL_UNVERIFIED", "OLD_PAYOUT_SKIP", ref checks);
            BillMathRequire(IndependentRateClass(sample) == "SKIP_JOB_SKILL_UNVERIFIED", "OLD_RATE_SKIP", ref checks);
            BillMathRequire(PayoutClass(sample) == PayoutClassCore(sample, false), "PAYOUT_WRAPPER_PARITY", ref checks);
            BillMathRequire(IndependentRateClass(sample) == IndependentRateClassCore(sample, false), "RATE_WRAPPER_PARITY", ref checks);
            BillMathRequire(PayoutClassCore(sample, true) == "PASS_PAYOUT", "BILL_PAYOUT_PASS", ref checks);
            BillMathRequire(IndependentRateClassCore(sample, true) == "PASS_INDEPENDENT_RATE", "BILL_RATE_PASS", ref checks);
            BillMathRequire(JointClassFromResults(PayoutClassCore(sample, true),
                IndependentRateClassCore(sample, true)) == "JOINT_PASS", "SAME_SAMPLE_PASS", ref checks);
            BillMathRequire(JointClass(sample) == "JOINT_SKIP", "OFFICIAL_JOINT_SKIP", ref checks);
            sample.DeltaXp = 4f;
            BillMathRequire(JointClassFromResults(PayoutClassCore(sample, true),
                IndependentRateClassCore(sample, true)) == "JOINT_REVIEW", "PAYOUT_REVIEW", ref checks);
            sample.DeltaXp = 7f; before.OracleRate = .3f;
            BillMathRequire(JointClassFromResults(PayoutClassCore(sample, true),
                IndependentRateClassCore(sample, true)) == "JOINT_REVIEW", "RATE_REVIEW", ref checks);
            before.OracleRate = .7f; before.Skill = "Social";
            BillMathRequire(PayoutClassCore(sample, true) == "SKIP_JOB_SKILL_UNVERIFIED", "SOCIAL_CANNOT_APPROVE", ref checks);
            before.Skill = "Medicine";
            BillMathRequire(IndependentRateClassCore(sample, true) == "SKIP_JOB_SKILL_UNVERIFIED", "MEDICINE_CANNOT_APPROVE", ref checks);
            before.Skill = "Crafting"; before.RegularSlot = false;
            BillMathRequire(PayoutClassCore(sample, true) == "SKIP_JOB_SKILL_UNVERIFIED", "POSTCAP_CANNOT_APPROVE", ref checks);
            before.RegularSlot = true; before.Category = "READING"; before.ReadingScope = false;
            BillMathRequire(PayoutClassCore(sample, true) == "SKIP_READING_SCOPE_UNCERTAIN", "READING_NO_APPROVAL", ref checks);
            // Stamp serialization must distinguish priority, method and order,
            // even when the owner and kind are identical (old B1-A weakness).
            string s0 = SourceAuthPatchRow("PRE", "zcn.xpcustomizer", "M1", 0, 400, 0, "0#", "0#");
            BillMathRequire(s0 != SourceAuthPatchRow("PRE", "zcn.xpcustomizer", "M1", 0, 401, 0, "0#", "0#"),
                "PRIORITY_DRIFT_DETECTED", ref checks);
            BillMathRequire(s0 != SourceAuthPatchRow("PRE", "zcn.xpcustomizer", "M2", 0, 400, 0, "0#", "0#"),
                "METHOD_DRIFT_DETECTED", ref checks);
            BillMathRequire(s0 != SourceAuthPatchRow("PRE", "zcn.xpcustomizer", "M1", 1, 400, 0, "0#", "0#"),
                "ORDER_DRIFT_DETECTED", ref checks);
            BillMathRequire(s0 != SourceAuthPatchRow("PRE", "zcn.xpcustomizer", "M1", 0, 400, 0, "1#3#abc", "0#"),
                "BEFORE_DRIFT_DETECTED", ref checks);
            BillMathRequire(s0 != SourceAuthPatchRow("PRE", "zcn.xpcustomizer", "M1", 0, 400, 0, "0#", "1#3#abc"),
                "AFTER_DRIFT_DETECTED", ref checks);
            BillMathRequire(s0 != SourceAuthPatchRow("PRE", "zcn.xpcustomizer", "M1", 0, 400, 1, "0#", "0#"),
                "INDEX_DRIFT_DETECTED", ref checks);
            BillMathRequire(SourceAuthField("a|b") != SourceAuthField("a") + SourceAuthField("b"),
                "FIELD_SEPARATORS_UNAMBIGUOUS", ref checks);
            BillMathRequire(!CanPreviewBillMath(sample), "UNFINALIZED_NOT_ADMITTED", ref checks);
            BillMathRequire(JointClassFromResults("PASS_PAYOUT", "SKIP_JOB_SKILL_UNVERIFIED") != "JOINT_PASS",
                "NO_CROSS_EVENT_JOIN", ref checks);
            Log.Message("[XPC-4CC][B1B_PREVIEW_AUDIT] status=PASS; cases=" + checks +
                "; mode=BASELINE_NO_SEAL_SELFTEST; actualJointPromotions=0; build=" + XpCustomizerMod.BuildId);
        }

        private static void BillMathRequire(bool yes, string name, ref int checks)
        {
            if (!yes) throw new InvalidOperationException("B1B0_" + name);
            checks++;
        }
    }
}
#endif
