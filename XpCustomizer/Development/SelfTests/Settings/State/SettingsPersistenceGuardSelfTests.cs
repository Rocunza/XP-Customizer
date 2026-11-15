#if DEBUG
using System;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// D2-B2-B: read-only checks of the live-recovery-to-settings-XML policy.
    /// All synthetic failures use a private isolated gate instance.
    /// Does NOT call WriteSettings, Scribe, GeneDef or the live latch.
    /// </summary>
    internal static class SettingsPersistenceGuardSelfTests
    {
        internal static void Run()
        {
            int checks = 0;
            bool originalLive = RuntimeRecoverySessionGate.LiveIsBlocked;
            RuntimeRecoverySessionGate gate = RuntimeRecoverySessionGate.CreateIsolatedForAudit();
            Check(!gate.ModSettingsIoBlockedForIsolatedAudit,
                "healthy game permits existing Mod.WriteSettings path", ref checks);

            gate.ObserveForIsolatedAudit(new SettingsRuntimeApplyResult(
                SettingsRuntimeApplyStatus.APPLY_REJECTED, "PREFLIGHT_ONLY", false, false, false),
                RuntimeRecoveryGateSource.LiveDiagnostic);
            Check(!gate.ModSettingsIoBlockedForIsolatedAudit,
                "preflight rejection does not suppress normal settings persistence", ref checks);

            gate.ObserveForIsolatedAudit(new SettingsRuntimeApplyResult(
                SettingsRuntimeApplyStatus.RUNTIME_REFRESH_ROLLBACK_PASS,
                "TEST_RECOVERED", false, false, false),
                RuntimeRecoveryGateSource.LiveDiagnostic);
            Check(!gate.ModSettingsIoBlockedForIsolatedAudit,
                "verified rollback does not suppress normal settings persistence", ref checks);

            gate.ReportVerificationFailureForIsolatedAudit(
                RuntimeRecoveryFailureStage.Biotech,
                RuntimeRecoveryGateSource.LiveDiagnostic);
            Check(gate.ModSettingsIoBlockedForIsolatedAudit,
                "unverified GeneDef rollback suppresses our ModSettings XML write", ref checks);
            Check(!gate.TryEnterForIsolatedAudit(RuntimeRecoveryGateSource.LiveDiagnostic),
                "recovery lock prevents another live diagnostic", ref checks);
            Check(RuntimeRecoverySessionGate.LiveIsBlocked == originalLive &&
                RuntimeRecoverySessionGate.LiveModSettingsIoBlocked == originalLive,
                "isolated fault cannot change actual persistence policy", ref checks);

            Log.Message("[XPC-R6P1D2B2B][MOD_SETTINGS_PERSISTENCE_GUARD_AUDIT] " +
                "status=PASS; checks=" + checks +
                "; scope=ISOLATED_POLICY_ONLY; actualWriteSettings=NOT_CALLED; " +
                "scribeCalls=0; geneDefWrites=0; liveSettingsWrites=0; " +
                "liveGateUnaffected=True; productionApply=NOT_EXERCISED_BY_THIS_TEST; " +
                "build=" + XpCustomizerMod.BuildId);
        }

        private static void Check(bool condition, string name, ref int checks)
        {
            checks++;
            if (!condition)
                throw new InvalidOperationException("[XPC-R6P1D2B2B] " + name);
        }
    }
}
#endif
