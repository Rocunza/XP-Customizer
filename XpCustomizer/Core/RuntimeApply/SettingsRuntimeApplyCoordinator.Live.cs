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
        private sealed class LiveRefreshActions : ISettingsRuntimeRefreshActions
        {
            private readonly SettingsRefreshReason reason;
            private readonly bool verifyProductionBiotech;
            internal LiveRefreshActions(SettingsRefreshReason value =
                SettingsRefreshReason.FutureProfileApply, bool verifyBiotech = false)
            {
                reason = value;
                verifyProductionBiotech = verifyBiotech;
            }
            public object CaptureBiotechRollbackState()
            {
                return BiotechGeneService.CaptureRollbackSnapshot();
            }
            public bool RestoreBiotechRollbackState(object snapshot, out string reason)
            {
                return BiotechGeneService.RestoreRollbackSnapshot(
                    snapshot as BiotechGeneService.RollbackSnapshot, out reason);
            }
            public void InvalidateCurveCache() { XpCurve.InvalidateCache(); }
            public void ReapplyBiotech()
            {
                BiotechGeneService.ApplySettings();
                // P0: invoked by ApplyCore ONLY for a changed Biotech setting.
                // Keep verification inside ApplyCore's recovery boundary;
                // throwing here restores the exact pre-transition GeneDefs.
                if (!verifyProductionBiotech) return;
                int count;
                string detail;
                if (!BiotechGeneService.AuditLiveValues(XpCustomizerMod.Settings,
                    out count, out detail))
                {
                    Log.Warning("[XPC-P0][BIOTECH_POST_APPLY] status=FAIL; reason=" +
                        detail + "; checked=" + count + "; action=TRANSACTION_ROLLBACK; build=" +
                        XpCustomizerMod.BuildId);
                    throw new InvalidOperationException("PROFILE_BIOTECH_VERIFY_FAILED");
                }
                Log.Message("[XPC-P0][BIOTECH_POST_APPLY] status=PASS; checked=" +
                    count + "; build=" + XpCustomizerMod.BuildId);
            }
            public void NotifyAllEditors()
            {
                SettingsRefreshCoordinator.NotifyAll(reason);
            }
        }

        /// <summary>
        /// Dormant live profile adapter, reserved for later integration.
        /// Must be invoked only from a RimWorld main/UI-thread user command.
        /// It does NOT persist to disk; the legacy route is not connected to UI.
        /// </summary>
        // D2-D3 independent user-confirmed profile route. Deliberately does
        // not open the previously blocked ApplyLiveAfterD4 legacy entry point.
        // The caller is responsible for XML backup, readback and consent.
        // R3-A: an owned settings-UI numerical reset, NOT a profile Apply.
        // Consent and a fully detached candidate are required by the caller.
        // This route does not call ProfileProductionWriteGate, Scribe, or JSON.
        internal static SettingsRuntimeApplyResult ApplyLiveForGlobalNumericDefaults(
            SettingsSnapshot candidate)
        {
            if (candidate == null || Event.current == null ||
                XpCustomizerMod.Settings == null ||
                ProfileProductionWriteGate.IsBusy || ProfileProductionWriteGate.IsUncertain ||
                RuntimeRecoverySessionGate.LiveIsBlocked ||
                XpCustomizer.Profiles.ProfileWriteSessionGate.IsBlocked ||
                (Current.Game != null &&
                    (Find.TickManager == null || !Find.TickManager.Paused)) ||
                (ModsConfig.BiotechActive &&
                    DefDatabase<GeneDef>.AllDefsListForReading == null))
                return Result(SettingsRuntimeApplyStatus.APPLY_REJECTED,
                    "NUMERIC_RESET_LIVE_PREFLIGHT_BLOCKED", false, false, false, true);
            if (!RuntimeRecoverySessionGate.TryEnterLive(RuntimeRecoveryGateSource.LiveTransaction))
                return Result(SettingsRuntimeApplyStatus.APPLY_REJECTED,
                    "RUNTIME_RECOVERY_LOCKED", false, false, false, true);
            try
            {
                SettingsRuntimeApplyResult result = SettingsRuntimeTransactionEngine.ApplyCore(XpCustomizerMod.Settings,
                    candidate, new LiveRefreshActions(SettingsRefreshReason.GlobalModNumericReset),
                    ModsConfig.BiotechActive, true);
                RuntimeRecoverySessionGate.ObserveLiveResult(result,
                    RuntimeRecoveryGateSource.LiveTransaction);
                return result;
            }
            catch (Exception ex)
            {
                RuntimeRecoverySessionGate.ReportLiveFinalVerificationFailure(
                    RuntimeRecoveryFailureStage.Unknown,
                    RuntimeRecoveryGateSource.LiveTransaction);
                return Result(SettingsRuntimeApplyStatus.RUNTIME_REFRESH_FAILED_UNRECOVERED,
                    "NUMERIC_RESET_UNHANDLED:" + ex.GetType().Name,
                    false, false, false, true);
            }
        }

        internal static SettingsRuntimeApplyResult ApplyLiveForProductionProfile(
            SettingsSnapshot candidate)
        {
            if (!ProfileProductionWriteGate.IsBusy ||
                ProfileProductionWriteGate.IsUncertain ||
                Event.current == null || XpCustomizerMod.Settings == null ||
                Current.Game == null || Find.TickManager == null ||
                !Find.TickManager.Paused ||
                (ModsConfig.BiotechActive && DefDatabase<GeneDef>.AllDefsListForReading == null))
                return Result(SettingsRuntimeApplyStatus.APPLY_REJECTED,
                    "PRODUCTION_PROFILE_PAUSED_GAME_AND_OWNED_WRITE_REQUIRED",
                    false, false, false, true);
            if (!RuntimeRecoverySessionGate.TryEnterLive(RuntimeRecoveryGateSource.LiveTransaction))
                return Result(SettingsRuntimeApplyStatus.APPLY_REJECTED,
                    "RUNTIME_RECOVERY_GATE_LOCKED", false, false, false, true);
            try
            {
                SettingsRuntimeApplyResult result = SettingsRuntimeTransactionEngine.ApplyCore(XpCustomizerMod.Settings,
                    candidate, new LiveRefreshActions(
                        SettingsRefreshReason.FutureProfileApply, true),
                    ModsConfig.BiotechActive, true);
                RuntimeRecoverySessionGate.ObserveLiveResult(result,
                    RuntimeRecoveryGateSource.LiveTransaction);
                return result;
            }
            catch (Exception ex)
            {
                RuntimeRecoverySessionGate.ReportLiveFinalVerificationFailure(
                    RuntimeRecoveryFailureStage.Unknown, RuntimeRecoveryGateSource.LiveTransaction);
                return Result(SettingsRuntimeApplyStatus.RUNTIME_REFRESH_FAILED_UNRECOVERED,
                    "PRODUCTION_PROFILE_UNHANDLED:" + ex.GetType().Name,
                    false, false, false, true);
            }
        }

        internal static SettingsRuntimeApplyResult ApplyLiveAfterD4(SettingsSnapshot candidate)
        {
            if (!RuntimeRecoverySessionGate.TryEnterLive(RuntimeRecoveryGateSource.LiveTransaction))
                return Result(SettingsRuntimeApplyStatus.APPLY_REJECTED,
                    "RUNTIME_RECOVERY_LOCKED_RESTART_REQUIRED", false, false, false, true);
            if (!RuntimeRecoveryIntegrated)
                return Result(SettingsRuntimeApplyStatus.APPLY_REJECTED,
                    "LEGACY_D4_APPLY_ENTRY_DISABLED_USE_PRODUCTION_ROUTE", false, false, false, true);
            XpCustomizerSettings target = XpCustomizerMod.Settings;
            if (target == null)
                return Result(SettingsRuntimeApplyStatus.APPLY_REJECTED, "LIVE_SETTINGS_UNAVAILABLE", false, false, false, true);

            bool biotechAvailable = ModsConfig.BiotechActive;
            if (biotechAvailable && DefDatabase<GeneDef>.AllDefsListForReading == null)
                return Result(SettingsRuntimeApplyStatus.APPLY_REJECTED, "GENE_DEFS_NOT_READY", false, false, false, true);

            try
            {
                SettingsRuntimeApplyResult result = SettingsRuntimeTransactionEngine.ApplyCore(target, candidate,
                    new LiveRefreshActions(), biotechAvailable, true);
                RuntimeRecoverySessionGate.ObserveLiveResult(result,
                    RuntimeRecoveryGateSource.LiveTransaction);
                return result;
            }
            catch (Exception ex)
            {
                // An exception outside the coordinator's own recovery boundary
                // leaves the mutation/verification phase uncertain. Fail closed.
                RuntimeRecoverySessionGate.ReportLiveFinalVerificationFailure(
                    RuntimeRecoveryFailureStage.Unknown,
                    RuntimeRecoveryGateSource.LiveTransaction);
                return Result(SettingsRuntimeApplyStatus.RUNTIME_REFRESH_FAILED_UNRECOVERED,
                    "UNHANDLED_RUNTIME_EXCEPTION:" + ex.GetType().Name,
                    false, false, false, true);
            }
        }

    }
}
