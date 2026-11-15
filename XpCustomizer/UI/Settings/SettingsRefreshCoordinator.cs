using Verse;

namespace XpCustomizer.UI
{
    /// <summary>
    /// R6-P0-D1: UI editor-state refresh ONLY. Does not apply settings,
    /// invalidate gameplay caches, write Scribe or mutate GeneDefs.
    /// Call on RimWorld's UI/main thread after an externally triggered change.
    /// Runtime-state reconciliation belongs to R6-P0-D2/D3.
    /// </summary>
    internal enum SettingsRefreshReason
    {
        GlobalVanillaReset,
        GlobalModNumericReset,
        BiotechVanillaReset,
        FutureProfileApply,
        DebugAudit
    }

    internal static class SettingsRefreshCoordinator
    {
        internal const string Revision = "R6P0D1-UI-REFRESH-20261106";

        /// <summary>
        /// One canonical fan-out for all six tab groups. Each group owns its
        /// own child editors. No duplicate calls to BiotechGeneService.
        /// </summary>
        internal static void NotifyAll(SettingsRefreshReason reason)
        {
            ProgressionSettingsUI.NotifySettingsExternallyChanged();
            LearningSettingsUI.NotifySettingsExternallyChanged();
            ConsciousnessSettingsUI.NotifySettingsExternallyChanged();
            DecaySettingsUI.NotifySettingsExternallyChanged();
            GwsSettingsUI.NotifySettingsExternallyChanged();
            BiotechSettingsUI.NotifySettingsExternallyChanged();

            LogIfRealChange("ALL_SIX_GROUPS", reason);
        }

        /// <summary>
        /// For the existing Biotech-only Vanilla reset button. Its preset
        /// already reapplies GeneDefs; never repeat that work from here.
        /// </summary>
        internal static void NotifyBiotechOnly(SettingsRefreshReason reason)
        {
            BiotechSettingsUI.NotifySettingsExternallyChanged();
            LogIfRealChange("BIOTECH_ONLY", reason);
        }

        private static void LogIfRealChange(string scope, SettingsRefreshReason reason)
        {
            if (reason == SettingsRefreshReason.DebugAudit)
                return;

            // This UI coordinator never mutates the underlying settings or
            // GeneDefs. A profile caller must account for its own writes.
            bool externalProfile = reason == SettingsRefreshReason.FutureProfileApply ||
                reason == SettingsRefreshReason.GlobalModNumericReset;
            Log.Message("[XPC-R6P0D1][UI_REFRESH] status=NOTIFIED; revision=" + Revision +
                "; scope=" + scope + "; reason=" + reason +
                "; dataApply=" + (externalProfile ? "EXTERNAL" : "NONE") +
                "; geneDefWrites=" + (externalProfile ? "EXTERNAL_NOT_COUNTED" : "0") +
                "; scribeCalls=0; cacheInvalidations=" +
                (externalProfile ? "EXTERNAL_NOT_COUNTED" : "0") +
                "; build=" + XpCustomizerMod.BuildId);
        }
    }
}
