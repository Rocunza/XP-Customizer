using System;
using System.Globalization;
using UnityEngine;
using Verse;
using XpCustomizer.FurtherLearning;
using XpCustomizer.ConsciousnessLearning;

namespace XpCustomizer.UI
{
    internal static partial class ConsciousnessFurtherLearningSettingsUI
    {

        private sealed class FieldState
        {
            internal readonly string Name;
            internal string Buffer;
            internal bool WasFocused;
            internal float LastValue = float.NaN;
            internal FieldState(string name) { Name = "ZCN.XpCustomizer.FurtherV2." + name; }
            internal void Reset() { Buffer = null; WasFocused = false; LastValue = float.NaN; }
        }

        private static readonly FieldState lowAnchorInput = new FieldState("LowAnchor");
        private static readonly FieldState lowFactorInput = new FieldState("LowFactor");
        private static readonly FieldState highAnchorInput = new FieldState("HighAnchor");
        private static readonly FieldState highFactorInput = new FieldState("HighFactor");
        private static readonly FieldState highLimitInput = new FieldState("HighLimit");
        private static readonly FieldState safetyInput = new FieldState("SafetyCap");

        internal static bool IsFurtherControl(string name)
        {
            return String.Equals(name, CustomPreviewControl, StringComparison.Ordinal) ||
                (name != null && (
                String.Equals(name, lowAnchorInput.Name, StringComparison.Ordinal) ||
                String.Equals(name, lowFactorInput.Name, StringComparison.Ordinal) ||
                String.Equals(name, highAnchorInput.Name, StringComparison.Ordinal) ||
                String.Equals(name, highFactorInput.Name, StringComparison.Ordinal) ||
                String.Equals(name, highLimitInput.Name, StringComparison.Ordinal) ||
                String.Equals(name, safetyInput.Name, StringComparison.Ordinal)));
        }

        // N1: validation is read-only. This includes the optional preview-only
        // number: invalid preview text must not be silently reverted on blur.
        private static bool ValidateNavigationEditor(FieldState state,
            string focusedName, float min, float max, out string reason)
        {
            reason = "NONE";
            if (!ProfileNumericNavigation.IsActive(state.Name,
                state.WasFocused, focusedName)) return true;
            if (ProfileNumericNavigation.ValidFloat(state.Buffer, min, max, out reason))
                return true;
            ProfileNumericNavigation.RejectControl(state.Name);
            return false;
        }

        internal static bool ValidateNavigationDraft(XpCustomizerSettings settings,
            string focusedName, out string reason)
        {
            reason = "NONE";
            if (settings == null) { reason = "MISSING_SETTINGS"; return false; }
            ConsciousnessFurtherV2Settings p = settings.consciousnessFurtherV2;
            if (p == null) { reason = "MISSING_FURTHER_SETTINGS"; return false; }
            if (ProfileNumericNavigation.IsActive(CustomPreviewControl,
                customPreviewWasFocused, focusedName) &&
                !ProfileNumericNavigation.ValidFloat(customPreviewBuffer, 0f, 100f,
                    out reason))
            {
                ProfileNumericNavigation.RejectControl(CustomPreviewControl);
                return false;
            }
            return ValidateNavigationEditor(lowAnchorInput, focusedName, 5f, 95f, out reason) &&
                ValidateNavigationEditor(lowFactorInput, focusedName, 0f, 1f, out reason) &&
                ValidateNavigationEditor(highAnchorInput, focusedName, 101f, 1000f, out reason) &&
                ValidateNavigationEditor(highFactorInput, focusedName, 1f, 19.99f, out reason) &&
                ValidateNavigationEditor(highLimitInput, focusedName,
                    Mathf.Min(20f, p.HighFactor + 0.01f), 20f, out reason) &&
                ValidateNavigationEditor(safetyInput, focusedName,
                    p.HighFactor, 20f, out reason);
        }


        private static void DrawNumber(Listing_Standard listing, string label,
            ref float value, FieldState state, float min, float max,
            float fallback, bool percentage)
        {
            float scaled = percentage ? value * 100f : value;
            float def = percentage ? fallback * 100f : fallback;
            if (float.IsNaN(scaled) || float.IsInfinity(scaled)) scaled = def;
            scaled = Mathf.Clamp(scaled, min, max);
            if (!state.WasFocused && (state.Buffer == null || float.IsNaN(state.LastValue) ||
                !Mathf.Approximately(state.LastValue, scaled)))
                state.Buffer = scaled.ToString("0.####", CultureInfo.InvariantCulture);

            // The same Cons labels are reused by Speed, Capacity and Further.
            float groupWidth = NumericRowLayout.MaxTranslatedWidth(
                "XPC_Cons_LowAnchor", "XPC_Cons_LowFactor",
                "XPC_Cons_HighAnchor", "XPC_Cons_HighFactor",
                "XPC_Cons_DiminishingLimit", "XPC_Cons_SafetyCap");
            NumericRowLayout.Placement geometry = NumericRowLayout.Allocate(
                listing, label, 105f, 0f, 30f, 28f, groupWidth);
            Widgets.Label(geometry.Label, label);
            Rect fieldRect = geometry.Field;

            Event evt = Event.current;
            if (state.WasFocused && evt != null && evt.type == EventType.KeyDown &&
                (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter))
            {
                GUI.FocusControl(null);
                evt.Use();
            }
            GUI.SetNextControlName(state.Name);
            state.Buffer = Widgets.TextField(fieldRect, state.Buffer ?? string.Empty);
            bool focused = string.Equals(GUI.GetNameOfFocusedControl(),
                state.Name, StringComparison.Ordinal);
            float parsed;
            if (focused)
            {
                if (ConsciousnessAnchorV2SettingsUI.TryParseNumericValue(state.Buffer, out parsed) &&
                    parsed >= min && parsed <= max && !Mathf.Approximately(parsed, scaled))
                    value = percentage ? parsed / 100f : parsed;
            }
            else if (state.WasFocused)
            {
                float committed = ConsciousnessAnchorV2SettingsUI.ResolveNumericCommit(
                    state.Buffer, scaled, min, max);
                if (!Mathf.Approximately(committed, scaled))
                    value = percentage ? committed / 100f : committed;
                state.Buffer = committed.ToString("0.####", CultureInfo.InvariantCulture);
            }
            state.WasFocused = focused;
            state.LastValue = percentage ? value * 100f : value;
        }

    }
}
