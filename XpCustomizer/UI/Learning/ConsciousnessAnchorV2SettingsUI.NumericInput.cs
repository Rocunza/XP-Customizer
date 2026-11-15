using System;
using System.Globalization;
using UnityEngine;
using Verse;
using XpCustomizer.ConsciousnessLearning;

namespace XpCustomizer.UI
{
    /// <summary>
    /// Single-source Anchor V2 editor for independent Speed and Capacity.
    /// GWS-style mode choices are placed directly below each Low/High toggle.
    /// No additional Harmony patches are introduced.
    /// </summary>
    internal static partial class ConsciousnessAnchorV2SettingsUI
    {
        // Dedicated editor state: never overwrite a half-typed number with
        // the bounded gameplay value until editing is finished.
        private sealed class ConsNumericEditState
        {
            internal readonly string ControlName;
            internal string Buffer;
            internal float LastValue = float.NaN;
            internal bool WasFocused;

            internal ConsNumericEditState(string controlName)
            {
                ControlName = "ZCN.XpCustomizer.ConsV2." + controlName;
            }

            internal void Reset()
            {
                Buffer = null;
                LastValue = float.NaN;
                WasFocused = false;
            }
        }

        private sealed class ProfileInputBuffers
        {
            internal readonly ConsNumericEditState LowAnchor;
            internal readonly ConsNumericEditState LowFactor;
            internal readonly ConsNumericEditState HighAnchor;
            internal readonly ConsNumericEditState HighFactor;
            internal readonly ConsNumericEditState DiminishingLimit;
            internal readonly ConsNumericEditState SafetyLimit;

            internal ProfileInputBuffers(string prefix)
            {
                LowAnchor = new ConsNumericEditState(prefix + ".LowAnchor");
                LowFactor = new ConsNumericEditState(prefix + ".LowFactor");
                HighAnchor = new ConsNumericEditState(prefix + ".HighAnchor");
                HighFactor = new ConsNumericEditState(prefix + ".HighFactor");
                DiminishingLimit = new ConsNumericEditState(prefix + ".DiminishingLimit");
                SafetyLimit = new ConsNumericEditState(prefix + ".SafetyLimit");
            }

            internal void Reset()
            {
                LowAnchor.Reset();
                LowFactor.Reset();
                HighAnchor.Reset();
                HighFactor.Reset();
                DiminishingLimit.Reset();
                SafetyLimit.Reset();
            }
        }

        private static readonly ProfileInputBuffers speedInputs = new ProfileInputBuffers("Speed");
        private static readonly ProfileInputBuffers capacityInputs = new ProfileInputBuffers("Capacity");
        internal static bool IsConsControl(string name)
        {
            return name != null && (
                String.Equals(name, speedInputs.LowAnchor.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, speedInputs.LowFactor.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, speedInputs.HighAnchor.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, speedInputs.HighFactor.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, speedInputs.DiminishingLimit.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, speedInputs.SafetyLimit.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, capacityInputs.LowAnchor.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, capacityInputs.LowFactor.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, capacityInputs.HighAnchor.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, capacityInputs.HighFactor.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, capacityInputs.DiminishingLimit.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, capacityInputs.SafetyLimit.ControlName, StringComparison.Ordinal));
        }

        // N1: use the same bounds as DrawNumber, without changing the profile
        // or its staged buffers. Real commit still belongs to DrawNumber.
        private static bool ValidateNavigationEditor(ConsNumericEditState editor,
            string focusedName, float min, float max, out string reason)
        {
            reason = "NONE";
            if (!ProfileNumericNavigation.IsActive(editor.ControlName,
                editor.WasFocused, focusedName)) return true;
            if (ProfileNumericNavigation.ValidFloat(editor.Buffer, min, max, out reason))
                return true;
            ProfileNumericNavigation.RejectControl(editor.ControlName);
            return false;
        }

        private static bool ValidateNavigationProfile(ProfileInputBuffers input,
            ConsAnchorV2ProfileSettings profile, string focusedName, out string reason)
        {
            reason = "NONE";
            if (profile == null) { reason = "MISSING_PROFILE_SETTINGS"; return false; }
            return ValidateNavigationEditor(input.LowAnchor, focusedName, 5f, 95f, out reason) &&
                ValidateNavigationEditor(input.LowFactor, focusedName, 0f, 1f, out reason) &&
                ValidateNavigationEditor(input.HighAnchor, focusedName, 101f, 1000f, out reason) &&
                ValidateNavigationEditor(input.HighFactor, focusedName, 1f, 19.99f, out reason) &&
                ValidateNavigationEditor(input.DiminishingLimit, focusedName,
                    Mathf.Min(20f, profile.HighFactor + 0.01f), 20f, out reason) &&
                ValidateNavigationEditor(input.SafetyLimit, focusedName,
                    profile.HighFactor, 20f, out reason);
        }

        internal static bool ValidateNavigationDraft(XpCustomizerSettings settings,
            string focusedName, out string reason)
        {
            reason = "NONE";
            if (settings == null) { reason = "MISSING_SETTINGS"; return false; }
            return ValidateNavigationProfile(speedInputs, settings.consciousnessAnchorSpeed,
                    focusedName, out reason) &&
                ValidateNavigationProfile(capacityInputs, settings.consciousnessAnchorCapacity,
                    focusedName, out reason);
        }

        /// <summary>
        /// Parse the raw edit buffer without imposing bounds while the user types.
        /// The old TextFieldNumeric(min,max) path clamped every intermediate digit
        /// (for example: 170 -> erase -> 2 -> 101), making anchor editing unusable.
        /// </summary>
        internal static bool TryParseNumericValue(string raw, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value) &&
                !float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return false;
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        // Pure testable rule: unfinished/invalid text restores the last valid
        // setting, while a finite completed value is bounded only on commit.
        internal static float ResolveNumericCommit(string raw, float prior, float min, float max)
        {
            float candidate;
            return TryParseNumericValue(raw, out candidate)
                ? Mathf.Clamp(candidate, min, max)
                : Mathf.Clamp(prior, min, max);
        }

        private static void DrawNumber(Listing_Standard listing, string label,
            ref float stored, ConsNumericEditState state, float min, float max,
            float fallback, bool consciousnessPercent)
        {
            float scaled = consciousnessPercent ? stored * 100f : stored;
            float defaultValue = consciousnessPercent ? fallback * 100f : fallback;
            if (float.IsNaN(scaled) || float.IsInfinity(scaled)) scaled = defaultValue;
            scaled = Mathf.Clamp(scaled, min, max);

            // Only discard an old buffer when the field is not being edited.
            // Preset buttons explicitly call NotifySettingsExternallyChanged().
            if (!state.WasFocused &&
                (state.Buffer == null || float.IsNaN(state.LastValue) ||
                 !Mathf.Approximately(state.LastValue, scaled)))
                state.Buffer = scaled.ToString("0.####", CultureInfo.InvariantCulture);

            // Common column includes hidden curve-mode labels to avoid jumping.
            float groupWidth = NumericRowLayout.MaxTranslatedWidth(
                "XPC_Cons_LowAnchor", "XPC_Cons_LowFactor",
                "XPC_Cons_HighAnchor", "XPC_Cons_HighFactor",
                "XPC_Cons_DiminishingLimit", "XPC_Cons_SafetyCap");
            NumericRowLayout.Placement geometry = NumericRowLayout.Allocate(
                listing, label, 105f, 0f, 30f, 28f, groupWidth);
            Widgets.Label(geometry.Label, label);
            Rect fieldRect = geometry.Field;

            // Enter commits and relinquishes focus. Clicking another control
            // also commits on the next IMGUI event; no global GUI state touched.
            Event evt = Event.current;
            if (state.WasFocused && evt != null && evt.type == EventType.KeyDown &&
                (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter))
            {
                GUI.FocusControl(null);
                evt.Use();
            }

            GUI.SetNextControlName(state.ControlName);
            state.Buffer = Widgets.TextField(fieldRect, state.Buffer ?? string.Empty);
            bool focused = string.Equals(GUI.GetNameOfFocusedControl(),
                state.ControlName, StringComparison.Ordinal);

            float candidate;
            if (focused)
            {
                // In-range completed numbers update previews immediately;
                // partial or out-of-range text stays visible without clamping.
                if (TryParseNumericValue(state.Buffer, out candidate) &&
                    candidate >= min && candidate <= max &&
                    !Mathf.Approximately(candidate, scaled))
                    stored = consciousnessPercent ? candidate / 100f : candidate;
            }
            else if (state.WasFocused)
            {
                float committed = ResolveNumericCommit(state.Buffer, scaled, min, max);
                if (!Mathf.Approximately(committed, scaled))
                    stored = consciousnessPercent ? committed / 100f : committed;
                state.Buffer = committed.ToString("0.####", CultureInfo.InvariantCulture);
            }

            state.WasFocused = focused;
            state.LastValue = consciousnessPercent ? stored * 100f : stored;
        }

        internal static void NotifySettingsExternallyChanged()
        {
            speedInputs.Reset();
            capacityInputs.Reset();
        }
    }
}
