#if DEBUG
using System;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// R3: passive payout evidence and report. No gameplay writes or new
    /// Harmony owners; decay classification retains the HF1 verdict logic.
    /// </summary>
    internal static partial class Phase4CCLiveXpCapture
    {
        private static void AppendSampleLine(StringBuilder sb, Sample s, int index)
        {
            StartState b = s.Before;
            sb.Append("\n[XPC-4CC][SAMPLE] n=").Append(index + 1)
                .Append("; tick=").Append(b.Tick)
                .Append("; pawn=").Append(b.PawnName)
                .Append("; job=").Append(b.Job)
                .Append("; kind=").Append(b.WorkKind)
                .Append("; category=").Append(b.Category)
                .Append("; skill=").Append(b.Skill);
            if (b.Category == "WORK_CANDIDATE") AppendTargetFields(sb, b);
            if (b.SourceProof != null)
                sb.Append("; sourceStatus=").Append(b.SourceProof.Status)
                    .Append("; sourceRoute=").Append(b.SourceProof.Route);
            sb.Append("; inputXp=").Append(F(b.Input))
                .Append("; direct=").Append(b.Direct)
                .Append("; ignoreLearnRate=").Append(b.IgnoreLearnRate)
                .Append("; readingScope=").Append(b.ReadingScope)
                .Append("; intervalScope=").Append(b.FromInterval)
                .Append("; levelRaw=").Append(b.RawLevel).Append("->").Append(s.AfterRawLevel)
                .Append("; level=").Append(b.Level).Append("->").Append(s.AfterLevel)
                .Append("; disabled=").Append(b.TotallyDisabled).Append("->").Append(s.AfterDisabled)
                .Append("; biologicalAgeYears=").Append(b.BiologicalAgeYears)
                .Append("; observedXp=").Append(s.Comparable ? F(s.DeltaXp) : "INCONCLUSIVE_LEVEL_CHANGE")
                .Append("; dailyXpDelta=").Append(F(s.DeltaDaily));
            if ((b.Category == "WORK_CANDIDATE" || b.Category == "READING") && b.Input > 0f)
                sb.Append("; jointClass=").Append(JointClass(s))
                    .Append("; payoutClass=").Append(PayoutClass(s))
                    .Append("; preRate=").Append(b.RateCaptured ? F(b.RateBefore) : "NA")
                    .Append("; expectedPayout=").Append(b.RateCaptured ? F(b.Input * b.RateBefore) : "NA")
                    .Append("; rateCaptureReason=").Append(b.RateCaptureReason ?? "NA")
                    .Append("; gwsFactor=").Append(b.RateCaptured ? F(b.GwsFactorBefore) : "NA")
                    .Append("; saturatedBefore=").Append(b.SaturatedBefore)
                    .Append("; independentRateClass=").Append(IndependentRateClass(s))
                    .Append("; independentlyExpectedRate=").Append(b.OracleReady ? F(b.OracleRate) : "NA");
            if (b.Input < 0f || (s.Comparable && s.DeltaXp < -0.0001f))
                sb.Append("; negativeClass=").Append(NegativeClassification(s))
                    .Append("; levelBasePerDay=").Append(b.HasExpectedDecay ? F(b.ConfiguredBaseDaily) : "NA")
                    .Append("; activeSkillMultiplier=").Append(b.HasExpectedDecay ? F(b.ActiveSkillMultiplier) : "NA")
                    .Append("; memoryRemaining=").Append(b.HasExpectedDecay ? F(b.MemoryRemaining) : "NA")
                    .Append("; greatMemory=").Append(b.HasGreatMemory)
                    .Append("; perfectMemory=").Append(b.HasPerfectMemory)
                    .Append("; anomalyActive=").Append(b.AnomalyActive)
                    .Append("; expectedPerDay=").Append(b.HasExpectedDecay ? F(b.ExpectedDaily) : "NA")
                    .Append("; expectedPerInterval=").Append(b.HasExpectedDecay ? F(-b.ExpectedInterval) : "NA");
        }

        // First select one representative per Job+Skill, then use remaining slots
        // for repeated samples. Later work types do not disappear behind harvest.
        private static int AppendPreviews(StringBuilder sb, string category, int limit)
        {
            var usedGroups = new HashSet<string>(StringComparer.Ordinal);
            var shown = new HashSet<int>();
            for (int pass = 0; pass < 2 && shown.Count < limit; pass++)
            {
                for (int i = 0; i < samples.Count && shown.Count < limit; i++)
                {
                    StartState b = samples[i].Before;
                    bool match = category == "OTHER" ?
                        b.Category != "WORK_CANDIDATE" && b.Category != "READING" &&
                        b.Category != "DECAY_INTERVAL" :
                        b.Category == category;
                    if (!match || shown.Contains(i)) continue;
                    if (pass == 0 && !usedGroups.Add(b.GroupKey)) continue;
                    shown.Add(i);
                    AppendSampleLine(sb, samples[i], i);
                }
            }
            return shown.Count;
        }

        private static int AppendDecayExamples(StringBuilder sb)
        {
            const int MaxDecayExamples = 12;
            int shown = 0;
            var disabledSeen = new HashSet<string>(StringComparer.Ordinal);
            var levelsSeen = new HashSet<int>();
            // Priority: real mismatches > disabled skill samples > level diversity.
            // A single child may generate multiple identical disabled-skill ticks;
            // log one representative instead of hiding other useful cases.
            for (int pass = 0; pass < 3 && shown < MaxDecayExamples; pass++)
            {
                foreach (Sample s in samples)
                {
                    StartState b = s.Before;
                    if (!b.FromInterval || b.Input >= 0f) continue;
                    string result = NegativeClassification(s);
                    bool mismatch = result == "INTERVAL_INPUT_CONFIG_MISMATCH" ||
                        result == "INTERVAL_DELTA_MISMATCH" ||
                        result == "INTERVAL_NEGATIVE_DELTA_UNEXPECTED" ||
                        result == "INTERVAL_DISABLED_SKILL_CHANGED_REVIEW";
                    bool disabledSkip = result == "INTERVAL_DISABLED_SKILL_SKIP";
                    if (pass == 0 && !mismatch) continue;
                    if (pass == 1 && (!disabledSkip ||
                        !disabledSeen.Add(b.PawnName + "|" + b.Skill))) continue;
                    if (pass == 2 && (mismatch || disabledSkip || !levelsSeen.Add(b.RawLevel))) continue;
                    sb.Append("\n[XPC-4CC][DECAY_SAMPLE] pawn=").Append(b.PawnName)
                        .Append("; skill=").Append(b.Skill)
                        .Append("; biologicalAgeYears=").Append(b.BiologicalAgeYears)
                        .Append("; disabledBefore=").Append(b.TotallyDisabled)
                        .Append("; disabledAfter=").Append(s.AfterDisabled)
                        .Append("; rawLevel=").Append(b.RawLevel).Append("->").Append(s.AfterRawLevel)
                        .Append("; displayedLevel=").Append(b.Level).Append("->").Append(s.AfterLevel)
                        .Append("; basePerDay=").Append(b.HasExpectedDecay ? F(b.ConfiguredBaseDaily) : "NA")
                        .Append("; activeSkillMultiplier=").Append(b.HasExpectedDecay ? F(b.ActiveSkillMultiplier) : "NA")
                        .Append("; greatMemory=").Append(b.HasGreatMemory)
                        .Append("; perfectMemory=").Append(b.HasPerfectMemory)
                        .Append("; anomalyActive=").Append(b.AnomalyActive)
                        .Append("; memoryRemaining=").Append(b.HasExpectedDecay ? F(b.MemoryRemaining) : "NA")
                        .Append("; finalPerDay=").Append(b.HasExpectedDecay ? F(b.ExpectedDaily) : "NA")
                        .Append("; expectedInput=").Append(b.HasExpectedDecay ? F(-b.ExpectedInterval) : "NA")
                        .Append("; actualInput=").Append(F(b.Input))
                        .Append("; observedDelta=").Append(s.Comparable ? F(s.DeltaXp) : "LEVEL_CHANGE")
                        .Append("; status=").Append(result);
                    shown++;
                    if (shown >= MaxDecayExamples) break;
                }
            }
            return shown;
        }

        private static string BuildReport()
        {
            FinalizeBillMathPreviewSession(); // Stop after observer unpatch; once per Sample.
            SealOfficialBillSession(); // Immutable approval decision BEFORE official counters.
            int work = 0, reading = 0, other = 0, decay = 0, comparable = 0, changedLevel = 0;
            int workMeasured = 0, readingMeasured = 0;
            int negativeTotal = 0, intervalMatched = 0, intervalMismatch = 0;
            int intervalDisabledSkip = 0, intervalInconclusive = 0, negativeOtherSource = 0;
            float workXp = 0f, readingXp = 0f;
            int payoutWorkPass = 0, payoutReadingPass = 0, payoutSkip = 0;
            int payoutInconclusive = 0, payoutReviews = 0;
            int payoutGwsLow = 0, payoutGwsHigh = 0, payoutGwsNeutral = 0;
            int payoutReadingGwsBypass = 0;
            int independentWorkPass = 0, independentReadingPass = 0;
            int independentReview = 0, independentSkip = 0, independentInconclusive = 0;
            int independentLowGws = 0, independentHighGws = 0, independentNeutralGws = 0;
            int independentSaturated = 0, independentReadingBypass = 0;
            int independentReadingBypassNonNeutral = 0;
            int jointCandidates = 0, jointWorkPass = 0, jointReadingPass = 0;
            int jointReview = 0, jointInconclusive = 0, jointSkip = 0;
            var skillSummary = new SkillSummaryAccumulator();
            var jointEvidence = new List<Sample>();
            var independentEvidence = new List<Sample>();
            var payoutEvidence = new List<Sample>();
            var workJobs = new Dictionary<string, WorkCoverage>(StringComparer.Ordinal);
            var distinctWorkJobSkills = new HashSet<string>(StringComparer.Ordinal);
            foreach (Sample s in samples)
            {
                StartState b = s.Before;
                string skillPayout = null, skillIndependent = null, skillJoint = null;
                bool isWork = b.Category == "WORK_CANDIDATE";
                bool isRead = b.Category == "READING";
                bool isDecay = b.Category == "DECAY_INTERVAL";
                if (isWork) work++;
                else if (isRead) reading++;
                else if (isDecay) decay++;
                else other++;
                WorkCoverage jobCoverage = null;
                if (isWork)
                {
                    distinctWorkJobSkills.Add(b.Job + "|" + b.Skill);
                    if (!workJobs.TryGetValue(b.GroupKey, out jobCoverage))
                    {
                        jobCoverage = new WorkCoverage { Pawn = b.PawnName, Job = b.Job, Skill = b.Skill, Kind = b.WorkKind };
                        workJobs.Add(b.GroupKey, jobCoverage);
                    }
                    jobCoverage.Calls++;
                }
                if (!s.Comparable) changedLevel++;
                else
                {
                    comparable++;
                    if (isWork && s.DeltaXp > 0.0001f)
                    {
                        workMeasured++; workXp += s.DeltaXp;
                        jobCoverage.PositiveCalls++; jobCoverage.PositiveXp += s.DeltaXp;
                    }
                    if (isRead && s.DeltaXp > 0.0001f)
                    {
                        readingMeasured++; readingXp += s.DeltaXp;
                    }
                }
                if ((isWork || isRead) && b.Input > 0f)
                {
                    string payout = PayoutClass(s);
                    skillPayout = payout;
                    if (payout == "PASS_PAYOUT")
                    {
                        if (isWork) payoutWorkPass++;
                        else payoutReadingPass++;
                        if (b.GwsFactorBefore < 0.999f) payoutGwsLow++;
                        else if (b.GwsFactorBefore > 1.001f) payoutGwsHigh++;
                        else payoutGwsNeutral++;
                        if (isRead && XpCustomizerMod.Settings != null &&
                            !XpCustomizerMod.Settings.gwsApplyToReading &&
                            Math.Abs(b.GwsFactorBefore - 1f) <= 0.001f)
                            payoutReadingGwsBypass++;
                    }
                    else if (payout.StartsWith("REVIEW_", StringComparison.Ordinal)) payoutReviews++;
                    else if (payout.StartsWith("INCONCLUSIVE_", StringComparison.Ordinal)) payoutInconclusive++;
                    else payoutSkip++;
                    payoutEvidence.Add(s);
                    string independent = IndependentRateClass(s);
                    skillIndependent = independent;
                    if (independent == "PASS_INDEPENDENT_RATE")
                    {
                        if (isWork) independentWorkPass++;
                        else independentReadingPass++;
                        if (b.OracleGwsFactor < 0.999f) independentLowGws++;
                        else if (b.OracleGwsFactor > 1.001f) independentHighGws++;
                        else independentNeutralGws++;
                        if (b.SaturatedBefore) independentSaturated++;
                        if (isRead && b.ReadingScope && XpCustomizerMod.Settings != null &&
                            !XpCustomizerMod.Settings.gwsApplyToReading &&
                            Math.Abs(b.OracleGwsFactor - 1f) <= 0.001f)
                        {
                            independentReadingBypass++;
                            if (b.OracleWorkSpeed >= 0f &&
                                Math.Abs(b.OracleGwsWithoutReading - 1f) > 0.001f)
                                independentReadingBypassNonNeutral++;
                        }
                    }
                    else if (independent.StartsWith("REVIEW_", StringComparison.Ordinal)) independentReview++;
                    else if (independent.StartsWith("INCONCLUSIVE_", StringComparison.Ordinal)) independentInconclusive++;
                    else independentSkip++;
                    independentEvidence.Add(s);
                    // Payout and independent Rate are classified against the
                    // SAME Sample. Legacy counters above remain separate.
                    string joint = JointClassFromResults(payout, independent);
                    skillJoint = joint;
                    jointCandidates++;
                    if (joint == "JOINT_PASS")
                    {
                        if (isWork) jointWorkPass++;
                        else jointReadingPass++;
                    }
                    else if (joint == "JOINT_REVIEW") jointReview++;
                    else if (joint == "JOINT_INCONCLUSIVE") jointInconclusive++;
                    else jointSkip++;
                    jointEvidence.Add(s);
                }
                if (b.Input < 0f || (s.Comparable && s.DeltaXp < -0.0001f))
                {
                    negativeTotal++;
                    string kind = NegativeClassification(s);
                    if (kind == "INTERVAL_CONFIG_MATCH") intervalMatched++;
                    else if (kind == "INTERVAL_DISABLED_SKILL_SKIP") intervalDisabledSkip++;
                    else if (kind == "INTERVAL_INPUT_CONFIG_MISMATCH" ||
                        kind == "INTERVAL_DELTA_MISMATCH" ||
                        kind == "INTERVAL_NEGATIVE_DELTA_UNEXPECTED" ||
                        kind == "INTERVAL_DISABLED_SKILL_CHANGED_REVIEW") intervalMismatch++;
                    else if (kind == "NEGATIVE_OUTSIDE_INTERVAL") negativeOtherSource++;
                    else intervalInconclusive++;
                }
                // Only the already-calculated, same-Sample results enter the T03 reducer.
                // Reserved post-cap/transition lists are deliberately excluded.
                skillSummary.Add(s, skillPayout, skillIndependent, skillJoint);
            }
            // R5-A1 post-cap summaries are calculated separately and may
            // overlap a regular sample; they are never added to old counters.
            lastPayoutReviewCount = payoutReviews;
            lastRateReviewCount = independentReview;
            string workStatus = workMeasured > 0 ? "OBSERVED" : work == 0 ?
                "SKIP_NO_WORK_JOB_XP_CALL" : "INCONCLUSIVE_NO_POSITIVE_XP";
            string readStatus = readingMeasured > 0 ? "OBSERVED" : reading == 0 ?
                "SKIP_NO_READING_XP_CALL" : "INCONCLUSIVE_NO_POSITIVE_XP";
            var sb = new StringBuilder(6500);
            sb.Append("[XP Customizer][4C-C] REPORT; session=").Append(sessionNumber)
                .Append("; build=").Append(XpCustomizerMod.BuildId)
                .Append("; revision=").Append(Revision)
                .Append("; startUtc=").Append(startedUtc.ToString("o"))
                .Append("; durationSeconds=").Append((int)(DateTime.UtcNow - startedUtc).TotalSeconds)
                .Append("; startTick=").Append(startedTick)
                .Append("; selectedPawn=").Append(sessionPawn == null ? "ALL_ON_MAP" : Clean(sessionPawn.LabelShort))
                .Append("; preexistingLearnHookOwners=").Append(foreignHooksAtStart)
                .Append("; preexistingIntervalHookOwners=").Append(intervalHookOwnersAtStart)
                .Append("; diagnosticHookStillInstalled=").Append(installed)
                .Append("; captureLimitReached=").Append(limitReached)
                .Append("; workKindCaps=construction32_harvest32_cutting32_mining32_other16; perJobSkillPawnCaps=10/10/8/8; disabledNonDecayCap=2");
            sb.Append("\n[XPC-4CC][SUMMARY] samples=").Append(samples.Count)
                .Append("; workCandidateCalls=").Append(work)
                .Append("; readingCalls=").Append(reading)
                .Append("; otherCalls=").Append(other)
                .Append("; decayIntervalCalls=").Append(decay)
                .Append("; distinctWorkJobSkills=").Append(distinctWorkJobSkills.Count)
                .Append("; distinctWorkJobSkillPawnGroups=").Append(workJobs.Count)
                .Append("; comparableSameLevel=").Append(comparable)
                .Append("; levelTransitions=INCONCLUSIVE:").Append(changedLevel)
                .Append("; hookErrors=").Append(hookErrors)
                .Append("; negativeXpCalls=").Append(negativeTotal);
            sb.Append("\n[XPC-4CC][WORK] status=").Append(workStatus)
                .Append("; measuredPositiveCalls=").Append(workMeasured)
                .Append("; observedXp=").Append(F(workXp));
            int construction = 0, cutting = 0, harvest = 0, mining = 0;
            foreach (WorkCoverage c in workJobs.Values)
            {
                if (c.PositiveCalls == 0) continue;
                if (c.Kind == "CONSTRUCTION_CANDIDATE") construction++;
                else if (c.Kind == "CUTTING_CANDIDATE") cutting++;
                else if (c.Kind == "HARVEST_CANDIDATE") harvest++;
                else if (c.Kind == "MINING_CANDIDATE") mining++;
            }
            sb.Append("; constructionJobSkills=").Append(construction)
                .Append("; cuttingJobSkills=").Append(cutting)
                .Append("; harvestJobSkills=").Append(harvest)
                .Append("; miningJobSkills=").Append(mining);
            sb.Append("\n[XPC-4CC][READING] status=").Append(readStatus)
                .Append("; measuredPositiveCalls=").Append(readingMeasured)
                .Append("; observedXp=").Append(F(readingXp));
            sb.Append("\n[XPC-4CC][PAYOUT] workPass=").Append(payoutWorkPass)
                .Append("; readingPass=").Append(payoutReadingPass)
                .Append("; review=").Append(payoutReviews)
                .Append("; skip=").Append(payoutSkip)
                .Append("; inconclusive=").Append(payoutInconclusive)
                .Append("; gwsLowPass=").Append(payoutGwsLow)
                .Append("; gwsHighPass=").Append(payoutGwsHigh)
                .Append("; gwsNeutralPass=").Append(payoutGwsNeutral)
                .Append("; readingGwsBypassPass=").Append(payoutReadingGwsBypass)
                .Append("; oracle=PRE_LEARN_RATE_TIMES_INPUT")
                .Append("; independentRateFormulaProof=NO")
                .Append("; jobAttribution=").Append(officialBillResults.Count > 0 &&
                    officialBillSeal == OfficialBillSealed ?
                    "JOB_CONTEXT_PLUS_P0_CALLSITE_AUTHORIZED_BILL_ONLY" :
                    "JOBDEF_NAME_AND_SKILL_CONTEXT_NOT_CALLSITE_PROVEN");
            sb.Append("\n[XPC-4CC][INDEPENDENT_RATE] workPass=").Append(independentWorkPass)
                .Append("; readingPass=").Append(independentReadingPass)
                .Append("; review=").Append(independentReview)
                .Append("; skip=").Append(independentSkip)
                .Append("; inconclusive=").Append(independentInconclusive)
                .Append("; gwsLowPass=").Append(independentLowGws)
                .Append("; gwsHighPass=").Append(independentHighGws)
                .Append("; gwsNeutralPass=").Append(independentNeutralGws)
                .Append("; saturatedPass=").Append(independentSaturated)
                .Append("; readingBypassPass=").Append(independentReadingBypass)
                .Append("; readingBypassNonNeutralPass=").Append(independentReadingBypassNonNeutral)
                .Append("; statInputs=VANILLA_API_READS")
                .Append("; model=PASSION_GLOBAL_ANIMALS_CONS_GWS_POSTCAP")
                .Append("; productionFactorCallsInOracle=NONE")
                .Append("; saturationThresholdIndependentlyProven=NO")
                .Append("; foreignRateHookOwners=").Append(rateHookOwnersAtStart);
            sb.Append("\n[XPC-4CC][JOINT] schema=").Append(JointRevision)
                .Append("; status=").Append(JointCoverageStatus(jointCandidates,
                    jointWorkPass + jointReadingPass, jointReview, jointInconclusive))
                .Append("; positiveInputCandidateEvents=").Append(jointCandidates)
                .Append("; workPass=").Append(jointWorkPass)
                .Append("; readingPass=").Append(jointReadingPass)
                .Append("; review=").Append(jointReview)
                .Append("; inconclusive=").Append(jointInconclusive)
                .Append("; skip=").Append(jointSkip)
                .Append("; proof=SAME_SAMPLE_PAYOUT_AND_INDEPENDENT_RATE")
                .Append("; sampleStorage=REGULAR_ONLY")
                .Append("; postCapReservedDuplicates=NOT_ADDED")
                .Append("; sourceAttribution=").Append(officialBillResults.Count > 0 &&
                    officialBillSeal == OfficialBillSealed ?
                    "MIXED_EVIDENCE_BY_SAMPLE" : "CONTEXT_NOT_CALLSITE_PROOF");
            lastSkillSummaryInvariantFailed = !skillSummary.AppendReport(sb,
                new SkillSummaryExpectedTotals {
                    Samples = samples.Count, Work = work, Reading = reading, Other = other, Decay = decay,
                    Candidates = jointCandidates, JointWorkPass = jointWorkPass,
                    JointReadingPass = jointReadingPass, JointReview = jointReview,
                    JointSkip = jointSkip, JointInconclusive = jointInconclusive,
                    WorkMeasured = workMeasured, ReadingMeasured = readingMeasured,
                    WorkXp = workXp, ReadingXp = readingXp
                });
            AppendSourceProofReport(sb);
            AppendSourceAuthorizationReport(sb); // Cached B1-A shadow only.
            AppendBillMathPreviewReport(sb); // B1-B1: read-only sealed crosscheck.
            AppendBillOfficialReport(sb, !lastSkillSummaryInvariantFailed);
            AppendWorkKindCoverage(sb);
            AppendTargetCoverage(sb);
            sb.Append("\n[XPC-4CC][DECAY] intervalConfigMatched=").Append(intervalMatched)
                .Append("; intervalDisabledSkillSkipped=").Append(intervalDisabledSkip)
                .Append("; intervalConfigMismatched=").Append(intervalMismatch)
                .Append("; intervalInconclusive=").Append(intervalInconclusive)
                .Append("; otherNegativeSources=").Append(negativeOtherSource)
                .Append("; note=DISABLED_SKILL_NO_XP_IS_SKIP_WHEN_DECAY_INPUT_MATCHES; PERFECT_MEMORY_ZERO_DECAY_MAY_NOT_EMIT_LEARN");
            var keys = new List<string>(workJobs.Keys);
            keys.Sort(StringComparer.Ordinal);
            const int MaxWorkJobLines = 24;
            for (int k = 0; k < keys.Count && k < MaxWorkJobLines; k++)
            {
                WorkCoverage c = workJobs[keys[k]];
                sb.Append("\n[XPC-4CC][WORK_JOB] pawn=").Append(c.Pawn)
                    .Append("; job=").Append(c.Job)
                    .Append("; kind=").Append(c.Kind)
                    .Append("; skill=").Append(c.Skill)
                    .Append("; calls=").Append(c.Calls)
                    .Append("; positiveCalls=").Append(c.PositiveCalls)
                    .Append("; observedPositiveXp=").Append(F(c.PositiveXp))
                    .Append("; contextOnly=True");
            }
            if (keys.Count > MaxWorkJobLines)
                sb.Append("\n[XPC-4CC] WORK_JOB_TRUNCATED; shown=").Append(MaxWorkJobLines)
                    .Append("; distinct=").Append(keys.Count);
            AppendPayoutExamples(sb, payoutEvidence);
            AppendIndependentRateExamples(sb, independentEvidence);
            AppendJointExamples(sb, jointEvidence);
            int decayExamples = AppendDecayExamples(sb);
            if (negativeTotal > 0 && decayExamples == 0)
                sb.Append("\n[XPC-4CC][DECAY_SAMPLE] status=SKIP_NO_INTERVAL_ORIGIN_NEGATIVE_INPUT");
            int shown = 0;
            shown += AppendPreviews(sb, "WORK_CANDIDATE", MaxPreviewLines / 3);
            shown += AppendPreviews(sb, "READING", MaxPreviewLines / 3);
            shown += AppendPreviews(sb, "OTHER", MaxPreviewLines / 3);
            if (shown < samples.Count)
                sb.Append("\n[XPC-4CC] DETAIL_TRUNCATED; shown=").Append(shown)
                    .Append("; captured=").Append(samples.Count);
            string status = installed || hookErrors > 0 ? "CHECK_ERROR" :
                intervalMismatch > 0 ? "REVIEW_DECAY_CONFIG" :
                payoutReviews > 0 ? "REVIEW_PAYOUT" :
                independentReview > 0 ? "REVIEW_INDEPENDENT_RATE" :
                jointReview > 0 ? "REVIEW_JOINT_XP" :
                jointWorkPass > 0 && jointReadingPass > 0
                    ? "RATE_AND_PAYOUT_VERIFIED_BOTH" :
                jointWorkPass > 0 || jointReadingPass > 0
                    ? "JOINT_PARTIAL_WORK_OR_READING" :
                payoutWorkPass > 0 || payoutReadingPass > 0 ? "PAYOUT_VERIFIED_RATE_PARTIAL" :
                "PARTIAL_PAYOUT_COVERAGE";
            AppendReservedPostCapReport(sb);
            AppendReadingCoverage(sb);
            RecordLiveRegression(work, reading, intervalMatched, intervalMismatch,
                payoutWorkPass, payoutReadingPass, independentWorkPass, independentReadingPass,
                payoutReviews, independentReview, hookErrors, jointWorkPass, jointReadingPass);
            // A reserved post-cap REVIEW must never be hidden by the old R4 verdict.
            if (lastPayoutReviewCount > 0 || lastRateReviewCount > 0)
                status = "REVIEW_PAYOUT_OR_INDEPENDENT_RATE";
            if (lastDailyCapReviewCount > 0)
                status = "REVIEW_INDEPENDENT_DAILY_CAP";
            if (lastSkillSummaryInvariantFailed) status = "CHECK_SKILL_SUMMARY_INVARIANT";
            if (officialBillSeal != OfficialBillSealed) status = "CHECK_BILL_OFFICIAL_SEAL";
            sb.Append("\n[XPC-4CC][VERDICT] status=").Append(status)
                .Append("; actualPawnXp=OBSERVED_NOT_INJECTED")
                .Append("; jointWorkPass=").Append(jointWorkPass)
                .Append("; jointReadingPass=").Append(jointReadingPass)
                .Append("; jointReview=").Append(jointReview)
                .Append("; xpPayoutFromActualRate=TESTED_WHEN_PASS")
                .Append("; independentRateFormula=RAW_SETTINGS_RECOMPOSITION_WHEN_PASS")
                .Append("; dynamicDailyCapIndependentlyProven=").Append(lastDailyCapStatus)
                .Append("; otherModsGameplayCompatibility=NOT_PROVEN")
                .Append("; jobSourceProof=").Append(officialBillResults.Count > 0 &&
                    officialBillSeal == OfficialBillSealed ?
                    "MIXED_BILL_P0_CALLSITE_AND_EXISTING_CONTEXT" :
                    "TARGET_AND_DESIGNATION_CONTEXT_NOT_CALLSITE_PROVEN")
                .Append("; note=DISABLED_SKILL_SKIPS_ARE_NOT_ERRORS; DECAY_REMOVED_FROM_WORK_CANDIDATES")
                .Append("; next=R5_C_POSTCAP_FURTHER_AND_MTH_LIVE");
            return sb.ToString();
        }

        private static void AppendPayoutExamples(StringBuilder sb, List<Sample> candidates)
        {
            const int Maximum = 18;
            int shown = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            // Failure-first, then diverse PASS and inconclusive/skip evidence.
            for (int pass = 0; pass < 3 && shown < Maximum; pass++)
            {
                foreach (Sample s in candidates)
                {
                    StartState b = s.Before;
                    string cls = PayoutClass(s);
                    bool review = cls.StartsWith("REVIEW_", StringComparison.Ordinal);
                    bool success = cls == "PASS_PAYOUT";
                    if (pass == 0 && !review) continue;
                    if (pass == 1 && !success) continue;
                    if (pass == 2 && (review || success)) continue;
                    string key = b.PawnName + "|" + b.Job + "|" + b.WorkKind + "|" + b.Skill + "|" + cls;
                    if (!review && !seen.Add(key)) continue;
                    sb.Append("\n[XPC-4CC][PAYOUT_SAMPLE] pawn=").Append(b.PawnName)
                        .Append("; job=").Append(b.Job)
                        .Append("; workKind=").Append(b.WorkKind)
                        .Append("; category=").Append(b.Category)
                        .Append("; skill=").Append(b.Skill);
                    if (b.Category == "WORK_CANDIDATE") AppendTargetFields(sb, b);
                    sb.Append("; input=").Append(F(b.Input))
                        .Append("; rateBefore=").Append(b.RateCaptured ? F(b.RateBefore) : "NA")
                        .Append("; expectedXp=").Append(b.RateCaptured ? F(b.Input * b.RateBefore) : "NA")
                        .Append("; observedXp=").Append(s.Comparable ? F(s.DeltaXp) : "LEVEL_BOUNDARY")
                        .Append("; observedDaily=").Append(F(s.DeltaDaily))
                        .Append("; gwsFactor=").Append(b.RateCaptured ? F(b.GwsFactorBefore) : "NA")
                        .Append("; consFactor=").Append(b.RateCaptured ? F(b.ConsciousnessFactorBefore) : "NA")
                        .Append("; saturatedBefore=").Append(b.SaturatedBefore)
                        .Append("; dailyCapBefore=").Append(b.RateCaptured ? F(b.DailyCapBefore) : "NA")
                        .Append("; furtherFactor=").Append(b.RateCaptured ? F(b.FurtherFactorBefore) : "NA")
                        .Append("; rateNote=").Append(b.RateCaptureReason ?? "NA")
                        .Append("; levelRaw=").Append(b.RawLevel).Append("->").Append(s.AfterRawLevel)
                        .Append("; levelShown=").Append(b.Level).Append("->").Append(s.AfterLevel)
                        .Append("; disabled=").Append(b.TotallyDisabled).Append("->").Append(s.AfterDisabled)
                        .Append("; status=").Append(cls);
                    shown++;
                    if (shown >= Maximum) break;
                }
            }
            if (candidates.Count > shown)
                sb.Append("\n[XPC-4CC][PAYOUT_DETAIL] representativeRows=").Append(shown)
                    .Append("; positiveInputCandidateSamples=").Append(candidates.Count)
                    .Append("; allReviewRowsLogged=").Append(candidates.FindAll(
                        x => PayoutClass(x).StartsWith("REVIEW_", StringComparison.Ordinal)).Count <= Maximum);
        }

        private static void AppendIndependentRateExamples(StringBuilder sb, List<Sample> candidates)
        {
            const int MaxRows = 20;
            int shown = 0;
            var groups = new HashSet<string>(StringComparer.Ordinal);
            // Failure-first, then one PASS per pawn/job/skill/rate-band, and skips.
            for (int pass = 0; pass < 3 && shown < MaxRows; pass++)
            {
                foreach (Sample sample in candidates)
                {
                    StartState b = sample.Before;
                    string state = IndependentRateClass(sample);
                    bool issue = state.StartsWith("REVIEW_", StringComparison.Ordinal);
                    bool match = state == "PASS_INDEPENDENT_RATE";
                    if (pass == 0 && !issue) continue;
                    if (pass == 1 && !match) continue;
                    if (pass == 2 && (issue || match)) continue;
                    string band = !b.OracleReady ? "NA" : b.SaturatedBefore ? "SATURATED" :
                        b.OracleGwsFactor < .999f ? "LOW_GWS" :
                        b.OracleGwsFactor > 1.001f ? "HIGH_GWS" : "NEUTRAL_GWS";
                    string key = b.PawnName + "|" + b.Job + "|" + b.WorkKind + "|" + b.Skill + "|" + band + "|" + state;
                    if (!issue && !groups.Add(key)) continue;
                    sb.Append("\n[XPC-4CC][INDEPENDENT_RATE_SAMPLE] pawn=").Append(b.PawnName)
                        .Append("; job=").Append(b.Job)
                        .Append("; skill=").Append(b.Skill);
                    if (b.Category == "WORK_CANDIDATE") AppendTargetFields(sb, b);
                    sb.Append("; rateActual=").Append(b.RateCaptured ? F(b.RateBefore) : "NA")
                        .Append("; rateIndependent=").Append(b.OracleReady ? F(b.OracleRate) : "NA")
                        .Append("; passion=").Append(b.OracleReady ? F(b.OraclePassion) : "NA")
                        .Append("; globalLearning=").Append(b.OracleReady ? F(b.OracleGlobal) : "NA")
                        .Append("; animalsLearning=").Append(b.OracleReady ? F(b.OracleAnimals) : "NA")
                        .Append("; consciousness=").Append(b.OracleReady ? F(b.OracleConsciousness) : "NA")
                        .Append("; consSpeed=").Append(b.OracleReady ? F(b.OracleSpeedFactor) : "NA")
                        .Append("; workSpeed=").Append(b.OracleWorkSpeed < 0f ? "NA" : F(b.OracleWorkSpeed))
                        .Append("; gws=").Append(b.OracleReady ? F(b.OracleGwsFactor) : "NA")
                        .Append("; gwsWithoutReading=").Append(b.OracleWorkSpeed >= 0f ?
                            F(b.OracleGwsWithoutReading) : "NA")
                        .Append("; saturatedFromEngine=").Append(b.SaturatedBefore)
                        .Append("; postCapFactor=").Append(b.OracleReady ? F(b.OraclePostCap) : "NA")
                        .Append("; readingScope=").Append(b.ReadingScope)
                        .Append("; sourceStatus=").Append(b.OracleReason ?? "NA")
                        .Append("; status=").Append(state);
                    shown++;
                    if (shown >= MaxRows) break;
                }
            }
            if (candidates.Count > shown)
                sb.Append("\n[XPC-4CC][INDEPENDENT_RATE_DETAIL] representativeRows=")
                    .Append(shown).Append("; candidateRows=").Append(candidates.Count)
                    .Append("; allReviewRowsLogged=").Append(candidates.FindAll(
                        x => IndependentRateClass(x).StartsWith("REVIEW_", StringComparison.Ordinal)).Count <= MaxRows);
        }

        private sealed class WorkCoverage
        {
            internal string Pawn;
            internal string Job;
            internal string Skill;
            internal string Kind;
            internal int Calls;
            internal int PositiveCalls;
            internal float PositiveXp;
        }

    }
}
#endif
