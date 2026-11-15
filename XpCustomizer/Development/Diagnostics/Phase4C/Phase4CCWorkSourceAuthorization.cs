#if DEBUG
using System;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// T04-B1-A: source AUTHORIZATION SHADOW, never a production XP policy.
    /// Pure evaluation of snapshots taken at the existing Learn observer.
    /// NO Harmony registration, XP writes, job mutation, or JOINT promotion.
    /// </summary>
    internal static partial class Phase4CCLiveXpCapture
    {
        private const string SourceAuthRevision = "4C-C-SOURCE-AUTH-B1A-R1";
        private const int SourceAuthSampleLimit = 12;

        private sealed class SourceAuthorizationEvidence
        {
            internal string Status;
            internal string Reason;
            internal string Route;
            internal bool Admissible;
        }

        private static bool InBillAuthorizationScope(StartState b)
        {
            return b != null && b.RegularSlot && b.Category == "WORK_CANDIDATE" &&
                b.Job == "DoBill" && (b.Skill == "Cooking" || b.Skill == "Crafting");
        }

        private static SourceAuthorizationEvidence SourceAuth(string status, string reason,
            string route = "NONE", bool admissible = false)
        {
            return new SourceAuthorizationEvidence {
                Status = status, Reason = reason, Route = route, Admissible = admissible
            };
        }

        // No pawn, job, settings, or Harmony reads. Only event-time immutable fields.
        // Status is independent of legacy SourceProofEvidence.Status / JOINT.
        private static SourceAuthorizationEvidence AuthorizeBillSourceShadow(StartState b,
            SourceProofEvidence p)
        {
            if (!InBillAuthorizationScope(b)) return SourceAuth("OUT_OF_SCOPE", "NOT_BILL_CRAFTING_COOKING");
            if (float.IsNaN(b.Input) || float.IsInfinity(b.Input) || b.Input <= 0f)
                return SourceAuth("BLOCKED_INPUT", "NONPOSITIVE_OR_NONFINITE_XP");
            if (b.Direct || b.IgnoreLearnRate || b.FromInterval || b.ReadingScope ||
                b.TotallyDisabled)
                return SourceAuth("BLOCKED_FLAGS", "NONSTANDARD_LEARN_CALL");
            if (p == null) return SourceAuth("INSUFFICIENT_PROOF", "NO_RETAINED_SOURCE_PROOF");
            if (!p.AssemblyMatchedAtEvent || p.Status == "UNSUPPORTED_GAME_ASSEMBLY")
                return SourceAuth("INSUFFICIENT_ASSEMBLY", "P0_MVID_MISMATCH");
            if (!p.TraceAttempted || p.Status == "BUDGET_EXHAUSTED")
                return SourceAuth("INSUFFICIENT_TRACE", "NOT_TRACED_OR_BUDGET_EXHAUSTED");
            if (p.Status == "OBSERVATION_ERROR" || p.Frames <= 0)
                return SourceAuth("INSUFFICIENT_TRACE", "OBSERVATION_ERROR_OR_NO_FRAMES");
            // Even if another patch query fails, a confirmed foreign owner
            // must not be concealed behind an incomplete audit.
            if (p.AuthPatchForeign)
                return SourceAuth("FOREIGN_PATCH", "FOREIGN_HARMONY_PATCH_ON_RELEVANT_METHOD");
            if (!p.AuthPatchAuditComplete)
                return SourceAuth("INSUFFICIENT_PATCH_AUDIT", p.AuthPatchReason);
            if (p.AuthPatchChanged)
                return SourceAuth("INSUFFICIENT_PATCH_DRIFT", "PATCH_SET_CHANGED_SINCE_START");
            if (!p.BillTickResolvedAtEvent || !p.BillFinishResolvedAtEvent ||
                !p.SkillTrackerResolvedAtEvent)
                return SourceAuth("INSUFFICIENT_CALLBACKS", "P0_CALLBACK_OR_SKILL_TRACKER_MISSING");
            if (p.Context != "CONTEXT_MATCH" || p.Driver != "JobDriver_DoBill" ||
                p.Recipe == "NONE" || p.RecipeSkill != b.Skill)
                return SourceAuth("BLOCKED_CONTEXT", "JOB_DRIVER_OR_RECIPE_SKILL_UNVERIFIED");
            if (p.RecipeUsesUnfinished != "True" && p.RecipeUsesUnfinished != "False")
                return SourceAuth("INSUFFICIENT_RECIPE_FLAG", "USES_UNFINISHED_UNKNOWN");
            if ((p.RouteMask & ~(SourceBillTickBit | SourceBillFinishBit | SourceTendBit |
                SourceSocialBit)) != 0 || !SourceAuthDepthsConsistent(p))
                return SourceAuth("BLOCKED_TOPOLOGY", "MISSING_INVALID_OR_DUPLICATE_DEPTH");
            if ((p.RouteMask & (SourceTendBit | SourceSocialBit)) != 0)
                return SourceAuth("BLOCKED_FOREIGN_SOURCE", "MEDICINE_OR_INTERACTION_IN_STACK");
            if (p.RouteMask == SourceBillTickBit)
            {
                if (p.RecipeUsesUnfinished != "True")
                    return SourceAuth("BLOCKED_RECIPE_ROUTE", "TICK_REQUIRES_UNFINISHED");
                // P0 IL: unfinished tick routes via Pawn_SkillTracker.Learn.
                if (p.SkillTrackerDepth < 0 || p.SkillTrackerDepth >= p.BillTickDepth)
                    return SourceAuth("INSUFFICIENT_CALL_CHAIN", "SKILL_TRACKER_NOT_BETWEEN_LEARN_AND_TICK");
                if (p.Status != "CALLSITE_MATCH_CANDIDATE" ||
                    p.Route != "BILL_UNFINISHED_TICK")
                    return SourceAuth("BLOCKED_LEGACY_EVIDENCE", "TICK_SOURCE_NOT_CONFIRMED");
                return SourceAuth("SHADOW_ADMISSIBLE_TICK", "P0_TICK_CHAIN_AND_RECIPE_CORRELATE",
                    "BILL_TICK", true);
            }
            if (p.RouteMask == (SourceBillTickBit | SourceBillFinishBit))
            {
                if (p.RecipeUsesUnfinished != "False")
                    return SourceAuth("BLOCKED_RECIPE_ROUTE", "FINISH_REQUIRES_NO_UNFINISHED");
                // P0 IL: vanilla finish callback calls SkillRecord.Learn directly,
                // often inside outer DoRecipeWork tick. Never choose by depth alone.
                if (p.BillFinishDepth >= p.BillTickDepth || p.SkillTrackerDepth >= 0)
                    return SourceAuth("BLOCKED_TOPOLOGY", "FINISH_MUST_BE_INNER_DIRECT_LEARN");
                if (p.Status != "CALLSITE_AMBIGUOUS" ||
                    p.Route != "NONE_OR_MULTIPLE")
                    return SourceAuth("BLOCKED_LEGACY_EVIDENCE", "EXPECTED_NESTED_STACK_NOT_RECORDED");
                return SourceAuth("SHADOW_ADMISSIBLE_FINISH_NESTED",
                    "P0_FINISH_DIRECT_AND_OUTER_TICK_CORRELATE", "BILL_FINISH_NESTED", true);
            }
            if (p.RouteMask == SourceBillFinishBit && p.RecipeUsesUnfinished == "False")
                return SourceAuth("INSUFFICIENT_FINISH_ONLY", "NOT_YET_OBSERVED_IN_LIVE_B1",
                    "BILL_FINISH_ONLY");
            if (p.RouteMask == SourceBillFinishBit && p.RecipeUsesUnfinished == "True")
                return SourceAuth("BLOCKED_RECIPE_ROUTE", "FINISH_ON_UNFINISHED");
            return SourceAuth("INSUFFICIENT_ROUTE", "NO_VERIFIED_P0_BILL_ROUTE");
        }

        private static bool SourceAuthDepthsConsistent(SourceProofEvidence p)
        {
            if (p.Frames <= 0 || p.Frames > MaxSourceProofFrames) return false;
            if (!SourceAuthDepthMatches(p.RouteMask, SourceBillTickBit, p.BillTickDepth, p.Frames) ||
                !SourceAuthDepthMatches(p.RouteMask, SourceBillFinishBit, p.BillFinishDepth, p.Frames) ||
                !SourceAuthDepthMatches(p.RouteMask, SourceTendBit, p.TendDepth, p.Frames) ||
                !SourceAuthDepthMatches(p.RouteMask, SourceSocialBit, p.SocialDepth, p.Frames)) return false;
            return p.SkillTrackerDepth >= -1 && p.SkillTrackerDepth < p.Frames;
        }

        private static bool SourceAuthDepthMatches(int mask, int bit, int depth, int frames)
        {
            return (mask & bit) != 0 ? depth >= 0 && depth < frames : depth == -1;
        }

        // Stop runs after the diagnostic Harmony owner has been removed.
        // Check again to reject events if relevant patches changed after their
        // last observation. Owner snapshots are already compared per event.
        private static string CheckSourceAuthorizationEndPatches()
        {
            if (!sourceAuthStartPatchReady) return "START_PATCH_SNAPSHOT_UNAVAILABLE";
            try
            {
                bool foreign, complete;
                string current = SourceAuthPatchStamp(target, true, out foreign, out complete);
                if (!complete || !string.Equals(current, sourceAuthLearnStamp, StringComparison.Ordinal))
                    return "LEARN_PATCH_SET_CHANGED";
                current = SourceAuthPatchStamp(sourceBillTickMethod, false, out foreign, out complete);
                if (!complete || !string.Equals(current, sourceAuthTickStamp, StringComparison.Ordinal))
                    return "BILL_TICK_PATCH_SET_CHANGED";
                current = SourceAuthPatchStamp(sourceBillFinishMethod, false, out foreign, out complete);
                if (!complete || !string.Equals(current, sourceAuthFinishStamp, StringComparison.Ordinal))
                    return "BILL_FINISH_PATCH_SET_CHANGED";
                current = SourceAuthPatchStamp(sourceSkillTrackerLearnMethod, false, out foreign, out complete);
                if (!complete || !string.Equals(current, sourceAuthTrackerStamp, StringComparison.Ordinal))
                    return "SKILL_TRACKER_PATCH_SET_CHANGED";
                return "PASS";
            }
            catch (Exception ex) { return "END_PATCH_AUDIT_" + ex.GetType().Name; }
        }

        private static void AppendSourceAuthorizationReport(StringBuilder sb)
        {
            // B1-B0: the one Stop-time snapshot was already finalized before
            // ANY classifier; never re-audit/re-authorize within the report.
            string stopPatchAudit = billMathStopPatchAudit;
            int eligible = 0, admittedTick = 0, admittedFinish = 0, blocked = 0;
            int insufficient = 0, foreign = 0, errors = 0;
            int examples = 0;
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (Sample sample in samples)
            {
                StartState b = sample.Before;
                if (!InBillAuthorizationScope(b)) continue;
                eligible++;
                SourceProofEvidence p = b.SourceProof;
                SourceAuthorizationEvidence result;
                try
                {
                    result = FinalizedBillAuthorization(sample);
                }
                catch (Exception ex)
                {
                    errors++;
                    result = SourceAuth("INSUFFICIENT_POLICY_ERROR", ex.GetType().Name);
                }
                if (result.Status == "SHADOW_ADMISSIBLE_TICK") admittedTick++;
                else if (result.Status == "SHADOW_ADMISSIBLE_FINISH_NESTED") admittedFinish++;
                else if (result.Status == "FOREIGN_PATCH") foreign++;
                else if (result.Status.StartsWith("INSUFFICIENT", StringComparison.Ordinal)) insufficient++;
                else blocked++;
                string pattern = b.Skill + "/" + result.Status + "/" +
                    (p == null ? "NONE" : SourceRouteMaskLabel(p.RouteMask));
                int count;
                counts.TryGetValue(pattern, out count);
                counts[pattern] = count + 1;
                if (examples < SourceAuthSampleLimit &&
                    (result.Admissible || result.Status == "FOREIGN_PATCH" ||
                     result.Status == "BLOCKED_FOREIGN_SOURCE" || examples < 3))
                {
                    examples++;
                    sb.Append("\n[XPC-4CC][SOURCE_AUTH_SAMPLE] schema=").Append(SourceAuthRevision)
                        .Append("; skill=").Append(b.Skill)
                        .Append("; job=").Append(b.Job)
                        .Append("; recipe=").Append(p == null ? "NONE" : p.Recipe)
                        .Append("; recipeUsesUnfinished=").Append(p == null ? "UNKNOWN" : p.RecipeUsesUnfinished)
                        .Append("; routeMask=").Append(p == null ? "NONE" : SourceRouteMaskLabel(p.RouteMask))
                        .Append("; tickDepth=").Append(p == null ? -1 : p.BillTickDepth)
                        .Append("; finishDepth=").Append(p == null ? -1 : p.BillFinishDepth)
                        .Append("; trackerDepth=").Append(p == null ? -1 : p.SkillTrackerDepth)
                        .Append("; auth=").Append(result.Status)
                        .Append("; authReason=").Append(result.Reason)
                        .Append("; patchOwners=").Append(p == null ? "NONE" : p.AuthPatchOwners)
                        .Append("; legacySource=").Append(p == null ? "NONE" : p.Status)
                        .Append("; officialJoint=").Append(JointClass(sample))
                        .Append("; legacyJoint=").Append(JointClassFromResults(
                            PayoutClassCore(sample, false), IndependentRateClassCore(sample, false)))
                        .Append("; officialEvidenceGrade=").Append(OfficialBillEvidenceGrade(sample))
                        .Append("; promotedJointPass=").Append(CanApproveBillForOfficial(sample) &&
                            JointClass(sample) == "JOINT_PASS");
                }
            }
            // Report all bounded patterns, even if the example row cap is hit.
            foreach (var kv in counts)
                sb.Append("\n[XPC-4CC][SOURCE_AUTH_PATTERN] schema=").Append(SourceAuthRevision)
                    .Append("; key=").Append(kv.Key)
                    .Append("; count=").Append(kv.Value)
                    .Append("; sourcePolicyUnchanged=True");
            bool invariant = errors == 0 && !billMathPreviewInvalid &&
                eligible == admittedTick + admittedFinish + blocked + insufficient + foreign;
            sb.Append("\n[XPC-4CC][SOURCE_AUTH_SUMMARY] schema=").Append(SourceAuthRevision)
                .Append("; mode=SHADOW; eligible=").Append(eligible)
                .Append("; admissibleTick=").Append(admittedTick)
                .Append("; admissibleFinishNested=").Append(admittedFinish)
                .Append("; blocked=").Append(blocked)
                .Append("; insufficient=").Append(insufficient)
                .Append("; foreign=").Append(foreign)
                .Append("; policyErrors=").Append(errors)
                .Append("; invariant=").Append(invariant ? "PASS" : "FAIL")
                .Append("; patterns=").Append(counts.Count)
                .Append("; exampleRows=").Append(examples)
                .Append("; startPatchSnapshot=").Append(sourceAuthStartPatchReason)
                .Append("; stopPatchAudit=").Append(stopPatchAudit)
                .Append("; jointPromotions=").Append(OfficialBillPromotionCount())
                .Append("; officialUsage=").Append(officialBillSeal)
                .Append("; gameplayWrites=0; newHarmonyPatches=0")
                .Append("; observedSubsetOnly=True; retainedRegularOnly=True");
        }

        // Detached fixtures exercise exactly the runtime policy, no game objects.
        private static void VerifySourceAuthorizationSelfAudit()
        {
            int checks = 0;
            var b = new StartState { RegularSlot = true, Category = "WORK_CANDIDATE",
                Job = "DoBill", Skill = "Crafting", Input = 10f };
            var p = new SourceProofEvidence {
                AssemblyMatchedAtEvent = true, BillTickResolvedAtEvent = true,
                BillFinishResolvedAtEvent = true, SkillTrackerResolvedAtEvent = true,
                TraceAttempted = true, Frames = 12, Context = "CONTEXT_MATCH",
                Driver = "JobDriver_DoBill", Recipe = "Make_Apparel_Duster",
                RecipeSkill = "Crafting", RecipeUsesUnfinished = "True",
                RouteMask = SourceBillTickBit, BillTickDepth = 3, SkillTrackerDepth = 2,
                Status = "CALLSITE_MATCH_CANDIDATE", Route = "BILL_UNFINISHED_TICK",
                AuthPatchAuditComplete = true, AuthPatchReason = "NO_FOREIGN_OR_PATCH_DRIFT_DETECTED"
            };
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "SHADOW_ADMISSIBLE_TICK", "CRAFT_TICK", ref checks);
            b.Skill = "Cooking"; p.RecipeSkill = "Cooking";
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "SHADOW_ADMISSIBLE_TICK", "COOK_TICK", ref checks);
            p.RecipeUsesUnfinished = "False";
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "BLOCKED_RECIPE_ROUTE", "TICK_WRONG_FLAG", ref checks);
            p.RecipeUsesUnfinished = "True";
            p.RouteMask = 3; p.BillTickDepth = 7; p.BillFinishDepth = 2;
            p.SkillTrackerDepth = -1; p.Status = "CALLSITE_AMBIGUOUS"; p.Route = "NONE_OR_MULTIPLE";
            p.RecipeUsesUnfinished = "False";
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "SHADOW_ADMISSIBLE_FINISH_NESTED", "COOK_NESTED_FINISH", ref checks);
            p.RouteMask = SourceBillFinishBit; p.BillTickDepth = -1;
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "INSUFFICIENT_FINISH_ONLY", "FINISH_ONLY_UNPROVEN", ref checks);
            p.RouteMask = 3; p.BillTickDepth = 7; p.RecipeUsesUnfinished = "True";
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "BLOCKED_RECIPE_ROUTE", "FINISH_ON_UNFINISHED", ref checks);
            p.RecipeUsesUnfinished = "False";
            p.BillFinishDepth = 8;
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "BLOCKED_TOPOLOGY", "FINISH_OUTER", ref checks);
            p.BillFinishDepth = 7;
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "BLOCKED_TOPOLOGY", "DEPTH_TIE", ref checks);
            p.BillFinishDepth = 2; p.SkillTrackerDepth = 1;
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "BLOCKED_TOPOLOGY", "NESTED_TRACKER_PRESENT", ref checks);
            p.SkillTrackerDepth = -1; p.RouteMask = 7; p.TendDepth = 4;
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "BLOCKED_FOREIGN_SOURCE", "TEND_INTRUSION", ref checks);
            p.RouteMask = 11; p.TendDepth = -1; p.SocialDepth = 4;
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "BLOCKED_FOREIGN_SOURCE", "SOCIAL_INTRUSION", ref checks);
            p.RouteMask = 3; p.SocialDepth = -1;
            p.AuthPatchForeign = true;
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "FOREIGN_PATCH", "FOREIGN_BEFORE_AMBIGUITY", ref checks);
            p.AuthPatchForeign = false; p.AuthPatchChanged = true;
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "INSUFFICIENT_PATCH_DRIFT", "OWNER_CHANGED", ref checks);
            p.AuthPatchChanged = false; p.AuthPatchAuditComplete = false;
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "INSUFFICIENT_PATCH_AUDIT", "PATCH_LOOKUP_FAILED", ref checks);
            p.AuthPatchAuditComplete = false; p.AuthPatchForeign = true;
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "FOREIGN_PATCH",
                "FOREIGN_OVER_INCOMPLETE_AUDIT", ref checks);
            p.AuthPatchForeign = false; p.AuthPatchAuditComplete = true;
            p.AssemblyMatchedAtEvent = false;
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "INSUFFICIENT_ASSEMBLY", "MVID_MISMATCH", ref checks);
            p.AssemblyMatchedAtEvent = true; p.BillFinishResolvedAtEvent = false;
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "INSUFFICIENT_CALLBACKS", "CALLBACK_MISSING", ref checks);
            p.BillFinishResolvedAtEvent = true; p.TraceAttempted = false;
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "INSUFFICIENT_TRACE", "NO_STACK", ref checks);
            p.TraceAttempted = true; p.Frames = 0;
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "INSUFFICIENT_TRACE", "NO_FRAMES", ref checks);
            p.Frames = 12; p.Context = "CONTEXT_CONFLICT";
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "BLOCKED_CONTEXT", "RECIPE_CONTEXT_CONFLICT", ref checks);
            p.Context = "CONTEXT_MATCH"; p.RecipeSkill = "Social";
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "BLOCKED_CONTEXT", "WRONG_RECIPE_SKILL", ref checks);
            p.RecipeSkill = "Cooking"; p.Driver = "JobDriver_TendPatient";
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "BLOCKED_CONTEXT", "DRIVER_MISMATCH", ref checks);
            p.Driver = "JobDriver_DoBill"; p.RecipeUsesUnfinished = "UNKNOWN";
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "INSUFFICIENT_RECIPE_FLAG", "UNKNOWN_RECIPE_FLAG", ref checks);
            p.RecipeUsesUnfinished = "False"; p.RouteMask = 3; p.BillTickDepth = -1;
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "BLOCKED_TOPOLOGY", "MASK_DEPTH_MISMATCH", ref checks);
            p.BillTickDepth = 7; p.RouteMask = 3; p.BillFinishDepth = 2;
            b.Direct = true;
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "BLOCKED_FLAGS", "DIRECT_EXCLUDED", ref checks);
            b.Direct = false; b.IgnoreLearnRate = true;
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "BLOCKED_FLAGS", "IGNORE_RATE_EXCLUDED", ref checks);
            b.IgnoreLearnRate = false; b.Input = float.NaN;
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "BLOCKED_INPUT", "NAN_EXCLUDED", ref checks);
            b.Input = 0f;
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "BLOCKED_INPUT", "ZERO_EXCLUDED", ref checks);
            b.Input = 10f; b.Skill = "Social";
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "OUT_OF_SCOPE", "SOCIAL_EXCLUDED", ref checks);
            b.Skill = "Medicine"; b.Job = "TendPatient";
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "OUT_OF_SCOPE", "MEDICINE_B1_EXCLUDED", ref checks);
            b.Skill = "Cooking"; b.Job = "DoBill"; b.RegularSlot = false;
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "OUT_OF_SCOPE", "RESERVED_EXCLUDED", ref checks);
            b.RegularSlot = true; b.Category = "READING";
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "OUT_OF_SCOPE", "READING_EXCLUDED", ref checks);
            b.Category = "WORK_CANDIDATE"; p.AuthPatchForeign = true; p.AuthPatchChanged = true;
            SourceAuthRequire(AuthorizeBillSourceShadow(b, p).Status == "FOREIGN_PATCH", "FOREIGN_OVER_CHANGED", ref checks);
            Log.Message("[XPC-4CC][SOURCE_AUTH_AUDIT] schema=" + SourceAuthRevision +
                "; status=PASS; cases=" + checks + "; gameplayWrites=0; newHarmonyPatches=0" +
                "; jointPromotions=0; mode=SHADOW; build=" + XpCustomizerMod.BuildId);
        }

        private static void SourceAuthRequire(bool condition, string name, ref int checks)
        {
            if (!condition)
                throw new InvalidOperationException("SourceAuth_B1A_" + name);
            checks++;
        }
    }
}
#endif
