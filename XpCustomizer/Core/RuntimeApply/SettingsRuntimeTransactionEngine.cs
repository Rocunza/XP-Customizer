using System;

namespace XpCustomizer
{
    // One synchronous transaction boundary: original data/apply/compensation order preserved.
    internal static class SettingsRuntimeTransactionEngine
    {
        private static bool applying;

        internal static SettingsRuntimeApplyResult ApplyCore(XpCustomizerSettings target,
            SettingsSnapshot snapshot, ISettingsRuntimeRefreshActions actions,
            bool biotechAvailable, bool emitLog, int failAfterDataFields = -1)
        {
            if (applying)
                return SettingsRuntimeApplyCoordinator.Result(SettingsRuntimeApplyStatus.APPLY_REJECTED, "REENTRANT_APPLY", false, false, false, emitLog);
            if (target == null || snapshot == null || actions == null)
                return SettingsRuntimeApplyCoordinator.Result(SettingsRuntimeApplyStatus.APPLY_REJECTED, "MISSING_ARGUMENT", false, false, false, emitLog);

            applying = true;
            try
            {
                XpCustomizerSettings candidate;
                XpCustomizerSettings previous;
                SettingsSnapshot frozen;
                SettingsRuntimeRecovery.Backup backup;
                bool equivalent;
                bool curveChanged;
                bool biotechChanged;
                object biotechBackup = null;
                try
                {
                    candidate = snapshot.CreateDetachedCopy();
                    SettingsCandidateValidator.Result validation = SettingsCandidateValidator.Validate(candidate);
                    if (!validation.Accepted)
                        return SettingsRuntimeApplyCoordinator.Result(SettingsRuntimeApplyStatus.APPLY_REJECTED,
                            "CANDIDATE:" + validation.Reason, false, false, false, emitLog);
                    previous = SettingsSnapshotService.CloneIndependent(target);
                    string mismatch;
                    equivalent = SettingsApplyCoordinator.Equivalent(previous, candidate, out mismatch);
                    curveChanged = SettingsRuntimeChangeDetector.CareerCurveChanged(previous, candidate);
                    biotechChanged = biotechAvailable && SettingsRuntimeChangeDetector.BiotechChanged(previous, candidate);
                    if (equivalent)
                        return SettingsRuntimeApplyCoordinator.Result(SettingsRuntimeApplyStatus.APPLY_NO_CHANGE, "NO_CHANGE", false, false, false, emitLog);
                    frozen = SettingsSnapshotService.Capture(candidate);
                    backup = SettingsRuntimeRecovery.Capture(target);
                    // Read-only capture, before data apply. Includes the live
                    // GeneDef values AND our baseline/ownership bookkeeping.
                    if (biotechChanged)
                    {
                        biotechBackup = actions.CaptureBiotechRollbackState();
                        if (biotechBackup == null)
                            return SettingsRuntimeApplyCoordinator.Result(SettingsRuntimeApplyStatus.APPLY_REJECTED,
                                "BIOTECH_SNAPSHOT_UNAVAILABLE", false, false, false, emitLog);
                    }
                }
                catch (Exception ex)
                {
                    return SettingsRuntimeApplyCoordinator.Result(SettingsRuntimeApplyStatus.APPLY_REJECTED,
                        "PREFLIGHT:" + ex.GetType().Name, false, false, false, emitLog);
                }

                bool cacheDone = false;
                bool biotechDone = false;
                bool biotechAttempted = false;
                bool uiDone = false;
                string stage = "DATA";
                try
                {
                    SettingsApplyResult data;
#if DEBUG
                    data = emitLog ? SettingsApplyCoordinator.Apply(target, frozen) :
                        SettingsApplyCoordinator.ApplyForTesting(target, frozen, failAfterDataFields);
#else
                    data = SettingsApplyCoordinator.Apply(target, frozen);
#endif
                    if (data.Status != SettingsApplyStatus.APPLY_PASS)
                    {
                        if (data.Status == SettingsApplyStatus.APPLY_ROLLBACK_FAILED)
                        {
                            string dataReason;
                            bool restored = SettingsRuntimeRecovery.Restore(target, backup, out dataReason);
                            return SettingsRuntimeApplyCoordinator.Result(restored ? SettingsRuntimeApplyStatus.RUNTIME_REFRESH_ROLLBACK_PASS :
                                SettingsRuntimeApplyStatus.RUNTIME_REFRESH_FAILED_UNRECOVERED,
                                "DATA_" + data.Status + ":" + data.Reason + "; recovery=" + dataReason,
                                false, false, false, emitLog);
                        }
                        return SettingsRuntimeApplyCoordinator.Result(SettingsRuntimeApplyStatus.APPLY_REJECTED,
                            "DATA_" + data.Status + ":" + data.Reason,
                            false, false, false, emitLog);
                    }

                    stage = "CACHE";
                    if (curveChanged)
                    {
                        actions.InvalidateCurveCache();
                        cacheDone = true;
                    }
                    stage = "BIOTECH";
                    if (biotechChanged)
                    {
                        biotechAttempted = true;
                        actions.ReapplyBiotech();
                        biotechDone = true;
                    }
                    stage = "UI";
                    actions.NotifyAllEditors();
                    uiDone = true;
                    return SettingsRuntimeApplyCoordinator.Result(SettingsRuntimeApplyStatus.APPLY_PASS, "PASS",
                        cacheDone, biotechDone, uiDone, emitLog);
                }
                catch (Exception ex)
                {
                    // Any stage may throw AFTER partially mutating state.
                    // Do not retry ApplySettings against old settings: that
                    // can corrupt the captured OFF->ON baseline. Instead
                    // restore the exact pre-transition GeneDefs + ownership.
                    string recovery;
                    bool restored = Recover(target, backup, actions, curveChanged,
                        biotechAttempted, biotechBackup, stage, out recovery);
                    return SettingsRuntimeApplyCoordinator.Result(restored ? SettingsRuntimeApplyStatus.RUNTIME_REFRESH_ROLLBACK_PASS :
                        SettingsRuntimeApplyStatus.RUNTIME_REFRESH_FAILED_UNRECOVERED,
                        stage + ":" + ex.GetType().Name + "; recovery=" + recovery,
                        cacheDone, biotechDone, uiDone, emitLog);
                }
            }
            finally
            {
                applying = false;
            }
        }

        private static bool Recover(XpCustomizerSettings target,
            SettingsRuntimeRecovery.Backup backup, ISettingsRuntimeRefreshActions actions,
            bool curveChanged, bool biotechAttempted, object biotechBackup,
            string failedStage, out string detail)
        {
            bool dataOk = false;
            bool geneOk = !biotechAttempted;
            bool cacheOk = !curveChanged;
            bool uiOk = false;
            string dataReason = "NONE";
            string geneReason = "NONE";
            string cacheReason = "NONE";
            string uiReason = "NONE";

            try { dataOk = SettingsRuntimeRecovery.Restore(target, backup, out dataReason); }
            catch (Exception ex) { dataReason = "THREW:" + ex.GetType().Name; }

            // On a CACHE failure Biotech was not invoked at all. Do not
            // rewrite any GeneDefs (or dirty pawns) in that case.
            if (biotechAttempted)
            {
                try
                {
                    geneOk = biotechBackup != null &&
                        actions.RestoreBiotechRollbackState(biotechBackup, out geneReason);
                    if (biotechBackup == null) geneReason = "NO_SNAPSHOT";
                }
                catch (Exception ex) { geneReason = "THREW:" + ex.GetType().Name; }
            }

            // Cache is derived, not a stored state. Invalidate again *after*
            // restoring the previous fields to force a fresh computation.
            if (curveChanged)
            {
                try { actions.InvalidateCurveCache(); cacheOk = true; }
                catch (Exception ex) { cacheReason = "THREW:" + ex.GetType().Name; }
            }
            // Clear partially updated UI input buffers even if another
            // recovery branch failed, so stale values cannot overwrite data.
            try { actions.NotifyAllEditors(); uiOk = true; }
            catch (Exception ex) { uiReason = "THREW:" + ex.GetType().Name; }

            detail = "data=" + (dataOk ? "PASS" : "FAIL:" + dataReason) +
                ",biotech=" + (!biotechAttempted ? "NOT_TOUCHED" :
                    (geneOk ? "PASS" : "FAIL:" + geneReason)) +
                ",cache=" + (cacheOk ? "PASS" : "FAIL:" + cacheReason) +
                ",ui=" + (uiOk ? "PASS" : "FAIL:" + uiReason) +
                ",failedStage=" + failedStage;
            return dataOk && geneOk && cacheOk && uiOk;
        }

    }
}
