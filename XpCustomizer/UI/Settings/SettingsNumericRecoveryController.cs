using XpCustomizer.Profiles;
#if DEBUG
using Verse;
#endif

namespace XpCustomizer.UI
{
    /// <summary>
    /// S1 passive diagnostic boundary; the separate S2 partial adds a
    /// narrowly scoped Skilltrainer permission after 15-owner inspection.
    /// S1 ObserveBlockedReadOnly remains read-only and Debug-only.
    /// </summary>
    internal static partial class SettingsNumericRecoveryController
    {
        internal const string Version = NumericDraftRecoveryRegistry.Version;

#if DEBUG
        // Never print dictionary keys, numeric text or arbitrary editor IDs.
        // Only static C# field names are accepted as printable diagnostics.
        internal static string SafeStaticFieldLabel(string field)
        {
            if (string.IsNullOrEmpty(field) || field.Length > 120)
                return "UNRESOLVED_OR_DYNAMIC";
            for (int i = 0; i < field.Length; i++)
            {
                char c = field[i];
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') || c == '.' || c == '_'))
                    return "UNRESOLVED_OR_DYNAMIC";
            }
            return field;
        }

        // Once per explicit rejected navigation, not on each Repaint.
        // No raw numeric text, profile name or dictionary key is logged.
        internal static void ObserveBlockedReadOnly(string phase,
            ProfileNavigationTarget target, SettingsTab tab, string reason)
        {
            try
            {
                NumericDraftRecoveryObservation observation =
                    NumericDraftRecoveryRegistry.InspectCurrentReadOnly();
                Log.Message("[XPC-N1-R2R3] version=" + Version +
                    "; stage=S1; action=OBSERVE_ONLY; phase=" + phase +
                    "; target=" + target + "; tab=" + tab +
                    "; blockReason=" + reason + "; result=" + observation.Code +
                    "; owner=" + observation.OwnerId +
                    "; field=" + SafeStaticFieldLabel(observation.FieldId) +
                    "; pendingFields=" + observation.PendingFields +
                    "; uncertainFields=" + observation.UncertainFields +
                    "; ownedControlIdentified=" + observation.IdentifiedOwnedNumericControl +
                    "; readyGate=UNCHANGED; discard=DIAGNOSTIC_ONLY" +
                    "; settingsWrites=0; profileWrites=0; foreignFocusChanges=0" +
                    "; build=" + XpCustomizerMod.BuildId);
            }
            catch (System.Exception ex)
            {
                // Diagnostics must never interrupt the original navigation
                // rejection or accidentally open a destination window.
                Log.Warning("[XPC-N1-R2R3] version=" + Version +
                    "; stage=S1; action=PROBE_FAILED; error=" +
                    ex.GetType().Name + "; navigationGate=UNCHANGED" +
                    "; build=" + XpCustomizerMod.BuildId);
            }
        }
#endif
    }
}
