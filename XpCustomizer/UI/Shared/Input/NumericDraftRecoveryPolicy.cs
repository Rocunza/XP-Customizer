using System;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    // Capability is deliberately NOT a permission to discard a draft.
    // Every new owner starts in deferred mode until its own S2/S4/S5
    // capture/validation/cleanup and live confirmation have passed.
    internal enum NumericDraftRecoveryFamily
    {
        ExistingCareerTotalOnly,
        NumericSingleSkilltrainerActive,
        NumericSingleDeferred,
        NumericCollectionDeferred,
        PairedOrRawDeferred,
        StagedDeferred,
        IndependentDraftDeferred
    }

    internal enum NumericDraftRecoveryObservationCode
    {
        AllReady,
        OneOwnerPending,
        MultiplePending,
        Uncertain,
        InvalidRegistry
    }

    internal sealed class NumericDraftRecoveryOwner
    {
        internal readonly Type OwnerType;
        internal readonly string OwnerId;
        internal readonly NumericDraftRecoveryFamily Family;
        internal NumericDraftRecoveryOwner(Type type, string id,
            NumericDraftRecoveryFamily family)
        {
            if (type == null || String.IsNullOrEmpty(id))
                throw new ArgumentException("Recovery owner identity required");
            OwnerType = type;
            OwnerId = id;
            Family = family;
        }
    }

    internal sealed class NumericDraftRecoveryObservation
    {
        internal readonly NumericDraftRecoveryObservationCode Code;
        internal readonly string OwnerId;
        internal readonly string FieldId;
        internal readonly bool IdentifiedOwnedNumericControl;
        internal readonly int PendingFields;
        internal readonly int UncertainFields;

        internal NumericDraftRecoveryObservation(NumericDraftRecoveryObservationCode code,
            string ownerId, string fieldId, bool hasControl, int pending, int uncertain)
        {
            Code = code;
            OwnerId = ownerId ?? "NONE";
            FieldId = fieldId; // inspection only, not emitted to logs
            IdentifiedOwnedNumericControl = hasControl;
            PendingFields = pending;
            UncertainFields = uncertain;
        }
    }
}
