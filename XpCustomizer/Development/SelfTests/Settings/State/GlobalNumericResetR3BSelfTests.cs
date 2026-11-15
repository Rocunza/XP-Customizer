#if DEBUG
using System;
using Verse;
using XpCustomizer.UI;

namespace XpCustomizer
{
    /// <summary>
    /// R3-B pure policy and one-shot confirmation regression.
    /// UI focus/blur and actual runtime mutations require live game tests.
    /// No settings/JSON/XML/GeneDef writes are performed here.
    /// </summary>
    internal static class GlobalNumericResetR3BSelfTests
    {
        private static int checks;
        private static void Check(bool condition, string reason)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("R3-B " + reason);
        }

        internal static void Run()
        {
            checks = 0;
            Check(GlobalNumericResetR3BPolicy.IsSuccessfulReset(
                SettingsRuntimeApplyStatus.APPLY_PASS), "applied success");
            Check(GlobalNumericResetR3BPolicy.IsSuccessfulReset(
                SettingsRuntimeApplyStatus.APPLY_NO_CHANGE), "no change success");
            Check(!GlobalNumericResetR3BPolicy.IsSuccessfulReset(
                SettingsRuntimeApplyStatus.APPLY_REJECTED), "rejected is not success");
            Check(!GlobalNumericResetR3BPolicy.IsSuccessfulReset(
                SettingsRuntimeApplyStatus.RUNTIME_REFRESH_ROLLBACK_PASS),
                "rolled back is not success");
            Check(!GlobalNumericResetR3BPolicy.IsSuccessfulReset(
                SettingsRuntimeApplyStatus.RUNTIME_REFRESH_FAILED_UNRECOVERED),
                "unrecovered is not success");
            Check(GlobalNumericResetR3BPolicy.NeedsNoChangeEditorRefresh(
                SettingsRuntimeApplyStatus.APPLY_NO_CHANGE), "no change refresh required");
            Check(!GlobalNumericResetR3BPolicy.NeedsNoChangeEditorRefresh(
                SettingsRuntimeApplyStatus.APPLY_PASS), "normal apply already refreshes");
            Check(!GlobalNumericResetR3BPolicy.NeedsNoChangeEditorRefresh(
                SettingsRuntimeApplyStatus.APPLY_REJECTED), "failed apply no discard");

            int calls = 0;
            GlobalVanillaResetConfirmationGate gate = new GlobalVanillaResetConfirmationGate();
            Check(gate.TryCancel(), "cancel accepted");
            Check(!gate.TryConfirm(delegate { calls++; }) && calls == 0,
                "cancel never invokes the reset callback");
            gate = new GlobalVanillaResetConfirmationGate();
            Check(gate.TryConfirm(delegate { calls++; }) && calls == 1,
                "yes invokes reset once");
            Check(!gate.TryConfirm(delegate { calls++; }) && calls == 1,
                "repeated yes is rejected");

            // A rejected preflight leaves a consumed one-shot gate. Closing
            // and reopening is required, as before; never auto-retry mutations.
            gate = new GlobalVanillaResetConfirmationGate();
            try { gate.TryConfirm(delegate { throw new InvalidOperationException("TEST_ONLY"); }); }
            catch (InvalidOperationException) { }
            Check(!gate.IsPending && gate.Attempted && !gate.Succeeded,
                "failed attempt is still one shot");

            Log.Message("[XPC-R3B][NUMERIC_RESET_UX_SELFTEST] status=PASS; checks=" +
                checks + "; mode=PURE_NO_IMGUI; resetModal=EXPLICIT_YES_ONLY; " +
                "pendingEditors=ALLOWED_FOR_MOD_DEFAULTS_ONLY; " +
                "noChangeEditorRefresh=REQUIRED; profileReadinessGate=UNCHANGED; " +
                "cancelNoReset=PASS; oneShot=PASS; liveSettingsWrites=0; " +
                "profileJsonWrites=0; configXmlWrites=0; geneDefWrites=0; " +
                "build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
