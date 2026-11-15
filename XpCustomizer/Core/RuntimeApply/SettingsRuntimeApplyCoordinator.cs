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
    internal enum SettingsRuntimeApplyStatus
    {
        APPLY_PASS,
        APPLY_NO_CHANGE,
        APPLY_REJECTED,
        RUNTIME_REFRESH_ROLLBACK_PASS,
        RUNTIME_REFRESH_FAILED_UNRECOVERED
    }

    internal sealed class SettingsRuntimeApplyResult
    {
        internal readonly SettingsRuntimeApplyStatus Status;
        internal readonly string Reason;
        internal readonly bool CacheInvalidated;
        internal readonly bool BiotechReapplied;
        internal readonly bool UiNotified;
        internal readonly bool RecoveryVerified;

        internal SettingsRuntimeApplyResult(SettingsRuntimeApplyStatus status, string reason,
            bool cacheInvalidated, bool biotechReapplied, bool uiNotified)
        {
            Status = status;
            Reason = reason;
            CacheInvalidated = cacheInvalidated;
            BiotechReapplied = biotechReapplied;
            UiNotified = uiNotified;
            RecoveryVerified = status == SettingsRuntimeApplyStatus.RUNTIME_REFRESH_ROLLBACK_PASS;
        }
    }

    /// <summary>
    /// R6-P0-D3: post-transaction derived state and recovery boundary.
    /// External snapshots are captured BEFORE any settings write. Detached
    /// tests use fake actions. The old ApplyLiveAfterD4 entry is intentionally blocked; the independent
    /// production profile route uses ApplyLiveForProductionProfile. The D2-D1A
    /// runtime-only DEBUG diagnostic remains separately gated.
    /// </summary>
    internal interface ISettingsRuntimeRefreshActions
    {
        object CaptureBiotechRollbackState();
        bool RestoreBiotechRollbackState(object snapshot, out string reason);
        void InvalidateCurveCache();
        void ReapplyBiotech();
        void NotifyAllEditors();
    }

    internal static partial class SettingsRuntimeApplyCoordinator
    {
        internal const string Revision = "R6P0D3-RUNTIME-ROLLBACK-20261106";
        // Legacy D4-only adapter is intentionally closed; production profile
        // loading uses the independent ApplyLiveForProductionProfile path.
        // Never open this flag as a shortcut around production consent gates.
        private static bool RuntimeRecoveryIntegrated { get { return false; } }
        internal static bool LiveApplyStillBlockedForAudit { get { return !RuntimeRecoveryIntegrated; } }

        internal static bool CareerCurveChanged(XpCustomizerSettings before, XpCustomizerSettings after)
        {
            return SettingsRuntimeChangeDetector.CareerCurveChanged(before, after);
        }

        internal static bool BiotechChanged(XpCustomizerSettings before, XpCustomizerSettings after)
        {
            return SettingsRuntimeChangeDetector.BiotechChanged(before, after);
        }

        internal static SettingsRuntimeApplyResult Result(SettingsRuntimeApplyStatus status,
            string reason, bool cache, bool biotech, bool ui, bool log)
        {
            SettingsRuntimeApplyResult result = new SettingsRuntimeApplyResult(status, reason, cache, biotech, ui);
            if (log)
            {
#if DEBUG
                bool debugSessionWriteBlocked =
                    ProfileDebugLiveApplySessionGuard.LiveWriteBlocked;
#else
                const bool debugSessionWriteBlocked = false;
#endif
                string message = "[XPC-R6P0D3][RUNTIME_APPLY] status=" + status +
                    "; reason=" + reason + "; curveCacheInvalidated=" + cache +
                    "; biotechReapplied=" + biotech + "; uiNotified=" + ui +
                    "; scribeCallsByRuntimeCoordinator=0; debugSessionWriteBlocked=" +
                    debugSessionWriteBlocked +
                    "; productionAvailability=NOT_EVALUATED_BY_RUNTIME_RESULT; legacyD4Entry=DISABLED_SEPARATE_ROUTE" +
                    "; build=" + XpCustomizerMod.BuildId;
                if (status == SettingsRuntimeApplyStatus.RUNTIME_REFRESH_FAILED_UNRECOVERED)
                    Log.Error(message);
                else if (status == SettingsRuntimeApplyStatus.APPLY_REJECTED ||
                         status == SettingsRuntimeApplyStatus.RUNTIME_REFRESH_ROLLBACK_PASS)
                    Log.Warning(message);
                else
                    Log.Message(message);
            }
            return result;
        }
    }
}
