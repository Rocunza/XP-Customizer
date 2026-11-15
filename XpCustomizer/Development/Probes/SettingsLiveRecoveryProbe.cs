#if DEBUG
using System;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.Biotech;
using XpCustomizer.Progression;
using XpCustomizer.Profiles;
using XpCustomizer.UI;

namespace XpCustomizer
{
    /// <summary>
    /// R6-P0-D4: real-game, deliberately OPT-IN, synchronous, paused-game
    /// verification. Only the two-click Debug settings UI calls RunManual.
    /// Startup performs read-only checks; never an automatic live mutation.
    /// The normal profile apply gate remains closed in both build variants.
    /// </summary>
    internal static class SettingsLiveRecoveryProbe
    {
        internal const string Revision = "R6P0D4-LIVE-OPTIN-20261106";
        private static bool running;

        internal static void RunGateSelfTest()
        {
            if (!SettingsRuntimeApplyCoordinator.LiveApplyStillBlockedForAudit)
                throw new InvalidOperationException("D4_LIVE_PROFILE_GATE_UNEXPECTEDLY_OPEN");
            SettingsRuntimeApplyResult denied =
                SettingsRuntimeApplyCoordinator.ApplyLiveForManualDiagnostic(
                    null, SettingsRuntimeApplyCoordinator.LiveDiagnosticFault.None);
            if (denied.Status != SettingsRuntimeApplyStatus.APPLY_REJECTED)
                throw new InvalidOperationException("D4_MANUAL_ENTRY_PREFLIGHT_NOT_REJECTED");
            Log.Message("[XPC-R6P0D4][LIVE_GATE_AUDIT] status=PASS; " +
                "legacyD4Entry=BLOCKED_BY_DESIGN; productionApply=NOT_EXERCISED_BY_D4_PROBE; nullCandidate=REJECTED; " +
                "testWrites=0; startupLiveGeneDefMutations=0; build=" + XpCustomizerMod.BuildId);
        }

        internal static void ReadOnlyAfterDefsLoaded()
        {
            if (!ModsConfig.BiotechActive)
            {
                Log.Message("[XPC-R6P0D4][LIVE_READONLY_AUDIT] status=SKIP; " +
                    "reason=BIOTECH_DLC_INACTIVE; liveWrites=0; build=" + XpCustomizerMod.BuildId);
                return;
            }
            try
            {
                XpCustomizerSettings current = XpCustomizerMod.Settings;
                int count;
                string reason;
                bool effective = BiotechGeneService.AuditLiveValues(current, out count, out reason);
                BiotechGeneService.RollbackSnapshot snapshot = BiotechGeneService.CaptureRollbackSnapshot();
                string snapshotReason;
                bool consistent = snapshot.MatchesCurrent(out snapshotReason);
                bool good = effective && consistent && count == snapshot.GeneCount;
                string status = good ? "PASS" : "FAIL";
                string details = good ? "NONE" :
                    (effective ? "SNAPSHOT:" + snapshotReason : "EFFECTIVE:" + reason);
                string line = "[XPC-R6P0D4][LIVE_READONLY_AUDIT] status=" + status +
                    "; revision=" + Revision + "; geneCount=" + count +
                    "; ownershipCount=" + snapshot.OwnershipCount +
                    "; applied=" + snapshot.WasApplied +
                    "; scope=" + (current != null && current.biotechCustomizationEnabled ?
                        "ON_VALUES_AND_OWNERSHIP" : "OFF_OWNERSHIP_ONLY") +
                    "; reason=" + details +
                    "; liveSettingsWrites=0; geneDefWrites=0; pawnWrites=0; scribeCalls=0" +
                    "; manualLiveTest=NOT_RUN; legacyD4Entry=BLOCKED_BY_DESIGN; productionApply=NOT_EXERCISED_BY_D4_PROBE" +
                    "; build=" + XpCustomizerMod.BuildId;
                if (good) Log.Message(line); else Log.Warning(line);
            }
            catch (Exception ex)
            {
                Log.Warning("[XPC-R6P0D4][LIVE_READONLY_AUDIT] status=FAIL; reason=" +
                    ex.GetType().Name + "; writes=0; build=" + XpCustomizerMod.BuildId);
            }
        }

        internal static void RunManual()
        {
            if (RuntimeRecoverySessionGate.LiveIsBlocked)
            {
                Log.Warning("[XPC-R6P0D4][LIVE_ROLLBACK_AUDIT] status=REJECTED; " +
                    "reason=RUNTIME_RECOVERY_LOCKED_RESTART_REQUIRED; writes=0; build=" +
                    XpCustomizerMod.BuildId);
                return;
            }
            if (running)
            {
                Log.Warning("[XPC-R6P0D4][LIVE_ROLLBACK_AUDIT] status=REJECTED; reason=REENTRANT");
                return;
            }
            // UI invocation, not a background task or an automatic game-start test.
            if (Event.current == null || Current.Game == null || Find.TickManager == null ||
                !Find.TickManager.Paused || !ModsConfig.BiotechActive ||
                XpCustomizerMod.Settings == null ||
                !SettingsRuntimeApplyCoordinator.LiveApplyStillBlockedForAudit)
            {
                Log.Warning("[XPC-R6P0D4][LIVE_ROLLBACK_AUDIT] status=REJECTED; " +
                    "reason=REQUIRES_DEBUG_UI_LOADED_PAUSED_BIOTECH_AND_BLOCKED_PROFILE_GATE" +
                    "; writes=0; build=" + XpCustomizerMod.BuildId);
                return;
            }

            running = true;
            Log.Warning("[XPC-R6P0D4][LIVE_ROLLBACK_AUDIT] status=START; " +
                "mode=MANUAL_DEBUG_ONLY; paused=True; legacyD4Entry=BLOCKED_BY_DESIGN; productionApply=NOT_EXERCISED_BY_D4_PROBE; " +
                "build=" + XpCustomizerMod.BuildId);
            XpCustomizerSettings target = XpCustomizerMod.Settings;
            SettingsRuntimeRecovery.Backup initialSettings = null;
            BiotechGeneService.RollbackSnapshot initialGenes = null;
            bool mayHaveWritten = false;
            bool mayHaveTouchedGenes = false;
            bool scenariosPassed = false;
            bool finalVerified = false;
            int transitions = 0;
            int faults = 0;
            int originalGeneCount = 0;
            string failure = "NONE";
            try
            {
                // Entire preflight is read-only. A failure cannot touch the game.
                // Numeric drafts can be discarded by UI refresh: reject rather
                // than silently committing/losing another editor's pending input.
                ProfileEditorReadinessResult editor = ProfileEditorReadinessService.Check();
                if (editor.Code != ProfileEditorReadinessCode.Ready)
                    throw new InvalidOperationException("PREFLIGHT_EDITOR_" + editor.Code +
                        ":" + editor.Source);
                SettingsCandidateValidator.Result validation = SettingsCandidateValidator.Validate(target);
                if (!validation.Accepted)
                    throw new InvalidOperationException("PREFLIGHT_CANDIDATE:" + validation.Reason);
                int count;
                string effectiveReason;
                if (!BiotechGeneService.AuditLiveValues(target, out count, out effectiveReason))
                    throw new InvalidOperationException("PREFLIGHT_GENE_STATE:" + effectiveReason);
                initialSettings = SettingsRuntimeRecovery.Capture(target);
                initialGenes = BiotechGeneService.CaptureRollbackSnapshot();
                originalGeneCount = initialGenes.GeneCount;
                if (originalGeneCount <= 0)
                    throw new InvalidOperationException("PREFLIGHT_NO_GENES");
                string initialReason;
                if (!initialGenes.MatchesCurrent(out initialReason))
                    throw new InvalidOperationException("PREFLIGHT_SNAPSHOT:" + initialReason);

                XpCustomizerSettings original = initialSettings.Values;
                float beforeCurve = XpCurve.GetTotalXp(target);

                // CACHE-only: verify the actual XP curve uses the candidate
                // and that no GeneDef or ownership reference was touched.
                XpCustomizerSettings curve = SettingsSnapshotService.CloneIndependent(target);
                curve.careerXpMultiplier = curve.careerXpMultiplier < 50f ?
                    curve.careerXpMultiplier + 1f : curve.careerXpMultiplier - 1f;
                mayHaveWritten = true;
                RunSuccessfulStep(curve, true, false, "CACHE_ONLY");
                transitions++;
                float changedCurve = XpCurve.GetTotalXp(target);
                if (Math.Abs(changedCurve - beforeCurve) < 0.01f)
                    throw new InvalidOperationException("CACHE_CURVE_RESULT_NOT_CHANGED");
                AssertGenesUnchanged(initialGenes, "CACHE_ONLY");
                RunSuccessfulStep(original, true, false, "CACHE_RESTORE");
                transitions++;
                AssertGenesUnchanged(initialGenes, "CACHE_RESTORE");

                // Test ON->ON, ON->OFF and OFF->ON with actual generated
                // GeneDefs. The order adapts to the user's original state.
                mayHaveTouchedGenes = true;
                if (!target.biotechCustomizationEnabled)
                {
                    RunSuccessfulStep(MakeOnCandidate(target, -7), false, true, "OFF_TO_ON");
                    transitions++;
                    RunSuccessfulStep(MakeOnCandidate(target, -8), false, true, "ON_TO_ON");
                    transitions++;
                    RunSuccessfulStep(MakeOffCandidate(target), false, true, "ON_TO_OFF");
                    transitions++;
                }
                else
                {
                    RunSuccessfulStep(MakeOnCandidate(target, -7), false, true, "ON_TO_ON");
                    transitions++;
                    RunSuccessfulStep(MakeOffCandidate(target), false, true, "ON_TO_OFF");
                    transitions++;
                    RunSuccessfulStep(MakeOnCandidate(target, -8), false, true, "OFF_TO_ON");
                    transitions++;
                }

                // A real, successfully mutated GeneDef is followed by an
                // injected exception. D3 must put back EXACT prior values,
                // descriptions, cachedDescription and ownership references.
                if (!target.biotechCustomizationEnabled)
                {
                    RunSuccessfulStep(MakeOnCandidate(target, -7), false, true, "PREPARE_ON");
                    transitions++;
                }
                CheckInjectedFailure(MakeOnCandidate(target, -8),
                    SettingsRuntimeApplyCoordinator.LiveDiagnosticFault.AfterBiotech,
                    "BIOTECH_AFTER_REAL_WRITE");
                faults++;

                XpCustomizerSettings curveFail = SettingsSnapshotService.CloneIndependent(target);
                curveFail.careerXpMultiplier = curveFail.careerXpMultiplier < 50f ?
                    curveFail.careerXpMultiplier + 1f : curveFail.careerXpMultiplier - 1f;
                CheckInjectedFailure(curveFail,
                    SettingsRuntimeApplyCoordinator.LiveDiagnosticFault.AfterCache,
                    "CACHE_AFTER_REAL_INVALIDATION");
                faults++;

                CheckInjectedFailure(MakeOnCandidate(target, -9),
                    SettingsRuntimeApplyCoordinator.LiveDiagnosticFault.AfterUi,
                    "UI_AFTER_REAL_REFRESH");
                faults++;
                scenariosPassed = true;
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ":" + ex.Message;
            }
            finally
            {
                // Fail closed even when a scenario or its internal recovery
                // fails. Restore the *original* user data references and
                // the real GeneDef state/ownership captured before all steps.
                bool dataOk = initialSettings == null;
                bool geneOk = initialGenes == null;
                bool cacheOk = true;
                bool uiOk = true;
                string dataReason = "NONE";
                string geneReason = "NONE";
                try
                {
                    if (initialSettings != null && mayHaveWritten)
                        dataOk = SettingsRuntimeRecovery.Restore(target, initialSettings, out dataReason);
                    else dataOk = true;
                }
                catch (Exception ex) { dataOk = false; dataReason = ex.GetType().Name; }
                try
                {
                    if (initialGenes != null && mayHaveTouchedGenes)
                        geneOk = BiotechGeneService.RestoreRollbackSnapshot(initialGenes, out geneReason);
                    else geneOk = true;
                }
                catch (Exception ex) { geneOk = false; geneReason = ex.GetType().Name; }
                if (mayHaveWritten)
                {
                    try { XpCurve.InvalidateCache(); }
                    catch (Exception ex) { cacheOk = false; failure += ";CACHE_FINAL:" + ex.GetType().Name; }
                    try { SettingsRefreshCoordinator.NotifyAll(SettingsRefreshReason.DebugAudit); }
                    catch (Exception ex) { uiOk = false; failure += ";UI_FINAL:" + ex.GetType().Name; }
                }
                try
                {
                    string mismatch;
                    string stateMismatch;
                    bool sameData = initialSettings == null ||
                        (SettingsApplyCoordinator.Equivalent(target, initialSettings.Values, out mismatch) &&
                         ReferencesMatch(target, initialSettings));
                    bool sameGenes = initialGenes == null ||
                        initialGenes.MatchesCurrent(out stateMismatch);
                    finalVerified = dataOk && geneOk && cacheOk && uiOk && sameData && sameGenes;
                }
                catch (Exception ex)
                {
                    finalVerified = false;
                    failure += ";FINAL_VERIFY:" + ex.GetType().Name;
                }
                string status = !mayHaveWritten ? "REJECTED_PREFLIGHT" :
                    (scenariosPassed && finalVerified ? "PASS" :
                    (finalVerified ? "FAIL_RECOVERED" : "UNRECOVERED"));
                if (mayHaveWritten && !finalVerified)
                {
                    // The D4 probe can run several real transactions. Final
                    // verification must latch even if an intermediate step
                    // claimed rollback success. Never clear this latch here.
                    RuntimeRecoveryFailureStage stage = !dataOk ? RuntimeRecoveryFailureStage.Data :
                        !geneOk ? RuntimeRecoveryFailureStage.Biotech :
                        !cacheOk ? RuntimeRecoveryFailureStage.Cache :
                        !uiOk ? RuntimeRecoveryFailureStage.Ui :
                        RuntimeRecoveryFailureStage.Verify;
                    RuntimeRecoverySessionGate.ReportLiveFinalVerificationFailure(stage,
                        RuntimeRecoveryGateSource.LiveDiagnostic);
                }
                string message = "[XPC-R6P0D4][LIVE_ROLLBACK_AUDIT] status=" + status +
                    "; revision=" + Revision + "; transitions=" + transitions +
                    "; injectedFailures=" + faults + "; geneCount=" + originalGeneCount +
                    "; finalSettingsAndReferences=" + (dataOk && finalVerified ? "RESTORED" : "CHECK") +
                    "; finalGeneAndOwnership=" + (geneOk && finalVerified ? "RESTORED" : "CHECK") +
                    "; cache=" + (cacheOk ? "INVALIDATED" : "ERROR") +
                    "; ui=" + (uiOk ? "REFRESHED" : "ERROR") +
                    "; pausedGame=True; entry=DEBUG_TWO_CLICK_ONLY" +
                    "; legacyD4Entry=BLOCKED_BY_DESIGN; productionApply=NOT_EXERCISED_BY_D4_PROBE; explicitScribeCalls=0" +
                    "; emergencyAction=" + (!finalVerified && mayHaveWritten ?
                        "EXIT_WITHOUT_SAVING" : "NONE") +
                    "; reason=" + (scenariosPassed && finalVerified ? "NONE" :
                        failure + ";dataRestore=" + dataReason + ";geneRestore=" + geneReason) +
                    "; build=" + XpCustomizerMod.BuildId;
                if (status == "PASS") Log.Message(message);
                else if (status == "FAIL_RECOVERED" || status == "REJECTED_PREFLIGHT") Log.Warning(message);
                else Log.Error(message);
                running = false;
            }
        }

        private static void RunSuccessfulStep(XpCustomizerSettings candidate,
            bool needsCache, bool needsGene, string label)
        {
            SettingsRuntimeApplyResult result = SettingsRuntimeApplyCoordinator.ApplyLiveForManualDiagnostic(
                SettingsSnapshotService.Capture(candidate),
                SettingsRuntimeApplyCoordinator.LiveDiagnosticFault.None);
            if (result.Status != SettingsRuntimeApplyStatus.APPLY_PASS ||
                result.CacheInvalidated != needsCache ||
                result.BiotechReapplied != needsGene || !result.UiNotified)
                throw new InvalidOperationException("STEP_" + label + ":" + result.Status + ":" + result.Reason);
            int checkedGenes;
            string reason;
            if (!BiotechGeneService.AuditLiveValues(XpCustomizerMod.Settings,
                out checkedGenes, out reason))
                throw new InvalidOperationException("STEP_" + label + ":GENES:" + reason);
            Log.Message("[XPC-R6P0D4][LIVE_STEP] step=" + label +
                "; status=PASS; geneCount=" + checkedGenes +
                "; cache=" + needsCache + "; biotech=" + needsGene +
                "; build=" + XpCustomizerMod.BuildId);
        }

        private static void CheckInjectedFailure(XpCustomizerSettings candidate,
            SettingsRuntimeApplyCoordinator.LiveDiagnosticFault fault, string label)
        {
            XpCustomizerSettings target = XpCustomizerMod.Settings;
            SettingsRuntimeRecovery.Backup before = SettingsRuntimeRecovery.Capture(target);
            BiotechGeneService.RollbackSnapshot geneBefore = BiotechGeneService.CaptureRollbackSnapshot();
            SettingsRuntimeApplyResult result = SettingsRuntimeApplyCoordinator.ApplyLiveForManualDiagnostic(
                SettingsSnapshotService.Capture(candidate), fault);
            string mismatch;
            string geneReason;
            if (result.Status != SettingsRuntimeApplyStatus.RUNTIME_REFRESH_ROLLBACK_PASS ||
                !result.RecoveryVerified ||
                !SettingsApplyCoordinator.Equivalent(target, before.Values, out mismatch) ||
                !ReferencesMatch(target, before) ||
                !geneBefore.MatchesCurrent(out geneReason))
                throw new InvalidOperationException("FAULT_" + label + ":" + result.Status + ":" + result.Reason);
            Log.Message("[XPC-R6P0D4][LIVE_STEP] step=" + label +
                "; status=ROLLBACK_PASS; build=" + XpCustomizerMod.BuildId);
        }

        private static XpCustomizerSettings MakeOnCandidate(XpCustomizerSettings from, int proposedAptitude)
        {
            XpCustomizerSettings s = SettingsSnapshotService.CloneIndependent(from);
            s.biotechCustomizationEnabled = true;
            // Avoid a no-op even for a custom setting already at this value.
            s.biotechAwfulAptitude = s.biotechAwfulAptitude == proposedAptitude ?
                proposedAptitude - 1 : proposedAptitude;
            return s;
        }

        private static XpCustomizerSettings MakeOffCandidate(XpCustomizerSettings from)
        {
            XpCustomizerSettings s = SettingsSnapshotService.CloneIndependent(from);
            s.biotechCustomizationEnabled = false;
            return s;
        }

        private static void AssertGenesUnchanged(BiotechGeneService.RollbackSnapshot backup, string label)
        {
            string reason;
            if (!backup.MatchesCurrent(out reason))
                throw new InvalidOperationException("GENE_TOUCH_WHEN_CACHE_ONLY:" + label + ":" + reason);
        }

        private static bool ReferencesMatch(XpCustomizerSettings live, SettingsRuntimeRecovery.Backup backup)
        {
            for (int i = 0; i < backup.Fields.Length; i++)
            {
                FieldInfo field = backup.Fields[i];
                if (!field.FieldType.IsValueType &&
                    !object.ReferenceEquals(field.GetValue(live), backup.References[i]))
                    return false;
            }
            return true;
        }
    }
}
#endif
