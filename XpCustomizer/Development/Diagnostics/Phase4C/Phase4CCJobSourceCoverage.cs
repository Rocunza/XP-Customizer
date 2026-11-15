#if DEBUG
using System;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// R4-S2 passive JobDef-name/SkillDef pairing and partitioned work quotas.
    /// This is contextual attribution, NOT a proof of the Learn() call site.
    /// Gameplay jobs, records, and XP are never modified here.
    /// </summary>
    internal static partial class Phase4CCLiveXpCapture
    {
        private const int MaxConstructionSamples = 32;
        private const int MaxHarvestSamples = 32;
        private const int MaxCuttingSamples = 32;
        private const int MaxMiningSamples = 32;
        private const int MaxOtherWorkSamples = 16;
        private const int MaxWorkSamples = MaxConstructionSamples + MaxHarvestSamples +
            MaxCuttingSamples + MaxMiningSamples + MaxOtherWorkSamples;

        private static readonly string[] WorkKeywords = {
            "Construct", "Build", "Repair", "Frame", "Mine", "DoBill", "Sow", "Harvest",
            "CutPlant", "CutTree", "Chop", "Fell", "Tend", "Hunt", "Train", "Research",
            "Cook", "Craft", "Smelt", "Make", "Fish", "Operate", "Refine", "Art", "Grow", "Smooth"
        };

        private static readonly string[] TrackedWorkKinds = {
            "CONSTRUCTION_CANDIDATE", "HARVEST_CANDIDATE", "CUTTING_CANDIDATE",
            "MINING_CANDIDATE", "OTHER_WORK_CANDIDATE"
        };
        private static readonly Dictionary<string, int> capturedByWorkKind =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> workKindQuotaDrops =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> perPawnGroupDrops =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private static string Classify(string job, bool readingScope)
        {
            if (readingScope || job.IndexOf("Read", StringComparison.OrdinalIgnoreCase) >= 0)
                return "READING";
            if (job == "NO_JOB") return "NO_JOB";
            foreach (string keyword in WorkKeywords)
                if (job.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return "WORK_CANDIDATE";
            return "OTHER_JOB";
        }

        private static string WorkKind(string job)
        {
            // Only the observed vanilla JobDef 'Mine' is accepted for the mining
            // formula; e.g. 'MineField' is NOT an accepted source pairing.
            if (string.Equals(job, "Mine", StringComparison.Ordinal))
                return "MINING_CANDIDATE";
            if (job.IndexOf("Construct", StringComparison.OrdinalIgnoreCase) >= 0 ||
                job.IndexOf("Build", StringComparison.OrdinalIgnoreCase) >= 0 ||
                job.IndexOf("Repair", StringComparison.OrdinalIgnoreCase) >= 0 ||
                job.IndexOf("Frame", StringComparison.OrdinalIgnoreCase) >= 0 ||
                job.IndexOf("Deconstruct", StringComparison.OrdinalIgnoreCase) >= 0 ||
                job.IndexOf("Smooth", StringComparison.OrdinalIgnoreCase) >= 0)
                return "CONSTRUCTION_CANDIDATE";
            if (job.IndexOf("CutPlant", StringComparison.OrdinalIgnoreCase) >= 0 ||
                job.IndexOf("Chop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                job.IndexOf("Fell", StringComparison.OrdinalIgnoreCase) >= 0 ||
                job.IndexOf("CutTree", StringComparison.OrdinalIgnoreCase) >= 0)
                return "CUTTING_CANDIDATE";
            if (job.IndexOf("Harvest", StringComparison.OrdinalIgnoreCase) >= 0)
                return "HARVEST_CANDIDATE";
            return "OTHER_WORK_CANDIDATE";
        }

        // Same contextual policy for R3 payout and R4 independent-rate audits.
        private static bool ExpectedWorkSkill(StartState b)
        {
            return b != null && ExpectedWorkSkill(b.Job, b.Skill, b.WorkKind);
        }

        // Shared with R5-A's reserved selection gate: never let an unrelated
        // work+skill pairing consume a scarce post-cap observation slot.
        private static bool ExpectedWorkSkill(string job, string skill, string kind)
        {
            if (kind == "MINING_CANDIDATE")
                return job == "Mine" && skill == "Mining";
            if (kind == "CONSTRUCTION_CANDIDATE") return skill == "Construction";
            if (kind == "HARVEST_CANDIDATE" || kind == "CUTTING_CANDIDATE")
                return skill == "Plants";
            return false;
        }

        private static int WorkKindCap(string kind)
        {
            switch (kind)
            {
                case "CONSTRUCTION_CANDIDATE": return MaxConstructionSamples;
                case "HARVEST_CANDIDATE": return MaxHarvestSamples;
                case "CUTTING_CANDIDATE": return MaxCuttingSamples;
                case "MINING_CANDIDATE": return MaxMiningSamples;
                default: return MaxOtherWorkSamples;
            }
        }

        private static bool WorkKindHasRoom(string kind)
        {
            int count;
            capturedByWorkKind.TryGetValue(kind, out count);
            return count < WorkKindCap(kind);
        }

        private static void RecordWorkKind(string kind)
        {
            int count;
            capturedByWorkKind.TryGetValue(kind, out count);
            capturedByWorkKind[kind] = count + 1;
        }

        private static void RecordWorkDrop(Dictionary<string, int> counter, string kind)
        {
            int prior;
            counter.TryGetValue(kind, out prior);
            counter[kind] = prior + 1;
        }

        private static int WorkDropCount(Dictionary<string, int> counter, string kind)
        {
            int count;
            counter.TryGetValue(kind, out count);
            return count;
        }

        // NOTE: Counters report only captured, admissible job/skill contexts.
        // Jobs or genuine XP events excluded by quotas cannot be asserted absent.
        private sealed class KindEvidence
        {
            internal int Captured;
            internal int PositiveInput;
            internal int Matched;
            internal int PayoutPass;
            internal int RatePass;
            internal int Skipped;
            internal int Review;
            internal int LowGwsPass;
            internal int HighGwsPass;
            internal int ConsciousnessNonNeutralPass;
            internal int SaturatedPass;
        }

        private static void AppendWorkKindCoverage(StringBuilder sb)
        {
            var evidence = new Dictionary<string, KindEvidence>(StringComparer.Ordinal);
            foreach (string kind in TrackedWorkKinds) evidence[kind] = new KindEvidence();
            foreach (Sample sample in samples)
            {
                StartState b = sample.Before;
                if (b.Category != "WORK_CANDIDATE") continue;
                KindEvidence e;
                if (!evidence.TryGetValue(b.WorkKind, out e)) e = evidence["OTHER_WORK_CANDIDATE"];
                e.Captured++;
                if (!(b.Input > 0f)) continue;
                e.PositiveInput++;
                if (!ExpectedWorkSkill(b)) { e.Skipped++; continue; }
                e.Matched++;
                string payout = PayoutClass(sample);
                string rate = IndependentRateClass(sample);
                if (payout == "PASS_PAYOUT") e.PayoutPass++;
                if (rate == "PASS_INDEPENDENT_RATE")
                {
                    e.RatePass++;
                    if (b.OracleGwsFactor < .999f) e.LowGwsPass++;
                    if (b.OracleGwsFactor > 1.001f) e.HighGwsPass++;
                    if (Math.Abs(b.OracleSpeedFactor - 1f) > .001f)
                        e.ConsciousnessNonNeutralPass++;
                    if (b.SaturatedBefore) e.SaturatedPass++;
                }
                if (payout.StartsWith("REVIEW_", StringComparison.Ordinal) ||
                    rate.StartsWith("REVIEW_", StringComparison.Ordinal)) e.Review++;
                else if (payout != "PASS_PAYOUT" || rate != "PASS_INDEPENDENT_RATE") e.Skipped++;
            }
            int kindsWithRatePass = 0;
            int allConsNonNeutral = 0;
            int allLow = 0, allHigh = 0, allSaturated = 0;
            foreach (string kind in TrackedWorkKinds)
            {
                KindEvidence e = evidence[kind];
                if (e.RatePass > 0 && kind != "OTHER_WORK_CANDIDATE") kindsWithRatePass++;
                allConsNonNeutral += e.ConsciousnessNonNeutralPass;
                allLow += e.LowGwsPass;
                allHigh += e.HighGwsPass;
                allSaturated += e.SaturatedPass;
                sb.Append("\n[XPC-4CC][WORK_KIND_COVERAGE] kind=").Append(kind)
                    .Append("; quota=").Append(WorkKindCap(kind))
                    .Append("; captured=").Append(e.Captured)
                    .Append("; positiveInput=").Append(e.PositiveInput)
                    .Append("; jobSkillMatched=").Append(e.Matched)
                    .Append("; payoutPass=").Append(e.PayoutPass)
                    .Append("; independentRatePass=").Append(e.RatePass)
                    .Append("; skipped=").Append(e.Skipped)
                    .Append("; review=").Append(e.Review)
                    .Append("; gwsLowPass=").Append(e.LowGwsPass)
                    .Append("; gwsHighPass=").Append(e.HighGwsPass)
                    .Append("; consciousnessNonNeutralPass=").Append(e.ConsciousnessNonNeutralPass)
                    .Append("; saturatedPass=").Append(e.SaturatedPass)
                    .Append("; droppedByKindQuota=").Append(WorkDropCount(workKindQuotaDrops, kind))
                    .Append("; droppedByPawnGroupCap=").Append(WorkDropCount(perPawnGroupDrops, kind))
                    .Append("; status=").Append(e.RatePass > 0 ? "OBSERVED_RATE_PASS" : "NOT_VERIFIED")
                    .Append("; attribution=JOBDEF_NAME_AND_SKILL_NOT_CALLSITE_PROOF");
            }
            int quotaDropsTotal = 0, groupDropsTotal = 0;
            foreach (string kind in TrackedWorkKinds)
            {
                quotaDropsTotal += WorkDropCount(workKindQuotaDrops, kind);
                groupDropsTotal += WorkDropCount(perPawnGroupDrops, kind);
            }
            int bypassNonNeutral = 0;
            int readingConsNonNeutral = 0;
            int readingSaturated = 0;
            foreach (Sample sample in samples)
            {
                StartState b = sample.Before;
                if (b.Category != "READING" || IndependentRateClass(sample) != "PASS_INDEPENDENT_RATE")
                    continue;
                if (Math.Abs(b.OracleSpeedFactor - 1f) > .001f) readingConsNonNeutral++;
                if (b.SaturatedBefore) readingSaturated++;
                if (b.ReadingScope && b.OracleWorkSpeed >= 0f &&
                    Math.Abs(b.OracleGwsFactor - 1f) <= .001f &&
                    Math.Abs(b.OracleGwsWithoutReading - 1f) > .001f &&
                    XpCustomizerMod.Settings != null && !XpCustomizerMod.Settings.gwsApplyToReading)
                    bypassNonNeutral++;
            }
            sb.Append("\n[XPC-4CC][SCENARIO_COVERAGE] verifiedWorkKinds=").Append(kindsWithRatePass)
                .Append("/4; lowGwsWorkPass=").Append(allLow)
                .Append("; highGwsWorkPass=").Append(allHigh)
                .Append("; consciousnessNonNeutralWorkPass=").Append(allConsNonNeutral)
                .Append("; consciousnessNonNeutralReadingPass=").Append(readingConsNonNeutral)
                .Append("; saturatedWorkPass=").Append(allSaturated)
                .Append("; saturatedReadingPass=").Append(readingSaturated)
                .Append("; readingBypassNonNeutralPass=").Append(bypassNonNeutral)
                .Append("; droppedByKindQuota=").Append(quotaDropsTotal)
                .Append("; droppedByPawnGroupCap=").Append(groupDropsTotal)
                .Append("; missingBuckets=NOT_OBSERVED_NOT_FAILURE")
                .Append("; attribution=JOBDEF_NAME_SKILL_CONTEXT_NOT_CALLSITE_PROVEN");
        }

        private static void VerifyJobSourceCoverageSelfAudit()
        {
            int cases = 0;
            RequireJobWorkKind("Mine", "MINING_CANDIDATE"); cases++;
            RequireJobWorkKind("FinishFrame", "CONSTRUCTION_CANDIDATE"); cases++;
            RequireJobWorkKind("HarvestDesignated", "HARVEST_CANDIDATE"); cases++;
            RequireJobWorkKind("CutPlant", "CUTTING_CANDIDATE"); cases++;
            RequireJobWorkKind("DoBill", "OTHER_WORK_CANDIDATE"); cases++;
            RequireJobWorkKind("MineField", "OTHER_WORK_CANDIDATE"); cases++;
            RequireJobSkill("Mine", "Mining", true); cases++;
            RequireJobSkill("Mine", "Social", false); cases++;
            RequireJobSkill("MineField", "Mining", false); cases++;
            RequireJobSkill("FinishFrame", "Construction", true); cases++;
            RequireJobSkill("FinishFrame", "Social", false); cases++;
            RequireJobSkill("HarvestDesignated", "Plants", true); cases++;
            RequireJobSkill("CutPlant", "Plants", true); cases++;
            RequireJobSkill("DoBill", "Crafting", false); cases++;
            foreach (string kind in TrackedWorkKinds)
            {
                if (WorkKindCap(kind) <= 0) throw new InvalidOperationException("Missing work quota: " + kind);
                cases++;
            }
            if (MaxWorkSamples != MaxConstructionSamples + MaxHarvestSamples +
                MaxCuttingSamples + MaxMiningSamples + MaxOtherWorkSamples)
                throw new InvalidOperationException("4C-C R4-S2 quota total mismatch");
            cases++;
            if (Classify("Mine", false) != "WORK_CANDIDATE" ||
                Classify("Reading", true) != "READING" ||
                Classify("NO_JOB", false) != "NO_JOB")
                throw new InvalidOperationException("4C-C R4-S2 category regression");
            cases++;
            // Prove both pre-existing production-payout and independent-rate
            // classifiers now accept exact Mine+Mining, never Mine+Social.
            var mining = new StartState {
                Job = "Mine", WorkKind = WorkKind("Mine"), Skill = "Mining",
                Category = "WORK_CANDIDATE", Input = 8f,
                RateBefore = .5f, RateCaptured = true,
                OracleRate = .5f, OracleReady = true
            };
            var observed = new Sample {
                Before = mining, Comparable = true,
                DeltaXp = 4f, DeltaDaily = 4f
            };
            RequirePayout(observed, "PASS_PAYOUT", "MINING_PAYOUT"); cases++;
            RequireOracleClass(observed, "PASS_INDEPENDENT_RATE", "MINING_RATE"); cases++;
            mining.Skill = "Social";
            RequirePayout(observed, "SKIP_JOB_SKILL_UNVERIFIED", "MINING_SOCIAL_PAYOUT"); cases++;
            RequireOracleClass(observed, "SKIP_JOB_SKILL_UNVERIFIED", "MINING_SOCIAL_RATE"); cases++;
            mining.Skill = "Mining";
            mining.Job = "MineField";
            mining.WorkKind = WorkKind("MineField");
            RequirePayout(observed, "SKIP_JOB_SKILL_UNVERIFIED", "MINING_OTHER_JOB_PAYOUT"); cases++;
            RequireOracleClass(observed, "SKIP_JOB_SKILL_UNVERIFIED", "MINING_OTHER_JOB_RATE"); cases++;
            Log.Message("[XPC-4CC][JOB_COVERAGE_AUDIT] status=PASS; cases=" + cases +
                "; miningExactJobDefOnly=True; unrelatedXpExcluded=True; partitionQuotaSum=" +
                MaxWorkSamples + "; noPawnWrites=True; noSettingsWrites=True; revision=" + Revision);
        }

        private static void RequireJobWorkKind(string job, string expected)
        {
            string actual = WorkKind(job);
            if (actual != expected)
                throw new InvalidOperationException("4C-C R4-S2 job=" + job + " expected=" + expected + " actual=" + actual);
        }

        private static void RequireJobSkill(string job, string skill, bool expected)
        {
            var state = new StartState { Job = job, Skill = skill, WorkKind = WorkKind(job) };
            if (ExpectedWorkSkill(state) != expected)
                throw new InvalidOperationException("4C-C R4-S2 pairing=" + job + "/" + skill + " expected=" + expected);
        }
    }
}
#endif
