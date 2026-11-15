#if DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.FurtherLearning;
using XpCustomizer.Gws;
using XpCustomizer.LearningCapacity;

namespace XpCustomizer
{
    /// <summary>
    /// Phase 4C-B: explicit, manual, detached-record integration tests.
    /// Uses a real colonist only as a read-only stat/health/settings context.
    /// No saved SkillRecord is passed to Learn, no settings are modified, and
    /// no job, item, pawn, map or save is created by this runner.
    /// A PASS does not assert actual job XP or compatibility with foreign hooks.
    /// </summary>
    internal static partial class Phase4CBDetachedDiagnostics
    {
        internal const string Revision = "4C-B-NONNEUTRAL-COVERAGE-R2-20261104";
        private const string OwnHarmonyId = "zcn.xpcustomizer";
        private const float SmallTolerance = 0.0005f;
        private enum ResultKind { Pass, Fail, Skip }

        internal sealed class SkipScenario : Exception
        {
            internal SkipScenario(string reason) : base(reason) { }
        }

        private sealed class Case
        {
            internal readonly string Id;
            internal readonly Action<Context> Run;
            internal Case(string id, Action<Context> run) { Id = id; Run = run; }
        }

        private sealed class CaseResult
        {
            internal string Id;
            internal ResultKind Kind;
            internal long Milliseconds;
            internal string Detail;
        }

        internal sealed class Context
        {
            internal Pawn Pawn;
            internal SkillRecord Ordinary;
            internal SkillRecord GwsOn;
            internal SkillRecord GwsOff;
            internal List<SkillSnapshot> OriginalSkills;
            internal XpCustomizerSettings Settings;
            internal string SettingsSignature;
            internal float Consciousness;
            internal float WorkSpeed;
            internal string ForeignRateHookOwners;
            internal float GwsCandidateFactor;
            internal float MentalThreshold;
            internal float MentalAdjustmentPp;
            internal readonly Dictionary<string, string> Evidence = new Dictionary<string, string>();
        }

        internal sealed class SkillSnapshot
        {
            internal SkillRecord Record;
            internal int Level;
            internal int RawLevel;
            internal bool Disabled;
            internal float LevelXp;
            internal float DailyXp;
            internal Passion Passion;
        }

        // Each detached integration scenario can use a different real pawn as a
        // read-only context. All selected pawns are snapshotted and verified
        // after EVERY scenario, including skipped and failed scenarios.
        private sealed class Selection
        {
            internal Context Baseline;
            internal Context GwsNonNeutral;
            internal Context MentalNonNeutral;
            internal readonly List<Context> Protected = new List<Context>();
            internal int EligiblePawns;
            internal int GwsEnabledCandidates;
            internal int GwsNonNeutralCandidates;
            internal int MentalHumanlikeCandidates;
            internal int MentalNonNeutralCandidates;
            internal int CandidateReadErrors;
            internal bool MentalStatAvailable;
            internal string GwsSkipReason;
            internal string MentalSkipReason;
        }

        private static readonly List<CaseResult> lastResults = new List<CaseResult>();
        private static string lastReport;
        private static bool running;
        private static int sequence;

        private static Case[] Cases()
        {
            return new[]
            {
                new Case("DET-001", CheckDetachedIsolation),
                new Case("XP-NORMAL", CheckNormal),
                new Case("XP-DIRECT", CheckDirect),
                new Case("XP-CAP", CheckCap),
                new Case("XP-CONS", CheckConsciousness),
                new Case("XP-GWS-ON", CheckGwsOn),
                new Case("XP-GWS-OFF", CheckGwsOff),
                new Case("XP-FURTHER", CheckFurther),
                new Case("XP-MTH", CheckMental),
                new Case("DET-SAFETY", CheckSafety)
            };
        }

        internal static void RunManually()
        {
            if (running)
            {
                Log.Warning("[XP Customizer][4C-B] SKIP; reason=ALREADY_RUNNING");
                return;
            }
            running = true;
            try
            {
                RunAll();
            }
            catch (Exception ex)
            {
                // Never allow a diagnostic error to escape into the RimWorld UI.
                Phase4CRegressionLedger.Record("4C-B-DETACHED", "FAIL", "RUNNER_FATAL");
                Log.Error("[XP Customizer][4C-B] RUNNER_FATAL; build=" +
                    XpCustomizerMod.BuildId + "; " + ex);
            }
            finally { running = false; }
        }

        private static void RunAll()
        {
            Case[] cases = Cases();
            var results = new List<CaseResult>(cases.Length);
            string preflightSkip = null;
            Selection selection = null;
            Context context = null;
            try
            {
                selection = PrepareSelection();
                context = selection.Baseline;
            }
            catch (SkipScenario ex) { preflightSkip = Clean(ex.Message); }
            catch (Exception ex)
            {
                // An unexpected preflight failure is not a SKIP.
                preflightSkip = null;
                var failure = new CaseResult { Id = "PRECHECK", Kind = ResultKind.Fail,
                    Detail = ex.GetType().Name + ": " + Clean(ex.Message) };
                results.Add(failure);
            }

            bool unsafeMutation = false;
            foreach (Case test in cases)
            {
                var result = new CaseResult { Id = test.Id, Kind = ResultKind.Skip };
                if (results.Count > 0 && results[0].Id == "PRECHECK")
                    result.Detail = "PRECHECK_FAILED";
                else if (preflightSkip != null)
                    result.Detail = preflightSkip;
                else if (unsafeMutation)
                    result.Detail = "UNEXPECTED_ORIGINAL_STATE_CHANGE";
                else
                {
                    Context testContext = context;
                    if (test.Id == "XP-GWS-ON")
                        testContext = selection.GwsNonNeutral;
                    else if (test.Id == "XP-MTH")
                        testContext = selection.MentalNonNeutral;
                    Stopwatch watch = Stopwatch.StartNew();
                    try
                    {
                        if (testContext == null)
                            throw new SkipScenario(test.Id == "XP-GWS-ON"
                                ? selection.GwsSkipReason : selection.MentalSkipReason);
                        test.Run(testContext);
                        result.Kind = ResultKind.Pass;
                        string evidence;
                        if (testContext.Evidence.TryGetValue(test.Id, out evidence))
                            result.Detail = evidence;
                    }
                    catch (SkipScenario ex)
                    {
                        result.Kind = ResultKind.Skip;
                        result.Detail = Clean(ex.Message);
                    }
                    catch (Exception ex)
                    {
                        result.Kind = ResultKind.Fail;
                        result.Detail = ex.GetType().Name + ": " + Clean(ex.Message);
                    }
                    finally
                    {
                        watch.Stop();
                        result.Milliseconds = watch.ElapsedMilliseconds;
                        try { CheckProtectedState(selection); }
                        catch (Exception ex)
                        {
                            result.Kind = ResultKind.Fail;
                            result.Detail = "SAVED_STATE_CHANGED: " + Clean(ex.Message);
                            unsafeMutation = true;
                        }
                    }
                }
                results.Add(result);
            }

            int pass = 0, fail = 0, skip = 0;
            var sb = new StringBuilder(2600);
            sb.Append("[XP Customizer][4C-B] RUN; id=").Append(++sequence)
                .Append("; build=").Append(XpCustomizerMod.BuildId)
                .Append("; runner=").Append(Revision)
                .Append("; mode=MANUAL_DETACHED; realPawnLearnCalls=0")
                .Append("; settingsWrites=0; saveWrites=0")
                .Append("; startedUtc=").Append(DateTime.UtcNow.ToString("o"));
            if (context != null)
            {
                sb.Append("; pawn=").Append(Clean(context.Pawn.LabelShort))
                    .Append("; skill=").Append(context.Ordinary.def.defName)
                    .Append("; gwsOnSkill=").Append(context.GwsOn == null ? "NONE" : context.GwsOn.def.defName)
                    .Append("; gwsOffSkill=").Append(context.GwsOff == null ? "NONE" : context.GwsOff.def.defName)
                    .Append("; consciousness=").Append(context.Consciousness.ToString("0.###"))
                    .Append("; workSpeed=").Append(context.WorkSpeed.ToString("0.###"))
                    .Append("; foreignRateHooks=").Append(context.ForeignRateHookOwners);
            }
            if (selection != null)
            {
                sb.Append("; scannedEligiblePawns=").Append(selection.EligiblePawns)
                    .Append("; gwsEnabledCandidates=").Append(selection.GwsEnabledCandidates)
                    .Append("; gwsNonNeutralCandidates=").Append(selection.GwsNonNeutralCandidates)
                    .Append("; mentalHumanlikeCandidates=").Append(selection.MentalHumanlikeCandidates)
                    .Append("; mentalNonNeutralCandidates=").Append(selection.MentalNonNeutralCandidates)
                    .Append("; candidateReadErrors=").Append(selection.CandidateReadErrors)
                    .Append("; protectedPawns=").Append(selection.Protected.Count)
                    .Append("; gwsPawn=").Append(PawnName(selection.GwsNonNeutral))
                    .Append("; mthPawn=").Append(PawnName(selection.MentalNonNeutral))
                    .Append("; gwsFactor=").Append(selection.GwsNonNeutral == null ? "N/A" : Fmt(selection.GwsNonNeutral.GwsCandidateFactor))
                    .Append("; mthThreshold=").Append(selection.MentalNonNeutral == null ? "N/A" : Fmt(selection.MentalNonNeutral.MentalThreshold))
                    .Append("; mthPp=").Append(selection.MentalNonNeutral == null ? "N/A" : Fmt(selection.MentalNonNeutral.MentalAdjustmentPp));
            }
            foreach (CaseResult r in results)
            {
                if (r.Kind == ResultKind.Pass) pass++;
                else if (r.Kind == ResultKind.Fail) fail++;
                else skip++;
                sb.Append("\n[XPC-4CB][").Append(r.Kind.ToString().ToUpperInvariant())
                    .Append("] ").Append(r.Id)
                    .Append("; ms=").Append(r.Milliseconds);
                if (!string.IsNullOrEmpty(r.Detail)) sb.Append("; detail=").Append(r.Detail);
            }
            sb.Append("\n[XP Customizer][4C-B] SUMMARY; pass=").Append(pass)
                .Append("; fail=").Append(fail)
                .Append("; skip=").Append(skip)
                .Append("; total=").Append(results.Count)
                .Append("; status=").Append(fail != 0 ? "FAIL" : skip != 0 ? "PARTIAL" : "PASS")
                .Append("; coverage=DETACHED_LEARN_ONLY")
                .Append("; realJobXP=NOT_TESTED; reading=NOT_TESTED; trainerUse=NOT_TESTED")
                .Append("; savedPawnXP=NOT_MODIFIED_BY_RUNNER")
                .Append("; selection=SEPARATE_GWS_AND_MTH_PAWNS")
                .Append("; nonNeutralOnlyWhenLiveCandidateExists=true");
            lastResults.Clear();
            lastResults.AddRange(results);
            lastReport = sb.ToString();
            Phase4CRegressionLedger.Record("4C-B-DETACHED",
                fail > 0 ? "FAIL" : skip > 0 ? "PARTIAL" : "PASS",
                "pass=" + pass + ",fail=" + fail + ",skip=" + skip);
            if (fail > 0) Log.Error(lastReport);
            else Log.Message(lastReport);
        }

        internal static void PrintLast()
        {
            if (string.IsNullOrEmpty(lastReport))
            {
                Log.Message("[XP Customizer][4C-B] NO_REPORT; run detached XP diagnostics first.");
                return;
            }
            Log.Message("[XP Customizer][4C-B] LAST_REPORT_REPRINT\n" + lastReport);
        }

        private static SkillRecord Fresh(Context ctx, SkillRecord record)
        {
            SkillRecord detached = XpCustomizerLiveSkillProbe.CreateDetachedFor4CB(ctx.Pawn, record);
            if (System.Object.ReferenceEquals(detached, record))
                throw new InvalidOperationException("DETACHED_REFERENCE_IS_ORIGINAL");
            return detached;
        }

        private static void CheckDetachedIsolation(Context ctx)
        {
            SkillRecord test = Fresh(ctx, ctx.Ordinary);
            if (test.levelInt != 3 || test.xpSinceLastLevel != 0f ||
                test.xpSinceMidnight != 0f)
                throw new InvalidOperationException("DETACHED_INITIAL_STATE_INCORRECT; rawLevel=" +
                    test.levelInt + "; displayedLevel=" + test.Level +
                    "; aptitude=" + test.Aptitude);
            ctx.Evidence["DET-001"] = "source=" + ctx.Ordinary.def.defName +
                "; detached=true; rawLevel=3; displayedLevel=" + test.Level +
                "; aptitude=" + test.Aptitude;
        }

        private static void CheckNormal(Context ctx)
        {
            var s = Fresh(ctx, ctx.Ordinary);
            ctx.Evidence["XP-NORMAL"] = ProbeLearn(s, false, 0f, "NORMAL");
        }
        private static void CheckDirect(Context ctx)
        {
            var s = Fresh(ctx, ctx.Ordinary);
            ctx.Evidence["XP-DIRECT"] = ProbeLearn(s, true, 0f, "DIRECT");
        }
        private static void CheckCap(Context ctx)
        {
            SkillRecord pre = Fresh(ctx, ctx.Ordinary);
            SkillRecord post = Fresh(ctx, ctx.Ordinary);
            float cap = LearningCapacityService.GetDailyCap(pre);
            if (float.IsNaN(cap) || float.IsInfinity(cap) || cap < 0f || cap > 1000000f)
                throw new SkipScenario("UNSAFE_CAP=" + cap);
            if (cap < 1f) throw new SkipScenario("ZERO_OR_TINY_CAP");
            float offset = Math.Max(64f, cap * 0.01f);
            float below = Math.Max(0f, cap - offset);
            if (below >= cap) throw new SkipScenario("NO_UNSATURATED_WINDOW");
            pre.xpSinceMidnight = below;
            post.xpSinceMidnight = cap + offset;
            if (pre.LearningSaturatedToday || !post.LearningSaturatedToday)
                throw new InvalidOperationException("SATURATION_BOUNDARY_MISMATCH; cap=" + cap);
            string preEvidence = ProbeLearn(pre, false, below, "PRE_CAP");
            string postEvidence = ProbeLearn(post, false, cap + offset, "POST_CAP");
            ctx.Evidence["XP-CAP"] = "cap=" + Fmt(cap) + "; pre=" + preEvidence + "; post=" + postEvidence;
        }
        private static void CheckConsciousness(Context ctx)
        {
            float factor = ConsciousnessLearningService.GetFactor(ctx.Pawn);
            VerifyFinite(factor, "CONS_FACTOR");
            if (Math.Abs(factor - 1f) < SmallTolerance)
                throw new SkipScenario("CONS_CURRENTLY_NEUTRAL");
            SkillRecord detached = Fresh(ctx, ctx.Ordinary);
            float probe = 1f;
            Patch_SkillRecord_LearnRateFactor_Consciousness.Postfix(detached, false, ref probe);
            AssertNear(probe, factor, "CONS_PATCH_NORMAL_FACTOR");
            probe = 1f;
            Patch_SkillRecord_LearnRateFactor_Consciousness.Postfix(detached, true, ref probe);
            AssertNear(probe, 1f, "CONS_PATCH_DIRECT_BYPASS");
            string actualLearn = ProbeLearn(detached, false, 0f, "CONS_NORMAL_LEARN");
            ctx.Evidence["XP-CONS"] = "factor=" + Fmt(factor) + "; " + actualLearn;
        }
        private static void CheckGwsOn(Context ctx)
        {
            if (ctx.Settings == null || !ctx.Settings.gwsEnabled)
                throw new SkipScenario("GWS_MASTER_OFF");
            if (ctx.GwsOn == null)
                throw new SkipScenario("NO_GWS_ENABLED_SKILL");
            SkillRecord detached = Fresh(ctx, ctx.GwsOn);
            float factor = GwsLearningService.GetFactor(detached);
            VerifyFinite(factor, "GWS_ON_FACTOR");
            if (Math.Abs(factor - 1f) < SmallTolerance)
                throw new SkipScenario("GWS_CURRENTLY_NEUTRAL");
            float observed = 1f;
            Patch_SkillRecord_GwsLearning.Postfix(detached, false, ref observed);
            AssertNear(observed, factor, "GWS_ON_PATCH_FACTOR");
            observed = 1f;
            Patch_SkillRecord_GwsLearning.Postfix(detached, true, ref observed);
            AssertNear(observed, 1f, "GWS_DIRECT_BYPASS");
            string actualLearn = ProbeLearn(detached, false, 0f, "GWS_ON_LEARN");
            float expectedFactor = GwsLearningService.Calculate(ctx.WorkSpeed, ctx.Settings);
            AssertNear(factor, expectedFactor, "GWS_CALCULATED_VS_PATCH_FACTOR");
            ctx.Evidence["XP-GWS-ON"] = "pawn=" + PawnName(ctx) + "; skill=" + detached.def.defName +
                "; gws=" + Fmt(ctx.WorkSpeed) + "; factor=" + Fmt(factor) + "; " + actualLearn;
        }
        private static void CheckGwsOff(Context ctx)
        {
            if (ctx.Settings == null || !ctx.Settings.gwsEnabled)
                throw new SkipScenario("GWS_MASTER_OFF");
            if (ctx.GwsOff == null)
                throw new SkipScenario("NO_GWS_EXCLUDED_SKILL");
            SkillRecord detached = Fresh(ctx, ctx.GwsOff);
            float factor = GwsLearningService.GetFactor(detached);
            AssertNear(factor, 1f, "GWS_OFF_FACTOR");
            float observed = 1f;
            Patch_SkillRecord_GwsLearning.Postfix(detached, false, ref observed);
            AssertNear(observed, 1f, "GWS_OFF_PATCH_NEUTRAL");
            string actualLearn = ProbeLearn(detached, false, 0f, "GWS_OFF_LEARN");
            ctx.Evidence["XP-GWS-OFF"] = "skill=" + detached.def.defName + "; factor=1; " + actualLearn;
        }
        private static void CheckFurther(Context ctx)
        {
            SkillRecord pre = Fresh(ctx, ctx.Ordinary);
            SkillRecord post = Fresh(ctx, ctx.Ordinary);
            float cap = LearningCapacityService.GetDailyCap(pre);
            if (float.IsNaN(cap) || float.IsInfinity(cap) || cap < 1f || cap > 1000000f)
                throw new SkipScenario("UNSAFE_OR_ZERO_CAP_FOR_FURTHER");
            post.xpSinceMidnight = cap + Math.Max(64f, cap * .01f);
            if (pre.LearningSaturatedToday || !post.LearningSaturatedToday)
                throw new InvalidOperationException("FURTHER_CAP_STATE_INCORRECT");
            float normalRate = pre.LearnRateFactor(false);
            float postRate = post.LearnRateFactor(false);
            VerifyFinite(normalRate, "PRE_CAP_RATE");
            VerifyFinite(postRate, "POST_CAP_RATE");
            if (normalRate <= 0.00001f)
                throw new SkipScenario("NO_MEASURABLE_NORMAL_RATE");
            float desired = FurtherLearningService.GetFactor(ctx.Pawn);
            AssertNear(postRate / normalRate, desired, "POST_CAP_RATE_RATIO");
            // Also exercise the exact saturated Learn() call, rather than
            // accepting the factor calculation alone.
            string actualLearn = ProbeLearn(post, false, post.xpSinceMidnight, "FURTHER_LEARN");
            ctx.Evidence["XP-FURTHER"] = "expectedRatio=" + Fmt(desired) + "; actualRatio=" +
                Fmt(postRate / normalRate) + "; " + actualLearn;
        }
        private static void CheckMental(Context ctx)
        {
            XpCustomizerSettings s = ctx.Settings;
            if (!s.furtherLearningMentalThresholdEnabled ||
                Math.Abs(s.furtherLearningMentalThresholdStrength) < SmallTolerance)
                throw new SkipScenario("MENTAL_EFFECT_DISABLED");
            if (ctx.Pawn.RaceProps == null || !ctx.Pawn.RaceProps.Humanlike)
                throw new SkipScenario("NON_HUMANLIKE_PAWN");
            StatDef mentalStat = DefDatabase<StatDef>.GetNamedSilentFail("MentalBreakThreshold");
            if (mentalStat == null) throw new SkipScenario("MENTAL_STAT_MISSING");
            float threshold = Mathf.Clamp(ctx.Pawn.GetStatValue(mentalStat), 0.01f, 0.50f);
            float adjustment = FurtherLearningService.CalculateThresholdAdjustment(
                threshold, s.furtherLearningMentalThresholdStrength);
            if (Math.Abs(adjustment) < SmallTolerance)
                throw new SkipScenario("MENTAL_CURRENT_EFFECT_NEUTRAL");
            float multiplier = s.consciousnessAffectsLearning &&
                s.consciousnessAffectsFurtherLearning
                    ? ConsciousnessFurtherLearningService.GetMultiplier(ctx.Pawn) : 1f;
            float expected = Mathf.Max(0f,
                (FurtherLearningService.NormalizeBaseFactor(s.furtherLearningBaseFactor) +
                adjustment / 100f) * multiplier);
            AssertNear(FurtherLearningService.GetFactor(ctx.Pawn), expected, "BASE_PLUS_MTH_PP_X_CONS_R5F1");
            SkillRecord saturated = Fresh(ctx, ctx.Ordinary);
            float cap = LearningCapacityService.GetDailyCap(saturated);
            if (cap < 1f || cap > 1000000f || float.IsNaN(cap) || float.IsInfinity(cap))
                throw new SkipScenario("UNSAFE_CAP_FOR_MTH_LEARN");
            saturated.xpSinceMidnight = cap + Math.Max(64f, cap * .01f);
            // A second check exercises the actual Harmony-patched rate under
            // daily saturation. The first arithmetic check alone would not
            // prove that MTH reaches LearnRateFactor().
            SkillRecord unsaturated = Fresh(ctx, ctx.Ordinary);
            float baseRate = unsaturated.LearnRateFactor(false);
            float saturatedRate = saturated.LearnRateFactor(false);
            VerifyFinite(baseRate, "MTH_NORMAL_RATE");
            VerifyFinite(saturatedRate, "MTH_SATURATED_RATE");
            if (baseRate < 0.00001f)
                throw new SkipScenario("MTH_NO_MEASURABLE_RATE");
            AssertNear(saturatedRate / baseRate, expected, "MTH_ACTUAL_POSTCAP_RATE_RATIO");
            string actualLearn = ProbeLearn(saturated, false, saturated.xpSinceMidnight, "MTH_POST_CAP_LEARN");
            ctx.Evidence["XP-MTH"] = "pawn=" + PawnName(ctx) + "; threshold=" + Fmt(threshold) +
                "; mentalPp=" + Fmt(adjustment) + "; expectedPostCap=" + Fmt(expected) +
                "; actualRatio=" + Fmt(saturatedRate / baseRate) + "; " + actualLearn;
        }
        private static void CheckSafety(Context ctx)
        {
            CheckSavedState(ctx);
            ctx.Evidence["DET-SAFETY"] = "baselineSavedSkills=" + ctx.OriginalSkills.Count +
                "; allSelectedPawnsVerifiedAfterEveryCase=true" +
                "; checkedLevelRawLevelDisabledXpDailyPassionAndTrackedSettings=true";
        }

        private static string ProbeLearn(SkillRecord record, bool direct, float originalDaily, string name)
        {
            float rate = record.LearnRateFactor(direct);
            VerifyFinite(rate, name + "_RATE");
            if (rate < 0f) throw new InvalidOperationException(name + "_NEGATIVE_RATE");
            if (rate < 0.00001f) throw new SkipScenario(name + "_NO_MEASURABLE_LEARNING_RATE");
            float threshold = SkillRecord.XpRequiredToLevelUpFrom(3);
            VerifyFinite(threshold, name + "_LEVEL_THRESHOLD");
            if (threshold <= 1f) throw new SkipScenario(name + "_INVALID_LEVEL_THRESHOLD");
            float sample = Math.Min(8f, threshold * 0.01f / Math.Max(1f, rate));
            float expected = sample * rate;
            if (sample <= 0f || float.IsNaN(sample))
                throw new SkipScenario(name + "_INVALID_SAMPLE");
            record.Learn(sample, direct);
            XpCustomizerLiveSkillProbe.AssertDetachedFor4CB(record, expected,
                direct ? originalDaily : originalDaily + expected, "4CB_" + name);
            return "rate=" + Fmt(rate) + "; expectedXp=" + Fmt(expected) +
                "; actualXp=" + Fmt(record.xpSinceLastLevel) +
                "; expectedDaily=" + Fmt(direct ? originalDaily : originalDaily + expected) +
                "; actualDaily=" + Fmt(record.xpSinceMidnight);
        }
        private static void VerifyFinite(float value, string label)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidOperationException(label + "_NONFINITE=" + value);
        }
        private static void AssertNear(float actual, float expected, string label)
        {
            VerifyFinite(actual, label + "_ACTUAL");
            VerifyFinite(expected, label + "_EXPECTED");
            if (Math.Abs(actual - expected) > Math.Max(0.0005f, Math.Abs(expected) * 0.001f))
                throw new InvalidOperationException(label + " expected=" + expected + " actual=" + actual);
        }
        private static string Fmt(float value) { return value.ToString("0.####", CultureInfo.InvariantCulture); }

        private static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value)) return "EMPTY";
            string result = value.Replace('\r', ' ').Replace('\n', ' ').Replace(';', ',');
            return result.Length <= 220 ? result : result.Substring(0, 220);
        }
    }

    // Only exposes the already-reviewed detached helpers from the same
    // partial class. The original automatic D2 probe remains unchanged.
    internal static partial class XpCustomizerLiveSkillProbe
    {
        internal static SkillRecord CreateDetachedFor4CB(Pawn pawn, SkillRecord record)
        {
            return CreateDetachedSkill(pawn, record);
        }
        internal static void AssertDetachedFor4CB(SkillRecord skill,
            float expectedXp, float expectedDaily, string label)
        {
            AssertDetachedLearnResult(skill, expectedXp, expectedDaily, label);
        }
    }

    public static class Phase4CBDetachedDebugActions
    {
        [DebugAction("XP Customizer", "Run detached XP diagnostics (4C-B)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run()
        {
            Phase4CBDetachedDiagnostics.RunManually();
        }

        [DebugAction("XP Customizer", "Print last 4C-B report",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Print()
        {
            Phase4CBDetachedDiagnostics.PrintLast();
        }
    }
}
#endif
