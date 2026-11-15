using System;
using Verse;

namespace XpCustomizer.Profiles
{
    internal enum ProfileNotesCode
    {
        Saved, NoChange, InvalidInput, Conflict, Blocked, StorageFailure, RecoveryRequired
    }

    internal sealed class ProfileNotesResult
    {
        internal readonly ProfileNotesCode Code;
        internal readonly string ProfileId;
        internal readonly ProfileStorageCode StorageCode;
        internal readonly string Reason;
        internal bool Success { get { return Code == ProfileNotesCode.Saved || Code == ProfileNotesCode.NoChange; } }

        internal ProfileNotesResult(ProfileNotesCode code, string profileId,
            ProfileStorageCode storage, string reason)
        { Code = code; ProfileId = profileId; StorageCode = storage; Reason = reason; }
    }

    /// <summary>Only profile JSON metadata; never touches current XP settings, Scribe or GeneDefs.</summary>
    internal sealed class ProfileNotesService
    {
        internal const string Revision = "PROFILE-NOTES-PN1-R1-20261114";
        private readonly Func<ProfileFileRepository> repository;
        private readonly bool logging;

        internal ProfileNotesService(Func<ProfileFileRepository> repository, bool logging = true)
        {
            if (repository == null) throw new ArgumentNullException("repository");
            this.repository = repository;
            this.logging = logging;
        }

        internal static ProfileNotesService ForCurrentUser()
        { return new ProfileNotesService(delegate { return ProfileFileRepository.ForCurrentUser(); }); }

        internal ProfileNotesResult Save(string profileId, string expectedHash, string newNotes,
            ProfileStorageFault fault = ProfileStorageFault.None)
        {
            if (ProfileWriteSessionGate.IsBlocked || RuntimeRecoverySessionGate.LiveIsBlocked ||
                ProfileProductionWriteGate.IsUncertain || ProfileProductionWriteGate.IsBusy)
                return Report(ProfileNotesCode.Blocked, profileId, ProfileStorageCode.RecoveryRequired, "RECOVERY_OR_APPLY_BUSY");
            string canonical;
            if (!ProfileStoragePaths.TryCanonicalId(profileId, out canonical) ||
                !IsHash(expectedHash) || newNotes == null)
                return Report(ProfileNotesCode.InvalidInput, profileId, ProfileStorageCode.InvalidArgument, "ID_HASH_OR_NOTES");
            string notes = newNotes.Replace("\r\n", "\n").Replace('\r', '\n');
            if (!ProfileFormatV2.ValidNotes(notes))
                return Report(ProfileNotesCode.InvalidInput, profileId, ProfileStorageCode.InvalidArgument, "INVALID_NOTES");
            bool writeAttempted = false;
            try
            {
                ProfileFileRepository repo = repository();
                ProfileStorageResult old = repo.Load(canonical);
                if (!old.Success) return FromStorage(old, canonical);
                if (!String.Equals(old.ContentHash, expectedHash, StringComparison.Ordinal))
                    return Report(ProfileNotesCode.Conflict, canonical, ProfileStorageCode.Conflict, "HASH_CHANGED");
                if (String.Equals(old.Document.Notes, notes, StringComparison.Ordinal))
                    return Report(ProfileNotesCode.NoChange, canonical, ProfileStorageCode.Ok, "NOTES_UNCHANGED");

                // Capture an immutable expected data-only document. Repository will
                // construct the replacement from the *latest locked* disk state.
                ProfileDocument expected = ProfileJsonCodec.Deserialize(
                    ProfileJsonCodec.Serialize(old.Document));
                expected.FormatVersion = ProfileFormatV2.FormatVersion;
                expected.Notes = notes;
                writeAttempted = true;
                ProfileStorageResult result = repo.UpdateNotes(canonical, notes, expectedHash, fault);
                ProfileWriteSessionGate.Observe(result.Code);
                if (!result.Success) return FromStorage(result, canonical);
                ProfileStorageResult readback = repo.Load(canonical);
                if (!readback.Success || readback.ContentHash != result.ContentHash ||
                    readback.Document == null ||
                    ProfileJsonCodec.Serialize(readback.Document) != ProfileJsonCodec.Serialize(expected))
                {
                    ProfileWriteSessionGate.Observe(ProfileStorageCode.RecoveryRequired);
                    return Report(ProfileNotesCode.RecoveryRequired, canonical,
                        ProfileStorageCode.RecoveryRequired, "METADATA_OR_SETTINGS_READBACK_MISMATCH");
                }
                return Report(ProfileNotesCode.Saved, canonical, ProfileStorageCode.Ok, "NOTES_ONLY_VERIFIED");
            }
            catch (Exception ex)
            {
                // Once a repository write was attempted, even an exception from
                // readback must never be reported as a definite no-write failure.
                if (writeAttempted)
                {
                    ProfileWriteSessionGate.Observe(ProfileStorageCode.RecoveryRequired);
                    return Report(ProfileNotesCode.RecoveryRequired, canonical,
                        ProfileStorageCode.RecoveryRequired, "POST_WRITE_UNCERTAIN_" + ex.GetType().Name);
                }
                return Report(ProfileNotesCode.StorageFailure, canonical, ProfileStorageCode.IoError,
                    "EXCEPTION_BEFORE_WRITE_" + ex.GetType().Name);
            }
        }

        private ProfileNotesResult FromStorage(ProfileStorageResult r, string id)
        {
            ProfileWriteSessionGate.Observe(r.Code);
            ProfileNotesCode code = r.Code == ProfileStorageCode.Conflict ? ProfileNotesCode.Conflict :
                r.Code == ProfileStorageCode.RecoveryRequired ? ProfileNotesCode.RecoveryRequired :
                ProfileNotesCode.StorageFailure;
            return Report(code, id, r.Code, r.DetailCode);
        }

        private static bool IsHash(string hash)
        {
            if (hash == null || hash.Length != 64) return false;
            foreach (char c in hash)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }

        private ProfileNotesResult Report(ProfileNotesCode code, string id,
            ProfileStorageCode storage, string reason)
        {
            ProfileNotesResult result = new ProfileNotesResult(code, id, storage, reason);
            if (logging)
            {
                string canonical;
                string prefix = ProfileStoragePaths.TryCanonicalId(id, out canonical) ?
                    canonical.Substring(0, 8) : "INVALID";
                string message = "[XPC-PN1][NOTES_WRITE] version=" + Revision +
                    "; status=" + code + "; storage=" + storage +
                    "; idPrefix=" + prefix + "; reason=" + reason +
                    "; settingsWrites=0; xmlWrites=0; runtimeApplies=0" +
                    "; build=" + XpCustomizerMod.BuildId;
                if (code == ProfileNotesCode.RecoveryRequired) Log.Error(message);
                else if (!result.Success) Log.Warning(message);
                else Log.Message(message);
            }
            return result;
        }
    }
}
