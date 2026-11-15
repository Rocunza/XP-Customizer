using System;
using System.Collections.Generic;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    /// <summary>
    /// S1/S2: explicit owner registry. Inspection never mutates an editor.
    /// Only individually verified R1 Career and S2 Skilltrainer tickets may
    /// discard their own text with explicit user consent.
    /// </summary>
    internal static class NumericDraftRecoveryRegistry
    {
        internal const string Version = "XPC-N1-R2R3-S1-20261113-R1";
        private static readonly NumericDraftRecoveryOwner[] Owners = {
            new NumericDraftRecoveryOwner(typeof(CareerProgressionSettingsUI),
                "CareerProgressionSettingsUI", NumericDraftRecoveryFamily.ExistingCareerTotalOnly),
            new NumericDraftRecoveryOwner(typeof(CareerXpStageBoundaryEditorUI),
                "CareerXpStageBoundaryEditorUI", NumericDraftRecoveryFamily.IndependentDraftDeferred),
            new NumericDraftRecoveryOwner(typeof(BookXpSettingsUI),
                "BookXpSettingsUI", NumericDraftRecoveryFamily.PairedOrRawDeferred),
            new NumericDraftRecoveryOwner(typeof(DecaySettingsUI),
                "DecaySettingsUI", NumericDraftRecoveryFamily.PairedOrRawDeferred),
            new NumericDraftRecoveryOwner(typeof(DecayModifiersSettingsUI),
                "DecayModifiersSettingsUI", NumericDraftRecoveryFamily.PairedOrRawDeferred),
            new NumericDraftRecoveryOwner(typeof(SkillSpecificXpSettingsUI),
                "SkillSpecificXpSettingsUI", NumericDraftRecoveryFamily.NumericCollectionDeferred),
            new NumericDraftRecoveryOwner(typeof(SkilltrainerSpecificXpSettingsUI),
                "SkilltrainerSpecificXpSettingsUI", NumericDraftRecoveryFamily.NumericCollectionDeferred),
            new NumericDraftRecoveryOwner(typeof(SkilltrainerSettingsUI),
                "SkilltrainerSettingsUI", NumericDraftRecoveryFamily.NumericSingleSkilltrainerActive),
            new NumericDraftRecoveryOwner(typeof(Level20ReserveSettingsUI),
                "Level20ReserveSettingsUI", NumericDraftRecoveryFamily.NumericSingleDeferred),
            new NumericDraftRecoveryOwner(typeof(LearningCapacitySettingsUI),
                "LearningCapacitySettingsUI", NumericDraftRecoveryFamily.NumericCollectionDeferred),
            new NumericDraftRecoveryOwner(typeof(FurtherLearningSettingsUI),
                "FurtherLearningSettingsUI", NumericDraftRecoveryFamily.NumericSingleDeferred),
            new NumericDraftRecoveryOwner(typeof(GwsSettingsUI),
                "GwsSettingsUI", NumericDraftRecoveryFamily.StagedDeferred),
            new NumericDraftRecoveryOwner(typeof(ConsciousnessAnchorV2SettingsUI),
                "ConsciousnessAnchorV2SettingsUI", NumericDraftRecoveryFamily.StagedDeferred),
            new NumericDraftRecoveryOwner(typeof(ConsciousnessFurtherLearningSettingsUI),
                "ConsciousnessFurtherLearningSettingsUI", NumericDraftRecoveryFamily.StagedDeferred),
            new NumericDraftRecoveryOwner(typeof(BiotechSettingsUI),
                "BiotechSettingsUI", NumericDraftRecoveryFamily.StagedDeferred)
        };

        internal static int OwnerCount { get { return Owners.Length; } }
        internal static bool HasSkilltrainerCapability()
        {
            int found = 0;
            foreach (NumericDraftRecoveryOwner owner in Owners)
                if (owner.OwnerType == typeof(SkilltrainerSettingsUI) &&
                    owner.OwnerId == "SkilltrainerSettingsUI" &&
                    owner.Family == NumericDraftRecoveryFamily.NumericSingleSkilltrainerActive)
                    found++;
            return found == 1;
        }
        internal static NumericDraftRecoveryOwner[] CopyOwners()
        {
            return (NumericDraftRecoveryOwner[])Owners.Clone();
        }

        // Pure assessment, including test fixtures. A duplicate/missing owner,
        // multiple pending fields or any uncertain editor is never resolved to
        // a unique candidate. No ProfileNumericFocus cache is changed.
        internal static NumericDraftRecoveryObservation Assess(
            ProfileEditorOwnerObservation[] observations)
        {
            if (observations == null || observations.Length != Owners.Length)
                return Fail(NumericDraftRecoveryObservationCode.InvalidRegistry, 0, 0);
            HashSet<Type> seen = new HashSet<Type>();
            int pending = 0, uncertain = 0, pendingOwners = 0;
            NumericDraftRecoveryOwner candidate = null;
            ProfileEditorOwnerObservation candidateState = null;
            foreach (ProfileEditorOwnerObservation state in observations)
            {
                if (state == null || state.OwnerType == null ||
                    !seen.Add(state.OwnerType))
                    return Fail(NumericDraftRecoveryObservationCode.InvalidRegistry,
                        pending, uncertain);
                NumericDraftRecoveryOwner match = null;
                foreach (NumericDraftRecoveryOwner owner in Owners)
                    if (owner.OwnerType == state.OwnerType) { match = owner; break; }
                if (match == null)
                    return Fail(NumericDraftRecoveryObservationCode.InvalidRegistry,
                        pending, uncertain);
                if (!state.InspectionComplete) uncertain++;
                uncertain += state.UncertainFields;
                pending += state.PendingFields;
                if (state.PendingFields > 0)
                {
                    pendingOwners++;
                    candidate = match;
                    candidateState = state;
                }
            }
            if (seen.Count != Owners.Length)
                return Fail(NumericDraftRecoveryObservationCode.InvalidRegistry,
                    pending, uncertain);
            if (uncertain > 0)
                return Fail(NumericDraftRecoveryObservationCode.Uncertain, pending, uncertain);
            if (pending > 1 || pendingOwners > 1)
                return Fail(NumericDraftRecoveryObservationCode.MultiplePending,
                    pending, uncertain);
            if (pending == 1 && candidate != null && candidateState != null)
                return new NumericDraftRecoveryObservation(
                    NumericDraftRecoveryObservationCode.OneOwnerPending,
                    candidate.OwnerId, candidateState.OnlyPendingField,
                    ProfileNumericFocus.IsNumericControl(candidateState.OnlyPendingOwnedControl),
                    pending, uncertain);
            return Fail(NumericDraftRecoveryObservationCode.AllReady, 0, 0);
        }

        internal static NumericDraftRecoveryObservation InspectCurrentReadOnly()
        {
            return Assess(ProfileEditorReadinessService.InspectOwnersReadOnly());
        }

        private static NumericDraftRecoveryObservation Fail(
            NumericDraftRecoveryObservationCode code, int pending, int uncertain)
        {
            return new NumericDraftRecoveryObservation(code, "NONE", null,
                false, pending, uncertain);
        }
    }
}
