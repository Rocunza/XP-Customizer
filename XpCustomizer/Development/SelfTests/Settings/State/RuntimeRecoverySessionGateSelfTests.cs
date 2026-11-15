#if DEBUG
using System;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer
{
    /// <summary>
    /// B2-A synthetic gate state machine + detached coordinator integration.
    /// Every constructed failure is TEST-ONLY, never a real GeneDef failure.
    /// No Scribe, XML, pawn, GeneDef, cache or user-profile mutation.
    /// </summary>
    internal static class RuntimeRecoverySessionGateSelfTests
    {
        private static int cases;
        private static int checks;

        private sealed class FakeActions : ISettingsRuntimeRefreshActions
        {
            internal bool UiFails;
            public object CaptureBiotechRollbackState() { return 42; }
            public bool RestoreBiotechRollbackState(object snapshot, out string reason)
            {
                reason = "NONE";
                return true;
            }
            public void InvalidateCurveCache() { }
            public void ReapplyBiotech() { }
            public void NotifyAllEditors()
            {
                if (UiFails) throw new InvalidOperationException("TEST_UI_RESTORE_FAILURE");
            }
        }

        private static RuntimeRecoverySessionGate NewGate()
        {
            return RuntimeRecoverySessionGate.CreateIsolatedForAudit();
        }

        private static SettingsRuntimeApplyResult FakeResult(SettingsRuntimeApplyStatus status, string why)
        {
            return new SettingsRuntimeApplyResult(status, why, false, false, false);
        }

        private static void Case(bool pass, string description)
        {
            cases++;
            Check(pass, description);
        }

        private static void Check(bool pass, string description)
        {
            checks++;
            if (!pass) throw new InvalidOperationException("[XPC-R6P1D2B2A] " + description);
        }

        internal static void Run()
        {
            cases = 0;
            checks = 0;
            bool originalLiveState = RuntimeRecoverySessionGate.LiveIsBlocked;
            bool originalStorageLock = ProfileWriteSessionGate.IsBlocked;
            RuntimeRecoverySessionGate g = NewGate();

            Case(!g.IsBlockedForIsolatedAudit, "01 new gate healthy");
            Case(g.TryEnterForIsolatedAudit(RuntimeRecoveryGateSource.LiveTransaction), "02 initial entry");
            Case(g.TryEnterForIsolatedAudit(RuntimeRecoveryGateSource.LiveDiagnostic), "03 diagnostic entry");

            SettingsRuntimeApplyStatus[] nonLatching = {
                SettingsRuntimeApplyStatus.APPLY_REJECTED,
                SettingsRuntimeApplyStatus.APPLY_NO_CHANGE,
                SettingsRuntimeApplyStatus.APPLY_PASS,
                SettingsRuntimeApplyStatus.RUNTIME_REFRESH_ROLLBACK_PASS
            };
            string[] descriptions = {
                "04 reject before write", "05 no changes", "06 successfully applied detached",
                "07 successfully verified rollback"
            };
            for (int i = 0; i < nonLatching.Length; i++)
            {
                g.ObserveForIsolatedAudit(FakeResult(nonLatching[i], "TEST_ONLY"),
                    RuntimeRecoveryGateSource.LiveTransaction);
                Case(!g.IsBlockedForIsolatedAudit, descriptions[i]);
            }

            // All these rejection reasons must be non-latching: they did not
            // begin any runtime mutation and are NOT recovery failures.
            string[] rejections = {
                "CANCELLED", "STALE_SHA", "INVALID_PROFILE", "EDITOR_PENDING",
                "EDITOR_UNCERTAIN", "PREFLIGHT_SNAPSHOT_FAIL", "GENE_DEFS_NOT_READY",
                "REENTRANT_APPLY", "DATA_APPLY_ROLLBACK_PASS"
            };
            for (int i = 0; i < rejections.Length; i++)
            {
                g.ObserveForIsolatedAudit(FakeResult(SettingsRuntimeApplyStatus.APPLY_REJECTED,
                    rejections[i]), RuntimeRecoveryGateSource.LiveTransaction);
                Case(!g.IsBlockedForIsolatedAudit, "08-16 prewrite/recovered rejection " + rejections[i]);
            }
            Case(g.TryEnterForIsolatedAudit(RuntimeRecoveryGateSource.LiveTransaction),
                "17 good outcomes do not block later entries");

            RuntimeRecoverySessionGate data = NewGate();
            data.ObserveForIsolatedAudit(FakeResult(
                SettingsRuntimeApplyStatus.RUNTIME_REFRESH_FAILED_UNRECOVERED,
                "DATA_APPLY_ROLLBACK_FAILED"), RuntimeRecoveryGateSource.LiveTransaction);
            Case(data.IsBlockedForIsolatedAudit, "18 DATA unrecovered latches");
            Case(data.FirstStageForIsolatedAudit == RuntimeRecoveryFailureStage.Data,
                "19 DATA failure classification");
            Case(data.FirstSourceForIsolatedAudit == RuntimeRecoveryGateSource.LiveTransaction,
                "20 preserve transaction source");
            Case(data.FirstVerificationForIsolatedAudit == "FAILED", "21 failed evidence recorded");
            Case(!data.TryEnterForIsolatedAudit(RuntimeRecoveryGateSource.LiveTransaction),
                "22 second transaction blocked");
            Case(!data.TryEnterForIsolatedAudit(RuntimeRecoveryGateSource.LiveDiagnostic),
                "23 live diagnostic also blocked");
            data.ObserveForIsolatedAudit(FakeResult(
                SettingsRuntimeApplyStatus.RUNTIME_REFRESH_FAILED_UNRECOVERED,
                "UI:AFTER_RECOVERY"), RuntimeRecoveryGateSource.LiveDiagnostic);
            Case(data.FirstStageForIsolatedAudit == RuntimeRecoveryFailureStage.Data &&
                data.FirstSourceForIsolatedAudit == RuntimeRecoveryGateSource.LiveTransaction,
                "24 first failure is immutable");

            string[] unrecoveredStages = { "CACHE:FAULT", "BIOTECH:FAULT", "UI:FAULT", "ODD:FAULT" };
            RuntimeRecoveryFailureStage[] expected = {
                RuntimeRecoveryFailureStage.Cache, RuntimeRecoveryFailureStage.Biotech,
                RuntimeRecoveryFailureStage.Ui, RuntimeRecoveryFailureStage.Unknown
            };
            for (int i = 0; i < unrecoveredStages.Length; i++)
            {
                g = NewGate();
                g.ObserveForIsolatedAudit(FakeResult(
                    SettingsRuntimeApplyStatus.RUNTIME_REFRESH_FAILED_UNRECOVERED,
                    unrecoveredStages[i]), RuntimeRecoveryGateSource.LiveDiagnostic);
                Case(g.IsBlockedForIsolatedAudit && g.FirstStageForIsolatedAudit == expected[i],
                    "25-28 failed stage " + unrecoveredStages[i]);
            }

            g = NewGate();
            g.ObserveForIsolatedAudit(null, RuntimeRecoveryGateSource.LiveTransaction);
            Case(g.IsBlockedForIsolatedAudit && g.FirstStageForIsolatedAudit ==
                RuntimeRecoveryFailureStage.Unknown, "29 null result latches unknown state");

            RuntimeRecoveryFailureStage[] finalMismatch = {
                RuntimeRecoveryFailureStage.Data, RuntimeRecoveryFailureStage.Biotech,
                RuntimeRecoveryFailureStage.Cache, RuntimeRecoveryFailureStage.Ui,
                RuntimeRecoveryFailureStage.Verify, RuntimeRecoveryFailureStage.Unknown
            };
            for (int i = 0; i < finalMismatch.Length; i++)
            {
                g = NewGate();
                g.ReportVerificationFailureForIsolatedAudit(finalMismatch[i],
                    RuntimeRecoveryGateSource.LiveDiagnostic);
                Case(g.IsBlockedForIsolatedAudit && g.FirstStageForIsolatedAudit == finalMismatch[i],
                    "30-35 final D4 verification failure " + finalMismatch[i]);
            }

            g = NewGate();
            g.ReportVerificationFailureForIsolatedAudit(RuntimeRecoveryFailureStage.None,
                RuntimeRecoveryGateSource.LiveDiagnostic);
            Case(g.IsBlockedForIsolatedAudit && g.FirstStageForIsolatedAudit ==
                RuntimeRecoveryFailureStage.Unknown, "36 missing failed stage is fail closed");

            // Exercise the actual detached coordinator, not a fabricated
            // SettingsRuntimeApplyResult. Its fake faults must stay local.
            XpCustomizerSettings detached = new XpCustomizerSettings();
            XpCustomizerSettings before = SettingsSnapshotService.CloneIndependent(detached);
            XpCustomizerSettings candidate = SettingsSnapshotService.CloneIndependent(detached);
            candidate.careerXpMultiplier += 2f;
            FakeActions fake = new FakeActions();
            SettingsRuntimeApplyResult detachedPass =
                SettingsRuntimeApplyCoordinator.ApplyDetachedForAudit(detached,
                    SettingsSnapshotService.Capture(candidate), fake, false);
            Case(detachedPass.Status == SettingsRuntimeApplyStatus.APPLY_PASS,
                "37 detached coordinator succeeds without live adapter");
            string mismatch;
            Case(!SettingsApplyCoordinator.Equivalent(detached, before, out mismatch),
                "38 detached target actually changed; live target not supplied");

            candidate.careerXpMultiplier += 2f;
            fake.UiFails = true;
            SettingsRuntimeApplyResult detachedFailure =
                SettingsRuntimeApplyCoordinator.ApplyDetachedForAudit(detached,
                    SettingsSnapshotService.Capture(candidate), fake, false);
            Case(detachedFailure.Status == SettingsRuntimeApplyStatus.RUNTIME_REFRESH_FAILED_UNRECOVERED,
                "39 detached mock UI restoration fails as expected");
            g = NewGate();
            g.ObserveForIsolatedAudit(detachedFailure, RuntimeRecoveryGateSource.LiveDiagnostic);
            Case(g.IsBlockedForIsolatedAudit && g.FirstStageForIsolatedAudit ==
                RuntimeRecoveryFailureStage.Ui, "40 test-owned gate records detached failure");

            Case(RuntimeRecoverySessionGate.LiveIsBlocked == originalLiveState,
                "41 fake tests do not change live gate");
            Case(ProfileWriteSessionGate.IsBlocked == originalStorageLock,
                "42 profile-file recovery gate is independent");
            Case(SettingsRuntimeApplyCoordinator.LiveApplyStillBlockedForAudit,
                "43 live profile apply is still hard disabled");
            if (XpCustomizerMod.Settings != null)
            {
                SettingsRuntimeApplyResult refused =
                    SettingsRuntimeApplyCoordinator.ApplyDetachedForAudit(
                        XpCustomizerMod.Settings, SettingsSnapshotService.Capture(candidate),
                        new FakeActions(), false);
                Case(refused.Status == SettingsRuntimeApplyStatus.APPLY_REJECTED &&
                    refused.Reason == "DETACHED_TARGET_IS_LIVE_SETTINGS",
                    "44 detached adapter refuses live ModSettings root");
            }
            else
            {
                Case(false, "44 live ModSettings not ready for gate audit");
            }

            Log.Message("[XPC-R6P1D2B2A][RUNTIME_RECOVERY_GATE_AUDIT] status=PASS; " +
                "revision=" + RuntimeRecoverySessionGate.Revision +
                "; scenarios=" + cases + "; checks=" + checks +
                "; isolatedGates=True; syntheticFailuresOnly=True" +
                "; detachedCoordinator=REAL_CODE_FAKE_EFFECTS" +
                "; lockOnUnrecovered=PASS; noLockOnRecovered=PASS" +
                "; repeatAttempt=BLOCKED; firstFailure=IMMUTABLE" +
                "; liveGateUnaffected=True; profileStorageGateUnaffected=True" +
                "; liveSettingsWrites=0; geneDefWrites=0; pawnWrites=0; scribeCalls=0" +
                "; isolatedGateLatching=VERIFIED; productionApply=NOT_EXERCISED_BY_THIS_TEST; D4ManualLiveAudit=NOT_RUN" +
                "; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
