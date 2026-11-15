using System;
using System.Reflection;
using Verse;

namespace XpCustomizer.Profiles
{
    /// <summary>R6-P1-C2-A: explicit new-profile creation only. Never applies settings.</summary>
    internal enum ProfileCreationCode
    {
        Created,
        InvalidName,
        InvalidSettings,
        InvalidProfile,
        StorageFailure
    }

    internal sealed class ProfileCreationResult
    {
        internal readonly ProfileCreationCode Code;
        internal readonly ProfileStorageCode StorageCode;
        internal readonly string ProfileId;
        internal readonly string Detail;
        internal bool Success { get { return Code == ProfileCreationCode.Created; } }

        internal ProfileCreationResult(ProfileCreationCode code,
            ProfileStorageCode storageCode = ProfileStorageCode.Ok,
            string profileId = null, string detail = null)
        {
            Code = code;
            StorageCode = storageCode;
            ProfileId = profileId;
            Detail = detail;
        }
    }

    internal sealed class ProfileCreationService
    {
        internal const string Revision = "R6P1C2A-CREATE-ONLY-20261107";
        private readonly Func<ProfileFileRepository> repositoryFactory;
        private readonly bool logging;

        internal ProfileCreationService(Func<ProfileFileRepository> repositoryFactory,
            bool logging = true)
        {
            if (repositoryFactory == null) throw new ArgumentNullException("repositoryFactory");
            this.repositoryFactory = repositoryFactory;
            this.logging = logging;
        }

        internal static ProfileCreationService ForCurrentUser()
        {
            return new ProfileCreationService(delegate { return ProfileFileRepository.ForCurrentUser(); });
        }

        // This is metadata, not a game-version compatibility assertion. Never fabricate 1.6/1.7.
        internal static string CurrentGameVersion()
        {
            try
            {
                Type type = typeof(RimWorld.VersionControl);
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                foreach (string name in new[] { "CurrentVersionString", "CurrentVersionStringWithRev" })
                {
                    PropertyInfo property = type.GetProperty(name, flags);
                    if (property != null && property.PropertyType == typeof(string))
                    {
                        string value = property.GetValue(null, null) as string;
                        if (!String.IsNullOrWhiteSpace(value))
                            return CleanVersion(value);
                    }
                    FieldInfo field = type.GetField(name, flags);
                    if (field != null && field.FieldType == typeof(string))
                    {
                        string value = field.GetValue(null) as string;
                        if (!String.IsNullOrWhiteSpace(value))
                            return CleanVersion(value);
                    }
                }
            }
            catch (Exception) { }
            return "UNKNOWN";
        }

        private static string CleanVersion(string version)
        {
            string clean = version.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return clean.Length <= 64 ? clean : clean.Substring(0, 64);
        }

        // Compatibility entry point for C2-A callers and existing tests.
        internal static bool TryName(string proposed, out string canonical)
        {
            return ProfileNamePolicy.TryCanonical(proposed, out canonical);
        }

        internal ProfileCreationResult Create(XpCustomizerSettings live, string proposedName,
            string gameVersion)
        {
            return CreateCore(live, proposedName, gameVersion, DateTime.UtcNow,
                Guid.NewGuid().ToString("D"), ProfileStorageFault.None);
        }

        // Only self-tests inject IDs, timestamps and failure sites.
        internal ProfileCreationResult CreateCore(XpCustomizerSettings live, string proposedName,
            string gameVersion, DateTime utc, string profileId, ProfileStorageFault fault)
        {
            if (ProfileWriteSessionGate.IsBlocked)
                return Report(new ProfileCreationResult(ProfileCreationCode.StorageFailure,
                    ProfileStorageCode.RecoveryRequired, detail: "SESSION_RECOVERY_REQUIRED"));
            string name;
            if (!TryName(proposedName, out name))
                return Report(new ProfileCreationResult(ProfileCreationCode.InvalidName));
            if (live == null)
                return Report(new ProfileCreationResult(ProfileCreationCode.InvalidSettings,
                    detail: "NULL_SETTINGS"));
            try
            {
                // A detached copy is frozen before export. Never serialize the live object directly.
                XpCustomizerSettings frozen = SettingsSnapshotService.CloneIndependent(live);
                SettingsCandidateValidator.Result checkedSettings =
                    SettingsCandidateValidator.Validate(frozen);
                if (!checkedSettings.Accepted)
                    return Report(new ProfileCreationResult(ProfileCreationCode.InvalidSettings,
                        detail: checkedSettings.Reason));
                ProfileDocument document = ProfileMapper.Export(frozen, name,
                    gameVersion ?? "UNKNOWN", utc, profileId);
                ProfileStorageResult written = repositoryFactory().Create(document, fault);
                ProfileWriteSessionGate.Observe(written.Code);
                if (!written.Success || written.Document == null ||
                    written.Document.ProfileId != profileId ||
                    String.IsNullOrEmpty(written.ContentHash))
                    return Report(new ProfileCreationResult(ProfileCreationCode.StorageFailure,
                        written.Code, profileId, written.DetailCode));
                return Report(new ProfileCreationResult(ProfileCreationCode.Created,
                    ProfileStorageCode.Ok, profileId));
            }
            catch (ProfileContractException ex)
            {
                return Report(new ProfileCreationResult(ProfileCreationCode.InvalidProfile,
                    detail: ex.Code));
            }
            catch (Exception ex)
            {
                return Report(new ProfileCreationResult(ProfileCreationCode.StorageFailure,
                    ProfileStorageCode.IoError, profileId, ex.GetType().Name));
            }
        }

        private ProfileCreationResult Report(ProfileCreationResult result)
        {
            if (logging)
            {
                string idPrefix = result.ProfileId == null ? "NONE" :
                    result.ProfileId.Substring(0, Math.Min(8, result.ProfileId.Length));
                Log.Message("[XPC-R6P1C2A][PROFILE_CREATE] status=" + result.Code.ToString().ToUpperInvariant() +
                    "; storage=" + result.StorageCode + "; idPrefix=" + idPrefix +
                    "; detail=" + (result.Detail ?? "NONE") +
                    "; source=COMMITTED_SETTINGS_SNAPSHOT; overwrites=0; applies=0; scribeCalls=0; " +
                    "build=" + XpCustomizerMod.BuildId);
            }
            return result;
        }
    }
}
