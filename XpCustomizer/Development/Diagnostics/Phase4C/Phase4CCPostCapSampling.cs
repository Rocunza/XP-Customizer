#if DEBUG
using System;
using System.Collections.Generic;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;
using XpCustomizer.FurtherLearning;
using XpCustomizer.LearningCapacity;

namespace XpCustomizer
{
    /// <summary>
    /// Phase 4C-C R5-A1: reserved passive post-cap and transition observations.
    /// Never calls Learn, changes settings, schedules a job or modifies XP.
    /// The engine saturation flag is observation only, NOT an independent
    /// mathematical proof of the daily threshold (scheduled for R5-B).
    /// </summary>
    internal static partial class Phase4CCLiveXpCapture
    {
        private const int MaxPostCapWorkSamples = 24;
        private const int MaxPostCapReadingSamples = 24;
        private const int MaxTransitionSamples = 12;
        private const int PostCapGroupLimit = 4;
        private static readonly List<Sample> postCapSamples = new List<Sample>();
        private static readonly List<Sample> transitionSamples = new List<Sample>();
        private static readonly Dictionary<string, int> postCapGroupCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly HashSet<string> observedTransitions =
            new HashSet<string>(StringComparer.Ordinal);
        private static int postCapWorkCount;
        private static int postCapReadingCount;
        private static int postCapOverlapRegular;
        private static int droppedByPostCapGroup;
        private static int droppedByPostCapQuota;
        private static int transitionWatchCalls;
        private static int transitionWatchWithoutBoundary;
        private static int postCapSelectionMismatch;

        private static void ResetPostCapSampling()
        {
            postCapSamples.Clear();
            transitionSamples.Clear();
            postCapGroupCounts.Clear();
            observedTransitions.Clear();
            postCapWorkCount = 0;
            postCapReadingCount = 0;
            postCapOverlapRegular = 0;
            droppedByPostCapGroup = 0;
            droppedByPostCapQuota = 0;
            transitionWatchCalls = 0;
            transitionWatchWithoutBoundary = 0;
            postCapSelectionMismatch = 0;
        }

        private static bool PostCapBudgetsExhausted()
        {
            return postCapWorkCount >= MaxPostCapWorkSamples &&
                postCapReadingCount >= MaxPostCapReadingSamples &&
                transitionSamples.Count >= MaxTransitionSamples;
        }

        private static bool MayObservePostCap(string category, string group)
        {
            bool room = category == "WORK_CANDIDATE" ?
                postCapWorkCount < MaxPostCapWorkSamples :
                postCapReadingCount < MaxPostCapReadingSamples;
            if (!room) { droppedByPostCapQuota++; return false; }
            int count;
            postCapGroupCounts.TryGetValue(group, out count);
            if (count >= PostCapGroupLimit)
            {
                droppedByPostCapGroup++;
                return false;
            }
            return true;
        }

        private static bool MayObserveTransition(string transitionKey)
        {
            return transitionSamples.Count < MaxTransitionSamples &&
                !observedTransitions.Contains(transitionKey);
        }

        private static bool PurePositiveRateSource(string category, float input,
            bool direct, bool ignore, bool disabled, bool fast)
        {
            return (category == "WORK_CANDIDATE" || category == "READING") &&
                input > 0f && IsFinite(input) && !direct && !ignore && !disabled && !fast;
        }

        private static void CaptureReservedPostCap(Sample sample)
        {
            StartState b = sample.Before;
            if (b.PostCapSlot)
            {
                if (!b.SaturatedAtSelection || !b.SaturatedBefore)
                {
                    // Fail closed rather than incorrectly marking an unsaturated
                    // event as an actual post-cap payout.
                    postCapSelectionMismatch++;
                }
                else
                {
                    int count;
                    postCapGroupCounts.TryGetValue(b.GroupKey, out count);
                    bool room = b.Category == "WORK_CANDIDATE" ?
                        postCapWorkCount < MaxPostCapWorkSamples :
                        postCapReadingCount < MaxPostCapReadingSamples;
                    if (room && count < PostCapGroupLimit)
                    {
                        postCapSamples.Add(sample);
                        postCapGroupCounts[b.GroupKey] = count + 1;
                        if (b.Category == "WORK_CANDIDATE") postCapWorkCount++;
                        else postCapReadingCount++;
                        if (b.RegularSlot) postCapOverlapRegular++;
                    }
                    else droppedByPostCapQuota++;
                }
            }
            if (b.WatchTransition)
            {
                transitionWatchCalls++;
                if (!b.SaturatedAtSelection && !b.SaturatedBefore && sample.AfterSaturated &&
                    !observedTransitions.Contains(b.TransitionKey) &&
                    transitionSamples.Count < MaxTransitionSamples)
                {
                    // R5-B: independently inspect only the ACTUAL engine boundary
                    // event. Running nine pawn-source probes on every watched
                    // Learn call would add excessive overhead on busy maps.
                    var capProof = Phase4CCIndependentDailyCapOracle.Compose(
                        b.Record, XpCustomizerMod.Settings);
                    Phase4CCIndependentDailyCapOracle.Compare(capProof, sample.AfterDaily,
                        b.RateCaptured ? b.DailyCapBefore : float.NaN,
                        sample.AfterSaturated, foreignSaturationHookPresent);
                    sample.TransitionCapAtAfter = capProof;
                    bool? predictedBefore = capProof.Ready ?
                        Phase4CCIndependentDailyCapOracle.PredictedState(capProof.Cap,
                            b.Daily, foreignSaturationHookPresent) : null;
                    bool? predictedAfter = capProof.PredictedSaturated;
                    sample.IndependentTransitionClass =
                        capProof.CapComparison == "REVIEW_CAP_MISMATCH" ?
                        "INCONCLUSIVE_CAP_CHANGED_OR_MISMATCH_AT_AFTER" :
                        !predictedBefore.HasValue || !predictedAfter.HasValue ?
                        "INCONCLUSIVE_VANILLA_OR_FOREIGN" :
                        !predictedBefore.Value && predictedAfter.Value ?
                        "PASS_INDEPENDENT_TRANSITION" : "REVIEW_TRANSITION_BOUNDARY";
                    transitionSamples.Add(sample);
                    observedTransitions.Add(b.TransitionKey);
                }
                else transitionWatchWithoutBoundary++;
            }
        }

        private static void AppendReservedPostCapReport(StringBuilder sb)
        {
            int workPass = 0, readingPass = 0, payoutReview = 0;
            int workPayoutReview = 0, readingPayoutReview = 0;
            int rateWorkPass = 0, rateReadingPass = 0, rateReview = 0;
            int workRateReview = 0, readingRateReview = 0;
            int skipped = 0, inconclusive = 0, consNonNeutral = 0;
            int capPass = 0, capReview = 0, capInconclusive = 0;
            int thresholdPass = 0, thresholdReview = 0, thresholdInconclusive = 0;
            // R5-C1: independent Further-component proof is separate from the
            // full XP rate and the pre-Learn rate->payout comparison.
            int furtherPass = 0, furtherReview = 0, furtherInconclusive = 0;
            int furtherWorkPass = 0, furtherReadingPass = 0;
            int mthPositivePass = 0, mthNeutralPass = 0, mthNegativePass = 0;
            int furtherConsNonNeutralPass = 0;
            int reportRows = 0;
            foreach (Sample sample in postCapSamples)
            {
                StartState b = sample.Before;
                string payout = PayoutClass(sample);
                string independent = IndependentRateClass(sample);
                var proof = b.IndependentDailyCapAtSelection;
                FurtherProof further = b.OracleFurtherProof;
                if (further == null || !further.Ready ||
                    further.Comparison != "PASS_FURTHER_FACTOR")
                {
                    if (further != null && further.Comparison ==
                        "REVIEW_FURTHER_FACTOR_MISMATCH") furtherReview++;
                    else furtherInconclusive++;
                }
                else
                {
                    furtherPass++;
                    if (b.Category == "WORK_CANDIDATE") furtherWorkPass++;
                    else if (b.Category == "READING") furtherReadingPass++;
                    if (further.MthBand == "POSITIVE") mthPositivePass++;
                    if (further.MthBand == "NEUTRAL") mthNeutralPass++;
                    if (further.MthBand == "NEGATIVE") mthNegativePass++;
                    if (Math.Abs(further.ConsMultiplier - 1f) > .001f)
                        furtherConsNonNeutralPass++;
                }
                if (proof == null || !proof.Ready) capInconclusive++;
                else if (proof.CapComparison == "PASS_CAP") capPass++;
                else if (proof.CapComparison == "REVIEW_CAP_MISMATCH") capReview++;
                if (proof == null || proof.SaturationComparison == null) thresholdInconclusive++;
                else if (proof.SaturationComparison == "PASS_THRESHOLD") thresholdPass++;
                else if (proof.SaturationComparison == "REVIEW_THRESHOLD_MISMATCH") thresholdReview++;
                else thresholdInconclusive++;
                if (payout == "PASS_PAYOUT")
                {
                    if (b.Category == "WORK_CANDIDATE") workPass++;
                    else readingPass++;
                }
                else if (payout.StartsWith("REVIEW_", StringComparison.Ordinal))
                {
                    payoutReview++;
                    if (b.Category == "WORK_CANDIDATE") workPayoutReview++;
                    else readingPayoutReview++;
                }
                else if (payout.StartsWith("INCONCLUSIVE_", StringComparison.Ordinal)) inconclusive++;
                else skipped++;
                if (independent == "PASS_INDEPENDENT_RATE")
                {
                    if (b.Category == "WORK_CANDIDATE") rateWorkPass++;
                    else rateReadingPass++;
                    if (Math.Abs(b.OracleSpeedFactor - 1f) > 0.001f) consNonNeutral++;
                }
                else if (independent.StartsWith("REVIEW_", StringComparison.Ordinal))
                {
                    rateReview++;
                    if (b.Category == "WORK_CANDIDATE") workRateReview++;
                    else readingRateReview++;
                }
                else if (independent.StartsWith("INCONCLUSIVE_", StringComparison.Ordinal)) inconclusive++;
                else skipped++;
                if (reportRows < 12 || payout.StartsWith("REVIEW_", StringComparison.Ordinal) ||
                    independent.StartsWith("REVIEW_", StringComparison.Ordinal))
                {
                    sb.Append("\n[XPC-4CC][POSTCAP_SAMPLE] pawn=").Append(b.PawnName)
                        .Append("; job=").Append(b.Job)
                        .Append("; kind=").Append(b.WorkKind)
                        .Append("; skill=").Append(b.Skill)
                        .Append("; input=").Append(F(b.Input))
                        .Append("; dailyBefore=").Append(F(b.Daily))
                        .Append("; engineSaturatedBefore=").Append(b.SaturatedBefore)
                        .Append("; capProductionRead=").Append(b.RateCaptured ? F(b.DailyCapBefore) : "NA")
                        .Append("; furtherProductionRead=").Append(b.RateCaptured ? F(b.FurtherFactorBefore) : "NA")
                        .Append("; furtherIndependent=").Append(further != null && further.Ready ? F(further.ExpectedFactor) : "NA")
                        .Append("; furtherFormula=BASE_PLUS_MTH_PP_TIMES_CONS_R5F1")
                        .Append("; furtherBase=").Append(further != null && further.Ready ? F(further.BaseFactor) : "NA")
                        .Append("; furtherConsciousnessMultiplier=").Append(further != null && further.Ready ? F(further.ConsMultiplier) : "NA")
                        .Append("; mentalBreakThreshold=").Append(further != null && IsFinite(further.MentalThreshold) ? F(further.MentalThreshold) : "NA")
                        .Append("; mthAdjustmentPp=").Append(further != null && further.Ready ? F(further.MthAdjustmentPp) : "NA")
                        .Append("; mthBand=").Append(further == null ? "NOT_EVALUATED" : further.MthBand)
                        .Append("; furtherSource=").Append(further == null ? "NOT_EVALUATED" : further.SourceStatus)
                        .Append("; furtherComparison=").Append(further == null ? "NOT_VERIFIED" : further.Comparison)
                        .Append("; observedXp=").Append(sample.Comparable ? F(sample.DeltaXp) : "LEVEL_CHANGE")
                        .Append("; expectedXp=").Append(b.RateCaptured ? F(b.Input * b.RateBefore) : "NA")
                        .Append("; rateActual=").Append(b.RateCaptured ? F(b.RateBefore) : "NA")
                        .Append("; rateIndependent=").Append(b.OracleReady ? F(b.OracleRate) : "NA")
                        .Append("; capIndependentAtSelection=").Append(proof != null && proof.Ready ? F(proof.Cap) : "NA")
                        .Append("; capComparison=").Append(proof == null ? "NA" : proof.CapComparison)
                        .Append("; saturationComparison=").Append(proof == null ? "NA" : proof.SaturationComparison)
                        .Append("; payout=").Append(payout)
                        .Append("; independentRate=").Append(independent);
                    reportRows++;
                }
            }
            // These are subset/overlap counters, NOT additional unique Learn calls.
            sb.Append("\n[XPC-4CC][POSTCAP_COVERAGE] workCaptured=").Append(postCapWorkCount)
                .Append("; readingCaptured=").Append(postCapReadingCount)
                .Append("; workPayoutPass=").Append(workPass)
                .Append("; readingPayoutPass=").Append(readingPass)
                .Append("; workIndependentRatePass=").Append(rateWorkPass)
                .Append("; readingIndependentRatePass=").Append(rateReadingPass)
                .Append("; payoutReview=").Append(payoutReview)
                .Append("; independentRateReview=").Append(rateReview)
                .Append("; skip=").Append(skipped)
                .Append("; inconclusive=").Append(inconclusive)
                .Append("; consciousnessNonNeutralRatePass=").Append(consNonNeutral)
                .Append("; duplicatedInRegular=").Append(postCapOverlapRegular)
                .Append("; droppedByReservedGroup=").Append(droppedByPostCapGroup)
                .Append("; droppedByReservedQuota=").Append(droppedByPostCapQuota)
                .Append("; saturationSelectionMismatch=").Append(postCapSelectionMismatch)
                .Append("; independentCapPass=").Append(capPass)
                .Append("; independentCapReview=").Append(capReview)
                .Append("; independentCapInconclusive=").Append(capInconclusive)
                .Append("; independentThresholdPass=").Append(thresholdPass)
                .Append("; independentThresholdReview=").Append(thresholdReview)
                .Append("; independentThresholdInconclusive=").Append(thresholdInconclusive)
                .Append("; maxWork=").Append(MaxPostCapWorkSamples)
                .Append("; maxReading=").Append(MaxPostCapReadingSamples)
                .Append("; groupCap=").Append(PostCapGroupLimit)
                .Append("; status=").Append(postCapSamples.Count == 0 ? "NOT_VERIFIED" :
                    payoutReview + rateReview + postCapSelectionMismatch + capReview + thresholdReview > 0 ? "REVIEW" :
                    skipped + inconclusive + capInconclusive + thresholdInconclusive > 0 ?
                    "PARTIAL" : "OBSERVED_PASS")
                .Append("; oracle=INDEPENDENT_CAP_RAW_SETTINGS_DYNAMIC_THRESHOLD_ONLY")
                .Append("; uniqueCallTotal=REGULAR_PLUS_RESERVED_MINUS_DUPLICATES");
            string furtherStatus = postCapSamples.Count == 0 ? "NOT_VERIFIED" :
                furtherReview + payoutReview + rateReview + capReview + thresholdReview +
                postCapSelectionMismatch > 0 ? "REVIEW" :
                furtherInconclusive + inconclusive + capInconclusive +
                thresholdInconclusive > 0 || furtherPass != postCapSamples.Count ||
                workPass != postCapWorkCount || readingPass != postCapReadingCount ||
                rateWorkPass != postCapWorkCount || rateReadingPass != postCapReadingCount ||
                furtherWorkPass == 0 || furtherReadingPass == 0 ||
                mthPositivePass == 0 || mthNegativePass == 0 ||
                furtherConsNonNeutralPass == 0 ? "PARTIAL" : "PASS";
            sb.Append("\n[XPC-4CC][POSTCAP_FURTHER_COVERAGE] captured=")
                .Append(postCapSamples.Count)
                .Append("; componentPass=").Append(furtherPass)
                .Append("; componentReview=").Append(furtherReview)
                .Append("; componentInconclusive=").Append(furtherInconclusive)
                .Append("; workPass=").Append(furtherWorkPass)
                .Append("; readingPass=").Append(furtherReadingPass)
                .Append("; mthPositivePass=").Append(mthPositivePass)
                .Append("; mthNeutralPass=").Append(mthNeutralPass)
                .Append("; mthNegativePass=").Append(mthNegativePass)
                .Append("; consciousnessNonNeutralPass=").Append(furtherConsNonNeutralPass)
                .Append("; status=").Append(furtherStatus)
                .Append("; oracle=RAW_BASE_PLUS_MTH_PP_TIMES_CONS_V2_R5F1")
                .Append("; foreignFurtherHookPresent=").Append(foreignFurtherHookPresent)
                .Append("; notVerifiedUnlessRealPostCap=True")
                .Append("; fullCoverageRequiresWorkReadingMthBothSignsNonNeutralCons=True");
            Phase4CRegressionLedger.Record("4C-C-FURTHER-LIVE", furtherStatus,
                "captured=" + postCapSamples.Count + ",pass=" + furtherPass +
                ",review=" + furtherReview + ",mthPositive=" + mthPositivePass +
                ",mthNegative=" + mthNegativePass);
            int transitionPass = 0, transitionReview = 0, transitionInconclusive = 0;
            foreach (Sample transition in transitionSamples)
            {
                if (transition.IndependentTransitionClass == "PASS_INDEPENDENT_TRANSITION") transitionPass++;
                else if (transition.IndependentTransitionClass == "REVIEW_TRANSITION_BOUNDARY") transitionReview++;
                else transitionInconclusive++;
            }
            sb.Append("\n[XPC-4CC][TRANSITION_COVERAGE] captured=").Append(transitionSamples.Count)
                .Append("; watchedPositiveCalls=").Append(transitionWatchCalls)
                .Append("; watchedWithoutBoundary=").Append(transitionWatchWithoutBoundary)
                .Append("; quota=").Append(MaxTransitionSamples)
                .Append("; independentPass=").Append(transitionPass)
                .Append("; independentReview=").Append(transitionReview)
                .Append("; independentInconclusive=").Append(transitionInconclusive)
                .Append("; status=").Append(transitionSamples.Count == 0 ? "NOT_VERIFIED" :
                    transitionReview > 0 ? "REVIEW" : transitionInconclusive > 0 ? "PARTIAL" : "PASS")
                .Append("; thresholdFormulaIndependent=DYNAMIC_ONLY_AFTER_EVENT_SNAPSHOT");
            foreach (Sample sample in transitionSamples)
            {
                StartState b = sample.Before;
                sb.Append("\n[XPC-4CC][TRANSITION_SAMPLE] pawn=").Append(b.PawnName)
                    .Append("; job=").Append(b.Job)
                    .Append("; skill=").Append(b.Skill)
                    .Append("; dailyBefore=").Append(F(b.Daily))
                    .Append("; dailyAfter=").Append(F(sample.AfterDaily))
                    .Append("; capProductionRead=").Append(b.RateCaptured ? F(b.DailyCapBefore) : "NA")
                    .Append("; engineSaturatedBefore=").Append(b.SaturatedAtSelection)
                    .Append("; engineSaturatedAfter=").Append(sample.AfterSaturated)
                    .Append("; capIndependentAtAfter=").Append(sample.TransitionCapAtAfter != null &&
                        sample.TransitionCapAtAfter.Ready ? F(sample.TransitionCapAtAfter.Cap) : "NA")
                    .Append("; independentTransition=").Append(sample.IndependentTransitionClass ?? "NOT_EVALUATED")
                    .Append("; oracleSnapshotTiming=AFTER_LEARN_NOT_BEFORE")
                    .Append("; payout=").Append(PayoutClass(sample))
                    .Append("; independentRate=").Append(IndependentRateClass(sample));
            }
            lastPayoutReviewCount += payoutReview;
            lastRateReviewCount += rateReview;
            lastDailyCapReviewCount += capReview + thresholdReview + transitionReview;
            lastDailyCapStatus = lastDailyCapReviewCount > 0 ? "REVIEW" :
                thresholdPass + transitionPass > 0 ? "DYNAMIC_THRESHOLD_OBSERVED" :
                capPass > 0 ? "CAP_COMPOSED_ONLY" : "NOT_VERIFIED";
            Phase4CRegressionLedger.Record("4C-C-POSTCAP-WORK", PostCapStatus(postCapWorkCount,
                workPass, rateWorkPass, workPayoutReview, workRateReview,
                postCapSelectionMismatch + capReview + thresholdReview),
                "captured=" + postCapWorkCount + ",payoutPass=" + workPass + ",ratePass=" + rateWorkPass);
            Phase4CRegressionLedger.Record("4C-C-POSTCAP-READING", PostCapStatus(postCapReadingCount,
                readingPass, rateReadingPass, readingPayoutReview, readingRateReview,
                postCapSelectionMismatch + capReview + thresholdReview),
                "captured=" + postCapReadingCount + ",payoutPass=" + readingPass + ",ratePass=" + rateReadingPass);
            Phase4CRegressionLedger.Record("4C-C-TRANSITION", transitionSamples.Count == 0 ?
                "NOT_VERIFIED" : transitionReview > 0 ? "REVIEW" :
                transitionInconclusive > 0 ? "PARTIAL" : "PASS",
                "engineTransitions=" + transitionSamples.Count + ",independentPass=" + transitionPass +
                ",independentInconclusive=" + transitionInconclusive);
            if (postCapSamples.Count > 0 || transitionSamples.Count > 0)
            {
                string capStatus = capReview + thresholdReview + transitionReview > 0 ? "REVIEW" :
                    capPass + thresholdPass + transitionPass == 0 ? "NOT_VERIFIED" :
                    capInconclusive + thresholdInconclusive + transitionInconclusive > 0 ? "PARTIAL" : "PASS";
                Phase4CRegressionLedger.Record("4C-C-DAILYCAP-LIVE", capStatus,
                    "postCapSamples=" + postCapSamples.Count + ",capPass=" + capPass +
                    ",thresholdPass=" + thresholdPass + ",transitionPass=" + transitionPass);
            }
        }

        private static string PostCapStatus(int captured, int payoutPass, int ratePass,
            int payoutReview, int rateReview, int selectionMismatch)
        {
            if (selectionMismatch + payoutReview + rateReview > 0) return "REVIEW";
            if (captured == 0) return "NOT_VERIFIED";
            return payoutPass == captured && ratePass == captured ? "PASS" : "PARTIAL";
        }

        private static void RecordLiveRegression(int work, int reading, int decay,
            int decayMismatch, int workPayout, int readingPayout,
            int independentWork, int independentReading, int payoutReview,
            int rateReview, int errors, int jointWork, int jointReading)
        {
            // Legacy component counters remain in the detail string, but a
            // regression PASS must have both proofs on the same event.
            Phase4CRegressionLedger.Record("4C-C-WORK", errors != 0 || payoutReview + rateReview != 0 ?
                "REVIEW" : work == 0 ? "NOT_VERIFIED" :
                jointWork == work ? "PASS" : "PARTIAL",
                "captured=" + work + ",payoutPass=" + workPayout + ",ratePass=" + independentWork +
                ",jointPass=" + jointWork);
            Phase4CRegressionLedger.Record("4C-C-READING", errors != 0 || payoutReview + rateReview != 0 ?
                "REVIEW" : reading == 0 ? "NOT_VERIFIED" :
                jointReading == reading ? "PASS" : "PARTIAL",
                "captured=" + reading + ",payoutPass=" + readingPayout + ",ratePass=" + independentReading +
                ",jointPass=" + jointReading);
            Phase4CRegressionLedger.Record("4C-C-DECAY", errors != 0 || decayMismatch != 0 ?
                "REVIEW" : decay == 0 ? "NOT_VERIFIED" : "PASS",
                "matched=" + decay + ",mismatch=" + decayMismatch);
        }

        private static void RequirePostCap(bool expected, bool actual, string tag, ref int cases)
        {
            cases++;
            if (expected != actual) throw new InvalidOperationException("R5A_POSTCAP_" + tag);
        }

        private static void VerifyPostCapSamplingSelfAudit()
        {
            int cases = 0;
            RequirePostCap(true, PurePositiveRateSource("WORK_CANDIDATE", 1f, false, false, false, false), "WORK", ref cases);
            RequirePostCap(true, PurePositiveRateSource("READING", 1f, false, false, false, false), "READING", ref cases);
            RequirePostCap(false, PurePositiveRateSource("OTHER_JOB", 1f, false, false, false, false), "OTHER", ref cases);
            RequirePostCap(false, PurePositiveRateSource("DECAY_INTERVAL", -1f, false, false, false, false), "DECAY", ref cases);
            RequirePostCap(false, PurePositiveRateSource("READING", 0f, false, false, false, false), "ZERO", ref cases);
            RequirePostCap(false, PurePositiveRateSource("READING", float.NaN, false, false, false, false), "NAN", ref cases);
            RequirePostCap(false, PurePositiveRateSource("READING", 1f, true, false, false, false), "DIRECT", ref cases);
            RequirePostCap(false, PurePositiveRateSource("READING", 1f, false, true, false, false), "IGNORE", ref cases);
            RequirePostCap(false, PurePositiveRateSource("READING", 1f, false, false, true, false), "DISABLED", ref cases);
            RequirePostCap(false, PurePositiveRateSource("READING", 1f, false, false, false, true), "FAST", ref cases);
            RequirePostCap(true, MaxPostCapWorkSamples > PostCapGroupLimit, "WORK_CAP", ref cases);
            RequirePostCap(true, MaxPostCapReadingSamples > PostCapGroupLimit, "READ_CAP", ref cases);
            RequirePostCap(true, MaxTransitionSamples > 0, "TRANSITION_CAP", ref cases);
            RequirePostCap(true, MaxSamples == 324, "REGULAR_324_UNCHANGED", ref cases);
            RequirePostCap(true, !observedTransitions.Contains("NO_PAWN|TEST"), "TRANSITION_EMPTY", ref cases);
            Log.Message("[XPC-4CC][POSTCAP_SAMPLING_AUDIT] status=PASS; cases=" + cases +
                "; syntheticOnly=True; normalCapsUnchanged=True; noPawnWrites=True; noSettingsWrites=True" +
                "; build=" + XpCustomizerMod.BuildId + "; revision=" + Revision);
        }

        internal static void PrintReadiness()
        {
            if (Current.Game == null || Find.CurrentMap == null)
            {
                Log.Warning("[XPC-4CC][POSTCAP_READINESS] status=SKIP; reason=NO_MAP");
                return;
            }
            Pawn pawn = Find.Selector == null ? null : Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null || pawn.Map != Find.CurrentMap || pawn.skills == null ||
                pawn.skills.skills == null || pawn.Faction != Faction.OfPlayer)
            {
                Log.Warning("[XPC-4CC][POSTCAP_READINESS] status=SKIP; reason=SELECT_PLAYER_COLONIST");
                return;
            }
            StatDef mental = DefDatabase<StatDef>.GetNamedSilentFail("MentalBreakThreshold");
            var sb = new StringBuilder(2000);
            sb.Append("[XPC-4CC][POSTCAP_READINESS] pawn=").Append(Clean(pawn.LabelShort))
                .Append("; build=").Append(XpCustomizerMod.BuildId)
                .Append("; scope=READ_ONLY; sources=ENGINE_AND_RAW_SETTINGS_ORACLE")
                .Append("; independentThresholdProof=DYNAMIC_ONLY_NOT_VANILLA; settingsWrites=0; xpWrites=0")
                .Append("; mentalBreakThresholdStat=");
            try { sb.Append(mental == null ? "NOT_FOUND" : F(pawn.GetStatValue(mental))); }
            catch (Exception ex) { sb.Append("READ_ERROR_").Append(Clean(ex.GetType().Name)); }
            int shown = 0;
            int capPass = 0, capReview = 0, thresholdPass = 0;
            int thresholdTruePass = 0, thresholdFalsePass = 0;
            int thresholdReview = 0, thresholdInconclusive = 0, oracleErrors = 0;
            foreach (SkillRecord record in pawn.skills.skills)
            {
                if (record == null || record.def == null) continue;
                try
                {
                    bool saturated = record.LearningSaturatedToday;
                    float cap = LearningCapacityService.GetDailyCap(record);
                    var proof = Phase4CCIndependentDailyCapOracle.Compose(
                        record, XpCustomizerMod.Settings);
                    Phase4CCIndependentDailyCapOracle.Compare(proof, record.xpSinceMidnight,
                        cap, saturated, Phase4CCLiveXpCapture.HasForeignSaturationHook());
                    if (!proof.Ready) oracleErrors++;
                    if (proof.CapComparison == "PASS_CAP") capPass++;
                    if (proof.CapComparison == "REVIEW_CAP_MISMATCH") capReview++;
                    if (proof.SaturationComparison == "PASS_THRESHOLD")
                    {
                        thresholdPass++;
                        if (proof.PredictedSaturated == true) thresholdTruePass++;
                        else thresholdFalsePass++;
                    }
                    if (proof.SaturationComparison == "REVIEW_THRESHOLD_MISMATCH") thresholdReview++;
                    if (proof.SaturationComparison != "PASS_THRESHOLD" &&
                        proof.SaturationComparison != "REVIEW_THRESHOLD_MISMATCH") thresholdInconclusive++;
                    sb.Append("\n[XPC-4CC][POSTCAP_READINESS_SKILL] skill=").Append(Clean(record.def.defName))
                        .Append("; disabled=").Append(record.TotallyDisabled)
                        .Append("; daily=").Append(F(record.xpSinceMidnight))
                        .Append("; capProductionRead=").Append(F(cap))
                        .Append("; remainingArithmetic=").Append(F(cap - record.xpSinceMidnight))
                        .Append("; engineSaturated=").Append(saturated)
                        .Append("; furtherProductionRead=").Append(saturated ?
                            F(FurtherLearningService.GetFactor(record)) : "NOT_POSTCAP")
                        .Append("; capIndependent=").Append(proof.Ready ? F(proof.Cap) : "NA")
                        .Append("; sourceModifierPp=").Append(proof.Ready ? F(proof.SourcePp) : "NA")
                        .Append("; consciousnessCapacityFactor=").Append(proof.Ready ? F(proof.CapacityFactor) : "NA")
                        .Append("; thresholdPath=").Append(proof.ThresholdPath ?? "NA")
                        .Append("; predictedSaturated=").Append(proof.PredictedSaturated.HasValue ?
                            proof.PredictedSaturated.Value.ToString() : "INCONCLUSIVE")
                        .Append("; capComparison=").Append(proof.CapComparison)
                        .Append("; saturationComparison=").Append(proof.SaturationComparison)
                        .Append("; oracleError=").Append(proof.Error);
                    shown++;
                }
                catch (Exception ex)
                {
                    oracleErrors++;
                    sb.Append("\n[XPC-4CC][POSTCAP_READINESS_SKILL] skill=")
                        .Append(Clean(record.def.defName)).Append("; status=READ_ERROR_")
                        .Append(Clean(ex.GetType().Name));
                }
            }
            string verdict = capReview + thresholdReview > 0 ? "REVIEW" :
                oracleErrors > 0 ? "PARTIAL" :
                thresholdTruePass > 0 && thresholdFalsePass > 0 &&
                thresholdInconclusive == 0 ? "PASS" : "PARTIAL";
            sb.Append("\n[XPC-4CC][POSTCAP_READINESS_SUMMARY] inspected=").Append(shown)
                .Append("; capPass=").Append(capPass)
                .Append("; capReview=").Append(capReview)
                .Append("; thresholdPass=").Append(thresholdPass)
                .Append("; thresholdTruePass=").Append(thresholdTruePass)
                .Append("; thresholdFalsePass=").Append(thresholdFalsePass)
                .Append("; thresholdReview=").Append(thresholdReview)
                .Append("; thresholdInconclusive=").Append(thresholdInconclusive)
                .Append("; oracleErrors=").Append(oracleErrors)
                .Append("; foreignSaturationHook=").Append(Phase4CCLiveXpCapture.HasForeignSaturationHook())
                .Append("; status=").Append(verdict)
                .Append("; thresholdOracle=R5B_RAW_SETTINGS_AND_PAWN_SOURCES")
                .Append("; vanillaPassthroughNotProven=True");
            Phase4CRegressionLedger.Record("4C-C-DAILYCAP-LIVE", verdict,
                "capPass=" + capPass + ",capReview=" + capReview +
                ",thresholdPass=" + thresholdPass + ",thresholdReview=" + thresholdReview +
                ",vanillaOrOtherInconclusive=" + thresholdInconclusive);
            if (verdict == "REVIEW") Log.Error(sb.ToString());
            else Log.Message(sb.ToString());
        }
    }

    public static class Phase4CCPostCapDebugActions
    {
        [DebugAction("XP Customizer", "Inspect selected pawn post-cap readiness (R5-A)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Inspect() { Phase4CCLiveXpCapture.PrintReadiness(); }

        [DebugAction("XP Customizer", "Inspect independent daily cap (R5-B)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void InspectIndependent() { Phase4CCLiveXpCapture.PrintReadiness(); }
    }
}
#endif
