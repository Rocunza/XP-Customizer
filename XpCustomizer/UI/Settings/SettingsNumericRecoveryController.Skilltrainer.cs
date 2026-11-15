using System;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    /// <summary>
    /// S2 production eligibility boundary. Never grants a capability based on
    /// the legacy first-pending-owner string alone; multiple/uncertain means no
    /// recovery. The original N1 readiness engine remains authoritative.
    /// </summary>
    internal static partial class SettingsNumericRecoveryController
    {
        internal const string S2Version = "XPC-N1-R2-S2-20261113-R1";
        private const string TrainerOwner = "SkilltrainerSettingsUI";
        private const string TrainerField = "SkilltrainerSettingsUI.multiplierInput";
        private const string TrainerReason = "PendingEdit_SkilltrainerSettingsUI";

        internal static bool IsSkilltrainerCandidate(SettingsTab tab, string reason,
            NumericDraftRecoveryObservation observation)
        {
            return NumericDraftRecoveryRegistry.HasSkilltrainerCapability() &&
                tab == SettingsTab.Progression &&
                String.Equals(reason, TrainerReason, StringComparison.Ordinal) &&
                observation != null &&
                observation.Code == NumericDraftRecoveryObservationCode.OneOwnerPending &&
                String.Equals(observation.OwnerId, TrainerOwner, StringComparison.Ordinal) &&
                String.Equals(observation.FieldId, TrainerField, StringComparison.Ordinal) &&
                observation.PendingFields == 1 && observation.UncertainFields == 0 &&
                observation.IdentifiedOwnedNumericControl;
        }

        internal static bool TryPrepareSkilltrainerRecovery(SettingsTab tab,
            string reason, out SkilltrainerNumericRecoveryTicket ticket)
        {
            ticket = null;
            NumericDraftRecoveryObservation observation;
            try { observation = NumericDraftRecoveryRegistry.InspectCurrentReadOnly(); }
            catch (Exception) { return false; }
            if (!IsSkilltrainerCandidate(tab, reason, observation)) return false;
            ProfileEditorReadinessResult readiness = ProfileEditorReadinessService.Check();
            if (readiness.Code != ProfileEditorReadinessCode.PendingEdit ||
                !String.Equals(readiness.Source, TrainerOwner, StringComparison.Ordinal))
                return false;
            if (!SkilltrainerSettingsUI.TryPrepareMultiplierRecovery(out ticket))
                return false;
            // Only the exact observed XP-owned input may be discarded.
            if (ticket == null || !ProfileNumericFocus.IsNumericControl(
                ticket.OwnedControlName) ||
                !String.Equals(ticket.OwnedControlName,
                    GetObservedOwnedControlForSkilltrainer(), StringComparison.Ordinal))
            {
                ticket = null; // never expose a partially validated consent ticket
                return false;
            }
            return true;
        }

        private static string GetObservedOwnedControlForSkilltrainer()
        {
            // Reinspect rather than trusting the earlier field identity alone.
            // The registry intentionally does not expose a dynamic control ID.
            ProfileEditorOwnerObservation[] owners =
                ProfileEditorReadinessService.InspectOwnersReadOnly();
            foreach (ProfileEditorOwnerObservation owner in owners)
                if (owner != null && owner.OwnerType == typeof(SkilltrainerSettingsUI) &&
                    owner.InspectionComplete && owner.PendingFields == 1 &&
                    owner.UncertainFields == 0 &&
                    String.Equals(owner.OnlyPendingField, TrainerField,
                        StringComparison.Ordinal))
                    return owner.OnlyPendingOwnedControl;
            return null;
        }

        internal static bool TryDiscardSkilltrainerRecovery(
            SkilltrainerNumericRecoveryTicket ticket)
        {
            if (ticket == null) return false;
            NumericDraftRecoveryObservation observation;
            try { observation = NumericDraftRecoveryRegistry.InspectCurrentReadOnly(); }
            catch (Exception) { return false; }
            if (!IsSkilltrainerCandidate(SettingsTab.Progression,
                TrainerReason, observation)) return false;
            ProfileEditorReadinessResult readiness = ProfileEditorReadinessService.Check();
            if (readiness.Code != ProfileEditorReadinessCode.PendingEdit ||
                !String.Equals(readiness.Source, TrainerOwner, StringComparison.Ordinal) ||
                !String.Equals(ticket.OwnedControlName,
                    GetObservedOwnedControlForSkilltrainer(), StringComparison.Ordinal))
                return false;
            return SkilltrainerSettingsUI.TryDiscardMultiplierRecovery(ticket);
        }
    }
}
