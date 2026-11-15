using System;

namespace XpCustomizer.Profiles
{
    /// <summary>
    /// D2-C1 only: pure, detached policy for a FUTURE profile Apply + XML commit.
    /// It does not create an authorization token, mutate settings, call Scribe,
    /// inspect files, or invoke any runtime adapter. Caller-provided evidence is
    /// a proposed contract, NOT proof that a live check has been performed.
    /// Actual evidence acquisition and verified disk I/O belong to C2/C3.
    /// </summary>
    internal enum ProfilePersistencePhase
    {
        ReadOnlyPreflight, ExplicitConfirmation, FinalRecheck,
        RuntimeApplyAttempt, RuntimeVerified, PersistAttempt,
        PersistVerified, Complete, NoChange, Cancelled, Rejected,
        Compensated, RuntimeUnrecovered, PersistenceUncertain
    }

    internal enum ProfilePersistenceDecision
    {
        AllowProposed, NoChange, Cancelled, Complete,
        BlockPendingEditor, BlockUncertainEditor,
        BlockStaleProfileHash, BlockStaleProfileMetadata,
        BlockStaleLiveSettings, BlockRuntimeRecovery,
        BlockProfileStorageRecovery, BlockAlreadyConsumed,
        BlockReentrant, BlockInvalidCandidate, BlockUnsupportedSchema,
        BlockPersistenceUncertain, BlockInvalidTransition,
        RuntimeRollbackVerified, RuntimeUnrecovered,
        PersistenceFailedKnownUnchanged, ErrorUnexpected
    }

    internal enum ProfilePersistenceEditorState { Ready, Pending, Uncertain }
    internal enum ProfilePersistenceRuntimeOutcome
    {
        AppliedAndVerified, RejectedWithoutWrites,
        RolledBackAndVerified, Unrecovered, AppliedButUnverified
    }
    internal enum ProfilePersistenceDiskOutcome
    {
        WrittenAndVerified, FailedKnownUnchanged, FailedOrUnknown
    }

    /// <summary>
    /// Evidence presented by a future C2 adapter after real D1/D2 and file
    /// checks. In C1 it is populated ONLY by isolated tests. All approval flags
    /// default FALSE so incomplete evidence always fails closed. The policy
    /// copies the evidence rather than trusting subsequent caller mutations.
    /// </summary>
    internal sealed class ProfilePersistenceEvidence
    {
        internal string ProfileId;
        internal string SelectedSha256;
        internal string DisplayName;
        internal DateTime CreatedUtc;
        internal DateTime UpdatedUtc;
        internal string SourceGameVersion;
        internal int ProfileFormatVersion;
        internal int SettingsSchemaVersion;
        internal string StrictBaselineFingerprint;
        internal string DetachedCandidateFingerprint;
        internal bool PortableFieldsDiffer;
        internal bool SelectedHashMatchesDisk;
        internal bool MetadataMatchesDisk;
        internal bool StrictLiveValuesMatch;
        internal bool StrictLiveReferencesMatch;
        internal bool DetachedCandidateValid;
        internal bool CandidateIsDetached;
        internal bool NonPortableBaselinePreserved;
        internal bool UnknownSkillKeysPreserved;
        internal bool EmptyCareerSplitsPreserved;
        internal bool DisabledValuesPreserved;
        internal bool DictionaryNormalizationDetachedOnly;
        internal ProfilePersistenceEditorState EditorState;
        internal bool RuntimeRecoveryBlocked;
        internal bool ProfileStorageRecoveryBlocked;
        internal bool PersistenceUncertain;
        internal bool Reentrant;

        internal ProfilePersistenceEvidence Copy()
        {
            return (ProfilePersistenceEvidence)MemberwiseClone();
        }
    }

    internal sealed class ProfileApplyPersistenceContract
    {
        internal const string Revision = "R6P1D2C1-PURE-COMMIT-CONTRACT-20261107";
        private ProfilePersistenceEvidence frozen;
        private bool confirmationConsumed;
        private bool runtimeMutationMayHaveOccurred;
        private bool persistenceMayHaveBeenAttempted;
        private bool runtimeVerified;
        private bool diskVerified;
        private bool diskStateUncertain;
        private ProfilePersistenceDecision firstFailure = ProfilePersistenceDecision.AllowProposed;
        private string firstReason = "NONE";

        internal ProfilePersistencePhase Phase { get; private set; }
        internal ProfilePersistenceDecision LastDecision { get; private set; }
        internal bool ConfirmationConsumed { get { return confirmationConsumed; } }
        internal bool RuntimeMutationMayHaveOccurred { get { return runtimeMutationMayHaveOccurred; } }
        internal bool PersistenceMayHaveBeenAttempted { get { return persistenceMayHaveBeenAttempted; } }
        internal bool RuntimeVerified { get { return runtimeVerified; } }
        internal bool DiskVerified { get { return diskVerified; } }
        internal bool DiskStateUncertain { get { return diskStateUncertain; } }
        internal ProfilePersistenceDecision FirstFailure { get { return firstFailure; } }
        internal string FirstFailureReason { get { return firstReason; } }
        internal bool Completed { get { return Phase == ProfilePersistencePhase.Complete; } }

        internal ProfileApplyPersistenceContract()
        {
            Phase = ProfilePersistencePhase.ReadOnlyPreflight;
            LastDecision = ProfilePersistenceDecision.BlockInvalidTransition;
        }

        // This is a decision about the existing vanilla Mod.WriteSettings path,
        // never a replacement or extra call. Profile editor state and other
        // profile locks must NOT block ordinary healthy settings saves.
        internal static bool NormalSettingsSavePolicy(bool runtimeRecoveryBlocked)
        {
            return !runtimeRecoveryBlocked;
        }

        internal ProfilePersistenceDecision Prepare(ProfilePersistenceEvidence evidence)
        {
            if (Phase != ProfilePersistencePhase.ReadOnlyPreflight)
                return RejectTransition("PREPARE_TWICE");
            ProfilePersistenceDecision decision = Validate(evidence);
            if (decision == ProfilePersistenceDecision.NoChange)
            {
                Phase = ProfilePersistencePhase.NoChange;
                return Set(ProfilePersistenceDecision.NoChange);
            }
            if (decision != ProfilePersistenceDecision.AllowProposed)
                return Stop(decision, "INITIAL_PREFLIGHT");
            frozen = evidence.Copy();
            Phase = ProfilePersistencePhase.ExplicitConfirmation;
            return Set(ProfilePersistenceDecision.AllowProposed);
        }

        // Consumes the local pure confirmation exactly once, even when denied.
        // This DOES NOT issue a production live-Apply token.
        internal ProfilePersistenceDecision Confirm(bool explicitUserConsent)
        {
            if (confirmationConsumed)
                return Stop(ProfilePersistenceDecision.BlockAlreadyConsumed, "CONSENT_REUSED");
            if (Phase != ProfilePersistencePhase.ExplicitConfirmation)
                return RejectTransition("CONFIRM_WRONG_PHASE");
            confirmationConsumed = true;
            if (!explicitUserConsent)
            {
                Phase = ProfilePersistencePhase.Cancelled;
                return Stop(ProfilePersistenceDecision.Cancelled, "USER_CANCELLED");
            }
            Phase = ProfilePersistencePhase.FinalRecheck;
            return Set(ProfilePersistenceDecision.AllowProposed);
        }

        internal ProfilePersistenceDecision Recheck(ProfilePersistenceEvidence evidence)
        {
            if (Phase != ProfilePersistencePhase.FinalRecheck || !confirmationConsumed || frozen == null)
                return RejectTransition("RECHECK_WRONG_PHASE");
            ProfilePersistenceDecision status = Validate(evidence);
            if (status != ProfilePersistenceDecision.AllowProposed)
                return Stop(status, "FINAL_PREFLIGHT");
            if (!SameSourceIdentity(frozen, evidence))
                return Stop(ProfilePersistenceDecision.BlockStaleProfileHash, "SOURCE_IDENTITY_CHANGED");
            if (!SameSourceMetadata(frozen, evidence))
                return Stop(ProfilePersistenceDecision.BlockStaleProfileMetadata, "SOURCE_METADATA_CHANGED");
            if (!String.Equals(frozen.StrictBaselineFingerprint,
                evidence.StrictBaselineFingerprint, StringComparison.Ordinal) ||
                !String.Equals(frozen.DetachedCandidateFingerprint,
                evidence.DetachedCandidateFingerprint, StringComparison.Ordinal))
                return Stop(ProfilePersistenceDecision.BlockStaleLiveSettings, "SNAPSHOT_FINGERPRINT_CHANGED");
            Phase = ProfilePersistencePhase.RuntimeApplyAttempt;
            return Set(ProfilePersistenceDecision.AllowProposed);
        }

        internal ProfilePersistenceDecision ObserveRuntime(ProfilePersistenceRuntimeOutcome outcome)
        {
            if (Phase != ProfilePersistencePhase.RuntimeApplyAttempt)
                return RejectTransition("RUNTIME_WRONG_PHASE");
            switch (outcome)
            {
                case ProfilePersistenceRuntimeOutcome.AppliedAndVerified:
                    runtimeMutationMayHaveOccurred = true;
                    runtimeVerified = true;
                    Phase = ProfilePersistencePhase.RuntimeVerified;
                    return Set(ProfilePersistenceDecision.AllowProposed);
                case ProfilePersistenceRuntimeOutcome.RejectedWithoutWrites:
                    return Stop(ProfilePersistenceDecision.BlockInvalidCandidate,
                        "RUNTIME_REJECTED_NO_WRITES");
                case ProfilePersistenceRuntimeOutcome.RolledBackAndVerified:
                    runtimeMutationMayHaveOccurred = true;
                    Phase = ProfilePersistencePhase.Compensated;
                    return Stop(ProfilePersistenceDecision.RuntimeRollbackVerified,
                        "RUNTIME_ROLLBACK_VERIFIED_NO_XML_WRITE");
                default:
                    runtimeMutationMayHaveOccurred = true;
                    Phase = ProfilePersistencePhase.RuntimeUnrecovered;
                    return Stop(ProfilePersistenceDecision.RuntimeUnrecovered,
                        "RUNTIME_NOT_VERIFIED");
            }
        }

        internal ProfilePersistenceDecision RequestPersistence()
        {
            if (Phase != ProfilePersistencePhase.RuntimeVerified || !runtimeVerified ||
                persistenceMayHaveBeenAttempted)
                return RejectTransition("PERSIST_NOT_ALLOWED");
            // Mark I/O uncertain BEFORE calling any future writer. An exception
            // or process failure during C2 cannot be reported as a clean no-op.
            persistenceMayHaveBeenAttempted = true;
            diskStateUncertain = true; // in-flight is NOT known unchanged
            Phase = ProfilePersistencePhase.PersistAttempt;
            return Set(ProfilePersistenceDecision.AllowProposed);
        }

        internal ProfilePersistenceDecision ObservePersistence(ProfilePersistenceDiskOutcome outcome)
        {
            if (Phase != ProfilePersistencePhase.PersistAttempt || !persistenceMayHaveBeenAttempted)
                return RejectTransition("PERSIST_RESULT_WRONG_PHASE");
            if (outcome == ProfilePersistenceDiskOutcome.WrittenAndVerified)
            {
                diskVerified = true;
                diskStateUncertain = false;
                Phase = ProfilePersistencePhase.PersistVerified;
                return Set(ProfilePersistenceDecision.AllowProposed);
            }
            if (outcome == ProfilePersistenceDiskOutcome.FailedKnownUnchanged)
            {
                diskStateUncertain = false;
                Phase = ProfilePersistencePhase.Rejected;
                return Stop(ProfilePersistenceDecision.PersistenceFailedKnownUnchanged,
                    "XML_KNOWN_UNCHANGED_RUNTIME_COMPENSATION_REQUIRED");
            }
            Phase = ProfilePersistencePhase.PersistenceUncertain;
            return Stop(ProfilePersistenceDecision.BlockPersistenceUncertain,
                "XML_STATE_UNKNOWN");
        }

        // Only after a failed persistence outcome, a future adapter may submit
        // proof of runtime compensation. This NEVER clears unknown disk state.
        internal ProfilePersistenceDecision ObserveCompensation(bool runtimeRollbackVerified)
        {
            if (!persistenceMayHaveBeenAttempted ||
                (Phase != ProfilePersistencePhase.Rejected &&
                 Phase != ProfilePersistencePhase.PersistenceUncertain))
                return RejectTransition("COMPENSATION_WRONG_PHASE");
            if (!runtimeRollbackVerified)
            {
                Phase = ProfilePersistencePhase.RuntimeUnrecovered;
                return Stop(ProfilePersistenceDecision.RuntimeUnrecovered,
                    "COMPENSATION_NOT_VERIFIED");
            }
            runtimeVerified = false; // Applied state is no longer in effect.
            if (Phase == ProfilePersistencePhase.PersistenceUncertain)
                return Set(ProfilePersistenceDecision.BlockPersistenceUncertain);
            Phase = ProfilePersistencePhase.Compensated;
            return Set(ProfilePersistenceDecision.RuntimeRollbackVerified);
        }

        internal ProfilePersistenceDecision Finish()
        {
            if (Phase != ProfilePersistencePhase.PersistVerified ||
                !confirmationConsumed || !runtimeVerified || !diskVerified || diskStateUncertain)
                return RejectTransition("COMPLETE_REQUIRES_RUNTIME_AND_XML_VERIFIED");
            Phase = ProfilePersistencePhase.Complete;
            return Set(ProfilePersistenceDecision.Complete);
        }

        private ProfilePersistenceDecision Stop(ProfilePersistenceDecision code, string reason)
        {
            if (firstReason == "NONE")
            {
                firstFailure = code;
                firstReason = reason;
            }
            // Do not turn an uncertain disk or unrecovered runtime into a
            // benign rejection via a later duplicate/wrong-phase call.
            if (Phase == ProfilePersistencePhase.PersistAttempt && !diskVerified)
                Phase = ProfilePersistencePhase.PersistenceUncertain;
            if (Phase != ProfilePersistencePhase.NoChange &&
                Phase != ProfilePersistencePhase.Cancelled &&
                Phase != ProfilePersistencePhase.Complete &&
                Phase != ProfilePersistencePhase.Compensated &&
                Phase != ProfilePersistencePhase.RuntimeUnrecovered &&
                Phase != ProfilePersistencePhase.PersistenceUncertain)
                Phase = ProfilePersistencePhase.Rejected;
            return Set(code);
        }

        private ProfilePersistenceDecision RejectTransition(string reason)
        {
            // Any phase misuse is fail-closed. Sticky uncertain/unrecovered
            // terminal phases retain their meaning rather than being downgraded.
            return Stop(ProfilePersistenceDecision.BlockInvalidTransition, reason);
        }

        private ProfilePersistenceDecision Set(ProfilePersistenceDecision decision)
        {
            LastDecision = decision;
            return decision;
        }

        private static bool HexHash(string hash)
        {
            if (hash == null || hash.Length != 64) return false;
            for (int i = 0; i < hash.Length; i++)
            {
                char c = hash[i];
                if ((c < '0' || c > '9') && (c < 'a' || c > 'f')) return false;
            }
            return true;
        }

        private static bool SameSourceIdentity(ProfilePersistenceEvidence a, ProfilePersistenceEvidence b)
        {
            return String.Equals(a.ProfileId, b.ProfileId, StringComparison.Ordinal) &&
                String.Equals(a.SelectedSha256, b.SelectedSha256, StringComparison.Ordinal);
        }

        private static bool SameSourceMetadata(ProfilePersistenceEvidence a, ProfilePersistenceEvidence b)
        {
            return String.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) &&
                a.CreatedUtc == b.CreatedUtc && a.UpdatedUtc == b.UpdatedUtc &&
                String.Equals(a.SourceGameVersion, b.SourceGameVersion, StringComparison.Ordinal) &&
                a.ProfileFormatVersion == b.ProfileFormatVersion &&
                a.SettingsSchemaVersion == b.SettingsSchemaVersion;
        }

        private static ProfilePersistenceDecision Validate(ProfilePersistenceEvidence e)
        {
            if (e == null) return ProfilePersistenceDecision.BlockInvalidCandidate;
            if (e.Reentrant) return ProfilePersistenceDecision.BlockReentrant;
            if (e.RuntimeRecoveryBlocked) return ProfilePersistenceDecision.BlockRuntimeRecovery;
            if (e.ProfileStorageRecoveryBlocked)
                return ProfilePersistenceDecision.BlockProfileStorageRecovery;
            if (e.PersistenceUncertain) return ProfilePersistenceDecision.BlockPersistenceUncertain;

            Guid id;
            if (!Guid.TryParse(e.ProfileId, out id) ||
                !String.Equals(id.ToString("D"), e.ProfileId, StringComparison.Ordinal) ||
                !HexHash(e.SelectedSha256) ||
                !HexHash(e.StrictBaselineFingerprint) ||
                !HexHash(e.DetachedCandidateFingerprint) ||
                String.IsNullOrWhiteSpace(e.DisplayName) ||
                String.IsNullOrWhiteSpace(e.SourceGameVersion) ||
                e.CreatedUtc.Kind != DateTimeKind.Utc ||
                e.UpdatedUtc.Kind != DateTimeKind.Utc ||
                e.UpdatedUtc < e.CreatedUtc)
                return ProfilePersistenceDecision.BlockInvalidCandidate;
            if ((e.ProfileFormatVersion != ProfileSchemaV1.FormatVersion &&
                 e.ProfileFormatVersion != ProfileFormatV2.FormatVersion) ||
                e.SettingsSchemaVersion != 1)
                return ProfilePersistenceDecision.BlockUnsupportedSchema;
            if (!e.SelectedHashMatchesDisk) return ProfilePersistenceDecision.BlockStaleProfileHash;
            if (!e.MetadataMatchesDisk) return ProfilePersistenceDecision.BlockStaleProfileMetadata;
            if (!e.StrictLiveValuesMatch || !e.StrictLiveReferencesMatch)
                return ProfilePersistenceDecision.BlockStaleLiveSettings;
            if (e.EditorState == ProfilePersistenceEditorState.Pending)
                return ProfilePersistenceDecision.BlockPendingEditor;
            if (e.EditorState != ProfilePersistenceEditorState.Ready)
                return ProfilePersistenceDecision.BlockUncertainEditor;
            if (!e.DetachedCandidateValid || !e.CandidateIsDetached ||
                !e.NonPortableBaselinePreserved || !e.UnknownSkillKeysPreserved ||
                !e.EmptyCareerSplitsPreserved || !e.DisabledValuesPreserved ||
                !e.DictionaryNormalizationDetachedOnly)
                return ProfilePersistenceDecision.BlockInvalidCandidate;
            if (!e.PortableFieldsDiffer) return ProfilePersistenceDecision.NoChange;
            return ProfilePersistenceDecision.AllowProposed;
        }
    }
}
