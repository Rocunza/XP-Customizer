using System;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    /// <summary>
    /// S2: a one-use cancellation ticket for the Skilltrainer TOTAL multiplier's
    /// unfinished string. Not an undo or a setting/profile transaction.
    /// </summary>
    internal sealed class SkilltrainerNumericRecoveryTicket
    {
        private readonly NumericInputState input;
        private readonly XpCustomizerSettings settings;
        private readonly string draft;
        private readonly string controlName;
        private readonly float acceptedValue;
        private bool consumed;

        private SkilltrainerNumericRecoveryTicket(NumericInputState input,
            XpCustomizerSettings settings)
        {
            this.input = input;
            this.settings = settings;
            draft = input.buffer;
            controlName = input.FocusControlName;
            acceptedValue = settings.skilltrainerXpMultiplier;
        }

        internal string OwnedControlName { get { return controlName; } }

        internal static bool TryCapture(NumericInputState input,
            XpCustomizerSettings settings, out SkilltrainerNumericRecoveryTicket ticket)
        {
            ticket = null;
            if (settings == null || !IsInvalidOwnedDraft(input) ||
                !IsValidAcceptedValue(settings.skilltrainerXpMultiplier))
                return false;
            ticket = new SkilltrainerNumericRecoveryTicket(input, settings);
            return true;
        }

        // The caller independently checks ALL fifteen editor owners before this
        // operation. This method never cleans sibling input or any game setting.
        internal bool TryDiscard(NumericInputState current,
            XpCustomizerSettings liveSettings)
        {
            if (consumed || current == null || liveSettings == null ||
                !Object.ReferenceEquals(input, current) ||
                !Object.ReferenceEquals(settings, liveSettings) ||
                !String.Equals(draft, current.buffer, StringComparison.Ordinal) ||
                !String.Equals(controlName, current.FocusControlName,
                    StringComparison.Ordinal) ||
                !acceptedValue.Equals(liveSettings.skilltrainerXpMultiplier) ||
                !IsValidAcceptedValue(liveSettings.skilltrainerXpMultiplier) ||
                !IsInvalidOwnedDraft(current))
                return false;
            consumed = true;
            current.Reset(); // buffer + observation only, preserve control ID
            return true;
        }

        private static bool IsInvalidOwnedDraft(NumericInputState value)
        {
            return value != null && value.buffer != null &&
                ProfileNumericFocus.IsNumericControl(value.FocusControlName) &&
                ProfileEditorReadinessService.ClassifyTextBuffer(value.buffer,
                    true, false) == ProfileEditorReadinessCode.PendingEdit;
        }

        private static bool IsValidAcceptedValue(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) &&
                value >= XpCustomizerDefaults.ProgressionMultiplierMin &&
                value <= XpCustomizerDefaults.ProgressionMultiplierMax;
        }
    }
}
