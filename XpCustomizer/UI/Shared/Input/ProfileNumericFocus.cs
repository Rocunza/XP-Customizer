using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace XpCustomizer.UI
{
    /// <summary>
    /// R6-P1-C2-B2-A1: observes ONLY XP Customizer-owned TextFieldNumeric controls.
    /// The underlying vanilla Widgets.TextFieldNumeric call and its commit/clamp
    /// behavior are unchanged. This class never focuses, clears, commits, saves,
    /// or applies a setting. All identities are local to this mod.
    /// </summary>
    internal static class ProfileNumericFocus
    {
        private const string Prefix = "ZCN.XpCustomizer.ProfileFocus.";
        private sealed class RawObservation
        {
            internal string Buffer;
            internal bool Focused;
            internal bool Observed;
            internal int ConsecutiveIdleObservations;
            internal bool Inactive;
            internal bool HadActiveDraw;
            internal bool UnsafeInactiveTransition;
        }

        private static readonly Dictionary<string, RawObservation> Raw =
            new Dictionary<string, RawObservation>(StringComparer.Ordinal);
        private static int nextControlId;

        // The last XP-owned widget draw supplies a read-only range snapshot.
        // No vanilla or foreign control is registered or inspected.
        private sealed class NavigationDraft
        {
            internal string Buffer;
            internal float Min;
            internal float Max;
            internal bool Integer;
            internal int IntMin;
            internal int IntMax;
            internal bool Focused;
        }
        private static readonly Dictionary<string, NavigationDraft> NavigationDrafts =
            new Dictionary<string, NavigationDraft>(StringComparer.Ordinal);
        private static string lastFocusedNumericControl;

        internal static bool IsNumericControl(string controlName)
        {
            return controlName != null &&
                controlName.StartsWith(Prefix, StringComparison.Ordinal);
        }

        private static void ObserveNavigationDraft(string name, string buffer,
            float min, float max, bool integer)
        {
            NavigationDraft state;
            if (!NavigationDrafts.TryGetValue(name, out state))
            {
                state = new NavigationDraft();
                NavigationDrafts.Add(name, state);
            }
            state.Buffer = buffer;
            state.Min = min;
            state.Max = max;
            state.Integer = integer;
            if (integer)
            {
                state.IntMin = (int)min;
                state.IntMax = (int)max;
            }
            state.Focused = String.Equals(GUI.GetNameOfFocusedControl(),
                name, StringComparison.Ordinal);
            if (state.Focused) lastFocusedNumericControl = name;
            else if (String.Equals(lastFocusedNumericControl, name,
                StringComparison.Ordinal)) lastFocusedNumericControl = null;
        }

        // If Widgets.ButtonText has already consumed focus on MouseUp, retain
        // the LAST observed owned numeric draft for safe pre-blur validation.
        // A previously blurred value is cleared by the owner on its next draw.
        private static void ObserveNavigationDraft(string name, string buffer,
            int min, int max, bool integer)
        {
            ObserveNavigationDraft(name, buffer, (float)min, (float)max, integer);
            NavigationDrafts[name].IntMin = min;
            NavigationDrafts[name].IntMax = max;
        }

        internal static bool ValidateLastFocusedDraft(out string invalidName,
            out string reason)
        {
            invalidName = null;
            reason = "NONE";
            if (String.IsNullOrEmpty(lastFocusedNumericControl)) return true;
            if (ValidateNavigationDraft(lastFocusedNumericControl, out reason)) return true;
            invalidName = lastFocusedNumericControl;
            return false;
        }

        internal static bool ValidateNavigationDraft(string controlName, out string reason)
        {
            reason = "NONE";
            NavigationDraft draft;
            if (!IsNumericControl(controlName) ||
                !NavigationDrafts.TryGetValue(controlName, out draft))
            {
                reason = "UNOBSERVED_NUMERIC_FOCUS";
                return false;
            }
            if (draft.Integer)
                return ProfileNumericNavigation.ValidInteger(draft.Buffer,
                    draft.IntMin, draft.IntMax, out reason);
            return ProfileNumericNavigation.ValidFloat(draft.Buffer,
                draft.Min, draft.Max, out reason);
        }

        internal static void Draw(Rect rect, ref float value, NumericInputState state,
            float min, float max)
        {
            if (state == null) throw new ArgumentNullException("state");
            string name = EnsureName(state);
            GUI.SetNextControlName(name);
            Widgets.TextFieldNumeric(rect, ref value, ref state.buffer, min, max);
            ObserveNavigationDraft(name, state.buffer, min, max, false);
            ObserveNumeric(state, name);
        }

        internal static void Draw(Rect rect, ref int value, NumericInputState state,
            int min, int max)
        {
            if (state == null) throw new ArgumentNullException("state");
            string name = EnsureName(state);
            GUI.SetNextControlName(name);
            Widgets.TextFieldNumeric(rect, ref value, ref state.buffer, min, max);
            ObserveNavigationDraft(name, state.buffer, min, max, true);
            ObserveNumeric(state, name);
        }

        internal static void Draw(Rect rect, ref float value, ref string buffer,
            string identity, float min, float max)
        {
            string name = RawName(identity);
            GUI.SetNextControlName(name);
            Widgets.TextFieldNumeric(rect, ref value, ref buffer, min, max);
            ObserveNavigationDraft(name, buffer, min, max, false);
            ObserveRaw(identity, buffer, string.Equals(
                GUI.GetNameOfFocusedControl(), name, StringComparison.Ordinal));
        }

        internal static void Draw(Rect rect, ref int value, ref string buffer,
            string identity, int min, int max)
        {
            string name = RawName(identity);
            GUI.SetNextControlName(name);
            Widgets.TextFieldNumeric(rect, ref value, ref buffer, min, max);
            ObserveNavigationDraft(name, buffer, min, max, true);
            ObserveRaw(identity, buffer, string.Equals(
                GUI.GetNameOfFocusedControl(), name, StringComparison.Ordinal));
        }

        // Bridges our pre-existing staged preview widget, which already has
        // its own control name and proven focused/commit behavior.
        internal static void ObserveExisting(string identity, string buffer, bool focused)
        {
            RawName(identity); // same identity policy, no GUI mutation
            ObserveRaw(identity, buffer, focused);
        }

        /// <summary>
        /// An XP-owned numeric buffer may be populated while its input is not drawn
        /// (for example, Decay OFF shows labels instead of numeric text fields).
        /// This is NOT a blanket exemption: only a buffer identical to the current
        /// committed display value can settle. A previously focused/unsettled or
        /// changed active buffer remains blocked until that field is drawn active
        /// again and settles. The observation never modifies settings or buffers.
        /// </summary>
        internal static void ObserveInactive(string identity, string buffer,
            string committedBuffer)
        {
            string name = RawName(identity);
            bool stillFocused = string.Equals(GUI.GetNameOfFocusedControl(),
                name, StringComparison.Ordinal);
            ObserveInactiveCore(identity, buffer, committedBuffer, stillFocused);
        }

        // Pure of Unity GUI state; callable from isolated tests with an explicit
        // focus flag. Does not touch game settings, widgets or files.
        internal static void ObserveInactiveCore(string identity, string buffer,
            string committedBuffer, bool stillFocused)
        {
            RawName(identity);
            RawObservation entry;
            if (!Raw.TryGetValue(identity, out entry))
            {
                entry = new RawObservation();
                Raw.Add(identity, entry);
            }
            bool canonical = string.Equals(buffer, committedBuffer, StringComparison.Ordinal);
            if (stillFocused || !canonical ||
                (entry.HadActiveDraw && !entry.Inactive &&
                 (entry.Focused || !entry.Observed ||
                  !string.Equals(entry.Buffer, buffer, StringComparison.Ordinal))) ||
                (entry.Inactive && !string.Equals(entry.Buffer, buffer, StringComparison.Ordinal)))
                entry.UnsafeInactiveTransition = true;

            // A previously unsafe draft must not turn Ready just because the
            // now-hidden field has been rendered as a label for several frames.
            if (entry.UnsafeInactiveTransition)
            {
                entry.ConsecutiveIdleObservations = 0;
                entry.Observed = false;
            }
            else
            {
                entry.ConsecutiveIdleObservations = NextIdleObservationCount(
                    entry.ConsecutiveIdleObservations, entry.Buffer, buffer, false);
                entry.Observed = entry.ConsecutiveIdleObservations >= 2;
            }
            entry.Inactive = true;
            entry.Focused = stillFocused;
            entry.Buffer = buffer;
        }

        /// <summary>
        /// R3-B: discard stale XP-owned focus observations only AFTER the
        /// user-approved Mod Default Values reset succeeded. Do not invoke on
        /// dialog open/cancel, failed preflight, or profile navigation.
        /// Editors reset their own text buffers through SettingsRefreshCoordinator.
        /// Keeping nextControlId unchanged preserves stable widget identities.
        /// </summary>
        internal static void ForgetAfterConfirmedModNumericReset()
        {
            Raw.Clear();
            NavigationDrafts.Clear();
            lastFocusedNumericControl = null;
        }

#if DEBUG
        // R4-HF1 isolated test seeding only. Never compiled into Release and
        // never interacts with the game GUI, settings, XML or profile files.
        internal static void DebugSeedStaleNumericNavigationDraft(string controlName,
            string invalidBuffer)
        {
            if (!IsNumericControl(controlName))
                throw new ArgumentException("Test control must be XP-owned", "controlName");
            NavigationDrafts[controlName] = new NavigationDraft
            {
                Buffer = invalidBuffer,
                Min = 0f,
                Max = 10f,
                Integer = false,
                Focused = true
            };
            lastFocusedNumericControl = controlName;
        }
#endif

        /// <summary>
        /// N1-R1: clean ONLY the one owned numeric control whose text was
        /// explicitly discarded by the user. Never call the global reset
        /// function for a single profile-navigation recovery.
        /// </summary>
        internal static void ForgetConfirmedNumericDraft(string controlName)
        {
            if (!IsNumericControl(controlName))
                throw new ArgumentException("Not an owned numeric control", "controlName");
            NavigationDrafts.Remove(controlName);
            if (String.Equals(lastFocusedNumericControl, controlName,
                StringComparison.Ordinal))
                lastFocusedNumericControl = null;
        }

        // The owning UI explicitly resets buffers on external settings changes.
        // Discard stale raw observations at the same time; scoped to that owner.
        internal static void ForgetRaw(string identity)
        {
            RawName(identity);
            Raw.Remove(identity);
        }

        // Used only for read-only runtime diagnostics, not as an eligibility gate.
        internal static bool IsInactiveRaw(string identity)
        {
            RawObservation entry;
            return identity != null && Raw.TryGetValue(identity, out entry) && entry.Inactive;
        }

        internal static bool TryGetRaw(string identity, string currentBuffer,
            out bool focused, out bool observedSameBuffer)
        {
            focused = false;
            observedSameBuffer = false;
            RawObservation entry;
            if (identity == null || !Raw.TryGetValue(identity, out entry)) return false;
            focused = entry.Focused;
            observedSameBuffer = entry.Observed &&
                string.Equals(entry.Buffer, currentBuffer, StringComparison.Ordinal);
            return entry.Observed;
        }

        // Pure state-policy primitive: no Unity access, suitable for self-tests.
        internal static bool IsSettled(bool observed, bool focused,
            string observedBuffer, string currentBuffer)
        {
            return observed && !focused &&
                string.Equals(observedBuffer, currentBuffer, StringComparison.Ordinal);
        }

        private static string EnsureName(NumericInputState state)
        {
            if (state.FocusControlName == null)
                state.FocusControlName = Prefix + "Numeric." + (++nextControlId);
            return state.FocusControlName;
        }

        private static string RawName(string identity)
        {
            if (string.IsNullOrEmpty(identity) || identity.Length > 200)
                throw new ArgumentException("Invalid XP Customizer control identity", "identity");
            return Prefix + "Raw." + identity;
        }

        private static void ObserveNumeric(NumericInputState state, string name)
        {
            bool focused = string.Equals(GUI.GetNameOfFocusedControl(),
                name, StringComparison.Ordinal);
            // Observe the same idle value repeatedly before attesting settlement.
            // A single transient focus change is not enough for an overwrite gate.
            state.IdleObservations = NextIdleObservationCount(
                state.IdleObservations, state.LastObservedBuffer, state.buffer, focused);
            state.FocusObserved = state.IdleObservations >= 2;
            state.WasFocused = focused;
            state.LastObservedBuffer = state.buffer;
        }

        internal static int NextIdleObservationCount(int current, string previous,
            string next, bool focused)
        {
            if (focused || !string.Equals(previous, next, StringComparison.Ordinal))
                return 0;
            return Math.Min(2, current + 1);
        }

        private static void ObserveRaw(string identity, string buffer, bool focused)
        {
            RawObservation entry;
            if (!Raw.TryGetValue(identity, out entry))
            {
                entry = new RawObservation();
                Raw.Add(identity, entry);
            }
            // Once the previously disabled control is rendered again, a full
            // stable unfocused sequence is required to clear any unsafe draft.
            if (entry.Inactive)
                entry.ConsecutiveIdleObservations = 0;
            entry.Inactive = false;
            entry.HadActiveDraw = true;
            entry.ConsecutiveIdleObservations = NextIdleObservationCount(
                entry.ConsecutiveIdleObservations, entry.Buffer, buffer, focused);
            if (!focused && entry.ConsecutiveIdleObservations >= 2)
                entry.UnsafeInactiveTransition = false;
            entry.Observed = !entry.UnsafeInactiveTransition &&
                entry.ConsecutiveIdleObservations >= 2;
            entry.Focused = focused;
            entry.Buffer = buffer;
        }
    }
}
