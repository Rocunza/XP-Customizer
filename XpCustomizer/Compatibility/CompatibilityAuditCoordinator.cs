using System;
using LudeonTK;
using Verse;

namespace XpCustomizer.Compatibility
{
    public static class CompatibilityAuditCoordinator
    {
        // The caller uses Verse.LongEventHandler.ExecuteWhenFinished, after
        // RimWorld's normal initialization. No game-tick patch is installed.
        internal static void RunStartupAudit()
        {
            Run("POST_LONG_EVENTS", false);
        }

        // Future UI/debug integration may call this public-to-assembly entry
        // without introducing a second collector or touching Harmony state.
        [DebugAction("XP Customizer", "CA-1 Compatibility Audit (read-only)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void RunManualAudit()
        {
            Run("MANUAL_RESCAN", true);
        }

        private static void Run(string phase, bool full)
        {
            try
            {
                CompatibilityAuditSnapshot snapshot = HarmonyPatchDiscovery.Capture();
#if DEBUG
                CompatibilityAuditSelfTests.Verify(snapshot);
#endif
                CompatibilityAuditReporter.Report(snapshot, phase, full);
            }
            catch (Exception ex)
            {
                // Observability must NEVER prevent normal RimWorld mod startup.
                Log.Warning("[XPC-CA][AUDIT_FAILED] schema=" +
                    CompatibilityAuditReporter.Schema +
                    "; build=" + XpCustomizerMod.BuildId +
                    "; phase=" + phase +
                    "; gameplayUnchanged=True; exception=" + ex);
            }
        }
    }
}
