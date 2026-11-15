#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using Verse;
using XpCustomizer.Gws;
using XpCustomizer.Decay;

namespace XpCustomizer
{
    /// <summary>
    /// Phase 4C-C R4: independent-rate and payout-aware, child-skill/memory-safe passive observation of real SkillRecord.Learn calls.
    /// Unlike the detached 4C-B tests, the game performs every Learn call itself.
    /// The probe never calls Learn, modifies XP/settings/jobs, or changes __result.
    /// It is deliberately NOT a full autonomous job/reading scenario test.
    /// </summary>
    internal static partial class Phase4CCLiveXpCapture
    {
        internal const string Revision = "4C-C-BILL-MATH-PREVIEW-T04-B1-B0-R1-20261115";
        private const string DiagnosticHarmonyId = "zcn.xpcustomizer.debug.4cc.live-observer";
        private const int MaxReadingSamples = 40;
        private const int MaxOtherSamples = 40;
        // Decay must not compete with work/reading/other events for the same quota.
        private const int MaxDecaySamples = 100;
        private const int MaxSamples = MaxWorkSamples + MaxReadingSamples + MaxOtherSamples + MaxDecaySamples;
        private const int MaxPreviewLines = 24;
        private const int WorkPerJobSkillLimit = 10;
        private const int ReadingPerJobSkillLimit = 10;
        private const int OtherPerJobSkillLimit = 8;
        private const int DecayPerPawnSkillLimit = 8;
        private static readonly TimeSpan MaxWallTime = TimeSpan.FromMinutes(4);
        private static readonly Harmony observerHarmony = new Harmony(DiagnosticHarmonyId);
        private static readonly List<Sample> samples = new List<Sample>();
        private static readonly Dictionary<string, int> perJobSkillCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);
        // Existing XP Customizer DEBUG Interval hooks signal the execution scope.
        // No new Interval Harmony owner is installed: the gameplay Decay hook's
        // foreign-owner safety check must not be invalidated by this observer.
        [ThreadStatic] private static Stack<SkillRecord> intervalScopes;
        private static MethodBase target;
        private static Map sessionMap;
        private static Pawn sessionPawn;
        private static DateTime startedUtc;
        private static int startedTick;
        private static int sessionNumber;
        private static bool installed;
        private static bool recording;
        private static bool limitReached;
        private static int hookErrors;
        private static int capturedWork;
        private static int capturedReading;
        private static int capturedOther;
        private static int capturedDecay;
        private static int lastPayoutReviewCount;
        private static int lastDailyCapReviewCount;
        private static string lastDailyCapStatus = "NOT_VERIFIED";
        private static bool foreignSaturationHookPresent;
        private static string saturationHookOwnersAtStart = "NONE";
        private static string foreignHooksAtStart = "NONE";
        private static string intervalHookOwnersAtStart = "NONE";
        private static bool intervalForeignHookPresent;
        private static bool learnForeignHookPresent;
        private static string lastReport;

        private sealed class StartState
        {
            internal SkillRecord Record;
            internal Pawn Pawn;
            internal int Level;
            internal float Xp;
            internal float Daily;
            internal float Input;
            internal bool Direct;
            internal bool IgnoreLearnRate;
            internal string Job;
            internal string Category;
            internal string Skill;
            internal string PawnName;
            internal bool ReadingScope;
            internal int Tick;
            internal int RawLevel;
            internal bool TotallyDisabled;
            internal int BiologicalAgeYears;
            internal bool FromInterval;
            internal bool HasExpectedDecay;
            internal bool DecayOverrideRequested;
            internal float ConfiguredBaseDaily;
            internal float ActiveSkillMultiplier;
            internal float MemoryRemaining;
            internal bool HasGreatMemory;
            internal bool HasPerfectMemory;
            internal bool AnomalyActive;
            internal float ExpectedDaily;
            internal float ExpectedInterval;
            internal string WorkKind;
            internal WorkTargetEvidence TargetEvidence;
            internal SourceProofEvidence SourceProof;
            internal string GroupKey;
            internal ReadingSkillCounter ReadingCounter;
            // R5-A1: independent retention flags; the passive observer never
            // changes the underlying Learn call or the actual job.
            internal bool RegularSlot;
            internal bool PostCapSlot;
            internal bool WatchTransition;
            internal bool SaturatedAtSelection;
            internal string TransitionKey;
            internal Phase4CCIndependentDailyCapOracle.Result IndependentDailyCapAtSelection;
            // R3 context: captured BEFORE vanilla SkillRecord.Learn runs.
            // The live rate is an oracle for payout only; it is not an independent
            // reimplementation of RimWorld's full passion/stat rate formula.
            internal bool RateCaptured;
            internal float RateBefore;
            internal bool SaturatedBefore;
            internal float DailyCapBefore;
            internal float GwsFactorBefore;
            internal float ConsciousnessFactorBefore;
            internal float FurtherFactorBefore;
            internal bool FastLearningBefore;
            internal bool ForeignLearnHookAtStart;
            internal string RateCaptureReason;
            // R4: independent equation, anchored only in raw settings + vanilla stat inputs.
            internal bool OracleReady;
            internal bool OracleForeignRateHook;
            internal string OracleReason;
            internal float OracleRate;
            internal float OraclePassion;
            internal float OracleGlobal;
            internal float OracleAnimals;
            internal float OracleConsciousness;
            internal float OracleSpeedFactor;
            internal float OracleWorkSpeed;
            internal float OracleGwsFactor;
            internal float OracleGwsWithoutReading;
            internal float OraclePostCap;
            internal FurtherProof OracleFurtherProof;
        }

        private sealed class Sample
        {
            internal StartState Before;
            internal int AfterLevel;
            internal int AfterRawLevel;
            internal bool AfterDisabled;
            internal float AfterXp;
            internal float AfterDaily;
            internal bool AfterSaturated;
            // Captured only when an actual engine boundary transition occurs.
            internal Phase4CCIndependentDailyCapOracle.Result TransitionCapAtAfter;
            internal string IndependentTransitionClass;
            internal bool Comparable;
            internal float DeltaXp;
            internal float DeltaDaily;
        }

        internal static void Start()
        {
            try
            {
                if (recording)
                {
                    Log.Warning("[XP Customizer][4C-C] START_SKIPPED; reason=ALREADY_RECORDING; use Stop first.");
                    return;
                }
                if (installed)
                {
                    RemoveHooks();
                    if (installed) return;
                }
                if (Current.Game == null || Find.CurrentMap == null)
                {
                    Log.Warning("[XP Customizer][4C-C] START_SKIPPED; reason=NO_LOADED_MAP");
                    return;
                }
                target = AccessTools.Method(typeof(SkillRecord), nameof(SkillRecord.Learn),
                    new[] { typeof(float), typeof(bool), typeof(bool) });
                if (target == null)
                {
                    Log.Error("[XP Customizer][4C-C] START_FAIL; reason=LEARN_METHOD_NOT_FOUND");
                    return;
                }
                foreignHooksAtStart = PatchOwnerNames(target);
                MethodBase intervalMethod = AccessTools.Method(typeof(SkillRecord), "Interval");
                intervalHookOwnersAtStart = intervalMethod == null ? "MISSING" : PatchOwnerNames(intervalMethod);
                intervalForeignHookPresent = HasForeignIntervalOwner(intervalMethod);
                learnForeignHookPresent = HasForeignIntervalOwner(target);
                MethodBase rateTarget = AccessTools.Method(typeof(SkillRecord),
                    nameof(SkillRecord.LearnRateFactor), new[] { typeof(bool) });
                foreignRateHookPresent = HasForeignIntervalOwner(rateTarget);
                rateHookOwnersAtStart = rateTarget == null ? "METHOD_MISSING" : PatchOwnerNames(rateTarget);
                MethodBase saturationTarget = AccessTools.PropertyGetter(typeof(SkillRecord),
                    nameof(SkillRecord.LearningSaturatedToday));
                foreignSaturationHookPresent = HasForeignIntervalOwner(saturationTarget);
                saturationHookOwnersAtStart = saturationTarget == null ? "METHOD_MISSING" :
                    PatchOwnerNames(saturationTarget);
                InspectFurtherHookOwners();
                MethodInfo before = AccessTools.Method(typeof(Phase4CCLiveXpCapture), "BeforeLearn");
                MethodInfo after = AccessTools.Method(typeof(Phase4CCLiveXpCapture), "AfterLearn");
                if (before == null || after == null)
                    throw new MissingMethodException("4C-C observation hooks unavailable");

                sessionMap = Find.CurrentMap;
                Pawn selected = Find.Selector == null ? null : Find.Selector.SingleSelectedThing as Pawn;
                sessionPawn = selected != null && selected.Map == sessionMap &&
                    selected.Faction == Faction.OfPlayer ? selected : null;
                samples.Clear();
                perJobSkillCounts.Clear();
                ResetReadingCoverage();
                ResetPostCapSampling();
                capturedByWorkKind.Clear();
                workKindQuotaDrops.Clear();
                perPawnGroupDrops.Clear();
                if (intervalScopes != null) intervalScopes.Clear();
                lastReport = null;
                hookErrors = 0;
                capturedWork = 0;
                capturedReading = 0;
                capturedOther = 0;
                capturedDecay = 0;
                lastPayoutReviewCount = 0;
                lastRateReviewCount = 0;
                lastDailyCapReviewCount = 0;
                lastDailyCapStatus = "NOT_VERIFIED";
                lastSkillSummaryInvariantFailed = false;
                ResetSourceProofSession();
                limitReached = false;
                startedUtc = DateTime.UtcNow;
                startedTick = Find.TickManager == null ? -1 : Find.TickManager.TicksGame;

                // Manual-only pure regression audit; no Pawn/SkillRecord mutation.
                VerifyDecayClassifierSelfAudit();
                VerifyLivePayoutClassifierSelfAudit();
                VerifyIndependentRateSelfAudit();
                VerifyJobSourceCoverageSelfAudit();
                VerifyJointVerdictSelfAudit();
                VerifySkillSummarySelfAudit();
                VerifySourceProofSelfAudit();
                VerifySourceAuthorizationSelfAudit();
                VerifyBillMathPreviewSelfAudit();
                VerifyBillOfficialSelfAudit();
                VerifyTargetProvenanceSelfAudit();
                VerifyPostCapSamplingSelfAudit();
                Phase4CCIndependentDailyCapOracle.RunSyntheticAudit();
                VerifyIndependentFurtherSelfAudit();
                VerifyReadingCoverageSelfAudit();

                // Our own unique owner ID prevents removal of XP Customizer's existing
                // gameplay patches or any other mod's patches when stopping.
                observerHarmony.Patch(target, prefix: new HarmonyMethod(before),
                    postfix: new HarmonyMethod(after));
                installed = true;
                InstallReadingBookHook();
                recording = true;
                sessionNumber++;
                Log.Message("[XPC-4CC][SOURCE_PROOF_START] mode=SHADOW; revision=" + SourceProofRevision +
                    "; runtimeMvid=" + sourceRuntimeMvid + "; expectedMvid=" + P0GameMvid +
                    "; assemblyMatched=" + sourceGameMvidMatches +
                    "; callbacks=" + sourceCallbackAvailability +
                    "; sourceTraceBudget=" + MaxSourceProofTraces +
                    "; perSkillTraceBudget=" + MaxSourceProofTracesPerSkill +
                    "; promotedJointPass=0");
                Log.Message("[XP Customizer][4C-C] START; session=" + sessionNumber +
                    "; build=" + XpCustomizerMod.BuildId + "; revision=" + Revision +
                    "; readingSchema=" + ReadingRevision + "; bookHook=" + bookTickHookStatus +
                    "; textbookDoerHook=" + textbookTickHookStatus +
                    "; mode=LIVE_PASSIVE; pawnScope=" +
                    (sessionPawn == null ? "ALL_PLAYER_COLONISTS_ON_MAP" : Clean(sessionPawn.LabelShort)) +
                    "; map=ACTIVE_MAP" +
                    "; hookOwner=" + DiagnosticHarmonyId +
                    "; preexistingLearnHookOwners=" + foreignHooksAtStart +
                    "; quotas=work144_reading40_other40_decay100; workKindCaps=construction32_harvest32_cutting32_mining32_other16; workSelection=PARTITIONED_FIRST_SEEN; groupIncludesWorkKind=True; perJobSkillPawnCaps=10/10/8/8; disabledNonDecayCap=2; regularMaxSamples=" + MaxSamples + "; postCapWorkCap=" + MaxPostCapWorkSamples +
                    "; postCapReadingCap=" + MaxPostCapReadingSamples +
                    "; transitionCap=" + MaxTransitionSamples +
                    "; postCapPerPawnGroupCap=" + PostCapGroupLimit +
                    "; maxWallMinutes=4" +
                    "; intervalHookOwners=" + intervalHookOwnersAtStart +
                    "; foreignLearnHookPresent=" + learnForeignHookPresent +
                    "; rateHookOwners=" + rateHookOwnersAtStart +
                    "; foreignRateHookPresent=" + foreignRateHookPresent +
                    "; saturationHookOwners=" + saturationHookOwnersAtStart +
                    "; foreignFurtherHookPresent=" + foreignFurtherHookPresent +
                    "; furtherHookOwners=" + furtherHookOwnersAtStart +
                    "; foreignSaturationHookPresent=" + foreignSaturationHookPresent +
                    "; payoutOracle=PRE_LEARN_RATE_SNAPSHOT; independentRateOracle=RAW_SETTINGS_AND_STAT_INPUTS" +
                    "; jointSchema=" + JointRevision + "; jointScope=SAME_REGULAR_SAMPLE_ONLY" +
                    "; skillSummarySchema=" + SkillSummaryRevision + "; skillSummaryScope=RETAINED_REGULAR_ONLY" +
                    "; sourceProofSchema=" + SourceProofRevision + "; sourceProofMode=SHADOW_NO_PROMOTION" +
                    "; saturationOracle=INDEPENDENT_DAILY_CAP_ORACLE_R5B; postCapFurtherOracle=RAW_BASE_PLUS_MTH_PP_TIMES_CONS_V2_R5F1; postCapCapture=SEPARATE_RESERVED_SLOTS; thresholdOracle=RAW_SETTINGS_ACTIVE_SOURCES_AND_CONS_V2; jobSourceProof=JOBDEF_TARGET_DESIGNATION_CONTEXT_NOT_CALLSITE; woodYield=WoodLog_DEFNAME; payoutRules=POSITIVE_NON_DIRECT_SAME_LEVEL" +
                    "; automaticJobControl=NO; learnCallsByProbe=0; xpWritesByProbe=0");
            }
            catch (Exception ex)
            {
                recording = false;
                Log.Error("[XP Customizer][4C-C] START_FAIL; " + ex);
                // Patch may have been partially applied before an exception.
                try { RemoveHooks(); }
                catch (Exception cleanup) { Log.Error("[XP Customizer][4C-C] CLEANUP_FAIL; " + cleanup); }
            }
        }

        internal static void StopAndReport()
        {
            try
            {
                if (!installed && !recording)
                {
                    Log.Warning("[XP Customizer][4C-C] STOP_SKIPPED; reason=NO_CAPTURE_ACTIVE");
                    if (!string.IsNullOrEmpty(lastReport)) PrintLast();
                    return;
                }
                recording = false;
                RemoveHooks();
                if (intervalScopes != null) intervalScopes.Clear();
                lastReport = BuildReport();
                Phase4CRegressionLedger.PrintSummaryAfterLive();
                if (hookErrors != 0 || installed || lastPayoutReviewCount != 0 || lastRateReviewCount != 0 ||
                    lastSkillSummaryInvariantFailed || billMathPreviewInvalid ||
                    officialBillSeal != OfficialBillSealed) Log.Error(lastReport);
                else Log.Message(lastReport);
            }
            catch (Exception ex)
            {
                recording = false;
                Log.Error("[XP Customizer][4C-C] STOP_FAIL; " + ex);
            }
        }

        internal static void PrintLast()
        {
            if (string.IsNullOrEmpty(lastReport))
                Log.Message("[XP Customizer][4C-C] NO_REPORT; run Start, allow work/reading, then Stop.");
            else
                Log.Message("[XP Customizer][4C-C] LAST_REPORT_REPRINT\n" + lastReport);
        }

        private static void RemoveHooks()
        {
            recording = false;
            if (textbookTickTarget != null)
            {
                observerHarmony.Unpatch(textbookTickTarget, HarmonyPatchType.All, DiagnosticHarmonyId);
                textbookTickHookInstalled = HasPatchOwner(textbookTickTarget, DiagnosticHarmonyId);
                if (textbookTickHookInstalled)
                    Log.Error("[XP Customizer][4C-C] TEXTBOOK_UNPATCH_INCOMPLETE; owner=" + DiagnosticHarmonyId);
            }
            if (bookTickTarget != null)
            {
                observerHarmony.Unpatch(bookTickTarget, HarmonyPatchType.All, DiagnosticHarmonyId);
                bookTickHookInstalled = HasPatchOwner(bookTickTarget, DiagnosticHarmonyId);
                if (bookTickHookInstalled)
                    Log.Error("[XP Customizer][4C-C] BOOK_UNPATCH_INCOMPLETE; owner=" + DiagnosticHarmonyId);
            }
            if (target == null)
            {
                installed = bookTickHookInstalled || textbookTickHookInstalled;
                return;
            }
            // Never use UnpatchAll() without an ID: other mods and the XP
            // Customizer gameplay Harmony instance must remain untouched.
            observerHarmony.Unpatch(target, HarmonyPatchType.All, DiagnosticHarmonyId);
            installed = HasPatchOwner(target, DiagnosticHarmonyId) ||
                bookTickHookInstalled || textbookTickHookInstalled;
            if (installed)
                Log.Error("[XP Customizer][4C-C] UNPATCH_INCOMPLETE; owner=" + DiagnosticHarmonyId);
        }

        // Called from the existing DEBUG-only SkillRecord.Interval patch (same
        // gameplay Harmony owner). This is identification of an Interval-origin
        // Learn call, not proof that a particular mod caused its amount.
        internal static void EnterInterval(SkillRecord skill)
        {
            if (!recording || skill == null) return;
            if (intervalScopes == null) intervalScopes = new Stack<SkillRecord>();
            intervalScopes.Push(skill);
        }

        internal static void LeaveInterval(SkillRecord skill)
        {
            if (intervalScopes == null || intervalScopes.Count == 0) return;
            if (System.Object.ReferenceEquals(intervalScopes.Peek(), skill))
                intervalScopes.Pop();
            else
            {
                intervalScopes.Clear();
                hookErrors++;
            }
        }

        private static bool InInterval(SkillRecord skill)
        {
            return intervalScopes != null && intervalScopes.Count != 0 &&
                System.Object.ReferenceEquals(intervalScopes.Peek(), skill);
        }

        // Independent diagnostic arithmetic based on the configured level/day,
        // enabled skill multiplier, and active memory trait. This does NOT call
        // the production DecayModifiersService.LossPerInterval().
        private static bool SnapshotExpectedDecay(StartState b)
        {
            XpCustomizerSettings s = XpCustomizerMod.Settings;
            if (s == null || b.Record == null || b.Record.def == null || b.Pawn == null ||
                b.RawLevel < 0 || b.RawLevel > 20) return false;
            float baseDaily = SkillDecayService.VanillaDaily[b.RawLevel];
            if (s.decayByLevelEnabled && s.decayDailyByLevel != null &&
                s.decayDailyByLevel.Count == 21)
            {
                float configured = s.decayDailyByLevel[b.RawLevel];
                if (!float.IsNaN(configured) && !float.IsInfinity(configured))
                    baseDaily = Math.Max(0f, Math.Min(SkillDecayService.MaxDaily, configured));
            }
            float skillFactor = 1f;
            float configuredSkill;
            if (s.decaySkillMultipliersEnabled && s.decaySkillMultipliers != null &&
                s.decaySkillMultipliers.TryGetValue(b.Record.def.defName, out configuredSkill) &&
                !float.IsNaN(configuredSkill) && !float.IsInfinity(configuredSkill))
                skillFactor = Math.Max(0f, Math.Min(100f, configuredSkill));
            bool great = b.Pawn.story != null && b.Pawn.story.traits != null &&
                b.Pawn.story.traits.HasTrait(TraitDefOf.GreatMemory);
            bool perfect = b.Pawn.story != null && b.Pawn.story.traits != null &&
                b.Pawn.story.traits.HasTrait(TraitDefOf.PerfectMemory);
            b.HasGreatMemory = great;
            b.HasPerfectMemory = perfect;
            b.AnomalyActive = ModsConfig.AnomalyActive;
            b.DecayOverrideRequested = DecayOverridePolicy.GetReasons(s, b.RawLevel,
                b.Record.def.defName, great, perfect, b.AnomalyActive) != DecayOverrideReason.None;
            float reduction = 0f;
            if (ModsConfig.AnomalyActive && perfect)
                reduction = s.perfectMemoryDecayReduction;
            else if (great)
                reduction = s.greatMemoryDecayReduction;
            if (float.IsNaN(reduction) || float.IsInfinity(reduction))
                reduction = ModsConfig.AnomalyActive && perfect ? 100f : great ? 50f : 0f;
            reduction = (float)(Math.Round(Math.Max(0f, Math.Min(100f, reduction)) * 100f,
                MidpointRounding.AwayFromZero) / 100d);
            float remaining = (100f - reduction) / 100f;
            double daily = Math.Max(0d, Math.Min(SkillDecayService.MaxDaily,
                (double)baseDaily * skillFactor * remaining));
            b.ConfiguredBaseDaily = baseDaily;
            b.ActiveSkillMultiplier = skillFactor;
            b.MemoryRemaining = remaining;
            b.ExpectedDaily = (float)daily;
            b.ExpectedInterval = b.ExpectedDaily / SkillDecayService.IntervalsPerDay;
            return true;
        }

        // Harmony argument positions rather than source parameter names avoid an
        // unnecessary dependency on RimWorld's decompiler parameter spelling.
        private static void BeforeLearn(SkillRecord __instance, float __0,
            bool __1, bool __2, out StartState __state)
        {
            __state = null;
            if (!recording) return;
            try
            {
                if (DateTime.UtcNow - startedUtc > MaxWallTime ||
                    (samples.Count >= MaxSamples && PostCapBudgetsExhausted()))
                {
                    recording = false; // disarm; explicit Stop removes the Harmony hooks
                    limitReached = true;
                    return;
                }
                if (__instance == null) return;
                Pawn pawn = __instance.Pawn;
                if (pawn == null || pawn.Dead || pawn.Map != sessionMap ||
                    pawn.Faction != Faction.OfPlayer ||
                    (sessionPawn != null && pawn != sessionPawn)) return;
                string job = pawn.CurJob?.def?.defName ?? "NO_JOB";
                bool inReading = GwsReadingScope.IsReadingTick;
                bool fromInterval = InInterval(__instance);
                // SkillRecord.Interval may call Learn while ANY job is active.
                // Treat it as decay, never as XP caused by that job.
                string category = fromInterval ? "DECAY_INTERVAL" : Classify(job, inReading);
                // Only plant jobs require an early target read to select a
                // quota. For mining/construction, inspect after the cap gates
                // to avoid allocating evidence for discarded Learn calls.
                string nameOnlyKind = WorkKind(job);
                bool targetDeterminesQuota = category == "WORK_CANDIDATE" &&
                    (nameOnlyKind == "HARVEST_CANDIDATE" ||
                     nameOnlyKind == "CUTTING_CANDIDATE");
                WorkTargetEvidence targetEvidence = targetDeterminesQuota ?
                    InspectWorkTarget(pawn) : null;
                string workKind = ResolvedWorkKind(job, targetEvidence);
                string skillName = Clean(__instance.def == null ? "UNKNOWN" : __instance.def.defName);
                // R3: one child or frequently-learning pawn must not consume the
                // entire Reading+Intellectual or Work+Construction group quota.
                string groupKey = fromInterval
                    ? category + "|" + pawn.ThingID + "|" + skillName
                    : category + "|" + (category == "WORK_CANDIDATE" ? workKind + "|" : "") +
                        job + "|" + skillName + "|" + pawn.ThingID;
                int currentForGroup;
                perJobSkillCounts.TryGetValue(groupKey, out currentForGroup);
                int groupLimit = category == "WORK_CANDIDATE" ? WorkPerJobSkillLimit :
                    category == "READING" ? ReadingPerJobSkillLimit :
                    category == "DECAY_INTERVAL" ? DecayPerPawnSkillLimit : OtherPerJobSkillLimit;
                if (__instance.TotallyDisabled && !fromInterval)
                    groupLimit = Math.Min(groupLimit, 2);
                bool regularSlot = currentForGroup < groupLimit && samples.Count < MaxSamples &&
                    (category == "WORK_CANDIDATE" ?
                        capturedWork < MaxWorkSamples && WorkKindHasRoom(workKind) :
                     category == "READING" ? capturedReading < MaxReadingSamples :
                     category == "DECAY_INTERVAL" ? capturedDecay < MaxDecaySamples :
                        capturedOther < MaxOtherSamples);
                ReadingSkillCounter readingCounter = null;
                if (category == "READING")
                    readingCounter = ObserveReadingLearn(pawn, skillName, __0,
                        __instance.TotallyDisabled, __instance.levelInt, __instance.Level,
                        regularSlot, currentForGroup >= groupLimit);
                if (category == "WORK_CANDIDATE" && !regularSlot)
                {
                    if (currentForGroup >= groupLimit) RecordWorkDrop(perPawnGroupDrops, workKind);
                    else RecordWorkDrop(workKindQuotaDrops, workKind);
                }
                // R5-A1: post-cap/threshold observations do not consume R4-S3
                // first-seen work/reading slots. Separate bounded buckets are
                // checked before any early return caused by a regular quota.
                bool positiveRateSource = PurePositiveRateSource(category, __0, __1,
                    __2, __instance.TotallyDisabled, DebugSettings.fastLearning);
                if (positiveRateSource && category == "WORK_CANDIDATE")
                    positiveRateSource = ExpectedWorkSkill(job, skillName, workKind);
                if (positiveRateSource && category == "READING")
                    positiveRateSource = inReading;
                bool saturatedAtSelection = false;
                bool postCapSlot = false;
                bool watchTransition = false;
                string transitionKey = pawn.ThingID + "|" + skillName;
                if (positiveRateSource)
                {
                    saturatedAtSelection = __instance.LearningSaturatedToday;
                    if (saturatedAtSelection)
                        postCapSlot = MayObservePostCap(category, groupKey);
                    else
                        watchTransition = MayObserveTransition(transitionKey);
                }
                if (!regularSlot && !postCapSlot && !watchTransition) return;
                if (category == "WORK_CANDIDATE" && targetEvidence == null)
                    targetEvidence = InspectWorkTarget(pawn);
                __state = new StartState
                {
                    Record = __instance,
                    Pawn = pawn,
                    PawnName = Clean(pawn.LabelShort),
                    Skill = skillName,
                    GroupKey = groupKey,
                    ReadingCounter = readingCounter,
                    RegularSlot = regularSlot,
                    PostCapSlot = postCapSlot,
                    WatchTransition = watchTransition,
                    SaturatedAtSelection = saturatedAtSelection,
                    TransitionKey = transitionKey,
                    WorkKind = workKind,
                    TargetEvidence = targetEvidence,
                    RawLevel = __instance.levelInt,
                    TotallyDisabled = __instance.TotallyDisabled,
                    BiologicalAgeYears = pawn.ageTracker == null ? -1 : pawn.ageTracker.AgeBiologicalYears,
                    FromInterval = fromInterval,
                    Job = Clean(job),
                    Category = category,
                    ReadingScope = inReading,
                    Level = __instance.Level,
                    Xp = __instance.xpSinceLastLevel,
                    Daily = __instance.xpSinceMidnight,
                    Input = __0,
                    Direct = __1,
                    IgnoreLearnRate = __2,
                    Tick = Find.TickManager == null ? -1 : Find.TickManager.TicksGame
                };
                // Shadow SourceProof is completely detached from XP/rate/slot policy.
                // Its own failures never skip or change the original Learn call.
                __state.SourceProof = ObserveSourceProof(__state);
                if (__0 < 0f) __state.HasExpectedDecay = SnapshotExpectedDecay(__state);
                if (__0 > 0f && (category == "WORK_CANDIDATE" || category == "READING"))
                    {
                        SnapshotPayoutContext(__state);
                        SnapshotIndependentRate(__state);
                        if (__state.PostCapSlot && __state.RateCaptured)
                        {
                            var capProof = Phase4CCIndependentDailyCapOracle.Compose(
                                __state.Record, XpCustomizerMod.Settings);
                            Phase4CCIndependentDailyCapOracle.Compare(capProof, __state.Daily,
                                __state.DailyCapBefore, __state.SaturatedBefore,
                                foreignSaturationHookPresent);
                            __state.IndependentDailyCapAtSelection = capProof;
                        }
                    }
            }
            catch (Exception) { hookErrors++; __state = null; }
        }

        private static void AfterLearn(SkillRecord __instance, StartState __state)
        {
            if (__state == null) return;
            try
            {
                if (!System.Object.ReferenceEquals(__instance, __state.Record))
                {
                    hookErrors++;
                    return;
                }
                var sample = new Sample
                {
                    Before = __state,
                    AfterLevel = __instance.Level,
                    AfterRawLevel = __instance.levelInt,
                    AfterDisabled = __instance.TotallyDisabled,
                    AfterXp = __instance.xpSinceLastLevel,
                    AfterDaily = __instance.xpSinceMidnight
                };
                // Public Level can remain zero for an age-disabled skill even
                // if its internal raw level changes. Check both boundaries.
                sample.Comparable = sample.AfterLevel == __state.Level &&
                    sample.AfterRawLevel == __state.RawLevel;
                if (sample.Comparable)
                    sample.DeltaXp = sample.AfterXp - __state.Xp;
                sample.DeltaDaily = sample.AfterDaily - __state.Daily;
                // Test the actual engine flag AFTER the original Learn call.
                // This is NOT an independent daily-cap oracle (R5-B).
                if (__state.WatchTransition || __state.PostCapSlot)
                    sample.AfterSaturated = __instance.LearningSaturatedToday;
                if (__state.RegularSlot && samples.Count < MaxSamples)
                {
                    samples.Add(sample);
                    int prior;
                    perJobSkillCounts.TryGetValue(__state.GroupKey, out prior);
                    perJobSkillCounts[__state.GroupKey] = prior + 1;
                    if (__state.Category == "WORK_CANDIDATE")
                    {
                        capturedWork++;
                        RecordWorkKind(__state.WorkKind);
                    }
                    else if (__state.Category == "READING")
                    {
                        capturedReading++;
                        NoteStoredReadingRegular(__state.ReadingCounter);
                    }
                    else if (__state.Category == "DECAY_INTERVAL") capturedDecay++;
                    else capturedOther++;
                }
                int beforePostCapReading = postCapReadingCount;
                CaptureReservedPostCap(sample);
                if (__state.Category == "READING" &&
                    postCapReadingCount > beforePostCapReading)
                    NoteStoredReadingPostCap(__state.ReadingCounter);
                if (samples.Count >= MaxSamples && PostCapBudgetsExhausted())
                {
                    recording = false;
                    limitReached = true;
                }
            }
            catch (Exception) { hookErrors++; }
        }

        // Shared read-only foreign Harmony ownership gate for Learn and Interval.
        internal static bool HasForeignSaturationHook()
        {
            MethodBase method = AccessTools.PropertyGetter(typeof(SkillRecord),
                nameof(SkillRecord.LearningSaturatedToday));
            return HasForeignIntervalOwner(method);
        }

        private static bool HasForeignIntervalOwner(MethodBase method)
        {
            if (method == null) return true;
            var info = Harmony.GetPatchInfo(method);
            if (info == null) return false;
            var names = new HashSet<string>(StringComparer.Ordinal);
            CollectOwners(names, info.Prefixes);
            CollectOwners(names, info.Postfixes);
            CollectOwners(names, info.Transpilers);
            CollectOwners(names, info.Finalizers);
            foreach (string name in names)
                if (name != "zcn.xpcustomizer") return true;
            return false;
        }

        private static bool HasPatchOwner(MethodBase method, string owner)
        {
            var info = Harmony.GetPatchInfo(method);
            if (info == null) return false;
            return ContainsOwner(info.Prefixes, owner) || ContainsOwner(info.Postfixes, owner) ||
                ContainsOwner(info.Transpilers, owner) || ContainsOwner(info.Finalizers, owner);
        }
        private static bool ContainsOwner(IEnumerable<Patch> patches, string owner)
        {
            if (patches == null) return false;
            foreach (Patch p in patches)
                if (p != null && string.Equals(p.owner, owner, StringComparison.Ordinal)) return true;
            return false;
        }
        private static string PatchOwnerNames(MethodBase method)
        {
            var info = Harmony.GetPatchInfo(method);
            if (info == null) return "NONE";
            var owners = new HashSet<string>(StringComparer.Ordinal);
            CollectOwners(owners, info.Prefixes);
            CollectOwners(owners, info.Postfixes);
            CollectOwners(owners, info.Transpilers);
            CollectOwners(owners, info.Finalizers);
            if (owners.Count == 0) return "NONE";
            var list = new List<string>(owners); list.Sort(StringComparer.Ordinal);
            return Clean(string.Join(",", list.ToArray()));
        }
        private static void CollectOwners(HashSet<string> owners, IEnumerable<Patch> patches)
        {
            if (patches == null) return;
            foreach (Patch p in patches) if (p != null) owners.Add(p.owner ?? "UNKNOWN");
        }
        private static string Clean(string input)
        {
            if (string.IsNullOrEmpty(input)) return "NONE";
            string safe = input.Replace('\r', ' ').Replace('\n', ' ').Replace(';', '_');
            return safe.Length > 140 ? safe.Substring(0, 140) : safe;
        }
        private static string F(float v)
        {
            return v.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static float InputTolerance(float expected)
        {
            return Math.Max(0.0002f, Math.Abs(expected) * 0.005f);
        }

        private static float DeltaTolerance(float input)
        {
            // Large level-20 float XP accumulators are rounded more coarsely.
            return Math.Max(0.005f, Math.Abs(input) * 0.002f);
        }

        private static string NegativeClassification(Sample s)
        {
            StartState b = s.Before;
            if (b.Input >= 0f && (!s.Comparable || s.DeltaXp >= -0.0001f)) return "NOT_NEGATIVE";
            if (!b.FromInterval) return "NEGATIVE_OUTSIDE_INTERVAL";
            if (!s.Comparable) return "INTERVAL_LEVEL_BOUNDARY_INCONCLUSIVE";
            if (!b.HasExpectedDecay) return "INTERVAL_NO_REFERENCE";
            // A negative input from a delegated Interval is not XP Customizer's
            // configured custom loss. Do not falsely blame the mod in that case.
            if (!b.DecayOverrideRequested) return "INTERVAL_DELEGATED_NOT_XPC";
            if (b.Input >= 0f) return "INTERVAL_NEGATIVE_DELTA_UNEXPECTED";
            // Always validate the computed loss submitted to Learn, even when
            // the skill is disabled and RimWorld ignores the XP update.
            if (Math.Abs(b.Input + b.ExpectedInterval) > InputTolerance(b.ExpectedInterval))
                return intervalForeignHookPresent
                    ? "INTERVAL_INPUT_MISMATCH_FOREIGN_PRESENT"
                    : "INTERVAL_INPUT_CONFIG_MISMATCH";
            if (b.TotallyDisabled || s.AfterDisabled)
            {
                // A child/other disabled skill can retain an internal levelInt
                // even when public Level is zero. It is NOT a decay bug when
                // Learn(-loss) is ignored for this disabled skill.
                if (b.TotallyDisabled != s.AfterDisabled)
                    return "INTERVAL_DISABLED_STATE_CHANGED_INCONCLUSIVE";
                return Math.Abs(s.DeltaXp) <= DeltaTolerance(b.Input)
                    ? "INTERVAL_DISABLED_SKILL_SKIP"
                    : "INTERVAL_DISABLED_SKILL_CHANGED_REVIEW";
            }
            if (Math.Abs(s.DeltaXp - b.Input) > DeltaTolerance(b.Input))
                return intervalForeignHookPresent
                    ? "INTERVAL_DELTA_MISMATCH_FOREIGN_PRESENT"
                    : "INTERVAL_DELTA_MISMATCH";
            return intervalForeignHookPresent
                ? "INTERVAL_MEASURED_MATCH_FOREIGN_PRESENT"
                : "INTERVAL_CONFIG_MATCH";
        }

        private static void VerifyDecayClassifierSelfAudit()
        {
            // Foreign owners no longer disable user customization. The
            // expected classification adapts to the current owner state.
            var b = new StartState
            {
                FromInterval = true,
                HasExpectedDecay = true,
                DecayOverrideRequested = true,
                ExpectedInterval = 0.1f,
                Input = -0.1f
            };
            var sample = new Sample { Before = b, Comparable = true, DeltaXp = -0.1f };
            RequireClass(sample, intervalForeignHookPresent
                ? "INTERVAL_MEASURED_MATCH_FOREIGN_PRESENT"
                : "INTERVAL_CONFIG_MATCH", "ACTIVE_DECAY");
            b.DecayOverrideRequested = false;
            RequireClass(sample, "INTERVAL_DELEGATED_NOT_XPC", "DELEGATED_OTHER_MOD");
            b.DecayOverrideRequested = true;
            b.TotallyDisabled = true;
            sample.AfterDisabled = true;
            sample.DeltaXp = 0f;
            RequireClass(sample, "INTERVAL_DISABLED_SKILL_SKIP", "DISABLED_NO_DELTA");
            b.Input = -0.2f;
            RequireClass(sample, intervalForeignHookPresent
                ? "INTERVAL_INPUT_MISMATCH_FOREIGN_PRESENT"
                : "INTERVAL_INPUT_CONFIG_MISMATCH", "DISABLED_WRONG_INPUT");
            b.Input = -0.1f;
            sample.DeltaXp = -0.1f;
            RequireClass(sample, "INTERVAL_DISABLED_SKILL_CHANGED_REVIEW", "DISABLED_MUTATION");
            sample.DeltaXp = 0f;
            sample.AfterDisabled = false;
            RequireClass(sample, "INTERVAL_DISABLED_STATE_CHANGED_INCONCLUSIVE", "STATE_CHANGE");
            sample.Comparable = false;
            RequireClass(sample, "INTERVAL_LEVEL_BOUNDARY_INCONCLUSIVE", "LEVEL_BOUNDARY");
            Log.Message("[XPC-4CC][CLASSIFIER_AUDIT] status=PASS; cases=7; foreignOwnerPresent=" +
                intervalForeignHookPresent + "; liveXpWritten=0");
        }

        private static void RequireClass(Sample s, string expected, string id)
        {
            string actual = NegativeClassification(s);
            if (actual != expected)
                throw new InvalidOperationException("4C-C classifier " + id +
                    " expected=" + expected + " actual=" + actual);
        }


    }

    public static class Phase4CCLiveXpDebugActions
    {
        [DebugAction("XP Customizer", "Start live work/reading XP capture (4C-C)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Start()
        {
            Phase4CCLiveXpCapture.Start();
        }

        [DebugAction("XP Customizer", "Stop and report live XP capture (4C-C)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Stop()
        {
            Phase4CCLiveXpCapture.StopAndReport();
        }

        [DebugAction("XP Customizer", "Print last 4C-C live XP report",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Print()
        {
            Phase4CCLiveXpCapture.PrintLast();
        }
    }
}
#endif
