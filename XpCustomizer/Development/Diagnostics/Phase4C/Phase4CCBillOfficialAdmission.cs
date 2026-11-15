#if DEBUG
using System;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// T04-B1-B1: observer-owned STOP-time admission seal.
    /// One immutable decision and one pair of preflight math results per retained Sample.
    /// This file neither grants XP nor changes another Harmony owner's patches.
    /// </summary>
    internal static partial class Phase4CCLiveXpCapture
    {
        private const string OfficialBillRevision = "4C-C-B1B1-OFFICIAL-R1-20261115";
        private const string OfficialBillSealed = "SEALED_PASS";
        private const string OfficialBillBlocked = "SEALED_BLOCKED";
        private const string OfficialBillNotFinalized = "NOT_FINALIZED";

        private sealed class OfficialBillResult
        {
            internal readonly string SourceStatus, SourceRoute, Payout, Rate, Joint;
            internal readonly string LegacyPayout, LegacyRate, LegacyJoint;
            internal OfficialBillResult(SourceAuthorizationEvidence source, string payout,
                string rate, string legacyPayout, string legacyRate)
            {
                SourceStatus = source.Status;
                SourceRoute = source.Route;
                Payout = payout;
                Rate = rate;
                Joint = JointClassFromResults(payout, rate);
                LegacyPayout = legacyPayout;
                LegacyRate = legacyRate;
                LegacyJoint = JointClassFromResults(legacyPayout, legacyRate);
            }
        }

        private static readonly Dictionary<Sample, OfficialBillResult> officialBillResults =
            new Dictionary<Sample, OfficialBillResult>();
        private static string officialBillSeal = OfficialBillNotFinalized;
        private static string officialBillSealReason = "NOT_STARTED";
        private static int officialBillPreflightErrors;

        private static void ResetOfficialBillSession()
        {
            officialBillResults.Clear();
            officialBillSeal = OfficialBillNotFinalized;
            officialBillSealReason = "NOT_STARTED";
            officialBillPreflightErrors = 0;
        }

        private static bool OfficialBillPolicyAllows(bool finalized, bool invalid,
            bool hooksRemoved, bool patchStable, Sample sample, SourceAuthorizationEvidence auth)
        {
            if (!finalized || invalid || !hooksRemoved || !patchStable || sample == null ||
                !InBillAuthorizationScope(sample.Before) || auth == null || !auth.Admissible)
                return false;
            StartState b = sample.Before;
            if (float.IsNaN(b.Input) || float.IsInfinity(b.Input) || b.Input <= 0f ||
                b.Direct || b.IgnoreLearnRate || b.FromInterval || b.ReadingScope ||
                b.TotallyDisabled) return false;
            return (auth.Status == "SHADOW_ADMISSIBLE_TICK" && auth.Route == "BILL_TICK") ||
                (auth.Status == "SHADOW_ADMISSIBLE_FINISH_NESTED" &&
                    auth.Route == "BILL_FINISH_NESTED");
        }

        // This is a STOP-only check. Never invoke it from either math classifier.
        private static bool OfficialBillObserverHooksRemoved()
        {
            return !installed && !bookTickHookInstalled && !textbookTickHookInstalled &&
                target != null && !HasPatchOwner(target, DiagnosticHarmonyId) &&
                (bookTickTarget == null || !HasPatchOwner(bookTickTarget, DiagnosticHarmonyId)) &&
                (textbookTickTarget == null || !HasPatchOwner(textbookTickTarget, DiagnosticHarmonyId));
        }

        private static void BlockOfficialBillSession(string reason)
        {
            officialBillResults.Clear();
            officialBillSeal = OfficialBillBlocked;
            officialBillSealReason = reason ?? "UNKNOWN";
            officialBillPreflightErrors++;
            // Set only during preflight, BEFORE any official classifier/counter.
            billMathPreviewInvalid = true;
            Log.Error("[XPC-4CC][B1B1_OFFICIAL_BLOCK] schema=" + OfficialBillRevision +
                "; reason=" + officialBillSealReason + "; officialBillAdmitted=0; build=" +
                XpCustomizerMod.BuildId);
        }

        // Must run precisely once after observer hook removal and B1-B0 finalization.
        // No report method may call this again or alter its sealed results.
        private static void SealOfficialBillSession()
        {
            if (officialBillSeal != OfficialBillNotFinalized)
            {
                // A sealed session must NEVER be rewritten, even on a duplicate call.
                Log.Error("[XPC-4CC][B1B1_DOUBLE_SEAL_REJECTED] state=" + officialBillSeal);
                return;
            }
            try
            {
                if (!billMathPreviewFinalized || billMathPreviewInvalid ||
                    billMathPolicyErrors != 0 ||
                    !string.Equals(billMathStopPatchAudit, "PASS", StringComparison.Ordinal) ||
                    billMathAdmissions.Count != billMathExpectedEntries)
                {
                    BlockOfficialBillSession("SOURCE_FINALIZATION_OR_PATCH_AUDIT_FAILED");
                    return;
                }
                if (!OfficialBillObserverHooksRemoved())
                {
                    BlockOfficialBillSession("DIAGNOSTIC_HOOK_STILL_INSTALLED");
                    return;
                }
                var seen = new HashSet<Sample>();
                int eligible = 0;
                foreach (Sample sample in samples)
                {
                    if (sample == null || sample.Before == null || !seen.Add(sample))
                    {
                        BlockOfficialBillSession("NULL_OR_DUPLICATE_RETAINED_SAMPLE");
                        return;
                    }
                    if (!InBillAuthorizationScope(sample.Before)) continue;
                    eligible++;
                    SourceAuthorizationEvidence auth;
                    if (!billMathAdmissions.TryGetValue(sample, out auth) || auth == null)
                    {
                        BlockOfficialBillSession("MISSING_SOURCE_ADMISSION");
                        return;
                    }
                    if (!auth.Admissible) continue;
                    if (!OfficialBillPolicyAllows(billMathPreviewFinalized, billMathPreviewInvalid,
                        true, true, sample, auth) || !CanPreviewBillMath(sample))
                    {
                        BlockOfficialBillSession("UNEXPECTED_SOURCE_AUTHORIZATION");
                        return;
                    }
                    // Compute BOTH independent checks for the exact same Sample now.
                    // REVIEW/INCONCLUSIVE/SKIP are legitimate math verdicts, not seal failures.
                    string payout = PayoutClassCore(sample, true);
                    string rate = IndependentRateClassCore(sample, true);
                    string legacyPayout = PayoutClassCore(sample, false);
                    string legacyRate = IndependentRateClassCore(sample, false);
                    if (string.IsNullOrEmpty(payout) || string.IsNullOrEmpty(rate) ||
                        legacyPayout != "SKIP_JOB_SKILL_UNVERIFIED" ||
                        legacyRate != "SKIP_JOB_SKILL_UNVERIFIED")
                    {
                        BlockOfficialBillSession("MATH_PREFLIGHT_CONTRACT_FAILED");
                        return;
                    }
                    officialBillResults.Add(sample, new OfficialBillResult(auth,
                        payout, rate, legacyPayout, legacyRate));
                }
                if (eligible != billMathExpectedEntries || eligible != billMathAdmissions.Count)
                {
                    BlockOfficialBillSession("SOURCE_CACHE_COUNT_MISMATCH");
                    return;
                }
                officialBillSeal = OfficialBillSealed;
                officialBillSealReason = "PATCH_AND_MATH_PREFLIGHT_PASS";
            }
            catch (Exception ex)
            {
                BlockOfficialBillSession("PREFLIGHT_" + ex.GetType().Name);
            }
        }

        // Read-only: NO new Harmony, pawn, game, or patch queries.
        private static bool CanApproveBillForOfficial(Sample sample)
        {
            return officialBillSeal == OfficialBillSealed && sample != null &&
                officialBillResults.ContainsKey(sample);
        }

        private static string OfficialBillEvidenceGrade(Sample sample)
        {
            if (sample == null || sample.Before == null) return "JOB_CONTEXT_ONLY";
            if (!InBillAuthorizationScope(sample.Before)) return "JOB_CONTEXT_ONLY";
            OfficialBillResult result;
            if (CanApproveBillForOfficial(sample) && officialBillResults.TryGetValue(sample, out result))
                return result.SourceRoute == "BILL_TICK" ?
                    "P0_CALLSITE_AUTHORIZED_BILL_TICK" :
                    "P0_CALLSITE_AUTHORIZED_BILL_FINISH_NESTED";
            return "BILL_SOURCE_UNVERIFIED";
        }

        private static int OfficialBillPromotionCount()
        {
            if (officialBillSeal != OfficialBillSealed) return 0;
            int count = 0;
            foreach (OfficialBillResult row in officialBillResults.Values)
                if (row.LegacyJoint == "JOINT_SKIP" && row.Joint == "JOINT_PASS") count++;
            return count;
        }

        // REPORT ONLY. Reuses the same preflight rows: no re-authorization, no mutation.
        private static void AppendBillOfficialReport(StringBuilder sb, bool skillSummaryInvariant)
        {
            int eligible = 0, nonAdmitted = 0, pass = 0, review = 0;
            int inconclusive = 0, skip = 0, newPass = 0, newReview = 0, newInconclusive = 0;
            int payoutDifferences = 0, rateDifferences = 0, jointDifferences = 0;
            int unexpectedNonBillChanges = 0, examples = 0;
            foreach (Sample sample in samples)
            {
                if (sample == null || sample.Before == null) continue;
                if (!InBillAuthorizationScope(sample.Before))
                {
                    // The original job/reading/other classifiers must be byte-for-byte equivalent.
                    if (PayoutClass(sample) != PayoutClassCore(sample, false) ||
                        IndependentRateClass(sample) != IndependentRateClassCore(sample, false))
                        unexpectedNonBillChanges++;
                    continue;
                }
                eligible++;
                if (!CanApproveBillForOfficial(sample)) { nonAdmitted++; continue; }
                OfficialBillResult row;
                if (!officialBillResults.TryGetValue(sample, out row))
                {
                    payoutDifferences++; rateDifferences++; jointDifferences++;
                    continue;
                }
                string actualPayout = PayoutClass(sample);
                string actualRate = IndependentRateClass(sample);
                string actualJoint = JointClassFromResults(actualPayout, actualRate);
                if (actualPayout != row.Payout) payoutDifferences++;
                if (actualRate != row.Rate) rateDifferences++;
                if (actualJoint != row.Joint) jointDifferences++;
                if (actualJoint == "JOINT_PASS") pass++;
                else if (actualJoint == "JOINT_REVIEW") review++;
                else if (actualJoint == "JOINT_INCONCLUSIVE") inconclusive++;
                else skip++;
                if (row.LegacyJoint == "JOINT_SKIP")
                {
                    if (actualJoint == "JOINT_PASS") newPass++;
                    else if (actualJoint == "JOINT_REVIEW") newReview++;
                    else if (actualJoint == "JOINT_INCONCLUSIVE") newInconclusive++;
                }
                if (examples++ < 12)
                {
                    SourceProofEvidence p = sample.Before.SourceProof;
                    sb.Append("\n[XPC-4CC][B1B1_OFFICIAL_SAMPLE] schema=").Append(OfficialBillRevision)
                        .Append("; sampleIndex=").Append(samples.IndexOf(sample) + 1)
                        .Append("; skill=").Append(sample.Before.Skill)
                        .Append("; job=").Append(sample.Before.Job)
                        .Append("; recipe=").Append(p == null ? "NONE" : p.Recipe)
                        .Append("; sourceStatus=").Append(row.SourceStatus)
                        .Append("; sourceRoute=").Append(row.SourceRoute)
                        .Append("; payoutClass=").Append(actualPayout)
                        .Append("; independentRateClass=").Append(actualRate)
                        .Append("; jointClass=").Append(actualJoint)
                        .Append("; legacyJointClass=").Append(row.LegacyJoint)
                        .Append("; sourceEvidenceGrade=").Append(OfficialBillEvidenceGrade(sample));
                }
            }
            bool parity = payoutDifferences == 0 && rateDifferences == 0 &&
                jointDifferences == 0 && unexpectedNonBillChanges == 0;
            bool invariant = parity && skillSummaryInvariant &&
                (officialBillSeal == OfficialBillSealed ?
                    pass + review + inconclusive + skip == officialBillResults.Count &&
                    nonAdmitted + officialBillResults.Count == eligible :
                    officialBillResults.Count == 0 && nonAdmitted == eligible &&
                    pass + review + inconclusive + skip == 0);
            sb.Append("\n[XPC-4CC][B1B1_OFFICIAL_SUMMARY] schema=").Append(OfficialBillRevision)
                .Append("; mode=OFFICIAL_JOINT_BILL_ONLY; seal=").Append(officialBillSeal)
                .Append("; sealReason=").Append(officialBillSealReason)
                .Append("; eligible=").Append(eligible)
                .Append("; sourceAdmitted=").Append(officialBillResults.Count)
                .Append("; officialBillPass=").Append(pass)
                .Append("; officialBillReview=").Append(review)
                .Append("; officialBillInconclusive=").Append(inconclusive)
                .Append("; officialBillSkip=").Append(skip)
                .Append("; legacyBillSkipToPass=").Append(newPass)
                .Append("; legacyBillSkipToReview=").Append(newReview)
                .Append("; legacyBillSkipToInconclusive=").Append(newInconclusive)
                .Append("; notAdmitted=").Append(nonAdmitted)
                .Append("; payoutParity=").Append(payoutDifferences == 0 ? "PASS" : "FAIL")
                .Append("; rateParity=").Append(rateDifferences == 0 ? "PASS" : "FAIL")
                .Append("; jointParity=").Append(jointDifferences == 0 ? "PASS" : "FAIL")
                .Append("; sourcePatchAudit=").Append(billMathStopPatchAudit)
                .Append("; skillSummaryInvariant=").Append(skillSummaryInvariant ? "PASS" : "FAIL")
                .Append("; unexpectedNonBillChanges=").Append(unexpectedNonBillChanges)
                .Append("; preflightErrors=").Append(officialBillPreflightErrors)
                .Append("; invariant=").Append(invariant ? "PASS" : "FAIL")
                .Append("; postCapSelection=UNCHANGED; newHarmonyPatches=0; gameplayWrites=0")
                .Append("; build=").Append(XpCustomizerMod.BuildId);
            if (!invariant)
                Log.Error("[XPC-4CC][B1B1_OFFICIAL_REPORT_ERROR] parity/invariant failed; build=" +
                    XpCustomizerMod.BuildId);
        }

        // Detached policy/maths fixtures. No actual session or Harmony state is changed.
        private static void VerifyBillOfficialSelfAudit()
        {
            int checks = 0;
            var b = new StartState {
                RegularSlot = true, Category = "WORK_CANDIDATE", Job = "DoBill",
                WorkKind = "OTHER_WORK_CANDIDATE", Skill = "Crafting", Input = 10f,
                RateCaptured = true, RateBefore = .7f, OracleReady = true, OracleRate = .7f
            };
            var s = new Sample { Before = b, Comparable = true, DeltaXp = 7f, DeltaDaily = 7f };
            var tick = SourceAuth("SHADOW_ADMISSIBLE_TICK", "P0", "BILL_TICK", true);
            var finish = SourceAuth("SHADOW_ADMISSIBLE_FINISH_NESTED", "P0", "BILL_FINISH_NESTED", true);
            CheckBillOfficial(!OfficialBillPolicyAllows(false, false, true, true, s, tick), "UNFINALIZED", ref checks);
            CheckBillOfficial(!OfficialBillPolicyAllows(true, true, true, true, s, tick), "INVALID_SESSION", ref checks);
            CheckBillOfficial(!OfficialBillPolicyAllows(true, false, false, true, s, tick), "HOOK_PRESENT", ref checks);
            CheckBillOfficial(!OfficialBillPolicyAllows(true, false, true, false, s, tick), "PATCH_DRIFT", ref checks);
            CheckBillOfficial(!OfficialBillPolicyAllows(true, false, true, true, null, tick), "NULL_SAMPLE", ref checks);
            CheckBillOfficial(!OfficialBillPolicyAllows(true, false, true, true, s, null), "NULL_AUTH", ref checks);
            CheckBillOfficial(OfficialBillPolicyAllows(true, false, true, true, s, tick), "TICK_ACCEPTED", ref checks);
            b.Skill = "Cooking";
            CheckBillOfficial(OfficialBillPolicyAllows(true, false, true, true, s, finish), "FINISH_ACCEPTED", ref checks);
            b.Skill = "Crafting";
            CheckBillOfficial(!OfficialBillPolicyAllows(true, false, true, true, s,
                SourceAuth("SHADOW_ADMISSIBLE_TICK", "P0", "BILL_FINISH_NESTED", true)), "STATUS_ROUTE_MISMATCH", ref checks);
            CheckBillOfficial(!OfficialBillPolicyAllows(true, false, true, true, s,
                SourceAuth("SHADOW_ADMISSIBLE_FINISH_NESTED", "P0", "BILL_TICK", true)), "FINISH_ROUTE_MISMATCH", ref checks);
            CheckBillOfficial(!OfficialBillPolicyAllows(true, false, true, true, s,
                SourceAuth("SHADOW_ADMISSIBLE_TICK", "P0", "BILL_TICK", false)), "FLAG_NOT_ADMITTED", ref checks);
            string[] rejects = { "FOREIGN_PATCH", "BLOCKED_CONTEXT", "INSUFFICIENT_TRACE",
                "INSUFFICIENT_RECIPE_FLAG", "BLOCKED_TOPOLOGY", "OUT_OF_SCOPE",
                "INSUFFICIENT_SESSION_PATCH_DRIFT", "SHADOW_ADMISSIBLE_UNKNOWN" };
            foreach (string name in rejects)
                CheckBillOfficial(!OfficialBillPolicyAllows(true, false, true, true, s,
                    SourceAuth(name, "fixture", "BILL_TICK", true)), "STATUS_" + name, ref checks);
            b.Job = "TendPatient";
            CheckBillOfficial(!OfficialBillPolicyAllows(true, false, true, true, s, tick), "TEND_EXCLUDED", ref checks);
            b.Job = "DoBill"; b.Skill = "Social";
            CheckBillOfficial(!OfficialBillPolicyAllows(true, false, true, true, s, tick), "SOCIAL_EXCLUDED", ref checks);
            b.Skill = "Medicine";
            CheckBillOfficial(!OfficialBillPolicyAllows(true, false, true, true, s, tick), "MEDICINE_EXCLUDED", ref checks);
            b.Skill = "Crafting"; b.RegularSlot = false;
            CheckBillOfficial(!OfficialBillPolicyAllows(true, false, true, true, s, tick), "POSTCAP_EXCLUDED", ref checks);
            b.RegularSlot = true; b.Category = "READING";
            CheckBillOfficial(!OfficialBillPolicyAllows(true, false, true, true, s, tick), "READING_EXCLUDED", ref checks);
            b.Category = "OTHER";
            CheckBillOfficial(!OfficialBillPolicyAllows(true, false, true, true, s, tick), "OTHER_EXCLUDED", ref checks);
            b.Category = "WORK_CANDIDATE"; b.RegularSlot = true; b.Job = "ConstructFinishFrame";
            CheckBillOfficial(!OfficialBillPolicyAllows(true, false, true, true, s, tick), "CONSTRUCTION_EXCLUDED", ref checks);
            b.Job = "DoBill";
            CheckBillOfficial(PayoutClassCore(s, false) == "SKIP_JOB_SKILL_UNVERIFIED", "LEGACY_PAYOUT_SKIP", ref checks);
            CheckBillOfficial(IndependentRateClassCore(s, false) == "SKIP_JOB_SKILL_UNVERIFIED", "LEGACY_RATE_SKIP", ref checks);
            CheckBillOfficial(PayoutClassCore(s, true) == "PASS_PAYOUT", "APPROVED_PAYOUT_PASS", ref checks);
            CheckBillOfficial(IndependentRateClassCore(s, true) == "PASS_INDEPENDENT_RATE", "APPROVED_RATE_PASS", ref checks);
            CheckBillOfficial(JointClassFromResults(PayoutClassCore(s, true),
                IndependentRateClassCore(s, true)) == "JOINT_PASS", "SAME_SAMPLE_PASS", ref checks);
            CheckBillOfficial(JointClassFromResults(PayoutClassCore(s, false),
                IndependentRateClassCore(s, false)) == "JOINT_SKIP", "LEGACY_JOINT_SKIP", ref checks);
            s.DeltaXp = 5f;
            CheckBillOfficial(JointClassFromResults(PayoutClassCore(s, true),
                IndependentRateClassCore(s, true)) == "JOINT_REVIEW", "PAYOUT_MISMATCH_REVIEW", ref checks);
            s.DeltaXp = 7f; b.OracleRate = .2f;
            CheckBillOfficial(JointClassFromResults(PayoutClassCore(s, true),
                IndependentRateClassCore(s, true)) == "JOINT_REVIEW", "RATE_MISMATCH_REVIEW", ref checks);
            b.OracleRate = .7f; s.Comparable = false;
            CheckBillOfficial(JointClassFromResults(PayoutClassCore(s, true),
                IndependentRateClassCore(s, true)) == "JOINT_INCONCLUSIVE", "LEVEL_INCONCLUSIVE", ref checks);
            s.Comparable = true; b.Direct = true;
            CheckBillOfficial(JointClassFromResults(PayoutClassCore(s, true),
                IndependentRateClassCore(s, true)) == "JOINT_SKIP", "DIRECT_SKIP", ref checks);
            b.Direct = false; b.IgnoreLearnRate = true;
            CheckBillOfficial(JointClassFromResults(PayoutClassCore(s, true),
                IndependentRateClassCore(s, true)) == "JOINT_SKIP", "IGNORE_SKIP", ref checks);
            b.IgnoreLearnRate = false; b.TotallyDisabled = true; s.AfterDisabled = true;
            CheckBillOfficial(JointClassFromResults(PayoutClassCore(s, true),
                IndependentRateClassCore(s, true)) == "JOINT_SKIP", "DISABLED_SKIP", ref checks);
            b.TotallyDisabled = false; s.AfterDisabled = false;
            b.Direct = true;
            CheckBillOfficial(!OfficialBillPolicyAllows(true, false, true, true, s, tick), "DIRECT_POLICY_REJECT", ref checks);
            b.Direct = false; b.IgnoreLearnRate = true;
            CheckBillOfficial(!OfficialBillPolicyAllows(true, false, true, true, s, tick), "IGNORE_POLICY_REJECT", ref checks);
            b.IgnoreLearnRate = false; b.FromInterval = true;
            CheckBillOfficial(!OfficialBillPolicyAllows(true, false, true, true, s, tick), "DECAY_POLICY_REJECT", ref checks);
            b.FromInterval = false; b.ReadingScope = true;
            CheckBillOfficial(!OfficialBillPolicyAllows(true, false, true, true, s, tick), "READING_FLAG_REJECT", ref checks);
            b.ReadingScope = false; b.TotallyDisabled = true;
            CheckBillOfficial(!OfficialBillPolicyAllows(true, false, true, true, s, tick), "DISABLED_FLAG_REJECT", ref checks);
            b.TotallyDisabled = false; b.Input = 0f;
            CheckBillOfficial(!OfficialBillPolicyAllows(true, false, true, true, s, tick), "ZERO_INPUT_REJECT", ref checks);
            b.Input = float.NaN;
            CheckBillOfficial(!OfficialBillPolicyAllows(true, false, true, true, s, tick), "NAN_INPUT_REJECT", ref checks);
            b.Input = 10f;
            CheckBillOfficial(!CanApproveBillForOfficial(s), "NO_LIVE_SEAL_IN_SELFTEST", ref checks);
            CheckBillOfficial(officialBillSeal == OfficialBillNotFinalized, "PURE_TEST_NO_STATE_CHANGE", ref checks);
            Log.Message("[XPC-4CC][B1B1_OFFICIAL_AUDIT] status=PASS; cases=" + checks +
                "; scope=DETACHED_POLICY_AND_REAL_CLASSIFIER_CORES; gameplayWrites=0; newHarmonyPatches=0; build=" +
                XpCustomizerMod.BuildId);
        }

        private static void CheckBillOfficial(bool ok, string name, ref int checks)
        {
            if (!ok) throw new InvalidOperationException("B1B1_OFFICIAL_" + name);
            checks++;
        }
    }
}
#endif
