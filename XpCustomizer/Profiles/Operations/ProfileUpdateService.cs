using System;
using Verse;

namespace XpCustomizer.Profiles
{
    /// <summary>
    /// R6-P1-C2-B1: metadata rename ONLY. No snapshot export, live apply,
    /// GeneDef, cache, pawn, Scribe or settings mutation. Existing storage
    /// provides a best-effort hash check and retained atomic-replace backup.
    /// </summary>
    internal enum ProfileRenameCode
    {
        Renamed,
        NoChange,
        InvalidName,
        InvalidSelection,
        Conflict,
        StorageFailure,
        RecoveryRequired,
        WriteLocked
    }

    internal sealed class ProfileRenameResult
    {
        internal readonly ProfileRenameCode Code;
        internal readonly ProfileStorageCode StorageCode;
        internal readonly string ProfileId;
        internal readonly string DetailCode;
        internal bool Success { get { return Code == ProfileRenameCode.Renamed; } }

        internal ProfileRenameResult(ProfileRenameCode code, ProfileStorageCode storageCode,
            string profileId, string detailCode = null)
        {
            Code = code;
            StorageCode = storageCode;
            ProfileId = profileId;
            DetailCode = detailCode;
        }
    }

    internal sealed class ProfileUpdateService
    {
        internal const string Revision = "R6P1C2B1-METADATA-RENAME-20261107";
        private readonly Func<ProfileFileRepository> repositoryFactory;
        private readonly bool logging;

        internal ProfileUpdateService(Func<ProfileFileRepository> repositoryFactory,
            bool logging = true)
        {
            if (repositoryFactory == null) throw new ArgumentNullException("repositoryFactory");
            this.repositoryFactory = repositoryFactory;
            this.logging = logging;
        }

        internal static ProfileUpdateService ForCurrentUser()
        {
            return new ProfileUpdateService(delegate { return ProfileFileRepository.ForCurrentUser(); });
        }

        internal ProfileRenameResult Rename(string profileId, string expectedHash, string proposedName)
        {
            return RenameCore(profileId, expectedHash, proposedName, ProfileStorageFault.None);
        }

        // Faults are injected only from isolated DEBUG self-tests.
        internal ProfileRenameResult RenameCore(string profileId, string expectedHash,
            string proposedName, ProfileStorageFault fault)
        {
            if (ProfileWriteSessionGate.IsBlocked)
                return Report(ProfileRenameCode.WriteLocked, ProfileStorageCode.RecoveryRequired,
                    profileId, "SESSION_RECOVERY_REQUIRED");

            string canonicalId;
            if (!ProfileStoragePaths.TryCanonicalId(profileId, out canonicalId) ||
                !IsHash(expectedHash))
                return Report(ProfileRenameCode.InvalidSelection, ProfileStorageCode.InvalidArgument,
                    profileId, "ID_OR_HASH_INVALID");

            string name;
            if (!ProfileNamePolicy.TryCanonical(proposedName, out name))
                return Report(ProfileRenameCode.InvalidName, ProfileStorageCode.InvalidArgument,
                    profileId, "NAME_INVALID");

            bool updateCommitted = false;
            try
            {
                ProfileFileRepository repository = repositoryFactory();
                ProfileStorageResult existing = repository.Load(canonicalId);
                if (!existing.Success)
                    return FromStorage(existing, profileId);
                if (!String.Equals(existing.ContentHash, expectedHash, StringComparison.Ordinal))
                    return Report(ProfileRenameCode.Conflict, ProfileStorageCode.Conflict,
                        profileId, "STALE_SELECTION");
                if (String.Equals(existing.Document.DisplayName, name, StringComparison.Ordinal))
                    return Report(ProfileRenameCode.NoChange, ProfileStorageCode.Ok,
                        profileId, "UNCHANGED_NO_WRITE");

                // Codec round-trip makes the replacement a deep, detached copy.
                // Only the display name changes; identity, source and all settings stay intact.
                ProfileDocument replacement = ProfileJsonCodec.Deserialize(
                    ProfileJsonCodec.Serialize(existing.Document));
                replacement.DisplayName = name;
                ProfileStorageResult stored = repository.Update(replacement, expectedHash, fault);
                if (!stored.Success)
                    return FromStorage(stored, profileId);
                updateCommitted = true;

                // A post-commit mismatch must not be presented as success.
                if (stored.Document == null || stored.Document.ProfileId != canonicalId ||
                    stored.Document.DisplayName != name ||
                    stored.Document.CreatedUtc != existing.Document.CreatedUtc ||
                    stored.Document.SourceBuildId != existing.Document.SourceBuildId ||
                    stored.Document.SourceGameVersion != existing.Document.SourceGameVersion ||
                    stored.Document.UpdatedUtc <= existing.Document.UpdatedUtc ||
                    String.IsNullOrEmpty(stored.ContentHash) ||
                    String.Equals(stored.ContentHash, expectedHash, StringComparison.Ordinal))
                {
                    ProfileWriteSessionGate.Observe(ProfileStorageCode.RecoveryRequired);
                    return Report(ProfileRenameCode.RecoveryRequired,
                        ProfileStorageCode.RecoveryRequired, profileId, "POST_UPDATE_VERIFY");
                }
                // Verify every portable DTO/metadata field survived the file rewrite.
                // Normalize only the explicitly allowed name and updated-time changes.
                ProfileDocument normalized = ProfileJsonCodec.Deserialize(
                    ProfileJsonCodec.Serialize(stored.Document));
                normalized.DisplayName = existing.Document.DisplayName;
                normalized.UpdatedUtc = existing.Document.UpdatedUtc;
                if (ProfileJsonCodec.Serialize(normalized) !=
                    ProfileJsonCodec.Serialize(existing.Document))
                {
                    ProfileWriteSessionGate.Observe(ProfileStorageCode.RecoveryRequired);
                    return Report(ProfileRenameCode.RecoveryRequired,
                        ProfileStorageCode.RecoveryRequired, profileId, "SNAPSHOT_MISMATCH");
                }
                return Report(ProfileRenameCode.Renamed, ProfileStorageCode.Ok, profileId);
            }
            catch (ProfileContractException ex)
            {
                if (updateCommitted) ProfileWriteSessionGate.Observe(ProfileStorageCode.RecoveryRequired);
                return Report(updateCommitted ? ProfileRenameCode.RecoveryRequired : ProfileRenameCode.StorageFailure,
                    updateCommitted ? ProfileStorageCode.RecoveryRequired : ProfileStorageCode.InvalidFormat,
                    profileId, ex.Code);
            }
            catch (Exception ex)
            {
                if (updateCommitted) ProfileWriteSessionGate.Observe(ProfileStorageCode.RecoveryRequired);
                return Report(updateCommitted ? ProfileRenameCode.RecoveryRequired : ProfileRenameCode.StorageFailure,
                    updateCommitted ? ProfileStorageCode.RecoveryRequired : ProfileStorageCode.IoError,
                    profileId, ex.GetType().Name);
            }
        }

        private ProfileRenameResult FromStorage(ProfileStorageResult result, string id)
        {
            ProfileWriteSessionGate.Observe(result.Code);
            ProfileRenameCode code = result.Code == ProfileStorageCode.RecoveryRequired
                ? ProfileRenameCode.RecoveryRequired
                : result.Code == ProfileStorageCode.Conflict
                    ? ProfileRenameCode.Conflict : ProfileRenameCode.StorageFailure;
            return Report(code, result.Code, id, result.DetailCode);
        }

        private ProfileRenameResult Report(ProfileRenameCode code, ProfileStorageCode storageCode,
            string id, string detail = null)
        {
            ProfileRenameResult result = new ProfileRenameResult(code, storageCode, id, detail);
            if (logging)
            {
                string canonicalId;
                string prefix = ProfileStoragePaths.TryCanonicalId(id, out canonicalId)
                    ? canonicalId.Substring(0, 8) : "INVALID";
                string message = "[XPC-R6P1C2B1][PROFILE_RENAME] action=CONFIRM; status=" +
                    code.ToString().ToUpperInvariant() + "; storage=" + storageCode +
                    "; idPrefix=" + prefix + "; detail=" + (detail ?? "NONE") +
                    "; sessionWriteBlocked=" + ProfileWriteSessionGate.IsBlocked +
                    "; applies=0; scribeCalls=0; build=" + XpCustomizerMod.BuildId;
                if (code == ProfileRenameCode.RecoveryRequired ||
                    code == ProfileRenameCode.WriteLocked) Log.Warning(message);
                else Log.Message(message);
            }
            return result;
        }

        private static bool IsHash(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }
    }
}
