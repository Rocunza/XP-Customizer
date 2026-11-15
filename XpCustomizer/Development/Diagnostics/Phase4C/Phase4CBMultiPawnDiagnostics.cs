#if DEBUG
using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.FurtherLearning;
using XpCustomizer.Gws;

namespace XpCustomizer
{
    /// <summary>
    /// 4C-M1. Enumerate up to 100 live colonists, but call Learn() ONLY on
    /// fresh detached SkillRecords. All selected saved SkillRecords are
    /// snapshotted before the first detached Learn and checked after each test.
    /// Reuses the R2 selection / Harmony safety gates from the partial class.
    /// </summary>
    internal static partial class Phase4CBDetachedDiagnostics
    {
        internal const string MultiRevision = "4C-M1-APTITUDE-AWARE-HF1-20261105";
        private static int multiSequence;
        private static string lastMultiReport;

        internal static void RunMultiPawnManually(int requested)
        {
            if (running)
            {
                Log.Warning("[XPC-4CM][SKIP] ALREADY_RUNNING");
                return;
            }
            running = true;
            try { RunMultiPawn(Math.Max(1, Math.Min(requested, 100))); }
            catch (SkipScenario ex)
            {
                lastMultiReport = "[XPC-4CM][SUMMARY] status=SKIP; build=" +
                    XpCustomizerMod.BuildId + "; reason=" + Clean(ex.Message) +
                    "; savedPawnXp=NOT_MODIFIED_BY_RUNNER";
                Phase4CRegressionLedger.Record("4C-M-MULTIPAWN", "SKIP", "SCENARIO_NOT_ELIGIBLE");
                Log.Warning(lastMultiReport);
            }
            catch (Exception ex)
            {
                lastMultiReport = "[XPC-4CM][SUMMARY] status=FAIL; build=" +
                    XpCustomizerMod.BuildId + "; reason=" + Clean(ex.ToString());
                Phase4CRegressionLedger.Record("4C-M-MULTIPAWN", "FAIL", "RUNNER_FATAL");
                Log.Error(lastMultiReport);
            }
            finally { running = false; }
        }

        private static void RunMultiPawn(int requested)
        {
            // No global settings mutation, no job control, no pawn spawning.
            CheckDetachedPreflight();
            VerifyAptitudeLevelClassifier();
            var selection = new Selection();
            var examined = new HashSet<Pawn>();
            int eligible = 0, skipped = 0;
            foreach (Map map in Find.Maps)
            {
                if (map == null || map.mapPawns == null) continue;
                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    if (pawn == null || !examined.Add(pawn) || pawn.Dead ||
                        pawn.skills == null || pawn.skills.skills == null ||
                        pawn.health == null || pawn.health.capacities == null) continue;
                    SkillRecord record = null;
                    SkillRecord gwsRecord = null;
                    foreach (SkillRecord skill in pawn.skills.skills)
                    {
                        if (skill == null || skill.def == null || skill.TotallyDisabled) continue;
                        if (record == null) record = skill;
                        if (gwsRecord == null && GwsLearningService.IsSkillEnabled(
                            XpCustomizerMod.Settings, skill.def.defName)) gwsRecord = skill;
                    }
                    if (record == null) { skipped++; continue; }
                    eligible++;
                    if (selection.Protected.Count < requested)
                    {
                        var ctx = new Context { Pawn = pawn, Ordinary = record,
                            GwsOn = gwsRecord, Settings = XpCustomizerMod.Settings };
                        AddProtected(selection, ctx);
                    }
                }
            }
            if (selection.Protected.Count == 0)
                throw new SkipScenario("NO_ELIGIBLE_COLONISTS; eligible=" + eligible);

            int pass = 0, fail = 0, caseSkip = 0;
            int gwsNonNeutral = 0, gwsNeutral = 0, mthNonNeutral = 0, mthNeutral = 0;
            bool unsafeMutation = false;
            int aptitudeNonNeutral = 0, extraAptitudeDetails = 0;
            var details = new StringBuilder(2400);
            StatDef mental = DefDatabase<StatDef>.GetNamedSilentFail("MentalBreakThreshold");
            foreach (Context ctx in selection.Protected)
            {
                if (unsafeMutation) { caseSkip++; continue; }
                string result = "PASS", note = "";
                bool nonZeroAptitude = false;
                try
                {
                    // Ordinary Learn() on an ephemeral record, NEVER the saved record.
                    SkillRecord detached = Fresh(ctx, ctx.Ordinary);
                    int rawBefore = detached.levelInt;
                    int displayBefore = detached.Level;
                    int aptitudeBefore = detached.Aptitude;
                    bool disabledBefore = detached.TotallyDisabled;
                    note = "skill=" + ctx.Ordinary.def.defName +
                        "; rawBefore=" + rawBefore + "; displayedBefore=" + displayBefore +
                        "; aptitudeBefore=" + aptitudeBefore + "; disabledBefore=" + disabledBefore;
                    if (rawBefore != 3)
                        throw new InvalidOperationException("DETACHED_INITIAL_RAW_LEVEL_NOT_3");
                    string evidence = ProbeLearn(detached, false, 0f, "MULTI_PAWN");
                    if (!SameDetachedLevelState(rawBefore, displayBefore, aptitudeBefore,
                        disabledBefore, detached.levelInt, detached.Level,
                        detached.Aptitude, detached.TotallyDisabled))
                        throw new InvalidOperationException("DETACHED_LEVEL_STATE_CHANGED: " +
                            "rawAfter=" + detached.levelInt + "; displayedAfter=" +
                            detached.Level + "; aptitudeAfter=" + detached.Aptitude +
                            "; disabledAfter=" + detached.TotallyDisabled);
                    VerifyFinite(detached.xpSinceLastLevel, "MULTI_XP");
                    nonZeroAptitude = aptitudeBefore != 0;
                    if (nonZeroAptitude) aptitudeNonNeutral++;
                    if (ctx.GwsOn != null && ctx.Pawn.RaceProps != null &&
                        ctx.Pawn.RaceProps.Humanlike && ctx.Settings.gwsEnabled)
                    {
                        float actual = GwsLearningService.GetFactor(Fresh(ctx, ctx.GwsOn));
                        float stat = ctx.Pawn.GetStatValue(StatDefOf.WorkSpeedGlobal);
                        AssertNear(actual, GwsLearningService.Calculate(stat, ctx.Settings),
                            "MULTI_GWS_FACTOR");
                        if (Math.Abs(actual - 1f) >= SmallTolerance) gwsNonNeutral++;
                        else gwsNeutral++;
                    }
                    if (mental != null && ctx.Pawn.RaceProps != null &&
                        ctx.Pawn.RaceProps.Humanlike && ctx.Settings.furtherLearningMentalThresholdEnabled)
                    {
                        float threshold = Mathf.Clamp(ctx.Pawn.GetStatValue(mental), .01f, .50f);
                        float pp = FurtherLearningService.CalculateThresholdAdjustment(threshold,
                            ctx.Settings.furtherLearningMentalThresholdStrength);
                        VerifyFinite(pp, "MULTI_MTH_PP");
                        if (Math.Abs(pp) >= SmallTolerance) mthNonNeutral++;
                        else mthNeutral++;
                    }
                    note += "; rawAfter=" + detached.levelInt +
                        "; displayedAfter=" + detached.Level +
                        "; aptitudeAfter=" + detached.Aptitude +
                        "; disabledAfter=" + detached.TotallyDisabled +
                        "; " + evidence;
                }
                catch (SkipScenario ex) { result = "SKIP"; note += "; " + Clean(ex.Message); }
                catch (Exception ex) { result = "FAIL"; note += "; " + Clean(ex.ToString()); }
                finally
                {
                    // Every selected pawn is checked after every case: catches
                    // indirect mutation of another context, not just this pawn.
                    try { CheckProtectedState(selection); }
                    catch (Exception ex)
                    {
                        result = "FAIL";
                        note = "ORIGINAL_STATE_CHANGED: " + Clean(ex.ToString());
                        unsafeMutation = true;
                    }
                }
                if (result == "PASS") pass++;
                else if (result == "SKIP") caseSkip++;
                else fail++;
                // Keep one bounded Unity log and preserve every failure detail.
                bool extraAptitude = result == "PASS" && pass > 12 &&
                    nonZeroAptitude && extraAptitudeDetails < 4;
                if (extraAptitude) extraAptitudeDetails++;
                if (result != "PASS" || pass <= 12 || extraAptitude)
                    details.Append("\n[XPC-4CM][").Append(result).Append("] pawn=")
                        .Append(Clean(ctx.Pawn.LabelShort)).Append("; ").Append(note);
            }
            string status = fail > 0 ? "FAIL" : caseSkip > 0 ? "PARTIAL" : "PASS";
            var sb = new StringBuilder(4200);
            sb.Append("[XPC-4CM][RUN] id=").Append(++multiSequence)
                .Append("; revision=").Append(MultiRevision)
                .Append("; build=").Append(XpCustomizerMod.BuildId)
                .Append("; requested=").Append(requested)
                .Append("; eligible=").Append(eligible)
                .Append("; selected=").Append(selection.Protected.Count)
                .Append("; disabledOnlySkipped=").Append(skipped)
                .Append("; selectedPawnCap=").Append(requested)
                .Append("; realPawnLearnCalls=0; globalSettingsWrites=0; savedXpWrites=0")
                .Append("; foreignLearnHookGuard=ENABLED");
            sb.Append(details);
            sb.Append("\n[XPC-4CM][SUMMARY] pass=").Append(pass)
                .Append("; fail=").Append(fail).Append("; skip=").Append(caseSkip)
                .Append("; selected=").Append(selection.Protected.Count)
                .Append("; gwsNonNeutral=").Append(gwsNonNeutral)
                .Append("; gwsNeutral=").Append(gwsNeutral)
                .Append("; mthNonNeutral=").Append(mthNonNeutral)
                .Append("; mthNeutral=").Append(mthNeutral)
                .Append("; aptitudeNonNeutralPawns=").Append(aptitudeNonNeutral)
                .Append("; originalStateChanged=").Append(unsafeMutation)
                .Append("; status=").Append(status)
                .Append("; coverage=DETACHED_PER_PAWN_NOT_REAL_JOB_XP")
                .Append("; unselectedEligible=").Append(Math.Max(0, eligible - selection.Protected.Count))
                .Append("; printedSuccessfulPawns=FIRST_12_PLUS_4_APTITUDE; failedPawns=ALL")
                .Append("; saveWrites=0");
            lastMultiReport = sb.ToString();
            Phase4CRegressionLedger.Record("4C-M-MULTIPAWN", status == "PASS" ? "PASS" :
                fail > 0 ? "FAIL" : "PARTIAL", "pass=" + pass + ",fail=" + fail +
                ",skip=" + caseSkip);
            if (fail > 0) Log.Error(lastMultiReport);
            else Log.Message(lastMultiReport);
        }

        // Pure comparator: the displayed level is not the stored level.
        // The displayed level can include Aptitude, clamps, or skill blocking.
        // These comparisons must not assume displayedLevel == rawLevel.
        private static bool SameDetachedLevelState(int rawBefore, int displayBefore,
            int aptitudeBefore, bool disabledBefore, int rawAfter,
            int displayAfter, int aptitudeAfter, bool disabledAfter)
        {
            return rawBefore == rawAfter && displayBefore == displayAfter &&
                aptitudeBefore == aptitudeAfter && disabledBefore == disabledAfter;
        }

        private static void VerifyAptitudeLevelClassifier()
        {
            int cases = 0;
            AssertLevelFixture(3, 3, 0, false, 3, 3, 0, false, true, ref cases);
            AssertLevelFixture(3, 11, 8, false, 3, 11, 8, false, true, ref cases);
            AssertLevelFixture(3, 0, -5, false, 3, 0, -5, false, true, ref cases);
            AssertLevelFixture(3, 0, 8, true, 3, 0, 8, true, true, ref cases);
            AssertLevelFixture(3, 11, 8, false, 4, 12, 8, false, false, ref cases);
            AssertLevelFixture(3, 11, 8, false, 3, 12, 8, false, false, ref cases);
            AssertLevelFixture(3, 11, 8, false, 3, 11, 9, false, false, ref cases);
            AssertLevelFixture(3, 0, 8, true, 3, 0, 8, false, false, ref cases);
            Log.Message("[XPC-4CM][APTITUDE_AUDIT] status=PASS; cases=" + cases +
                "; positiveNegativeAndDisabledCovered=True; rawLevelAndShownLevelIndependent=True" +
                "; pawnWrites=0; settingsWrites=0; build=" + XpCustomizerMod.BuildId);
        }

        private static void AssertLevelFixture(int rawBefore, int displayBefore,
            int aptitudeBefore, bool disabledBefore, int rawAfter,
            int displayAfter, int aptitudeAfter, bool disabledAfter,
            bool expected, ref int cases)
        {
            bool actual = SameDetachedLevelState(rawBefore, displayBefore,
                aptitudeBefore, disabledBefore, rawAfter, displayAfter,
                aptitudeAfter, disabledAfter);
            if (actual != expected)
                throw new InvalidOperationException("APTITUDE_COMPARATOR_REGRESSION_CASE=" + cases);
            cases++;
        }

        internal static void PrintLastMultiPawn()
        {
            Log.Message(string.IsNullOrEmpty(lastMultiReport)
                ? "[XPC-4CM] NO_REPORT"
                : "[XPC-4CM] LAST_REPORT_REPRINT\n" + lastMultiReport);
        }
    }

    public static class Phase4CMultiPawnDebugActions
    {
        [DebugAction("XP Customizer", "Multi-pawn detached XP audit (30) (4C-M1)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run30() { Phase4CBDetachedDiagnostics.RunMultiPawnManually(30); }

        [DebugAction("XP Customizer", "Multi-pawn detached XP audit (60) (4C-M1)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run60() { Phase4CBDetachedDiagnostics.RunMultiPawnManually(60); }

        [DebugAction("XP Customizer", "Multi-pawn detached XP audit (100) (4C-M1)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run100() { Phase4CBDetachedDiagnostics.RunMultiPawnManually(100); }

        [DebugAction("XP Customizer", "Print last multi-pawn audit (4C-M1)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Print() { Phase4CBDetachedDiagnostics.PrintLastMultiPawn(); }
    }
}
#endif
