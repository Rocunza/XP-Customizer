#if DEBUG
using System;
using System.Reflection;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer
{
    /// <summary>
    /// Q1: catches a return to the misleading assumption that the legacy
    /// D4-only adapter controls the production profile UI. Reflection only:
    /// no IMGUI, live settings, filesystem, Scribe or GeneDef mutations.
    /// The literal-log inventory is independently verified by the Q1 manifest.
    /// </summary>
    internal static class DiagnosticLogSemanticsQ1SelfTests
    {
        internal const string Revision = "Q1-DIAGNOSTIC-SCOPE-20261109";
        private static int checks;

        private static void Check(bool condition, string why)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("[XPC-Q1] " + why);
        }

        internal static void Run()
        {
            checks = 0;
            BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public |
                BindingFlags.Static | BindingFlags.Instance;

            // These are different routes. The old one MUST remain disabled.
            Check(SettingsRuntimeApplyCoordinator.LiveApplyStillBlockedForAudit,
                "legacy D4 adapter guard must remain closed");
            Check(typeof(SettingsRuntimeApplyCoordinator).GetMethod(
                "ApplyLiveAfterD4", flags) != null, "legacy guarded adapter still present");
            Check(typeof(SettingsRuntimeApplyCoordinator).GetMethod(
                "ApplyLiveForProductionProfile", flags) != null,
                "separate production runtime adapter is present");
            Check(typeof(ProfileProductionApplyService).GetMethod(
                "Prepare", flags) != null, "production preflight exists");
            Check(typeof(ProfileProductionApplyService).GetMethod(
                "ConfirmAndApply", flags) != null, "production commit entry exists");
            Check(typeof(ProfileProductionApplyService).GetMethod(
                "Report", flags) != null, "production result logging exists");
            Check(typeof(ProfileProductionApplyCode).GetField(
                "WrittenVerifiedNoRestart", flags) != null,
                "verified write result code retained");
            Check(typeof(ProfileProductionApplyCode).GetField(
                "PersistenceUncertain", flags) != null,
                "uncertain disk outcome result code retained");
            Check(typeof(ProfileProductionApplyCode).GetField(
                "RuntimeRecoveryRequired", flags) != null,
                "live recovery result code retained");
            Check(typeof(RuntimeRecoverySessionGate).GetProperty(
                "LiveIsBlocked", flags) != null,
                "live recovery latch still exists");
            Check(typeof(ProfileProductionWriteGate).GetProperty(
                "IsUncertain", flags) != null,
                "owned XML uncertain-write gate still exists");
            Check(typeof(ProfileProductionWriteGate).GetProperty(
                "IsBusy", flags) != null,
                "owned XML in-flight gate still exists");

            Log.Message("[XPC-Q1][LOG_SEMANTICS_SELFTEST] status=PASS; checks=" + checks +
                "; revision=" + Revision +
                "; scope=REFLECTION_ONLY_NO_PRODUCTION_EXECUTION" +
                "; legacyD4Entry=BLOCKED_BY_DESIGN" +
                "; productionRoute=EXISTS_NOT_INVOKED" +
                "; actualProductionAvailability=NOT_EVALUATED" +
                "; realSettingsWrites=0; profileJsonWrites=0; configXmlWrites=0" +
                "; geneDefWrites=0; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
