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
    /// T04-B1-PREP: unchanged source-trace state and observation functions moved
    /// from Phase4CCWorkSourceProof.cs. Same CLR partial type, same method bodies,
    /// same StackTrace(1, false) call site. No new Harmony patch or XP writes.
    /// </summary>
    internal static partial class Phase4CCLiveXpCapture
    {
        private const string SourceTraceRevision = "4C-C-SOURCE-TRACE-T04-B0-R1";
        // Evidence-only bits; they NEVER grant work-source authorization.
        private const int SourceBillTickBit = 1;
        private const int SourceBillFinishBit = 2;
        private const int SourceTendBit = 4;
        private const int SourceSocialBit = 8;
        private const string P0GameMvid = "61e41735-6189-4da4-9d21-0260257b5097";
        private const int MaxSourceProofTraces = 24;
        private const int MaxSourceProofTracesPerSkill = 6;
        private const int MaxSourceProofFrames = 48;

        private static readonly Dictionary<string, int> sourceProofPerSkill =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private static MethodBase sourceBillTickMethod;
        private static MethodBase sourceBillFinishMethod;
        private static MethodBase sourceTendMethod;
        private static MethodBase sourceSocialMethod;
        private static MethodBase sourceSkillTrackerLearnMethod;
        private static bool sourceBillTickForeign;
        private static bool sourceBillFinishForeign;
        private static bool sourceTendForeign;
        private static bool sourceSocialForeign;
        private static bool sourceSkillTrackerForeign;
        private static bool sourceGameMvidMatches;
        private static string sourceRuntimeMvid = "NOT_CHECKED";
        private static string sourceCallbackAvailability = "NOT_CHECKED";
        private static int sourceProofTraces;
        private static int sourceProofErrors;
        private static long sourceProofElapsedTicks;
        // B1-A: independent, untruncated patch signatures. The Learn signature
        // deliberately omits only our DEBUG observer, installed after Start.
        private static bool sourceAuthStartPatchReady;
        private static string sourceAuthStartPatchReason = "NOT_CAPTURED";
        private static string sourceAuthLearnStamp, sourceAuthTickStamp,
            sourceAuthFinishStamp, sourceAuthTrackerStamp,
            sourceAuthTendStamp, sourceAuthSocialStamp;

        private static void ResetSourceProofSession()
        {
            sourceProofPerSkill.Clear();
            sourceProofTraces = 0;
            sourceProofErrors = 0;
            sourceProofElapsedTicks = 0;
            sourceBillTickMethod = null;
            sourceBillFinishMethod = null;
            sourceTendMethod = null;
            sourceSocialMethod = null;
            sourceSkillTrackerLearnMethod = null;
            sourceBillTickForeign = sourceBillFinishForeign = sourceTendForeign = false;
            sourceSocialForeign = sourceSkillTrackerForeign = false;
            sourceRuntimeMvid = "UNAVAILABLE";
            sourceGameMvidMatches = false;
            sourceCallbackAvailability = "UNAVAILABLE";
            sourceAuthStartPatchReady = false;
            sourceAuthStartPatchReason = "NOT_CAPTURED";
            sourceAuthLearnStamp = sourceAuthTickStamp = sourceAuthFinishStamp = null;
            sourceAuthTrackerStamp = sourceAuthTendStamp = sourceAuthSocialStamp = null;
            ResetBillMathPreviewSession(); // B1-B0: prevent cross-session cached admission.
            try
            {
                // MVID is an admission gate, not proof that transpilers are absent.
                Assembly game = typeof(SkillRecord).Assembly;
                sourceRuntimeMvid = typeof(SkillRecord).Module.ModuleVersionId.ToString("D");
                sourceGameMvidMatches = string.Equals(sourceRuntimeMvid, P0GameMvid,
                    StringComparison.OrdinalIgnoreCase);
                if (!sourceGameMvidMatches)
                {
                    sourceCallbackAvailability = "DISABLED_MVID_MISMATCH";
                    return;
                }
                sourceBillTickMethod = LookupSourceCallback(game,
                    "Verse.AI.Toils_Recipe+<>c__DisplayClass2_0", "<DoRecipeWork>b__2");
                sourceBillFinishMethod = LookupSourceCallback(game,
                    "Verse.AI.Toils_Recipe+<>c__DisplayClass3_0", "<FinishRecipeAndStartStoringProduct>b__1");
                sourceTendMethod = LookupSourceCallback(game,
                    "RimWorld.Toils_Tend+<>c__DisplayClass3_0", "<FinalizeTend>b__0");
                sourceSocialMethod = LookupSourceCallback(game,
                    "RimWorld.Pawn_InteractionsTracker", "TryInteractWith");
                sourceSkillTrackerLearnMethod = LookupSkillTrackerLearn(game);
                sourceBillTickForeign = sourceBillTickMethod != null &&
                    HasForeignIntervalOwner(sourceBillTickMethod);
                sourceBillFinishForeign = sourceBillFinishMethod != null &&
                    HasForeignIntervalOwner(sourceBillFinishMethod);
                sourceTendForeign = sourceTendMethod != null &&
                    HasForeignIntervalOwner(sourceTendMethod);
                sourceSocialForeign = sourceSocialMethod != null &&
                    HasForeignIntervalOwner(sourceSocialMethod);
                sourceSkillTrackerForeign = sourceSkillTrackerLearnMethod == null ||
                    HasForeignIntervalOwner(sourceSkillTrackerLearnMethod);
                sourceCallbackAvailability = "BILL_TICK_" + (sourceBillTickMethod == null ? "MISSING" : "FOUND") +
                    "_BILL_FINISH_" + (sourceBillFinishMethod == null ? "MISSING" : "FOUND") +
                    "_TEND_" + (sourceTendMethod == null ? "MISSING" : "FOUND") +
                    "_SOCIAL_" + (sourceSocialMethod == null ? "MISSING" : "FOUND") +
                    "_SKILL_TRACKER_" + (sourceSkillTrackerLearnMethod == null ? "MISSING" : "FOUND");
                CaptureSourceAuthStartPatches();
            }
            catch (Exception ex)
            {
                sourceProofErrors++;
                sourceCallbackAvailability = "LOOKUP_ERROR_" + ex.GetType().Name;
                sourceGameMvidMatches = false;
            }
        }

        // B1-B0: full, fail-closed Harmony 2.x fingerprint. Unlike the B1-A
        // owner-only signature, this distinguishes patch METHOD, module, order,
        // priority and before/after dependencies while preserving duplicates.
        // It never modifies/unpatches an external or vanilla owner.
        private static string SourceAuthPatchStamp(MethodBase method, bool ignoreDiagnostic,
            out bool foreign, out bool complete)
        {
            foreign = false;
            complete = method != null;
            if (method == null) return "METHOD_MISSING";
            string methodId = SourceAuthMethodIdentity(method, ref complete);
            if (!complete) return "METHOD_ID_UNAVAILABLE";
            var info = Harmony.GetPatchInfo(method);
            if (info == null) return "V2:" + methodId + ":NONE";
            var entries = new List<string>();
            SourceAuthAppendPatches(entries, "PRE", info.Prefixes, ignoreDiagnostic, ref foreign, ref complete);
            SourceAuthAppendPatches(entries, "POST", info.Postfixes, ignoreDiagnostic, ref foreign, ref complete);
            SourceAuthAppendPatches(entries, "TRANS", info.Transpilers, ignoreDiagnostic, ref foreign, ref complete);
            SourceAuthAppendPatches(entries, "FINAL", info.Finalizers, ignoreDiagnostic, ref foreign, ref complete);
            entries.Sort(StringComparer.Ordinal);
            return "V2:" + methodId + ":" + (entries.Count == 0 ? "NONE" : string.Join("|", entries.ToArray()));
        }

        private static string SourceAuthMethodIdentity(MethodBase method, ref bool complete)
        {
            if (method == null) { complete = false; return "NULL"; }
            try
            {
                Type owner = method.DeclaringType;
                if (owner == null || method.Module == null) { complete = false; return "NO_DECLARING_TYPE"; }
                return SourceAuthField(owner.FullName ?? "NO_NAME") +
                    SourceAuthField(method.Name) +
                    SourceAuthField(method.Module.ModuleVersionId.ToString("D")) +
                    SourceAuthField(method.MetadataToken.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (Exception) { complete = false; return "NO_METHOD_METADATA"; }
        }

        // Length-prefixing prevents delimiters inside a mod's HarmonyId from
        // making two different patch sets produce the same signature.
        private static string SourceAuthField(string value)
        {
            value = value ?? "NULL";
            return value.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "#" + value;
        }

        private static object SourceAuthPatchMember(Patch patch, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Type type = patch.GetType();
            FieldInfo field = type.GetField(name, flags);
            if (field != null) return field.GetValue(patch);
            PropertyInfo prop = type.GetProperty(name, flags);
            if (prop != null) return prop.GetValue(patch, null);
            throw new MissingMemberException(type.FullName, name);
        }

        private static string SourceAuthPatchVector(object value, ref bool complete)
        {
            if (value == null) return "0#";
            string[] strings = value as string[];
            if (strings == null) { complete = false; return "BAD_VECTOR"; }
            var result = new StringBuilder();
            result.Append(strings.Length).Append('#');
            foreach (string item in strings)
            {
                if (string.IsNullOrEmpty(item)) complete = false;
                result.Append(SourceAuthField(item));
            }
            return result.ToString();
        }

        private static string SourceAuthPatchRow(string kind, string owner, string patchMethodId,
            int ordinal, int priority, int index, string before, string after)
        {
            return SourceAuthField(kind) + SourceAuthField(owner) + SourceAuthField(patchMethodId) +
                SourceAuthField(ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)) +
                SourceAuthField(priority.ToString(System.Globalization.CultureInfo.InvariantCulture)) +
                SourceAuthField(index.ToString(System.Globalization.CultureInfo.InvariantCulture)) +
                SourceAuthField(before) + SourceAuthField(after);
        }

        private static void SourceAuthAppendPatches(List<string> entries, string kind,
            IEnumerable<Patch> patches, bool ignoreDiagnostic, ref bool foreign,
            ref bool complete)
        {
            if (patches == null) return;
            int ordinal = 0; // After excluding only our diagnostic observer.
            foreach (Patch patch in patches)
            {
                if (patch == null) { complete = false; continue; }
                string owner = patch.owner;
                if (string.IsNullOrEmpty(owner)) { complete = false; owner = "UNKNOWN_OWNER"; }
                if (ignoreDiagnostic && owner == DiagnosticHarmonyId) continue;
                if (owner != "zcn.xpcustomizer") foreign = true;
                try
                {
                    MethodBase patchMethod = SourceAuthPatchMember(patch, "PatchMethod") as MethodBase;
                    string patchId = SourceAuthMethodIdentity(patchMethod, ref complete);
                    object priorityObj = SourceAuthPatchMember(patch, "priority");
                    object indexObj = SourceAuthPatchMember(patch, "index");
                    if (!(priorityObj is int) || !(indexObj is int)) complete = false;
                    int priority = priorityObj is int ? (int)priorityObj : -1;
                    int index = indexObj is int ? (int)indexObj : -1;
                    string before = SourceAuthPatchVector(SourceAuthPatchMember(patch, "before"), ref complete);
                    string after = SourceAuthPatchVector(SourceAuthPatchMember(patch, "after"), ref complete);
                    entries.Add(SourceAuthPatchRow(kind, owner, patchId, ordinal++, priority, index, before, after));
                }
                catch (Exception)
                {
                    // Unknown Harmony Patch layout => no formal authorization.
                    complete = false;
                    entries.Add(SourceAuthPatchRow(kind, owner, "UNREADABLE", ordinal++, -1, -1, "UNKNOWN", "UNKNOWN"));
                }
            }
        }

        private static void CaptureSourceAuthStartPatches()
        {
            try
            {
                bool foreign, complete;
                sourceAuthLearnStamp = SourceAuthPatchStamp(target, true, out foreign, out complete);
                if (!complete) { sourceAuthStartPatchReason = "LEARN_UNAVAILABLE"; return; }
                sourceAuthTickStamp = SourceAuthPatchStamp(sourceBillTickMethod, false, out foreign, out complete);
                if (!complete) { sourceAuthStartPatchReason = "BILL_TICK_UNAVAILABLE"; return; }
                sourceAuthFinishStamp = SourceAuthPatchStamp(sourceBillFinishMethod, false, out foreign, out complete);
                if (!complete) { sourceAuthStartPatchReason = "BILL_FINISH_UNAVAILABLE"; return; }
                sourceAuthTrackerStamp = SourceAuthPatchStamp(sourceSkillTrackerLearnMethod, false, out foreign, out complete);
                if (!complete) { sourceAuthStartPatchReason = "SKILL_TRACKER_UNAVAILABLE"; return; }
                // Tend/Social are independently observed negative-control origins.
                // Missing optional callbacks do not invalidate bill-only evidence.
                sourceAuthTendStamp = SourceAuthPatchStamp(sourceTendMethod, false, out foreign, out complete);
                sourceAuthSocialStamp = SourceAuthPatchStamp(sourceSocialMethod, false, out foreign, out complete);
                sourceAuthStartPatchReady = true;
                sourceAuthStartPatchReason = "CAPTURED";
            }
            catch (Exception ex)
            {
                sourceAuthStartPatchReady = false;
                sourceAuthStartPatchReason = "START_PATCH_AUDIT_" + ex.GetType().Name;
            }
        }

        private static void CaptureSourceAuthPatchEvidence(SourceProofEvidence p,
            bool billTick, bool billFinish, bool tend, bool social)
        {
            p.AuthPatchAuditComplete = sourceAuthStartPatchReady;
            p.AuthPatchReason = sourceAuthStartPatchReason;
            if (!sourceAuthStartPatchReady) return;
            try
            {
                bool foreign = false, changed = false, complete = true;
                var owners = new StringBuilder();
                SourceAuthAuditOnePatch("Learn", target, sourceAuthLearnStamp, true,
                    ref foreign, ref changed, ref complete, owners);
                if (billTick)
                    SourceAuthAuditOnePatch("BillTick", sourceBillTickMethod, sourceAuthTickStamp, false,
                        ref foreign, ref changed, ref complete, owners);
                if (billFinish)
                    SourceAuthAuditOnePatch("BillFinish", sourceBillFinishMethod, sourceAuthFinishStamp, false,
                        ref foreign, ref changed, ref complete, owners);
                if (billTick || tend || social)
                    SourceAuthAuditOnePatch("SkillTracker", sourceSkillTrackerLearnMethod,
                        sourceAuthTrackerStamp, false, ref foreign, ref changed, ref complete, owners);
                if (tend)
                    SourceAuthAuditOnePatch("Tend", sourceTendMethod, sourceAuthTendStamp, false,
                        ref foreign, ref changed, ref complete, owners);
                if (social)
                    SourceAuthAuditOnePatch("Social", sourceSocialMethod, sourceAuthSocialStamp, false,
                        ref foreign, ref changed, ref complete, owners);
                p.AuthPatchAuditComplete = complete;
                p.AuthPatchForeign = foreign;
                p.AuthPatchChanged = changed;
                p.AuthPatchOwners = Clean(owners.ToString()); // Display only; never used as gate.
                p.AuthPatchReason = !complete ? "PATCH_AUDIT_INCOMPLETE" :
                    foreign ? "FOREIGN_PATCH_PRESENT" : changed ? "PATCH_STATE_CHANGED" :
                    "NO_FOREIGN_OR_PATCH_DRIFT_DETECTED";
            }
            catch (Exception ex)
            {
                // Authentication degrades to INSUFFICIENT, never a false PASS.
                p.AuthPatchAuditComplete = false;
                p.AuthPatchReason = "PATCH_AUDIT_" + ex.GetType().Name;
            }
        }

        private static void SourceAuthAuditOnePatch(string label, MethodBase method,
            string original, bool ignoreDiagnostic, ref bool foreign, ref bool changed,
            ref bool complete, StringBuilder owners)
        {
            bool hasForeign, ok;
            string current = SourceAuthPatchStamp(method, ignoreDiagnostic, out hasForeign, out ok);
            foreign |= hasForeign;
            complete &= ok && original != null;
            changed |= !string.Equals(original, current, StringComparison.Ordinal);
            if (owners.Length != 0) owners.Append(",");
            owners.Append(label).Append("=").Append(current);
        }

        private static MethodBase LookupSourceCallback(Assembly game, string typeName, string methodName)
        {
            Type type = game.GetType(typeName, false);
            if (type == null) return null;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            MethodBase match = null;
            foreach (MethodInfo method in type.GetMethods(flags))
            {
                if (method.Name != methodName) continue;
                if (match != null) return null; // Overloads are ambiguous, never assume.
                match = method;
            }
            return match;
        }

        private static MethodBase LookupSkillTrackerLearn(Assembly game)
        {
            Type tracker = game.GetType("RimWorld.Pawn_SkillTracker", false);
            if (tracker == null) return null;
            foreach (MethodInfo method in tracker.GetMethods(BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.Name != "Learn") continue;
                ParameterInfo[] args = method.GetParameters();
                if (args.Length == 4 && args[0].ParameterType == typeof(SkillDef) &&
                    args[1].ParameterType == typeof(float) &&
                    args[2].ParameterType == typeof(bool) &&
                    args[3].ParameterType == typeof(bool)) return method;
            }
            return null;
        }

        // Pure trace metadata helpers, shared with synthetic B0 audits.
        // They do not modify the original source decision or XP calculation.
        private static void ObserveSourceFrame(SourceProofEvidence p, int bit, int depth)
        {
            if (p == null || depth < 0 || depth >= MaxSourceProofFrames) return;
            p.RouteMask |= bit;
            if (bit == SourceBillTickBit && (p.BillTickDepth < 0 || depth < p.BillTickDepth))
                p.BillTickDepth = depth;
            else if (bit == SourceBillFinishBit && (p.BillFinishDepth < 0 || depth < p.BillFinishDepth))
                p.BillFinishDepth = depth;
            else if (bit == SourceTendBit && (p.TendDepth < 0 || depth < p.TendDepth))
                p.TendDepth = depth;
            else if (bit == SourceSocialBit && (p.SocialDepth < 0 || depth < p.SocialDepth))
                p.SocialDepth = depth;
        }

        private static int SourceRouteCount(int mask)
        {
            int count = 0;
            if ((mask & SourceBillTickBit) != 0) count++;
            if ((mask & SourceBillFinishBit) != 0) count++;
            if ((mask & SourceTendBit) != 0) count++;
            if ((mask & SourceSocialBit) != 0) count++;
            return count;
        }

        private static string SourceRouteMaskLabel(int mask)
        {
            if (mask == 0) return "NONE";
            var sb = new StringBuilder();
            if ((mask & SourceBillTickBit) != 0) sb.Append("BILL_TICK");
            if ((mask & SourceBillFinishBit) != 0) sb.Append(sb.Length == 0 ? "" : "|").Append("BILL_FINISH");
            if ((mask & SourceTendBit) != 0) sb.Append(sb.Length == 0 ? "" : "|").Append("TEND");
            if ((mask & SourceSocialBit) != 0) sb.Append(sb.Length == 0 ? "" : "|").Append("SOCIAL");
            return sb.ToString();
        }

        private static string NearestObservedSource(SourceProofEvidence p)
        {
            if (p == null || p.RouteMask == 0) return "NONE";
            int nearest = MaxSourceProofFrames;
            string route = "NONE";
            bool tied = false;
            if (p.BillTickDepth >= 0 && p.BillTickDepth < nearest)
            { nearest = p.BillTickDepth; route = "BILL_TICK"; tied = false; }
            if (p.BillFinishDepth >= 0)
            {
                if (p.BillFinishDepth < nearest)
                { nearest = p.BillFinishDepth; route = "BILL_FINISH"; tied = false; }
                else if (p.BillFinishDepth == nearest) tied = true;
            }
            if (p.TendDepth >= 0)
            {
                if (p.TendDepth < nearest)
                { nearest = p.TendDepth; route = "TEND"; tied = false; }
                else if (p.TendDepth == nearest) tied = true;
            }
            if (p.SocialDepth >= 0)
            {
                if (p.SocialDepth < nearest)
                { nearest = p.SocialDepth; route = "SOCIAL"; tied = false; }
                else if (p.SocialDepth == nearest) tied = true;
            }
            return tied ? "TIED@" + nearest : route + "@" + nearest;
        }

        private static void RecordDepthRange(ref int min, ref int max, int depth)
        {
            if (depth < 0) return;
            if (min < 0 || depth < min) min = depth;
            if (max < depth) max = depth;
        }

        private static string SourceDepthRange(int min, int max)
        {
            if (min < 0) return "NONE";
            return min == max ? min.ToString() : min + ".." + max;
        }

        private static SourceProofEvidence ObserveSourceProof(StartState b)
        {
            if (!IsSourceProofEligible(b)) return null;
            var proof = new SourceProofEvidence();
            proof.AssemblyMatchedAtEvent = sourceGameMvidMatches;
            proof.BillTickResolvedAtEvent = sourceBillTickMethod != null;
            proof.BillFinishResolvedAtEvent = sourceBillFinishMethod != null;
            proof.SkillTrackerResolvedAtEvent = sourceSkillTrackerLearnMethod != null;
            if (!sourceGameMvidMatches)
            {
                proof.Status = "UNSUPPORTED_GAME_ASSEMBLY";
                proof.Reason = sourceCallbackAvailability;
                return proof;
            }
            try
            {
                CaptureSourceContext(b, proof);
                int perSkill;
                sourceProofPerSkill.TryGetValue(b.Skill, out perSkill);
                if (sourceProofTraces >= MaxSourceProofTraces ||
                    perSkill >= MaxSourceProofTracesPerSkill)
                {
                    proof.Status = "BUDGET_EXHAUSTED";
                    proof.Reason = "BOUNDED_TRACE_QUOTA";
                    return proof;
                }
                sourceProofTraces++;
                sourceProofPerSkill[b.Skill] = perSkill + 1;
                proof.TraceAttempted = true;
                long begun = Stopwatch.GetTimestamp();
                try
                {
                    // We capture from the existing SkillRecord.Learn Prefix only.
                    // Do NOT install any patch on compiler-generated Toil callbacks.
                    var trace = new StackTrace(1, false);
                    int limit = Math.Min(MaxSourceProofFrames, trace.FrameCount);
                    bool billTick = false, billFinish = false, tend = false, social = false;
                    for (int i = 0; i < limit; i++)
                    {
                        StackFrame frame = trace.GetFrame(i);
                        MethodBase method = frame == null ? null : frame.GetMethod();
                        if (method == null) continue;
                        if (sourceSkillTrackerLearnMethod != null &&
                            method.Equals(sourceSkillTrackerLearnMethod) &&
                            (proof.SkillTrackerDepth < 0 || i < proof.SkillTrackerDepth))
                            proof.SkillTrackerDepth = i;
                        if (sourceBillTickMethod != null && method.Equals(sourceBillTickMethod))
                        { billTick = true; ObserveSourceFrame(proof, SourceBillTickBit, i); }
                        if (sourceBillFinishMethod != null && method.Equals(sourceBillFinishMethod))
                        { billFinish = true; ObserveSourceFrame(proof, SourceBillFinishBit, i); }
                        if (sourceTendMethod != null && method.Equals(sourceTendMethod))
                        { tend = true; ObserveSourceFrame(proof, SourceTendBit, i); }
                        if (sourceSocialMethod != null && method.Equals(sourceSocialMethod))
                        { social = true; ObserveSourceFrame(proof, SourceSocialBit, i); }
                    }
                    proof.Frames = limit;
                    int matches = (billTick ? 1 : 0) + (billFinish ? 1 : 0) +
                        (tend ? 1 : 0) + (social ? 1 : 0);
                    proof.Route = matches != 1 ? "NONE_OR_MULTIPLE" : billTick ? "BILL_UNFINISHED_TICK" :
                        billFinish ? "BILL_FINISH" : tend ? "TEND_FINALIZE" : "INTERACTION";
                    string owners = "NONE";
                    bool foreign = learnForeignHookPresent;
                    if (billTick) { foreign |= sourceBillTickForeign || sourceSkillTrackerForeign;
                        owners = sourceBillTickMethod == null ? "MISSING" : PatchOwnerNames(sourceBillTickMethod); }
                    if (billFinish) { foreign |= sourceBillFinishForeign;
                        owners = sourceBillFinishMethod == null ? "MISSING" : PatchOwnerNames(sourceBillFinishMethod); }
                    if (tend) { foreign |= sourceTendForeign || sourceSkillTrackerForeign;
                        owners = sourceTendMethod == null ? "MISSING" : PatchOwnerNames(sourceTendMethod); }
                    if (social) { foreign |= sourceSocialForeign || sourceSkillTrackerForeign;
                        owners = sourceSocialMethod == null ? "MISSING" : PatchOwnerNames(sourceSocialMethod); }
                    proof.PatchOwners = owners;
                    // B1-A extension is independent of legacy status and does not
                    // move StackTrace, create a wrapper, or grant JOINT eligibility.
                    CaptureSourceAuthPatchEvidence(proof, billTick, billFinish, tend, social);
                    proof.Status = DecideSourceProof(b.Job, b.Skill, proof.Context, proof.Route,
                        matches, foreign, sourceGameMvidMatches);
                    proof.Reason = proof.Status == "CALLSITE_MATCH_CANDIDATE" ?
                        "STACK_AND_CONTEXT_CORRELATE_SHADOW_ONLY" :
                        proof.Status == "CALLSITE_UNAVAILABLE" ? "EXPECTED_FRAME_NOT_SEEN" :
                        proof.Status == "CALLSITE_FOREIGN" ? "POTENTIAL_FOREIGN_PATCH" :
                        proof.Status == "CALLSITE_AMBIGUOUS" ? "MULTIPLE_SOURCE_FRAMES" :
                        proof.Status == "CONTEXT_CONFLICT" ? "JOB_SKILL_ROUTE_CONFLICT" :
                        proof.Status == "CONTEXT_UNAVAILABLE" ? "MISSING_CONTEXT" : proof.Status;
                }
                finally { sourceProofElapsedTicks += Stopwatch.GetTimestamp() - begun; }
            }
            catch (Exception ex)
            {
                sourceProofErrors++;
                proof.Status = "OBSERVATION_ERROR";
                proof.Reason = ex.GetType().Name;
            }
            return proof;
        }
    }
}
#endif
