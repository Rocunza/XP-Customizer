using System;
using Verse;

namespace XpCustomizer
{
    // B2-A: live runtime recovery state belongs to this process only.
    // It is separate from the JSON file recovery gate and from detached audits.
    internal enum RuntimeRecoveryGateSource
    {
        LiveTransaction,
        LiveDiagnostic
    }

    internal enum RuntimeRecoveryFailureStage
    {
        None,
        Data,
        Cache,
        Biotech,
        Ui,
        Verify,
        Unknown
    }

    /// <summary>
    /// R6-P1-D2-B2-A: sticky fail-closed interlock for LIVE runtime work.
    /// The production gate cannot be reset. Only detached DEBUG test instances
    /// can be created separately; fake rollback results never touch Live.
    /// This is not a permission to apply any profile.
    /// </summary>
    internal sealed class RuntimeRecoverySessionGate
    {
        internal const string Revision = "R6P1D2B2A-LIVE-SESSION-LATCH-20261107";
        private static readonly RuntimeRecoverySessionGate Live =
            new RuntimeRecoverySessionGate(true);

        private readonly bool isLive;
        private bool recoveryRequired;
        private RuntimeRecoveryFailureStage firstStage;
        private RuntimeRecoveryGateSource firstSource;
        private string firstVerification;

        private RuntimeRecoverySessionGate(bool live)
        {
            isLive = live;
            firstStage = RuntimeRecoveryFailureStage.None;
            firstVerification = "NONE";
        }

        internal static bool LiveIsBlocked { get { return Live.recoveryRequired; } }

        // D2-B2-B: RimWorld closes Dialog_ModSettings by invoking Mod.WriteSettings.
        // When live rollback cannot be verified, never serialize a potentially
        // partially updated XP Customizer settings object to the user's XML.
        // This affects our mod only; it does not alter vanilla/other mod saves.
        internal static bool LiveModSettingsIoBlocked { get { return Live.recoveryRequired; } }

        internal static bool TryEnterLive(RuntimeRecoveryGateSource source)
        {
            return Live.TryEnter(source);
        }

        internal static void ObserveLiveResult(SettingsRuntimeApplyResult result,
            RuntimeRecoveryGateSource source)
        {
            Live.ObserveResult(result, source);
        }

        // For an opt-in D4 probe that performs more than one live transaction:
        // even if individual stages reported rollback PASS, final cross-stage
        // verification may reveal an un-restored reference or GeneDef value.
        internal static void ReportLiveFinalVerificationFailure(
            RuntimeRecoveryFailureStage stage, RuntimeRecoveryGateSource source)
        {
            Live.Latch(stage, source, "FAILED");
        }

        private bool TryEnter(RuntimeRecoveryGateSource source)
        {
            if (!recoveryRequired) return true;
            Report("REJECT", source, firstStage, firstVerification);
            return false;
        }

        private void ObserveResult(SettingsRuntimeApplyResult result,
            RuntimeRecoveryGateSource source)
        {
            // A null outcome leaves write progress unknown: never retry blindly.
            if (result == null)
            {
                Latch(RuntimeRecoveryFailureStage.Unknown, source, "UNKNOWN");
                return;
            }

            if (result.Status == SettingsRuntimeApplyStatus.RUNTIME_REFRESH_FAILED_UNRECOVERED)
            {
                Latch(StageFromReason(result.Reason), source, "FAILED");
                return;
            }

            // A reported rollback without verifiable recovery is not success.
            if (result.Status == SettingsRuntimeApplyStatus.RUNTIME_REFRESH_ROLLBACK_PASS &&
                !result.RecoveryVerified)
            {
                Latch(RuntimeRecoveryFailureStage.Verify, source, "UNKNOWN");
                return;
            }

            // Preflight rejection, no change, successful apply, and a verified
            // rollback are non-latching outcomes. These do NOT grant permission
            // to use the separate legacy D4 diagnostic entry point.
            if (result.Status != SettingsRuntimeApplyStatus.APPLY_PASS &&
                result.Status != SettingsRuntimeApplyStatus.APPLY_NO_CHANGE &&
                result.Status != SettingsRuntimeApplyStatus.APPLY_REJECTED &&
                result.Status != SettingsRuntimeApplyStatus.RUNTIME_REFRESH_ROLLBACK_PASS)
                Latch(RuntimeRecoveryFailureStage.Unknown, source, "UNKNOWN");
            else
                Report("OBSERVE", source, RuntimeRecoveryFailureStage.None,
                    result.RecoveryVerified ? "VERIFIED" : "NOT_VERIFIED");
        }

        private void Latch(RuntimeRecoveryFailureStage stage,
            RuntimeRecoveryGateSource source, string verification)
        {
            if (recoveryRequired) return; // First failure wins; no silent clear.
            recoveryRequired = true;
            firstStage = stage == RuntimeRecoveryFailureStage.None ?
                RuntimeRecoveryFailureStage.Unknown : stage;
            firstSource = source;
            firstVerification = verification;
            Report("LATCH", source, firstStage, firstVerification);
        }

        private void Report(string action, RuntimeRecoveryGateSource source,
            RuntimeRecoveryFailureStage stage, string verification)
        {
            if (!isLive) return; // Never log a fake case as live evidence.
            string message = "[XPC-R6P1D2B2A][RUNTIME_RECOVERY_LOCK] action=" + action +
                "; state=" + (recoveryRequired ? "RecoveryRequired" : "Healthy") +
                "; reason=" + (recoveryRequired ? "RECOVERY_NOT_VERIFIED" : "NONE") +
                "; failedStage=" + stage +
                "; verification=" + verification +
                "; source=" + (source == RuntimeRecoveryGateSource.LiveTransaction ?
                    "LIVE_TRANSACTION" : "LIVE_DIAGNOSTIC") +
                "; originalSource=" + (!recoveryRequired ? "NONE" :
                    firstSource == RuntimeRecoveryGateSource.LiveTransaction ?
                    "LIVE_TRANSACTION" : "LIVE_DIAGNOSTIC") +
                "; recoveryLatchBlocksApply=" + recoveryRequired +
                "; liveProfileApply=" + ExplainProfileApplyLatch(recoveryRequired) +
                "; blockingScope=" + (recoveryRequired ? "LIVE_RECOVERY_SESSION" : "NONE") +
                "; actionRequired=" +
                (recoveryRequired ? "EXIT_WITHOUT_SAVING_RESTART" : "NONE") +
                "; build=" + XpCustomizerMod.BuildId;
            if (action == "LATCH") Log.Error(message);
            else if (action == "REJECT") Log.Warning(message);
            else Log.Message(message);
        }

        // A Healthy session does not veto production Apply. This describes
        // ONLY the recovery latch; other preflight gates still apply.
        internal static string ExplainProfileApplyLatch(bool blocked)
        {
            return blocked ? "BLOCKED_BY_ACTIVE_RECOVERY_LATCH" :
                "NOT_BLOCKED_BY_RECOVERY_LATCH";
        }

        private static RuntimeRecoveryFailureStage StageFromReason(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return RuntimeRecoveryFailureStage.Unknown;
            if (reason.StartsWith("DATA", StringComparison.Ordinal)) return RuntimeRecoveryFailureStage.Data;
            if (reason.StartsWith("CACHE", StringComparison.Ordinal)) return RuntimeRecoveryFailureStage.Cache;
            if (reason.StartsWith("BIOTECH", StringComparison.Ordinal)) return RuntimeRecoveryFailureStage.Biotech;
            if (reason.StartsWith("UI", StringComparison.Ordinal)) return RuntimeRecoveryFailureStage.Ui;
            if (reason.StartsWith("VERIFY", StringComparison.Ordinal)) return RuntimeRecoveryFailureStage.Verify;
            return RuntimeRecoveryFailureStage.Unknown;
        }

#if DEBUG
        // The only test constructor. No production reset method exists.
        internal static RuntimeRecoverySessionGate CreateIsolatedForAudit()
        {
            return new RuntimeRecoverySessionGate(false);
        }

        internal bool TryEnterForIsolatedAudit(RuntimeRecoveryGateSource source)
        {
            if (isLive) throw new InvalidOperationException("ISOLATED_AUDIT_ON_LIVE_GATE");
            return TryEnter(source);
        }

        internal void ObserveForIsolatedAudit(SettingsRuntimeApplyResult result,
            RuntimeRecoveryGateSource source)
        {
            if (isLive) throw new InvalidOperationException("ISOLATED_AUDIT_ON_LIVE_GATE");
            ObserveResult(result, source);
        }

        internal void ReportVerificationFailureForIsolatedAudit(
            RuntimeRecoveryFailureStage stage, RuntimeRecoveryGateSource source)
        {
            if (isLive) throw new InvalidOperationException("ISOLATED_AUDIT_ON_LIVE_GATE");
            Latch(stage, source, "FAILED");
        }

        internal bool IsBlockedForIsolatedAudit { get { return recoveryRequired; } }
        internal bool ModSettingsIoBlockedForIsolatedAudit
        {
            get
            {
                if (isLive) throw new InvalidOperationException("ISOLATED_AUDIT_ON_LIVE_GATE");
                return recoveryRequired;
            }
        }
        internal RuntimeRecoveryFailureStage FirstStageForIsolatedAudit { get { return firstStage; } }
        internal RuntimeRecoveryGateSource FirstSourceForIsolatedAudit { get { return firstSource; } }
        internal string FirstVerificationForIsolatedAudit { get { return firstVerification; } }
#endif
    }
}
