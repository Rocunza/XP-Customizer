#if DEBUG
using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.Biotech;
using XpCustomizer.Progression;
using XpCustomizer.UI;

namespace XpCustomizer
{
    internal static partial class SettingsRuntimeApplyCoordinator
    {
        // D4: the only temporary real-world bridge. Not a profile API.
        // Called synchronously from the two-click Debug settings UI while
        // the game is paused. Release builds do not contain this method.
        internal enum LiveDiagnosticFault
        {
            None,
            AfterCache,
            AfterBiotech,
            AfterUi
        }

        private sealed class LiveDiagnosticActions : ISettingsRuntimeRefreshActions
        {
            private readonly LiveDiagnosticFault fault;
            private bool injected;
            internal LiveDiagnosticActions(LiveDiagnosticFault faultPoint) { fault = faultPoint; }
            public object CaptureBiotechRollbackState()
            {
                return BiotechGeneService.CaptureRollbackSnapshot();
            }
            public bool RestoreBiotechRollbackState(object snapshot, out string reason)
            {
                return BiotechGeneService.RestoreRollbackSnapshot(
                    snapshot as BiotechGeneService.RollbackSnapshot, out reason);
            }
            public void InvalidateCurveCache()
            {
                XpCurve.InvalidateCache();
                if (fault == LiveDiagnosticFault.AfterCache && !injected)
                {
                    injected = true;
                    throw new InvalidOperationException("D4_AFTER_REAL_CACHE_INJECTED");
                }
            }
            public void ReapplyBiotech()
            {
                BiotechGeneService.ApplySettings();
                if (fault == LiveDiagnosticFault.AfterBiotech && !injected)
                {
                    injected = true;
                    throw new InvalidOperationException("D4_AFTER_REAL_BIOTECH_INJECTED");
                }
            }
            public void NotifyAllEditors()
            {
                SettingsRefreshCoordinator.NotifyAll(SettingsRefreshReason.DebugAudit);
                if (fault == LiveDiagnosticFault.AfterUi && !injected)
                {
                    injected = true;
                    throw new InvalidOperationException("D4_AFTER_REAL_UI_INJECTED");
                }
            }
        }

        internal static SettingsRuntimeApplyResult ApplyLiveForManualDiagnostic(
            SettingsSnapshot snapshot, LiveDiagnosticFault fault)
        {
            if (!RuntimeRecoverySessionGate.TryEnterLive(RuntimeRecoveryGateSource.LiveDiagnostic))
                return Result(SettingsRuntimeApplyStatus.APPLY_REJECTED,
                    "RUNTIME_RECOVERY_LOCKED_RESTART_REQUIRED", false, false, false, false);
            // Fail closed. The entry point is reached only by the Debug UI,
            // but the preconditions are also checked here independently.
            if (Event.current == null || Current.Game == null || Find.TickManager == null ||
                !Find.TickManager.Paused || !ModsConfig.BiotechActive ||
                XpCustomizerMod.Settings == null ||
                DefDatabase<GeneDef>.AllDefsListForReading == null)
                return Result(SettingsRuntimeApplyStatus.APPLY_REJECTED,
                    "D4_LIVE_DIAGNOSTIC_PREFLIGHT_FAILED", false, false, false, false);
            try
            {
                SettingsRuntimeApplyResult result = SettingsRuntimeTransactionEngine.ApplyCore(XpCustomizerMod.Settings, snapshot,
                    new LiveDiagnosticActions(fault), true, false);
                RuntimeRecoverySessionGate.ObserveLiveResult(result,
                    RuntimeRecoveryGateSource.LiveDiagnostic);
                return result;
            }
            catch (Exception ex)
            {
                RuntimeRecoverySessionGate.ReportLiveFinalVerificationFailure(
                    RuntimeRecoveryFailureStage.Unknown,
                    RuntimeRecoveryGateSource.LiveDiagnostic);
                return Result(SettingsRuntimeApplyStatus.RUNTIME_REFRESH_FAILED_UNRECOVERED,
                    "UNHANDLED_LIVE_DIAGNOSTIC:" + ex.GetType().Name,
                    false, false, false, false);
            }
        }

        // D2-D1A: single-use, Debug-only REAL profile runtime transaction.
        // The separate profile service must arm the session-wide XP XML write
        // latch BEFORE entering here, and this method consumes its one permit.
        // Does not alter the Release entry point or invoke WriteSettings.
        internal static SettingsRuntimeApplyResult ApplyLiveForDebugProfile(
            SettingsSnapshot candidate)
        {
            if (!ProfileDebugLiveApplySessionGuard.ConsumeLivePermit())
                return Result(SettingsRuntimeApplyStatus.APPLY_REJECTED,
                    "DEBUG_PROFILE_CONSENT_NOT_ARMED", false, false, false, true);
            if (!XpCustomizerSelfTests.DebugStartupCompleteAndPassed ||
                Event.current == null || Current.Game == null || Find.TickManager == null ||
                !Find.TickManager.Paused || !ProfileDebugLiveApplySessionGuard.LiveWriteBlocked ||
                XpCustomizerMod.Settings == null ||
                (ModsConfig.BiotechActive && DefDatabase<GeneDef>.AllDefsListForReading == null))
                return Result(SettingsRuntimeApplyStatus.APPLY_REJECTED,
                    "DEBUG_PROFILE_RUNTIME_PREFLIGHT_FAILED", false, false, false, true);
            if (!RuntimeRecoverySessionGate.TryEnterLive(RuntimeRecoveryGateSource.LiveTransaction))
                return Result(SettingsRuntimeApplyStatus.APPLY_REJECTED,
                    "RUNTIME_RECOVERY_LOCKED_RESTART_REQUIRED", false, false, false, true);
            try
            {
                SettingsRuntimeApplyResult applied = SettingsRuntimeTransactionEngine.ApplyCore(XpCustomizerMod.Settings,
                    candidate, new LiveRefreshActions(), ModsConfig.BiotechActive, true);
                RuntimeRecoverySessionGate.ObserveLiveResult(applied,
                    RuntimeRecoveryGateSource.LiveTransaction);
                return applied;
            }
            catch (Exception ex)
            {
                RuntimeRecoverySessionGate.ReportLiveFinalVerificationFailure(
                    RuntimeRecoveryFailureStage.Unknown, RuntimeRecoveryGateSource.LiveTransaction);
                return Result(SettingsRuntimeApplyStatus.RUNTIME_REFRESH_FAILED_UNRECOVERED,
                    "DEBUG_PROFILE_UNHANDLED:" + ex.GetType().Name,
                    false, false, false, true);
            }
        }

        internal static SettingsRuntimeApplyResult ApplyDetachedForAudit(
            XpCustomizerSettings detachedTarget, SettingsSnapshot candidate,
            ISettingsRuntimeRefreshActions actions, bool biotechAvailable)
        {
            // Debug fake adapters may never target the real ModSettings root.
            if (object.ReferenceEquals(detachedTarget, XpCustomizerMod.Settings))
                return Result(SettingsRuntimeApplyStatus.APPLY_REJECTED,
                    "DETACHED_TARGET_IS_LIVE_SETTINGS", false, false, false, false);
            return SettingsRuntimeTransactionEngine.ApplyCore(detachedTarget, candidate, actions, biotechAvailable, false);
        }

        // B1: DATA fault injection is available solely for isolated Debug tests.
        // Live adapters and Release always use the original non-injected path.
        internal static SettingsRuntimeApplyResult ApplyDetachedForAuditWithDataFault(
            XpCustomizerSettings detachedTarget, SettingsSnapshot candidate,
            ISettingsRuntimeRefreshActions actions, bool biotechAvailable, int failAfterDataFields)
        {
            if (object.ReferenceEquals(detachedTarget, XpCustomizerMod.Settings))
                return Result(SettingsRuntimeApplyStatus.APPLY_REJECTED,
                    "DETACHED_TARGET_IS_LIVE_SETTINGS", false, false, false, false);
            return SettingsRuntimeTransactionEngine.ApplyCore(detachedTarget, candidate, actions, biotechAvailable,
                false, failAfterDataFields);
        }
    }
}
#endif
