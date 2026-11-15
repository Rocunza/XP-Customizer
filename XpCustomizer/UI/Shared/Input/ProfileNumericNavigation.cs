using System;
using System.Globalization;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    /// <summary>
    /// N1: read-only validation BEFORE moving focus away from an XP-owned
    /// numeric editor. Existing owners retain their original commit mechanics;
    /// no reflection or foreign GUI controls are touched here.
    /// </summary>
    internal static class ProfileNumericNavigation
    {
        internal const string Revision = "N1-OWNED-FOCUS-TRANSITION-20261108";
        private const string OwnedPrefix = "ZCN.XpCustomizer.";
        private static string rejectedControl;
        internal static string RejectedControl { get { return rejectedControl; } }
        internal static void RejectControl(string name) { rejectedControl = name; }

        internal static bool IsOwnedControl(string name)
        {
            return !String.IsNullOrEmpty(name) &&
                name.StartsWith(OwnedPrefix, StringComparison.Ordinal);
        }

        internal static bool IsActive(string name, bool wasFocused, string focusedName)
        {
            return wasFocused || String.Equals(name, focusedName, StringComparison.Ordinal);
        }

        internal static bool ValidInteger(string raw, int min, int max, out string reason)
        {
            reason = "NONE";
            int parsed;
            if (ProfileEditorReadinessService.ClassifyTextBuffer(raw, true, false) ==
                    ProfileEditorReadinessCode.PendingEdit ||
                !BiotechSettingsUI.TryParseInteger(raw, out parsed))
            {
                reason = "INVALID_DRAFT";
                return false;
            }
            if (parsed < min || parsed > max)
            {
                reason = "OUT_OF_RANGE";
                return false;
            }
            return true;
        }

        internal static bool ValidFloat(string raw, float min, float max, out string reason)
        {
            reason = "NONE";
            float parsed;
            if (ProfileEditorReadinessService.ClassifyTextBuffer(raw, true, false) ==
                    ProfileEditorReadinessCode.PendingEdit ||
                !(float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed) ||
                  float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)) ||
                float.IsNaN(parsed) || float.IsInfinity(parsed))
            {
                reason = "INVALID_DRAFT";
                return false;
            }
            if (parsed < min || parsed > max)
            {
                reason = "OUT_OF_RANGE";
                return false;
            }
            return true;
        }

        internal static bool ValidateBeforeBlur(SettingsTab tab, XpCustomizerSettings settings,
            string focusedName, out string owner, out string reason)
        {
            rejectedControl = null;
            reason = "NONE";
            owner = tab.ToString();
            // Validate the owning staged editor before its legacy focus-loss
            // handler can clamp/revert an incomplete number silently.
            switch (tab)
            {
                case SettingsTab.Biotech:
                    owner = "BiotechSettingsUI";
                    if (!BiotechSettingsUI.ValidateNavigationDraft(settings, focusedName,
                        out reason)) return false;
                    break;
                case SettingsTab.GWS:
                    owner = "GwsSettingsUI";
                    if (!GwsSettingsUI.ValidateNavigationDraft(settings, focusedName,
                        out reason)) return false;
                    break;
                case SettingsTab.Cons:
                    owner = "ConsciousnessSettingsUI";
                    if (!ConsciousnessAnchorV2SettingsUI.ValidateNavigationDraft(settings,
                            focusedName, out reason)) return false;
                    if (!ConsciousnessFurtherLearningSettingsUI.ValidateNavigationDraft(settings,
                            focusedName, out reason)) return false;
                    break;
            }
            // All the remaining 11+ numeric sources use owned vanilla widgets;
            // validate their actual observed raw text and bounds. An unknown
            // owned focus is NOT permission to clear it.
            if (ProfileNumericFocus.IsNumericControl(focusedName))
            {
                owner = "ProfileNumericFocus";
                if (!ProfileNumericFocus.ValidateNavigationDraft(focusedName, out reason))
                {
                    RejectControl(focusedName);
                    return false;
                }
                return true;
            }
            string lastNumericControl, lastNumericReason;
            if (!ProfileNumericFocus.ValidateLastFocusedDraft(
                out lastNumericControl, out lastNumericReason))
            {
                owner = "ProfileNumericFocus";
                reason = lastNumericReason;
                RejectControl(lastNumericControl);
                return false;
            }
            if (IsOwnedControl(focusedName))
            {
                // Custom staged controls in other tabs or an unregistered new
                // editor cannot be authenticated by this navigation route.
                if ((tab == SettingsTab.Biotech &&
                     BiotechSettingsUI.IsBiotechControl(focusedName)) ||
                    (tab == SettingsTab.GWS &&
                     GwsSettingsUI.IsGwsControl(focusedName)) ||
                    (tab == SettingsTab.Cons &&
                     (ConsciousnessAnchorV2SettingsUI.IsConsControl(focusedName) ||
                      ConsciousnessFurtherLearningSettingsUI.IsFurtherControl(focusedName))))
                    return true; // owner-specific validity already checked
                reason = "UNRECOGNIZED_OWN_FOCUS";
                return false;
            }
            return true; // empty/non-XPC focus: never forcibly blur a foreign field
        }
    }
}
