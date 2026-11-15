#if DEBUG
using System;
using System.Collections.Generic;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;
using XpCustomizer.LearningCapacity;

namespace XpCustomizer
{
    /// <summary>
    /// R5-D1: read-only current-map candidate discovery for the existing
    /// R5-B daily-cap comparison and R5-C1 live post-cap observer.
    /// Does not create a pawn/job, call Learn(), change XP, settings or saves.
    /// It is NOT evidence that any observed XP payout has passed verification.
    /// </summary>
    internal static class Phase4CCReadinessScanner
    {
        internal const string Schema = "4C-C-READINESS-SCAN-R5D1-20261106";
        private const int MaxDisplayed = 24;

        private sealed class Candidate
        {
            internal string PawnName;
            internal string SkillName;
            internal float Daily;
            internal float ProductionCap;
            internal float IndependentCap;
            internal float Remaining;
            internal float RemainingFraction;
            internal bool Saturated;
            internal string CapComparison;
            internal string SaturationComparison;
            internal string ThresholdPath;
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string F(float value)
        {
            return value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value)) return "UNKNOWN";
            return value.Replace(';', '_').Replace('\r', '_').Replace('\n', '_');
        }

        // Ranking is for diagnostic convenience ONLY: actual job availability
        // and the rate of XP gain cannot be inferred from an XP balance.
        private static int CompareCandidates(Candidate a, Candidate b)
        {
            // Put review cases first so any disagreement cannot be hidden by
            // the 24-row log limit on very large colonies.
            bool aReview = a.CapComparison == "REVIEW_CAP_MISMATCH" ||
                a.SaturationComparison == "REVIEW_THRESHOLD_MISMATCH";
            bool bReview = b.CapComparison == "REVIEW_CAP_MISMATCH" ||
                b.SaturationComparison == "REVIEW_THRESHOLD_MISMATCH";
            if (aReview != bReview) return aReview ? -1 : 1;
            if (a.Saturated != b.Saturated) return a.Saturated ? -1 : 1;
            int remaining = a.RemainingFraction.CompareTo(b.RemainingFraction);
            if (remaining != 0) return remaining;
            int name = StringComparer.Ordinal.Compare(a.PawnName, b.PawnName);
            return name != 0 ? name : StringComparer.Ordinal.Compare(a.SkillName, b.SkillName);
        }

        internal static void ScanCurrentMap()
        {
            if (Current.Game == null || Find.CurrentMap == null ||
                Find.CurrentMap.mapPawns == null)
            {
                Log.Warning("[XPC-4CC][READINESS_SCAN] status=SKIP; reason=NO_LOADED_MAP" +
                    "; schema=" + Schema + "; build=" + XpCustomizerMod.BuildId);
                return;
            }

            var candidates = new List<Candidate>();
            int pawns = 0, scanned = 0, disabled = 0, errors = 0;
            int capPass = 0, capReview = 0, thresholdPass = 0, thresholdReview = 0;
            int thresholdInconclusive = 0, saturated = 0, dynamicSaturated = 0;
            bool foreignSaturationHook = Phase4CCLiveXpCapture.HasForeignSaturationHook();

            foreach (Pawn pawn in Find.CurrentMap.mapPawns.FreeColonistsSpawned)
            {
                if (pawn == null || pawn.Dead || pawn.skills == null ||
                    pawn.skills.skills == null || pawn.Faction != Faction.OfPlayer)
                    continue;
                pawns++;
                foreach (SkillRecord record in pawn.skills.skills)
                {
                    if (record == null || record.def == null) continue;
                    if (record.TotallyDisabled) { disabled++; continue; }
                    scanned++;
                    try
                    {
                        float daily = record.xpSinceMidnight;
                        float productionCap = LearningCapacityService.GetDailyCap(record);
                        bool engineSaturated = record.LearningSaturatedToday;
                        var proof = Phase4CCIndependentDailyCapOracle.Compose(
                            record, XpCustomizerMod.Settings);
                        Phase4CCIndependentDailyCapOracle.Compare(proof, daily,
                            productionCap, engineSaturated, foreignSaturationHook);
                        if (!proof.Ready || !Finite(daily) || !Finite(productionCap) ||
                            !Finite(proof.Cap))
                        {
                            errors++;
                            continue;
                        }
                        if (proof.CapComparison == "PASS_CAP") capPass++;
                        else if (proof.CapComparison == "REVIEW_CAP_MISMATCH") capReview++;
                        if (proof.SaturationComparison == "PASS_THRESHOLD") thresholdPass++;
                        else if (proof.SaturationComparison == "REVIEW_THRESHOLD_MISMATCH")
                            thresholdReview++;
                        else thresholdInconclusive++;
                        if (engineSaturated) saturated++;
                        if (engineSaturated && proof.ThresholdPath != "VANILLA_PASSTHROUGH" &&
                            proof.SaturationComparison == "PASS_THRESHOLD") dynamicSaturated++;

                        // Zero-cap is immediately saturated. For nonzero caps,
                        // sort unsaturated skills by fraction remaining, not by
                        // raw XP, so high/low configured caps are comparable.
                        float remaining = Math.Max(0f, productionCap - daily);
                        float fraction = productionCap > 0f ?
                            remaining / productionCap : (engineSaturated ? 0f : 1f);
                        if (!Finite(fraction)) { errors++; continue; }
                        candidates.Add(new Candidate
                        {
                            PawnName = Clean(pawn.LabelShort),
                            SkillName = Clean(record.def.defName),
                            Daily = daily,
                            ProductionCap = productionCap,
                            IndependentCap = proof.Cap,
                            Remaining = remaining,
                            RemainingFraction = fraction,
                            Saturated = engineSaturated,
                            CapComparison = proof.CapComparison,
                            SaturationComparison = proof.SaturationComparison,
                            ThresholdPath = proof.ThresholdPath
                        });
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        Log.Warning("[XPC-4CC][READINESS_SCAN_ERROR] pawn=" +
                            Clean(pawn.LabelShort) + "; skill=" + Clean(record.def.defName) +
                            "; exception=" + Clean(ex.GetType().Name) +
                            "; schema=" + Schema);
                    }
                }
            }

            candidates.Sort(CompareCandidates);
            int displayed = Math.Min(MaxDisplayed, candidates.Count);
            string status = capReview > 0 || thresholdReview > 0 ? "REVIEW" :
                errors > 0 ? "PARTIAL_READ_ERROR" : "SCAN_COMPLETE_NOT_XP_PROOF";
            var sb = new StringBuilder(3500);
            sb.Append("[XPC-4CC][READINESS_SCAN] status=").Append(status)
                .Append("; schema=").Append(Schema)
                .Append("; build=").Append(XpCustomizerMod.BuildId)
                .Append("; map=ACTIVE_MAP; scope=FREE_COLONISTS_SPAWNED")
                .Append("; pawnWrites=0; xpWrites=0; settingsWrites=0; saveWrites=0")
                .Append("; liveXpVerifiedByScan=False")
                .Append("; foreignSaturationHook=").Append(foreignSaturationHook)
                .Append("; pawns=").Append(pawns)
                .Append("; eligibleSkills=").Append(scanned)
                .Append("; disabledSkills=").Append(disabled)
                .Append("; candidates=").Append(candidates.Count)
                .Append("; engineSaturated=").Append(saturated)
                .Append("; independentlyConfirmedDynamicSaturated=").Append(dynamicSaturated)
                .Append("; capPass=").Append(capPass)
                .Append("; capReview=").Append(capReview)
                .Append("; thresholdPass=").Append(thresholdPass)
                .Append("; thresholdReview=").Append(thresholdReview)
                .Append("; thresholdInconclusive=").Append(thresholdInconclusive)
                .Append("; readErrors=").Append(errors)
                .Append("; printed=").Append(displayed)
                .Append("; omitted=").Append(candidates.Count - displayed);
            for (int i = 0; i < displayed; i++)
            {
                Candidate c = candidates[i];
                sb.Append("\n[XPC-4CC][READINESS_CANDIDATE] rank=").Append(i + 1)
                    .Append("; pawn=").Append(c.PawnName)
                    .Append("; skill=").Append(c.SkillName)
                    .Append("; daily=").Append(F(c.Daily))
                    .Append("; productionCap=").Append(F(c.ProductionCap))
                    .Append("; independentCap=").Append(F(c.IndependentCap))
                    .Append("; remaining=").Append(F(c.Remaining))
                    .Append("; remainingFraction=").Append(F(c.RemainingFraction))
                    .Append("; engineSaturated=").Append(c.Saturated)
                    .Append("; thresholdPath=").Append(c.ThresholdPath)
                    .Append("; capComparison=").Append(c.CapComparison)
                    .Append("; saturationComparison=").Append(c.SaturationComparison);
            }
            sb.Append("\n[XPC-4CC][READINESS_SCAN_NEXT] scanOnly=True")
                .Append("; ifSaturated=RUN_SELECTED_PAWN_R5B_THEN_LIVE_CAPTURE")
                .Append("; ifNone=WAIT_FOR_REAL_XP_OR_TEST_SAVE_ONLY")
                .Append("; noAutomaticJobsOrSkillMutation=True")
                .Append("; vanillaThresholdAndForeignModsAreNotIndependentlyProven=True");
            if (status == "REVIEW") Log.Warning(sb.ToString());
            else Log.Message(sb.ToString());
        }
    }

    public static class Phase4CCReadinessScannerDebugActions
    {
        [DebugAction("XP Customizer", "Scan all colonists post-cap readiness (R5-D)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Scan() { Phase4CCReadinessScanner.ScanCurrentMap(); }
    }
}
#endif
