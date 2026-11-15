using System;
using System.Collections.Generic;
using XpCustomizer.Profiles;
using XpCustomizer.Progression;

namespace XpCustomizer.UI
{
    /// <summary>
    /// N1-R1: a one-use, narrowly scoped cancellation ticket for the malformed
    /// text draft in the TOTAL career XP multiplier widget. It is NOT a settings
    /// undo transaction. It never writes game settings or profile data.
    /// </summary>
    internal sealed class CareerTotalMultiplierRecovery
    {
        private readonly NumericInputState input;
        private readonly string controlName;
        private readonly string draft;
        private readonly XpCustomizerSettings settings;
        private readonly float committedValue;
        private bool consumed;

        private CareerTotalMultiplierRecovery(NumericInputState input,
            XpCustomizerSettings settings)
        {
            this.input = input;
            controlName = input.FocusControlName;
            draft = input.buffer;
            this.settings = settings;
            committedValue = settings.careerXpMultiplier;
        }

        internal string OwnedControlName { get { return controlName; } }

        internal static bool TryCapture(NumericInputState total,
            IList<NumericInputState> weights, IList<NumericInputState> locks,
            XpCustomizerSettings settings, out CareerTotalMultiplierRecovery ticket)
        {
            ticket = null;
            if (settings == null || !IsInvalidOwnedDraft(total) ||
                !AreOtherEditorsReady(weights) || !AreOtherEditorsReady(locks) ||
                !ValidCommittedValue(settings.careerXpMultiplier))
                return false;
            ticket = new CareerTotalMultiplierRecovery(total, settings);
            return true;
        }

        // Detached tests exercise this exact production method with local input
        // objects and isolated settings; there is no IMGUI or file interaction.
        internal bool TryDiscard(NumericInputState current,
            IList<NumericInputState> weights, IList<NumericInputState> locks,
            XpCustomizerSettings liveSettings)
        {
            if (consumed || !Object.ReferenceEquals(input, current) ||
                !Object.ReferenceEquals(settings, liveSettings) ||
                !String.Equals(controlName, current.FocusControlName,
                    StringComparison.Ordinal) ||
                !String.Equals(draft, current.buffer, StringComparison.Ordinal) ||
                !committedValue.Equals(liveSettings.careerXpMultiplier) ||
                !IsInvalidOwnedDraft(current) ||
                !AreOtherEditorsReady(weights) || !AreOtherEditorsReady(locks))
                return false;

            consumed = true; // consume before the only intended mutation
            current.Reset(); // text/observation ONLY; preserve control identity
            return true;
        }

        private static bool IsInvalidOwnedDraft(NumericInputState input)
        {
            return input != null && input.buffer != null &&
                ProfileNumericFocus.IsNumericControl(input.FocusControlName) &&
                ProfileEditorReadinessService.ClassifyTextBuffer(input.buffer,
                    true, false) == ProfileEditorReadinessCode.PendingEdit;
        }

        private static bool ValidCommittedValue(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) &&
                value >= CareerXpDistributionService.MinTotalMultiplier &&
                value <= CareerXpDistributionService.MaxTotalMultiplier;
        }

        private static bool AreOtherEditorsReady(IList<NumericInputState> inputs)
        {
            if (inputs == null) return false;
            for (int i = 0; i < inputs.Count; i++)
            {
                NumericInputState input = inputs[i];
                if (input == null ||
                    ProfileEditorReadinessService.ClassifyObservedText(
                        input.buffer, input.FocusObserved, input.WasFocused,
                        String.Equals(input.LastObservedBuffer, input.buffer,
                            StringComparison.Ordinal)) != ProfileEditorReadinessCode.Ready)
                    return false;
            }
            return true;
        }
    }
}
