using System;
using Verse;

namespace XpCustomizer.Profiles
{
    /// <summary>
    /// R6-P1-C2-B2-A: performs ONLY reads and detached mapping.
    /// No ProfileFileRepository.Update/Create, no settings mutation, no Scribe,
    /// no runtime application. A candidate is NOT authorization to write.
    /// B2-B must repeat every check and require explicit user confirmation.
    /// </summary>
    internal enum ProfileOverwritePreflightCode
    {
        Ready, PendingEdit, Uncertain, InvalidSelection, Conflict,
        InvalidSettings, InvalidProfile, StorageFailure, RecoveryRequired
    }

    internal sealed class ProfileOverwritePreflightResult
    {
        internal readonly ProfileOverwritePreflightCode Code;
        internal readonly ProfileStorageCode StorageCode;
        internal readonly string Detail;
        internal readonly string ProfileId;
        internal readonly string ExpectedHash;
        internal readonly ProfileDocument Candidate;
        internal readonly bool ContentDiffers;
        internal bool Success { get { return Code == ProfileOverwritePreflightCode.Ready; } }
        internal ProfileOverwritePreflightResult(ProfileOverwritePreflightCode code,
            ProfileStorageCode storageCode, string detail, string profileId = null,
            string expectedHash = null, ProfileDocument candidate = null,
            bool contentDiffers = false)
        {
            Code = code;
            StorageCode = storageCode;
            Detail = detail ?? "NONE";
            ProfileId = profileId;
            ExpectedHash = expectedHash;
            Candidate = candidate;
            ContentDiffers = contentDiffers;
        }
    }

    internal sealed class ProfileOverwritePreflightService
    {
        internal const string Revision = "R6P1C2B2A-READONLY-PREFLIGHT-20261107";
        private readonly Func<ProfileFileRepository> repository;
        private readonly Func<ProfileEditorReadinessResult> readiness;
        private readonly bool logging;
        private readonly Func<bool> productionWriteBlocked;

        internal ProfileOverwritePreflightService(Func<ProfileFileRepository> repository,
            Func<ProfileEditorReadinessResult> readiness, bool logging = true,
            Func<bool> productionWriteBlocked = null)
        {
            if (repository == null) throw new ArgumentNullException("repository");
            if (readiness == null) throw new ArgumentNullException("readiness");
            this.repository = repository;
            this.readiness = readiness;
            this.logging = logging;
            this.productionWriteBlocked = productionWriteBlocked ??
                delegate { return ProfileProductionWriteGate.IsUncertain ||
                    ProfileProductionWriteGate.IsBusy; };
        }

        internal static ProfileOverwritePreflightService ForCurrentUser()
        {
            return new ProfileOverwritePreflightService(
                delegate { return ProfileFileRepository.ForCurrentUser(); },
                delegate { return ProfileEditorReadinessService.Check(); });
        }

        internal ProfileOverwritePreflightResult Check(ProfileDocument selected,
            string expectedHash, XpCustomizerSettings live)
        {
            // A detached JSON snapshot must not be authorized from potentially
            // inconsistent live XP settings after a failed production Apply.
            if (ProfileWriteSessionGate.IsBlocked ||
                RuntimeRecoverySessionGate.LiveIsBlocked || productionWriteBlocked())
                return Report(ProfileOverwritePreflightCode.RecoveryRequired,
                    ProfileStorageCode.RecoveryRequired, "LIVE_OR_XML_RECOVERY_LOCK");
            string canonical;
            if (selected == null ||
                !ProfileStoragePaths.TryCanonicalId(selected.ProfileId, out canonical) ||
                !ValidHash(expectedHash))
                return Report(ProfileOverwritePreflightCode.InvalidSelection,
                    ProfileStorageCode.InvalidArgument, "ID_OR_HASH_INVALID");
            if (live == null)
                return Report(ProfileOverwritePreflightCode.InvalidSettings,
                    ProfileStorageCode.Ok, "NULL_SETTINGS");
            try
            {
                ProfileEditorReadinessResult gate = readiness();
                if (gate == null || gate.Code == ProfileEditorReadinessCode.Uncertain)
                    return Report(ProfileOverwritePreflightCode.Uncertain,
                        ProfileStorageCode.Ok, gate == null ? "EDITOR_GATE_NULL" : gate.Source);
                if (gate.Code == ProfileEditorReadinessCode.PendingEdit)
                    return Report(ProfileOverwritePreflightCode.PendingEdit,
                        ProfileStorageCode.Ok, gate.Source);

                ProfileStorageResult fresh = repository().Load(canonical);
                if (!fresh.Success)
                    return Report(fresh.Code == ProfileStorageCode.RecoveryRequired
                        ? ProfileOverwritePreflightCode.RecoveryRequired
                        : ProfileOverwritePreflightCode.StorageFailure,
                        fresh.Code, fresh.DetailCode);
                if (!String.Equals(fresh.ContentHash, expectedHash,
                    StringComparison.Ordinal))
                    return Report(ProfileOverwritePreflightCode.Conflict,
                        ProfileStorageCode.Conflict, "EXPECTED_HASH_MISMATCH");
                // Compare detached known metadata, never trust the display index/name.
                if (!String.Equals(selected.DisplayName, fresh.Document.DisplayName,
                    StringComparison.Ordinal) || selected.CreatedUtc != fresh.Document.CreatedUtc)
                    return Report(ProfileOverwritePreflightCode.Conflict,
                        ProfileStorageCode.Conflict, "SELECTED_METADATA_STALE");
                XpCustomizerSettings frozen = SettingsSnapshotService.CloneIndependent(live);
                SettingsCandidateValidator.Result valid = SettingsCandidateValidator.Validate(frozen);
                if (!valid.Accepted)
                    return Report(ProfileOverwritePreflightCode.InvalidSettings,
                        ProfileStorageCode.Ok, valid.Reason);
                DateTime utc = DateTime.UtcNow;
                if (utc <= fresh.Document.UpdatedUtc)
                {
                    if (fresh.Document.UpdatedUtc == DateTime.MaxValue)
                        return Report(ProfileOverwritePreflightCode.InvalidProfile,
                            ProfileStorageCode.InvalidFormat, "INVALID_UPDATED_TIME");
                    utc = fresh.Document.UpdatedUtc.AddTicks(1);
                }
                ProfileDocument candidate = ProfileMapper.Export(frozen,
                    fresh.Document.DisplayName, ProfileCreationService.CurrentGameVersion(),
                    utc, canonical);
                candidate.CreatedUtc = fresh.Document.CreatedUtc;
                candidate.Notes = fresh.Document.Notes;
                candidate.FormatVersion = ProfileFormatV2.FormatVersion;
                ProfileJsonCodec.Validate(candidate);
                // Compare portable settings independent of updated time/metadata.
                string originalSettings = PortableSettingsOnly(fresh.Document);
                string candidateSettings = PortableSettingsOnly(candidate);
                return Report(ProfileOverwritePreflightCode.Ready,
                    ProfileStorageCode.Ok, "DETACHED_CANDIDATE_VALID", canonical,
                    fresh.ContentHash, candidate,
                    !String.Equals(originalSettings, candidateSettings,
                        StringComparison.Ordinal));
            }
            catch (ProfileContractException ex)
            {
                return Report(ProfileOverwritePreflightCode.InvalidProfile,
                    ProfileStorageCode.InvalidFormat, ex.Code);
            }
            catch (Exception ex)
            {
                return Report(ProfileOverwritePreflightCode.StorageFailure,
                    ProfileStorageCode.IoError, ex.GetType().Name);
            }
        }

        // Internal comparison excludes metadata, with the strict codec as the
        // single serialization contract. This modifies only fresh *detached*
        // documents, never repository data or the real settings object.
        private static string PortableSettingsOnly(ProfileDocument doc)
        {
            ProfileDocument detached = ProfileJsonCodec.Deserialize(
                ProfileJsonCodec.Serialize(doc));
            detached.ProfileId = "00000000-0000-4000-8000-000000000001";
            detached.DisplayName = "comparison";
            detached.CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            detached.UpdatedUtc = detached.CreatedUtc;
            detached.FormatVersion = ProfileFormatV2.FormatVersion;
            detached.Notes = "";
            detached.SourceBuildId = "COMPARISON";
            detached.SourceGameVersion = "COMPARISON";
            return ProfileJsonCodec.Serialize(detached);
        }

        private static bool ValidHash(string hash)
        {
            if (hash == null || hash.Length != 64) return false;
            for (int i = 0; i < hash.Length; i++)
                if (!((hash[i] >= '0' && hash[i] <= '9') ||
                      (hash[i] >= 'a' && hash[i] <= 'f'))) return false;
            return true;
        }

        private ProfileOverwritePreflightResult Report(ProfileOverwritePreflightCode code,
            ProfileStorageCode storage, string detail, string id = null,
            string hash = null, ProfileDocument candidate = null, bool differs = false)
        {
            var result = new ProfileOverwritePreflightResult(code, storage, detail, id,
                hash, candidate, differs);
            if (logging)
                Log.Message("[XPC-R6P1C2B2A][PROFILE_OVERWRITE_PREFLIGHT] status=" + code +
                    "; storage=" + storage + "; reason=" + result.Detail +
                    "; settingsDifference=" + (candidate == null ? "NOT_CHECKED" :
                        (differs ? "CHANGED" : "SAME")) +
                    "; source=READ_ONLY; fileWrites=0; liveSettingsWrites=0; " +
                    "geneDefWrites=0; scribeCalls=0; applyByPreflight=NO; build=" + XpCustomizerMod.BuildId);
            return result;
        }
    }
}
