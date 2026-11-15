#if DEBUG
using System;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.Biotech;

namespace XpCustomizer.Profiles
{
    internal enum ProfileDebugLiveApplyCode
    {
        Prepared,
        NoChange,
        PrecheckRejected,
        Conflict,
        Duplicate,
        RuntimeAppliedVerified,
        RollbackVerified,
        Unrecovered
    }

    internal sealed class ProfileDebugLiveApplyResult
    {
        internal readonly ProfileDebugLiveApplyCode Code;
        internal readonly string Reason;
        internal readonly ProfileApplyBridgeSession Session;
        internal bool Prepared { get { return Code == ProfileDebugLiveApplyCode.Prepared && Session != null; } }

        internal ProfileDebugLiveApplyResult(ProfileDebugLiveApplyCode code,
            string reason, ProfileApplyBridgeSession session)
        { Code = code; Reason = reason ?? "NONE"; Session = session; }
    }

    /// <summary>
    /// A separate Debug-only, opt-in REAL runtime bridge. The D1 dry-run,
    /// D2-A1 read-only gate, D2-B1 fake bridge, and Release paths remain unchanged.
    /// No Scribe calls or disk writes; the session-wide XP-owned XML guard
    /// intentionally stays latched after beginning any real transition.
    /// </summary>
    internal sealed class ProfileDebugLiveApplyService
    {
        internal const string Revision = "R6P1D2D1AF1-DEBUG-LIVE-REFEQ-FIX-20261108";
        private static bool executing;
        private readonly ProfileApplyGateService gate;
        private readonly ProfileFileRepository repository;
        private ProfileApplyBridgeSession loggingSession;

        internal ProfileDebugLiveApplyService()
        {
            gate = new ProfileApplyGateService(ProfileApplyPreflightService.ForCurrentUser(), false);
            repository = ProfileFileRepository.ForCurrentUser();
        }

        internal ProfileDebugLiveApplyResult Prepare(ProfileApplyReviewSession original,
            XpCustomizerSettings live)
        {
            if (!XpCustomizerSelfTests.DebugStartupCompleteAndPassed ||
                !CanRunNow() || ProfileDebugLiveApplySessionGuard.LiveWriteBlocked ||
                ProfileWriteSessionGate.IsBlocked || RuntimeRecoverySessionGate.LiveIsBlocked)
                return Report("PREPARE", ProfileDebugLiveApplyCode.PrecheckRejected,
                    "DEBUG_PAUSED_GAME_OR_RECOVERY_GATE_REQUIRED", null, "NONE");
            if (original == null || !original.ContentDiffers || live == null)
                return Report("PREPARE", ProfileDebugLiveApplyCode.NoChange,
                    "NO_CHANGED_REVIEW", null, "NONE");
            try
            {
                string mismatch;
                if (!original.BaselineMatches(live, out mismatch))
                    return Report("PREPARE", ProfileDebugLiveApplyCode.Conflict,
                        "LIVE_BASELINE_CHANGED", null, mismatch);
                ProfileStorageResult loaded = repository.Load(original.ProfileId);
                if (loaded == null || !loaded.Success || loaded.Document == null ||
                    !String.Equals(loaded.ContentHash, original.ExpectedHash, StringComparison.Ordinal))
                    return Report("PREPARE", ProfileDebugLiveApplyCode.Conflict,
                        "PROFILE_SHA_CHANGED", null, "NONE");
                ProfileDocument doc = loaded.Document;
                if (!String.Equals(doc.DisplayName, original.DisplayName, StringComparison.Ordinal) ||
                    doc.UpdatedUtc != original.UpdatedUtc ||
                    !String.Equals(doc.SourceGameVersion, original.SourceGameVersion, StringComparison.Ordinal))
                    return Report("PREPARE", ProfileDebugLiveApplyCode.Conflict,
                        "PROFILE_METADATA_CHANGED", null, "NONE");
                ProfileApplyGateResult checkedGate = gate.Prepare(doc, loaded.ContentHash, live);
                if (!checkedGate.Prepared)
                    return Report("PREPARE", checkedGate.Code == ProfileApplyGateCode.NoChange ?
                        ProfileDebugLiveApplyCode.NoChange : ProfileDebugLiveApplyCode.PrecheckRejected,
                        "READONLY_GATE:" + checkedGate.Reason, null, "NONE");
                ProfileApplyBridgeSession session = new ProfileApplyBridgeSession(
                    checkedGate.Session, doc, loaded.ContentHash, live);
                if (!session.BaselineMatches(live, out mismatch))
                    return Report("PREPARE", ProfileDebugLiveApplyCode.Conflict,
                        "BASELINE_CHANGED_AFTER_PREPARE", null, mismatch);
                return Report("PREPARE", ProfileDebugLiveApplyCode.Prepared,
                    "SECOND_EXPLICIT_CLICK_REQUIRED", session, "NONE");
            }
            catch (ProfileContractException ex)
            {
                return Report("PREPARE", ProfileDebugLiveApplyCode.PrecheckRejected,
                    "PROFILE:" + ex.Code, null, "NONE");
            }
            catch (Exception ex)
            {
                return Report("PREPARE", ProfileDebugLiveApplyCode.PrecheckRejected,
                    "EXCEPTION:" + ex.GetType().Name, null, "NONE");
            }
        }

        internal ProfileDebugLiveApplyResult ConfirmAndApply(ProfileApplyBridgeSession session,
            XpCustomizerSettings live)
        {
            if (session == null)
                return Report("CONFIRM", ProfileDebugLiveApplyCode.PrecheckRejected,
                    "NULL_SESSION", null, "NONE");
            // One-shot even on rejection. A prior D1 dry-run cannot authorize a write.
            if (!session.Consume())
                return Report("CONFIRM", ProfileDebugLiveApplyCode.Duplicate,
                    "SESSION_CONSUMED", null, "NONE");
            if (executing)
                return Report("CONFIRM", ProfileDebugLiveApplyCode.PrecheckRejected,
                    "REENTRANT", null, "NONE");
            executing = true;
            loggingSession = session;
            bool mutationMayHaveStarted = false;
            try
            {
                if (!XpCustomizerSelfTests.DebugStartupCompleteAndPassed ||
                    !CanRunNow() || live == null ||
                    !System.Object.ReferenceEquals(live, XpCustomizerMod.Settings) ||
                    ProfileDebugLiveApplySessionGuard.LiveWriteBlocked ||
                    ProfileWriteSessionGate.IsBlocked || RuntimeRecoverySessionGate.LiveIsBlocked)
                    return Report("RECHECK", ProfileDebugLiveApplyCode.PrecheckRejected,
                        "DEBUG_RUNTIME_OR_RECOVERY_GATE", null, "NONE");

                ProfileApplyGateResult confirmed = gate.ConfirmForReadOnlyAudit(
                    session.GateSession, live, true);
                if (confirmed.Code != ProfileApplyGateCode.EligibleReadOnly)
                    return Report("CONSENT", confirmed.Code == ProfileApplyGateCode.Conflict ?
                        ProfileDebugLiveApplyCode.Conflict : ProfileDebugLiveApplyCode.PrecheckRejected,
                        "READONLY_RECHECK:" + confirmed.Reason, null, "NONE");
                string mismatch;
                if (!session.BaselineMatches(live, out mismatch))
                    return Report("RECHECK", ProfileDebugLiveApplyCode.Conflict,
                        "SETTINGS_CHANGED", null, mismatch);
                ProfileStorageResult loaded = repository.Load(session.Id);
                if (!MatchesFrozenFile(loaded, session))
                    return Report("RECHECK", ProfileDebugLiveApplyCode.Conflict,
                        "FILE_OR_METADATA_CHANGED", null, "NONE");

                XpCustomizerSettings before = SettingsSnapshotService.CloneIndependent(live);
                XpCustomizerSettings candidate = ProfileMapper.ImportDetached(loaded.Document, before);
                string mapField;
                if (!ProfileApplyBridgeService.AlignPortableMapComparers(before, candidate, out mapField))
                    return Report("CANDIDATE", ProfileDebugLiveApplyCode.PrecheckRejected,
                        "UNSAFE_MAP_COMPARER", null, mapField);
                if (!SettingsCandidateValidator.Validate(candidate).Accepted)
                    return Report("CANDIDATE", ProfileDebugLiveApplyCode.PrecheckRejected,
                        "INVALID_CANDIDATE", null, "NONE");
                if (ProfileApplySettingsComparison.PortableEquivalent(before, candidate, out mismatch))
                    return Report("CANDIDATE", ProfileDebugLiveApplyCode.NoChange,
                        "NO_PORTABLE_CHANGE", null, "NONE");

                // Last read-only check, synchronously before mutation.
                ProfileEditorReadinessResult editor = ProfileEditorReadinessService.Check();
                if (editor == null || editor.Code != ProfileEditorReadinessCode.Ready ||
                    ProfileWriteSessionGate.IsBlocked || RuntimeRecoverySessionGate.LiveIsBlocked ||
                    !session.BaselineMatches(live, out mismatch) ||
                    !MatchesFrozenFile(repository.Load(session.Id), session) || !CanRunNow())
                    return Report("FINAL_PREFLIGHT", ProfileDebugLiveApplyCode.Conflict,
                        "LATE_EDITOR_SETTINGS_OR_FILE_CONFLICT", null, mismatch);

                // Read-only rollback evidence is captured before the first write.
                SettingsRuntimeRecovery.Backup settingsBefore = SettingsRuntimeRecovery.Capture(live);
                BiotechGeneService.RollbackSnapshot genesBefore = ModsConfig.BiotechActive ?
                    BiotechGeneService.CaptureRollbackSnapshot() : null;
                bool biotechChanged = ModsConfig.BiotechActive &&
                    SettingsRuntimeApplyCoordinator.BiotechChanged(before, candidate);
                bool curveChanged = SettingsRuntimeApplyCoordinator.CareerCurveChanged(before, candidate);
                if (ModsConfig.BiotechActive)
                {
                    int geneCount;
                    string geneReason;
                    if (!BiotechGeneService.AuditLiveValues(before, out geneCount, out geneReason))
                        return Report("FINAL_PREFLIGHT", ProfileDebugLiveApplyCode.PrecheckRejected,
                            "GENES_NOT_COHERENT:" + geneReason, null, "NONE");
                }

                // Arm the XP-owned XML write guard BEFORE applying to live settings.
                // It remains blocked until restart regardless of result.
                if (!ProfileDebugLiveApplySessionGuard.BeginLiveAttempt())
                    return Report("BEGIN", ProfileDebugLiveApplyCode.Duplicate,
                        "DEBUG_SESSION_ALREADY_USED", null, "NONE");
                mutationMayHaveStarted = true;
                SettingsRuntimeApplyResult applied =
                    SettingsRuntimeApplyCoordinator.ApplyLiveForDebugProfile(
                        SettingsSnapshotService.Capture(candidate));
                if (applied.Status == SettingsRuntimeApplyStatus.APPLY_PASS)
                {
                    if (!applied.UiNotified ||
                        (curveChanged && !applied.CacheInvalidated) ||
                        (biotechChanged && !applied.BiotechReapplied))
                        return Unrecovered("RUNTIME_REFRESH_FLAGS_INCONSISTENT");
                    if (!SettingsApplyCoordinator.Equivalent(live, candidate, out mismatch))
                        return Unrecovered("POST_APPLY_SETTINGS:" + mismatch);
                    if (ModsConfig.BiotechActive)
                    {
                        string geneReason = "NONE";
                        if (biotechChanged)
                        {
                            int count;
                            if (!BiotechGeneService.AuditLiveValues(live, out count, out geneReason))
                                return Unrecovered("POST_APPLY_GENES:" + geneReason);
                        }
                        else if (genesBefore == null || !genesBefore.MatchesCurrent(out geneReason))
                            return Unrecovered("UNEXPECTED_GENE_MUTATION:" + geneReason);
                    }
                    return Report("VERIFY", ProfileDebugLiveApplyCode.RuntimeAppliedVerified,
                        "LIVE_MEMORY_AND_GENE_AUDIT_PASS_NO_DISK_SAVE", null, "NONE");
                }
                if (applied.Status == SettingsRuntimeApplyStatus.RUNTIME_REFRESH_ROLLBACK_PASS ||
                    (applied.Status == SettingsRuntimeApplyStatus.APPLY_REJECTED &&
                     applied.Reason.StartsWith("DATA_APPLY_ROLLBACK_PASS", StringComparison.Ordinal)))
                {
                    if (!Restored(live, settingsBefore, genesBefore, out mismatch))
                        return Unrecovered("ROLLBACK_RECHECK:" + mismatch);
                    return Report("VERIFY", ProfileDebugLiveApplyCode.RollbackVerified,
                        "RECOVERED_NO_DISK_SAVE", null, "NONE");
                }
                if (applied.Status == SettingsRuntimeApplyStatus.APPLY_NO_CHANGE ||
                    applied.Status == SettingsRuntimeApplyStatus.APPLY_REJECTED)
                {
                    if (!Restored(live, settingsBefore, genesBefore, out mismatch))
                        return Unrecovered("REJECTED_BUT_MUTATED:" + mismatch);
                    return Report("ABORT", ProfileDebugLiveApplyCode.PrecheckRejected,
                        "COORDINATOR:" + applied.Status + ":" + applied.Reason, null, "NONE");
                }
                return Unrecovered("COORDINATOR:" + applied.Status + ":" + applied.Reason);
            }
            catch (ProfileContractException ex)
            {
                return mutationMayHaveStarted ? Unrecovered("PROFILE_AFTER_BEGIN:" + ex.Code) :
                    Report("ABORT", ProfileDebugLiveApplyCode.PrecheckRejected,
                        "PROFILE:" + ex.Code, null, "NONE");
            }
            catch (Exception ex)
            {
                return mutationMayHaveStarted ? Unrecovered("EXCEPTION_AFTER_BEGIN:" + ex.GetType().Name) :
                    Report("ABORT", ProfileDebugLiveApplyCode.PrecheckRejected,
                        "EXCEPTION:" + ex.GetType().Name, null, "NONE");
            }
            finally
            {
                loggingSession = null;
                executing = false;
            }
        }

        private static bool Restored(XpCustomizerSettings live,
            SettingsRuntimeRecovery.Backup backup,
            BiotechGeneService.RollbackSnapshot genes, out string mismatch)
        {
            mismatch = "NONE";
            if (!SettingsApplyCoordinator.Equivalent(live, backup.Values, out mismatch)) return false;
            for (int i = 0; i < backup.Fields.Length; i++)
            {
                if (!backup.Fields[i].FieldType.IsValueType &&
                    !System.Object.ReferenceEquals(backup.Fields[i].GetValue(live), backup.References[i]))
                { mismatch = "REFERENCE:" + backup.Fields[i].Name; return false; }
            }
            if (genes != null && !genes.MatchesCurrent(out mismatch)) return false;
            return true;
        }

        private ProfileDebugLiveApplyResult Unrecovered(string reason)
        {
            RuntimeRecoverySessionGate.ReportLiveFinalVerificationFailure(
                RuntimeRecoveryFailureStage.Verify, RuntimeRecoveryGateSource.LiveTransaction);
            return Report("VERIFY", ProfileDebugLiveApplyCode.Unrecovered,
                reason, null, "NONE");
        }

        private static bool MatchesFrozenFile(ProfileStorageResult loaded,
            ProfileApplyBridgeSession session)
        {
            if (loaded == null || !loaded.Success || loaded.Document == null ||
                !String.Equals(loaded.ContentHash, session.Hash, StringComparison.Ordinal))
                return false;
            ProfileDocument doc = loaded.Document;
            return String.Equals(doc.ProfileId, session.Id, StringComparison.Ordinal) &&
                String.Equals(doc.DisplayName, session.Name, StringComparison.Ordinal) &&
                doc.CreatedUtc == session.CreatedUtc && doc.UpdatedUtc == session.UpdatedUtc &&
                String.Equals(doc.SourceGameVersion, session.GameVersion, StringComparison.Ordinal);
        }

        private static bool CanRunNow()
        {
            return Event.current != null && Current.Game != null &&
                Find.TickManager != null && Find.TickManager.Paused &&
                (!ModsConfig.BiotechActive ||
                 DefDatabase<GeneDef>.AllDefsListForReading != null);
        }

        private ProfileDebugLiveApplyResult Report(string phase, ProfileDebugLiveApplyCode code,
            string reason, ProfileApplyBridgeSession session, string mismatch)
        {
            ProfileDebugLiveApplyResult result = new ProfileDebugLiveApplyResult(code, reason, session);
            ProfileApplyBridgeSession current = session ?? loggingSession;
            string prefix = current == null || String.IsNullOrEmpty(current.Hash) ? "NONE" :
                current.Hash.Substring(0, Math.Min(12, current.Hash.Length));
            string msg = "[XPC-R6P1D2D1A][DEBUG_PROFILE_LIVE_APPLY] revision=" + Revision +
                "; phase=" + phase + "; status=" + code + "; reason=" + result.Reason +
                "; selectedShaPrefix=" + prefix +
                "; firstMismatch=" + (mismatch ?? "NONE") +
                "; runMode=DEBUG_ONLY; pausedGameRequired=True" +
                "; debugSessionWriteBlocked=" + ProfileDebugLiveApplySessionGuard.LiveWriteBlocked +
                "; baseWriteSettingsCallsByService=0; profileJsonWrites=0" +
                "; persistence=NOT_VERIFIED; releaseRoute=NOT_EXERCISED_BY_DEBUG_ONLY_SERVICE" +
                "; build=" + XpCustomizerMod.BuildId;
            if (code == ProfileDebugLiveApplyCode.Unrecovered) Log.Error(msg);
            else if (code == ProfileDebugLiveApplyCode.PrecheckRejected ||
                code == ProfileDebugLiveApplyCode.Conflict ||
                code == ProfileDebugLiveApplyCode.RollbackVerified) Log.Warning(msg);
            else Log.Message(msg);
            return result;
        }
    }
}
#endif
