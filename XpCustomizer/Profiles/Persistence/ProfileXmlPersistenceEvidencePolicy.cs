using System;

namespace XpCustomizer.Profiles
{
    /// <summary>
    /// D2-C2: isolated XML-write evidence classifier for a future profile Apply.
    /// NO Scribe, file I/O, settings mutation or live authorization is implemented here.
    /// All hashes and assurances are supplied by an independent caller, so this policy
    /// alone is NOT proof of real Verse.Mod.WriteSettings behavior or OS durability.
    /// </summary>
    internal enum ProfileXmlEvidencePhase
    {
        New, Prepared, WriteInFlight, WrittenVerified, FailedKnownUnchanged,
        Uncertain, RestoredButUncertain, Rejected
    }

    internal enum ProfileXmlEvidenceResult
    {
        Prepared, WriteAllowed, WrittenVerified, FailedKnownUnchanged,
        PersistenceUncertain, RestoreVerifiedButUncertain,
        RestoreUnverified, BlockedInvalidEvidence, BlockedChangedTarget,
        BlockedInvalidTransition
    }

    internal sealed class ProfileXmlPersistenceEvidencePolicy
    {
        internal const string Revision = "R6P1D2C2-ISOLATED-XML-FAULT-EVIDENCE-20261107";
        private string originalSha;
        private string proposedSha;
        private string firstFailure = "NONE";
        private bool mayHaveWritten;
        private bool uncertaintyEverObserved;

        internal ProfileXmlEvidencePhase Phase { get; private set; }
        internal ProfileXmlEvidenceResult LastResult { get; private set; }
        internal string FirstFailure { get { return firstFailure; } }
        internal bool WriteAttempted { get { return mayHaveWritten; } }
        internal bool DiskStateUncertain
        {
            get
            {
                return Phase == ProfileXmlEvidencePhase.WriteInFlight ||
                    Phase == ProfileXmlEvidencePhase.Uncertain ||
                    Phase == ProfileXmlEvidencePhase.RestoredButUncertain;
            }
        }
        internal bool UncertaintyEverObserved { get { return uncertaintyEverObserved; } }
        internal bool HasVerifiedNewXml { get { return Phase == ProfileXmlEvidencePhase.WrittenVerified; } }
        internal bool HasVerifiedOldXmlAfterCompensation
        {
            get { return Phase == ProfileXmlEvidencePhase.RestoredButUncertain; }
        }

        internal ProfileXmlPersistenceEvidencePolicy()
        {
            Phase = ProfileXmlEvidencePhase.New;
            LastResult = ProfileXmlEvidenceResult.BlockedInvalidTransition;
        }

        /// <summary>
        /// Independent read-back of the target AND retained backup must match.
        /// This step must run before requesting the actual persistence attempt.
        /// It NEVER creates, locates or touches the XP Customizer settings file.
        /// </summary>
        internal ProfileXmlEvidenceResult Prepare(
            string targetBeforeSha, string retainedBackupSha, string proposedSha,
            bool backupRetainedAndReadBack)
        {
            if (Phase != ProfileXmlEvidencePhase.New)
                return InvalidTransition("PREPARE_REPEATED");
            if (!ValidSha(targetBeforeSha) || !ValidSha(retainedBackupSha) ||
                !ValidSha(proposedSha) || !backupRetainedAndReadBack ||
                !String.Equals(targetBeforeSha, retainedBackupSha, StringComparison.Ordinal) ||
                String.Equals(targetBeforeSha, proposedSha, StringComparison.Ordinal))
            {
                Phase = ProfileXmlEvidencePhase.Rejected;
                return Failure(ProfileXmlEvidenceResult.BlockedInvalidEvidence,
                    "PREPARE_SNAPSHOT_OR_BACKUP_NOT_VERIFIED");
            }
            originalSha = targetBeforeSha;
            this.proposedSha = proposedSha;
            Phase = ProfileXmlEvidencePhase.Prepared;
            return Set(ProfileXmlEvidenceResult.Prepared);
        }

        /// <summary>
        /// Recheck the target immediately before writing. Once approved, mark
        /// the state UNCERTAIN before an external writer may open/truncate it.
        /// </summary>
        internal ProfileXmlEvidenceResult BeginWrite(string currentTargetSha)
        {
            if (Phase != ProfileXmlEvidencePhase.Prepared)
                return InvalidTransition("BEGIN_WRONG_PHASE");
            if (!ValidSha(currentTargetSha) ||
                !String.Equals(currentTargetSha, originalSha, StringComparison.Ordinal))
            {
                Phase = ProfileXmlEvidencePhase.Rejected;
                return Failure(ProfileXmlEvidenceResult.BlockedChangedTarget,
                    "TARGET_CHANGED_BEFORE_WRITE");
            }
            mayHaveWritten = true;
            uncertaintyEverObserved = true;
            Phase = ProfileXmlEvidencePhase.WriteInFlight;
            return Set(ProfileXmlEvidenceResult.WriteAllowed);
        }

        /// <summary>
        /// A normal return AND exact read-back digest are both mandatory.
        /// A correctly matching file after an exception is NOT a success result.
        /// </summary>
        internal ProfileXmlEvidenceResult ObserveWriterReturn(
            bool writerReturnedNormally, string readBackTargetSha)
        {
            if (Phase != ProfileXmlEvidencePhase.WriteInFlight)
                return InvalidTransition("WRITER_RETURN_WRONG_PHASE");
            if (!writerReturnedNormally || !ValidSha(readBackTargetSha) ||
                !String.Equals(readBackTargetSha, proposedSha, StringComparison.Ordinal))
            {
                Phase = ProfileXmlEvidencePhase.Uncertain;
                return Failure(ProfileXmlEvidenceResult.PersistenceUncertain,
                    "WRITE_RETURN_OR_READBACK_UNVERIFIED");
            }
            Phase = ProfileXmlEvidencePhase.WrittenVerified;
            return Set(ProfileXmlEvidenceResult.WrittenVerified);
        }

        /// <summary>
        /// Known unchanged is allowed ONLY when the caller can prove the writer
        /// never opened the target, and the target was read back unchanged.
        /// A caught exception alone is NOT such proof. If there is any doubt,
        /// return PersistenceUncertain, even if bytes now match the old hash.
        /// </summary>
        internal ProfileXmlEvidenceResult ObserveWriteFailure(
            bool provedWriterNeverOpenedTarget, string readBackTargetSha)
        {
            if (Phase != ProfileXmlEvidencePhase.WriteInFlight)
                return InvalidTransition("WRITE_FAILURE_WRONG_PHASE");
            if (provedWriterNeverOpenedTarget && ValidSha(readBackTargetSha) &&
                String.Equals(readBackTargetSha, originalSha, StringComparison.Ordinal))
            {
                mayHaveWritten = false;
                uncertaintyEverObserved = false;
                Phase = ProfileXmlEvidencePhase.FailedKnownUnchanged;
                return Failure(ProfileXmlEvidenceResult.FailedKnownUnchanged,
                    "WRITER_NOT_OPENED_TARGET_HASH_MATCHED");
            }
            Phase = ProfileXmlEvidencePhase.Uncertain;
            return Failure(ProfileXmlEvidenceResult.PersistenceUncertain,
                "WRITER_MAY_HAVE_CHANGED_TARGET");
        }

        /// <summary>
        /// A restore proof is useful for forensic review but deliberately does
        /// NOT clear the earlier unknown-write state or authorize a retry.
        /// This is a separate issue from a verified runtime rollback.
        /// </summary>
        internal ProfileXmlEvidenceResult ObserveRestore(
            bool restoreWasAttempted, bool retainedBackupStillVerified,
            string restoredTargetReadBackSha)
        {
            if (Phase != ProfileXmlEvidencePhase.Uncertain &&
                Phase != ProfileXmlEvidencePhase.RestoredButUncertain)
                return InvalidTransition("RESTORE_WRONG_PHASE");
            if (!restoreWasAttempted || !retainedBackupStillVerified ||
                !ValidSha(restoredTargetReadBackSha) ||
                !String.Equals(restoredTargetReadBackSha, originalSha, StringComparison.Ordinal))
            {
                Phase = ProfileXmlEvidencePhase.Uncertain;
                return Failure(ProfileXmlEvidenceResult.RestoreUnverified,
                    "RESTORE_NOT_VERIFIED");
            }
            Phase = ProfileXmlEvidencePhase.RestoredButUncertain;
            return Set(ProfileXmlEvidenceResult.RestoreVerifiedButUncertain);
        }

        internal ProfilePersistenceDiskOutcome ToPersistenceContractOutcome()
        {
            if (Phase == ProfileXmlEvidencePhase.WrittenVerified)
                return ProfilePersistenceDiskOutcome.WrittenAndVerified;
            if (Phase == ProfileXmlEvidencePhase.FailedKnownUnchanged)
                return ProfilePersistenceDiskOutcome.FailedKnownUnchanged;
            return ProfilePersistenceDiskOutcome.FailedOrUnknown;
        }

        private ProfileXmlEvidenceResult InvalidTransition(string reason)
        {
            // Keep a prior verified/uncertain terminal state unchanged.
            return Failure(ProfileXmlEvidenceResult.BlockedInvalidTransition, reason);
        }

        private ProfileXmlEvidenceResult Failure(ProfileXmlEvidenceResult result, string reason)
        {
            if (firstFailure == "NONE") firstFailure = reason;
            return Set(result);
        }

        private ProfileXmlEvidenceResult Set(ProfileXmlEvidenceResult result)
        {
            LastResult = result;
            return result;
        }

        private static bool ValidSha(string digest)
        {
            if (digest == null || digest.Length != 64) return false;
            for (int i = 0; i < digest.Length; i++)
            {
                char c = digest[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    return false;
            }
            return true;
        }
    }
}
