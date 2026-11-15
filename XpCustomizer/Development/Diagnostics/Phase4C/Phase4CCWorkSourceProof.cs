#if DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace XpCustomizer
{
    /// <summary>
    /// T04-A/B0: passive, bounded CALLSITE CANDIDATE evidence gathered only for
    /// regular, already-selected 4C-C samples. No XP writes or extra Harmony
    /// patches. Even a matching stack remains evidence, NOT an authorization
    /// to upgrade JOINT_SKIP to JOINT_PASS (reserved for T04-B/C).
    /// </summary>
    internal static partial class Phase4CCLiveXpCapture
    {
        private const string SourceProofRevision = "4C-C-SOURCE-PROOF-SHADOW-T04-B0-R1";
        private const int MaxSourceProofRows = 12;

        private sealed class SourceProofEvidence
        {
            internal string Status = "CALLSITE_UNAVAILABLE";
            internal string Context = "CONTEXT_UNAVAILABLE";
            internal string Route = "NONE";
            internal string Recipe = "NONE";
            internal string RecipeSkill = "NONE";
            internal string Driver = "NONE";
            internal string Reason = "NOT_OBSERVED";
            internal string PatchOwners = "NONE";
            internal string RecipeUsesUnfinished = "UNKNOWN";
            internal int Frames;
            internal bool TraceAttempted;
            // First (nearest to Learn) frame depth for each distinct source.
            // -1 means not seen. Repeated identical frames count only once.
            internal int RouteMask;
            internal int BillTickDepth = -1;
            internal int BillFinishDepth = -1;
            internal int TendDepth = -1;
            internal int SocialDepth = -1;
            // T04-B1-A: read-only event-time authentication evidence. Legacy
            // SourceProof.Status, Route, PatchOwners, and JOINT remain untouched.
            internal bool AssemblyMatchedAtEvent;
            internal bool BillTickResolvedAtEvent;
            internal bool BillFinishResolvedAtEvent;
            internal bool SkillTrackerResolvedAtEvent;
            internal int SkillTrackerDepth = -1;
            internal bool AuthPatchAuditComplete;
            internal bool AuthPatchForeign;
            internal bool AuthPatchChanged;
            internal string AuthPatchReason = "NOT_AUDITED";
            internal string AuthPatchOwners = "NOT_AUDITED";
        }

        // B0 pattern aggregation is bounded by MaxSourceProofTraces. Unlike
        // SOURCE_SAMPLE's 12 examples it retains every observed combination.
        private sealed class SourceTracePattern
        {
            internal string Skill;
            internal string Status;
            internal int Mask;
            internal int Count;
            internal int BillTickMin = -1, BillTickMax = -1;
            internal int BillFinishMin = -1, BillFinishMax = -1;
            internal int TendMin = -1, TendMax = -1;
            internal int SocialMin = -1, SocialMax = -1;

            internal void Add(SourceProofEvidence p)
            {
                Count++;
                RecordDepthRange(ref BillTickMin, ref BillTickMax, p.BillTickDepth);
                RecordDepthRange(ref BillFinishMin, ref BillFinishMax, p.BillFinishDepth);
                RecordDepthRange(ref TendMin, ref TendMax, p.TendDepth);
                RecordDepthRange(ref SocialMin, ref SocialMax, p.SocialDepth);
            }
        }


        private static bool IsSourceProofEligible(StartState b)
        {
            if (b == null || !b.RegularSlot || b.Category != "WORK_CANDIDATE" ||
                b.Input <= 0f || b.Direct || b.IgnoreLearnRate || b.FromInterval ||
                b.ReadingScope || (b.TotallyDisabled && b.Skill != "Social")) return false;
            if (b.Job == "DoBill")
                return b.Skill == "Cooking" || b.Skill == "Crafting" || b.Skill == "Social";
            return b.Job == "TendPatient" && b.Skill == "Medicine";
        }


        // No method in this evaluator grants actual JOINT/Payout/Rate eligibility.
        // A stack MATCH is just a B/C input candidate.
        private static string DecideSourceProof(string job, string skill, string context,
            string route, int matches, bool foreign, bool assemblyMatched)
        {
            if (!assemblyMatched) return "UNSUPPORTED_GAME_ASSEMBLY";
            if (matches > 1) return "CALLSITE_AMBIGUOUS";
            if (context == "CONTEXT_CONFLICT") return "CONTEXT_CONFLICT";
            if (context != "CONTEXT_MATCH") return "CONTEXT_UNAVAILABLE";
            if (matches == 0) return "CALLSITE_UNAVAILABLE";
            if (foreign) return "CALLSITE_FOREIGN";
            // A callback from a different job family is a conflict, even if
            // current job context happens to match. In particular DoBill+Social
            // must NEVER be certified as bill work XP.
            if (job == "DoBill" && (skill == "Cooking" || skill == "Crafting") &&
                (route == "BILL_UNFINISHED_TICK" || route == "BILL_FINISH"))
                return "CALLSITE_MATCH_CANDIDATE";
            if (job == "TendPatient" && skill == "Medicine" && route == "TEND_FINALIZE")
                return "CALLSITE_MATCH_CANDIDATE";
            return "CONTEXT_CONFLICT";
        }

        private static void CaptureSourceContext(StartState b, SourceProofEvidence proof)
        {
            Pawn pawn = b.Pawn;
            if (pawn == null || pawn.CurJob == null || pawn.jobs == null)
            {
                proof.Context = "CONTEXT_UNAVAILABLE";
                return;
            }
            object driver = ReadSourceMember(pawn.jobs, "curDriver");
            proof.Driver = driver == null ? "NONE" : driver.GetType().Name;
            if (driver == null) { proof.Context = "CONTEXT_UNAVAILABLE"; return; }
            if (b.Job == "DoBill")
            {
                if (proof.Driver != "JobDriver_DoBill")
                {
                    proof.Context = "CONTEXT_CONFLICT";
                    return;
                }
                object bill = ReadSourceMember(pawn.CurJob, "bill");
                object recipe = bill == null ? null : ReadSourceMember(bill, "recipe");
                if (recipe == null) { proof.Context = "CONTEXT_UNAVAILABLE"; return; }
                object skill = ReadSourceMember(recipe, "workSkill");
                proof.Recipe = CleanSourceValue(ReadSourceMember(recipe, "defName"));
                proof.RecipeSkill = CleanSourceValue(ReadSourceMember(skill, "defName"));
                // Context only: never use the recipe flag to authorize a callsite.
                // A missing/throwing accessor must not invalidate prior evidence.
                try
                {
                    object unfinished = ReadSourceMember(recipe, "UsesUnfinishedThing");
                    if (unfinished is bool)
                        proof.RecipeUsesUnfinished = (bool)unfinished ? "True" : "False";
                }
                catch { proof.RecipeUsesUnfinished = "UNKNOWN"; }
                proof.Context = proof.RecipeSkill == "NONE" ? "CONTEXT_UNAVAILABLE" :
                    proof.RecipeSkill == b.Skill ? "CONTEXT_MATCH" : "CONTEXT_CONFLICT";
                return;
            }
            if (b.Job == "TendPatient")
            {
                if (proof.Driver != "JobDriver_TendPatient")
                {
                    proof.Context = "CONTEXT_CONFLICT";
                    return;
                }
                // The patient is target A in the surveyed vanilla JobDriver.
                Pawn patient = pawn.CurJob.GetTarget(TargetIndex.A).Thing as Pawn;
                proof.Context = patient == null ? "CONTEXT_UNAVAILABLE" :
                    b.Skill == "Medicine" ? "CONTEXT_MATCH" : "CONTEXT_CONFLICT";
            }
        }

        private static object ReadSourceMember(object obj, string member)
        {
            if (obj == null) return null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = obj.GetType();
            for (; type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(member, flags | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(obj);
                PropertyInfo prop = type.GetProperty(member, flags | BindingFlags.DeclaredOnly);
                if (prop != null && prop.GetIndexParameters().Length == 0)
                    return prop.GetValue(obj, null);
            }
            return null;
        }

        private static string CleanSourceValue(object raw)
        {
            return raw == null ? "NONE" : Clean(raw.ToString());
        }

        private static void AppendSourceProofReport(StringBuilder sb)
        {
            int retained = 0, traced = 0, candidates = 0, unavailable = 0, conflict = 0;
            int ambiguous = 0, foreign = 0, wrongAssembly = 0, budget = 0, errors = 0;
            var distinctExamples = new List<Sample>();
            var patterns = new List<SourceTracePattern>();
            int patternSampleTotal = 0;
            foreach (Sample sample in samples)
            {
                SourceProofEvidence p = sample.Before.SourceProof;
                if (p == null) continue;
                retained++;
                if (p.TraceAttempted)
                {
                    traced++;
                    SourceTracePattern pattern = null;
                    foreach (SourceTracePattern group in patterns)
                    {
                        if (group.Skill == sample.Before.Skill && group.Mask == p.RouteMask &&
                            group.Status == p.Status) { pattern = group; break; }
                    }
                    if (pattern == null)
                    {
                        pattern = new SourceTracePattern {
                            Skill = sample.Before.Skill, Status = p.Status, Mask = p.RouteMask
                        };
                        patterns.Add(pattern);
                    }
                    pattern.Add(p);
                    patternSampleTotal++;
                }
                if (p.Status == "CALLSITE_MATCH_CANDIDATE") candidates++;
                else if (p.Status == "CALLSITE_FOREIGN") foreign++;
                else if (p.Status == "CALLSITE_AMBIGUOUS") ambiguous++;
                else if (p.Status == "CONTEXT_CONFLICT") conflict++;
                else if (p.Status == "UNSUPPORTED_GAME_ASSEMBLY") wrongAssembly++;
                else if (p.Status == "BUDGET_EXHAUSTED") budget++;
                else if (p.Status == "OBSERVATION_ERROR") errors++;
                else unavailable++;
                // Distinct source masks must not be hidden under NONE_OR_MULTIPLE.
                // Capture representatives without a row limit; choose at most 12
                // at report time, with ambiguous combinations prioritized.
                bool duplicate = false;
                foreach (Sample existing in distinctExamples)
                {
                    SourceProofEvidence old = existing.Before.SourceProof;
                    if (existing.Before.Skill == sample.Before.Skill &&
                        old.Status == p.Status && old.Route == p.Route &&
                        old.RouteMask == p.RouteMask) { duplicate = true; break; }
                }
                if (!duplicate) distinctExamples.Add(sample);
            }
            var examples = new List<Sample>();
            foreach (Sample sample in distinctExamples)
            {
                if (sample.Before.SourceProof.Status == "CALLSITE_AMBIGUOUS" &&
                    examples.Count < MaxSourceProofRows) examples.Add(sample);
            }
            foreach (Sample sample in distinctExamples)
            {
                if (sample.Before.SourceProof.Status != "CALLSITE_AMBIGUOUS" &&
                    examples.Count < MaxSourceProofRows) examples.Add(sample);
            }
            // No new Harmony owner was installed, no JOINT eligibility was changed.
            sb.Append("\n[XPC-4CC][SOURCE_PROOF_SUMMARY] schema=").Append(SourceProofRevision)
                .Append("; mode=SHADOW; assemblyMvid=").Append(sourceRuntimeMvid)
                .Append("; p0Mvid=").Append(P0GameMvid)
                .Append("; assemblyMatched=").Append(sourceGameMvidMatches)
                .Append("; callbacks=").Append(sourceCallbackAvailability)
                .Append("; foreignBillTick=").Append(sourceBillTickForeign)
                .Append("; foreignBillFinish=").Append(sourceBillFinishForeign)
                .Append("; foreignTend=").Append(sourceTendForeign)
                .Append("; foreignSocial=").Append(sourceSocialForeign)
                .Append("; foreignSkillTracker=").Append(sourceSkillTrackerForeign)
                .Append("; retainedEligible=").Append(retained)
                .Append("; tracedRetained=").Append(traced)
                .Append("; attemptsTotal=").Append(sourceProofTraces)
                .Append("; matchedCandidates=").Append(candidates)
                .Append("; unavailable=").Append(unavailable)
                .Append("; conflict=").Append(conflict)
                .Append("; ambiguous=").Append(ambiguous)
                .Append("; foreign=").Append(foreign)
                .Append("; unsupported=").Append(wrongAssembly)
                .Append("; budgetDrops=").Append(budget)
                .Append("; observationErrorsRetained=").Append(errors)
                .Append("; observationErrorsTotal=").Append(sourceProofErrors)
                .Append("; traceCostMs=").Append(
                    ((double)sourceProofElapsedTicks * 1000d / Stopwatch.Frequency).ToString("0.###",
                        System.Globalization.CultureInfo.InvariantCulture))
                .Append("; proofHealth=").Append(sourceProofErrors != 0 ? "CHECK_ERROR" :
                    !sourceGameMvidMatches ? "DISABLED" : "SHADOW_OBSERVATION")
                .Append("; traceCostScope=STACK_ONLY")
                .Append("; sourceTraceSchema=").Append(SourceTraceRevision)
                .Append("; tracePatternGroups=").Append(patterns.Count)
                .Append("; tracePatternSamples=").Append(patternSampleTotal)
                .Append("; tracePatternInvariant=").Append(patternSampleTotal == traced &&
                    patterns.Count <= MaxSourceProofTraces ? "PASS" : "FAIL")
                .Append("; sourceProofDirectPromotions=0; officialBillPromotions=")
                .Append(OfficialBillPromotionCount())
                .Append("; proofIsAuthorization=False")
                .Append("; evaluated=RETAINED_REGULAR_SAMPLES_ONLY")
                .Append("; quotaPolicy=UNCHANGED");
            // At most 24 distinct patterns because only 24 traces are allowed.
            // Every trace is counted, including source masks not chosen as examples.
            foreach (SourceTracePattern pattern in patterns)
            {
                sb.Append("\n[XPC-4CC][SOURCE_TRACE_PATTERN] schema=").Append(SourceTraceRevision)
                    .Append("; skill=").Append(pattern.Skill)
                    .Append("; sourceStatus=").Append(pattern.Status)
                    .Append("; routeMask=").Append(SourceRouteMaskLabel(pattern.Mask))
                    .Append("; matches=").Append(SourceRouteCount(pattern.Mask))
                    .Append("; count=").Append(pattern.Count)
                    .Append("; billTickSeen=").Append((pattern.Mask & SourceBillTickBit) != 0)
                    .Append("; billFinishSeen=").Append((pattern.Mask & SourceBillFinishBit) != 0)
                    .Append("; tendSeen=").Append((pattern.Mask & SourceTendBit) != 0)
                    .Append("; socialSeen=").Append((pattern.Mask & SourceSocialBit) != 0)
                    .Append("; billTickDepth=").Append(SourceDepthRange(pattern.BillTickMin, pattern.BillTickMax))
                    .Append("; billFinishDepth=").Append(SourceDepthRange(pattern.BillFinishMin, pattern.BillFinishMax))
                    .Append("; tendDepth=").Append(SourceDepthRange(pattern.TendMin, pattern.TendMax))
                    .Append("; socialDepth=").Append(SourceDepthRange(pattern.SocialMin, pattern.SocialMax))
                    .Append("; verdictChanged=False");
            }
            foreach (Sample sample in examples)
            {
                SourceProofEvidence p = sample.Before.SourceProof;
                sb.Append("\n[XPC-4CC][SOURCE_SAMPLE] pawn=").Append(sample.Before.PawnName)
                    .Append("; job=").Append(sample.Before.Job)
                    .Append("; skill=").Append(sample.Before.Skill)
                    .Append("; context=").Append(p.Context)
                    .Append("; driver=").Append(p.Driver)
                    .Append("; recipe=").Append(p.Recipe)
                    .Append("; recipeSkill=").Append(p.RecipeSkill)
                    .Append("; recipeUsesUnfinished=").Append(p.RecipeUsesUnfinished)
                    .Append("; callsite=").Append(p.Route)
                    .Append("; routeMask=").Append(SourceRouteMaskLabel(p.RouteMask))
                    .Append("; routeCount=").Append(SourceRouteCount(p.RouteMask))
                    .Append("; billTickSeen=").Append((p.RouteMask & SourceBillTickBit) != 0)
                    .Append("; billFinishSeen=").Append((p.RouteMask & SourceBillFinishBit) != 0)
                    .Append("; tendSeen=").Append((p.RouteMask & SourceTendBit) != 0)
                    .Append("; socialSeen=").Append((p.RouteMask & SourceSocialBit) != 0)
                    .Append("; billTickDepth=").Append(p.BillTickDepth)
                    .Append("; billFinishDepth=").Append(p.BillFinishDepth)
                    .Append("; tendDepth=").Append(p.TendDepth)
                    .Append("; socialDepth=").Append(p.SocialDepth)
                    .Append("; nearestObservedSource=").Append(NearestObservedSource(p))
                    .Append("; sourceStatus=").Append(p.Status)
                    .Append("; reason=").Append(p.Reason)
                    .Append("; callbackPatchOwners=").Append(p.PatchOwners)
                    .Append("; inspectedFrames=").Append(p.Frames)
                    .Append("; officialJoint=").Append(JointClass(sample))
                    .Append("; legacyJoint=").Append(JointClassFromResults(
                        PayoutClassCore(sample, false), IndependentRateClassCore(sample, false)))
                    .Append("; sourceEvidenceGrade=").Append(OfficialBillEvidenceGrade(sample));
            }
            if (retained > examples.Count)
                sb.Append("\n[XPC-4CC][SOURCE_SAMPLE_TRUNCATED] shown=").Append(examples.Count)
                    .Append("; retainedEligible=").Append(retained);
        }

        private static void VerifySourceProofSelfAudit()
        {
            int checks = 0;
            RequireSourceProof(DecideSourceProof("DoBill", "Crafting", "CONTEXT_MATCH", "BILL_FINISH", 1, false, true) ==
                "CALLSITE_MATCH_CANDIDATE", "BILL_FINISH", ref checks);
            RequireSourceProof(DecideSourceProof("DoBill", "Crafting", "CONTEXT_MATCH", "BILL_UNFINISHED_TICK", 1, false, true) ==
                "CALLSITE_MATCH_CANDIDATE", "BILL_TICK", ref checks);
            RequireSourceProof(DecideSourceProof("TendPatient", "Medicine", "CONTEXT_MATCH", "TEND_FINALIZE", 1, false, true) ==
                "CALLSITE_MATCH_CANDIDATE", "TEND", ref checks);
            RequireSourceProof(DecideSourceProof("DoBill", "Crafting", "CONTEXT_CONFLICT", "BILL_FINISH", 1, false, true) ==
                "CONTEXT_CONFLICT", "WRONG_SKILL", ref checks);
            RequireSourceProof(DecideSourceProof("DoBill", "Crafting", "CONTEXT_UNAVAILABLE", "BILL_FINISH", 1, false, true) ==
                "CONTEXT_UNAVAILABLE", "MISSING_RECIPE", ref checks);
            RequireSourceProof(DecideSourceProof("DoBill", "Crafting", "CONTEXT_MATCH", "NONE_OR_MULTIPLE", 0, false, true) ==
                "CALLSITE_UNAVAILABLE", "MISSING_STACK", ref checks);
            RequireSourceProof(DecideSourceProof("DoBill", "Crafting", "CONTEXT_MATCH", "NONE_OR_MULTIPLE", 2, false, true) ==
                "CALLSITE_AMBIGUOUS", "MULTIPLE_FRAMES", ref checks);
            RequireSourceProof(DecideSourceProof("DoBill", "Crafting", "CONTEXT_MATCH", "BILL_FINISH", 1, true, true) ==
                "CALLSITE_FOREIGN", "FOREIGN_CALLBACK", ref checks);
            RequireSourceProof(DecideSourceProof("DoBill", "Crafting", "CONTEXT_MATCH", "BILL_FINISH", 1, false, false) ==
                "UNSUPPORTED_GAME_ASSEMBLY", "MVID_MISMATCH", ref checks);
            RequireSourceProof(DecideSourceProof("DoBill", "Social", "CONTEXT_MATCH", "INTERACTION", 1, false, true) ==
                "CONTEXT_CONFLICT", "SOCIAL_NOT_BILL", ref checks);
            RequireSourceProof(DecideSourceProof("DoBill", "Cooking", "CONTEXT_MATCH",
                "TEND_FINALIZE", 1, false, true) == "CONTEXT_CONFLICT",
                "BILL_CANNOT_USE_TEND", ref checks);
            RequireSourceProof(DecideSourceProof("TendPatient", "Medicine", "CONTEXT_MATCH",
                "BILL_FINISH", 1, false, true) == "CONTEXT_CONFLICT",
                "TEND_CANNOT_USE_BILL", ref checks);
            RequireSourceProof(DecideSourceProof("DoBill", "Social", "CONTEXT_MATCH",
                "BILL_FINISH", 1, false, true) == "CONTEXT_CONFLICT",
                "SOCIAL_CANNOT_BE_BILL", ref checks);
            RequireSourceProof(!IsSourceProofEligible(new StartState {
                Job = "DoBill", Skill = "Crafting", Category = "WORK_CANDIDATE",
                Input = 2f, RegularSlot = false
            }), "NO_RESERVED_ONLY", ref checks);
            RequireSourceProof(!IsSourceProofEligible(new StartState {
                Job = "DoBill", Skill = "Artistic", Category = "WORK_CANDIDATE",
                Input = 2f, RegularSlot = true
            }), "NO_UNSURVEYED_SKILLS", ref checks);
            RequireSourceProof(!IsSourceProofEligible(new StartState {
                Job = "TendEntity", Skill = "Medicine", Category = "WORK_CANDIDATE",
                Input = 250f, RegularSlot = true
            }), "TEND_ENTITY_NOT_PATIENT", ref checks);
            RequireSourceProof(!IsSourceProofEligible(new StartState {
                Job = "DoBill", Skill = "Crafting", Category = "READING",
                Input = 1f, RegularSlot = true
            }), "READING_ISOLATION", ref checks);
            RequireSourceProof(IsSourceProofEligible(new StartState {
                Job = "DoBill", Skill = "Social", Category = "WORK_CANDIDATE",
                Input = 4f, RegularSlot = true
            }), "SOCIAL_NEGATIVE_CONTROL", ref checks);
            RequireSourceProof(IsSourceProofEligible(new StartState {
                Job = "TendPatient", Skill = "Medicine", Category = "WORK_CANDIDATE",
                Input = 250f, RegularSlot = true
            }), "MEDICINE_POSITIVE_CONTEXT", ref checks);
            RequireSourceProof(!IsSourceProofEligible(new StartState {
                Job = "DoBill", Skill = "Cooking", Category = "WORK_CANDIDATE",
                Input = 1f, RegularSlot = true, Direct = true
            }), "NO_DIRECT", ref checks);
            RequireSourceProof(!IsSourceProofEligible(new StartState {
                Job = "DoBill", Skill = "Cooking", Category = "WORK_CANDIDATE",
                Input = 1f, RegularSlot = true, IgnoreLearnRate = true
            }), "NO_IGNORE_RATE", ref checks);
            RequireSourceProof(IsSourceProofEligible(new StartState {
                Job = "DoBill", Skill = "Social", Category = "WORK_CANDIDATE",
                Input = 4f, RegularSlot = true, TotallyDisabled = true
            }), "SOCIAL_DISABLED_NEGATIVE_CONTROL", ref checks);
            // T04-B0 pure route-mask/depth invariants. Does not simulate
            // StackTrace or change the Shadow evaluator's decisions.
            RequireSourceProof(SourceRouteCount(0) == 0 && SourceRouteMaskLabel(0) == "NONE",
                "B0_EMPTY_MASK", ref checks);
            RequireSourceProof(SourceRouteCount(SourceBillTickBit) == 1 &&
                SourceRouteMaskLabel(SourceBillTickBit) == "BILL_TICK", "B0_TICK_MASK", ref checks);
            RequireSourceProof(SourceRouteCount(SourceBillFinishBit) == 1 &&
                SourceRouteMaskLabel(SourceBillFinishBit) == "BILL_FINISH", "B0_FINISH_MASK", ref checks);
            RequireSourceProof(SourceRouteCount(SourceTendBit) == 1 &&
                SourceRouteMaskLabel(SourceTendBit) == "TEND", "B0_TEND_MASK", ref checks);
            RequireSourceProof(SourceRouteCount(SourceSocialBit) == 1 &&
                SourceRouteMaskLabel(SourceSocialBit) == "SOCIAL", "B0_SOCIAL_MASK", ref checks);
            RequireSourceProof(SourceRouteCount(3) == 2 &&
                SourceRouteMaskLabel(3) == "BILL_TICK|BILL_FINISH", "B0_BILL_PAIR", ref checks);
            RequireSourceProof(SourceRouteCount(7) == 3 &&
                SourceRouteMaskLabel(7) == "BILL_TICK|BILL_FINISH|TEND", "B0_TRIPLE", ref checks);
            RequireSourceProof(SourceRouteCount(15) == 4 &&
                SourceRouteMaskLabel(15) == "BILL_TICK|BILL_FINISH|TEND|SOCIAL", "B0_ALL_FOUR", ref checks);
            var maskForward = new SourceProofEvidence();
            ObserveSourceFrame(maskForward, SourceBillTickBit, 7);
            ObserveSourceFrame(maskForward, SourceBillFinishBit, 3);
            RequireSourceProof(maskForward.RouteMask == 3 &&
                NearestObservedSource(maskForward) == "BILL_FINISH@3", "B0_NEAREST_FINISH", ref checks);
            var maskReverse = new SourceProofEvidence();
            ObserveSourceFrame(maskReverse, SourceBillFinishBit, 3);
            ObserveSourceFrame(maskReverse, SourceBillTickBit, 7);
            RequireSourceProof(maskReverse.RouteMask == maskForward.RouteMask &&
                maskReverse.BillTickDepth == maskForward.BillTickDepth &&
                maskReverse.BillFinishDepth == maskForward.BillFinishDepth,
                "B0_REVERSE_ORDER", ref checks);
            ObserveSourceFrame(maskForward, SourceBillTickBit, 11);
            ObserveSourceFrame(maskForward, SourceBillTickBit, 4);
            RequireSourceProof(maskForward.RouteMask == 3 && maskForward.BillTickDepth == 4,
                "B0_REPEAT_SAME_SOURCE_NEAREST", ref checks);
            ObserveSourceFrame(maskForward, SourceSocialBit, MaxSourceProofFrames);
            ObserveSourceFrame(maskForward, SourceSocialBit, -1);
            RequireSourceProof(maskForward.RouteMask == 3 && maskForward.SocialDepth == -1,
                "B0_BOUNDED_48", ref checks);
            ObserveSourceFrame(maskForward, SourceTendBit, MaxSourceProofFrames - 1);
            RequireSourceProof(maskForward.RouteMask == 7 &&
                maskForward.TendDepth == 47 && SourceRouteCount(maskForward.RouteMask) == 3,
                "B0_LAST_VALID_FRAME", ref checks);
            var tiedMask = new SourceProofEvidence();
            ObserveSourceFrame(tiedMask, SourceBillTickBit, 4);
            ObserveSourceFrame(tiedMask, SourceSocialBit, 4);
            RequireSourceProof(NearestObservedSource(tiedMask) == "TIED@4",
                "B0_TIED_DEPTH_NO_AUTHORIZATION", ref checks);
            int min = -1, max = -1;
            RecordDepthRange(ref min, ref max, -1);
            RecordDepthRange(ref min, ref max, 8);
            RecordDepthRange(ref min, ref max, 4);
            RecordDepthRange(ref min, ref max, 11);
            RequireSourceProof(min == 4 && max == 11 && SourceDepthRange(min, max) == "4..11" &&
                SourceDepthRange(-1, -1) == "NONE", "B0_PATTERN_DEPTH_RANGE", ref checks);
            var maskPattern = new SourceProofEvidence();
            ObserveSourceFrame(maskPattern, SourceBillTickBit, 4);
            ObserveSourceFrame(maskPattern, SourceBillFinishBit, 3);
            var pattern = new SourceTracePattern { Skill = "Cooking", Status = "CALLSITE_AMBIGUOUS", Mask = 3 };
            pattern.Add(maskPattern);
            pattern.Add(maskReverse);
            RequireSourceProof(pattern.Count == 2 && pattern.BillTickMin == 4 &&
                pattern.BillTickMax == 7 && pattern.BillFinishMin == 3 &&
                pattern.BillFinishMax == 3 && pattern.SocialMin == -1,
                "B0_PATTERN_GROUP_ACCUMULATION", ref checks);
            RequireSourceProof(DecideSourceProof("DoBill", "Cooking", "CONTEXT_MATCH",
                "NONE_OR_MULTIPLE", 2, false, true) == "CALLSITE_AMBIGUOUS",
                "B0_AMBIGUOUS_REMAINS_CLOSED", ref checks);
            RequireSourceProof(!IsSourceProofEligible(new StartState {
                Job = "DoBill", Skill = "Cooking", Category = "WORK_CANDIDATE",
                Input = 1f, RegularSlot = true, FromInterval = true
            }), "B0_DECAY_ISOLATION", ref checks);
            Log.Message("[XPC-4CC][SOURCE_PROOF_AUDIT] status=PASS; cases=" + checks +
                "; schema=" + SourceProofRevision +
                "; proofPolicy=SHADOW_ONLY; promotedJointPass=0" +
                "; fixture=SOURCE_ROUTE_AND_FAIL_CLOSED_PLUS_TRACE_PATTERN_B0" +
                "; runtimeStackVisibility=NOT_TESTED; gameplayWrites=0; newHarmonyPatches=0" +
                "; build=" + XpCustomizerMod.BuildId);
        }

        private static void RequireSourceProof(bool result, string name, ref int checks)
        {
            if (!result) throw new InvalidOperationException("T04-B0 source proof self-audit " + name);
            checks++;
        }
    }
}
#endif
