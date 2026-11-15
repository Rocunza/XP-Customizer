using System;
using Verse;

namespace XpCustomizer.Profiles
{
    // R6-P1-C2-E: explicitly authorized JSON update only. Does NOT write XP XML,
    // call Scribe, change GeneDefs, or modify the running settings instance.
    internal enum ProfileCurrentOverwriteCode
    {
        Updated, NoChange, Rejected, Conflict, RecoveryRequired, StorageFailure
    }

    internal sealed class ProfileCurrentOverwriteResult
    {
        internal readonly ProfileCurrentOverwriteCode Code;
        internal readonly string ProfileId;
        internal readonly string Reason;
        internal readonly ProfileStorageCode StorageCode;
        internal bool Success { get { return Code == ProfileCurrentOverwriteCode.Updated; } }
        internal ProfileCurrentOverwriteResult(ProfileCurrentOverwriteCode code, string id,
            string reason, ProfileStorageCode storage = ProfileStorageCode.Ok)
        { Code = code; ProfileId = id; Reason = reason ?? "NONE"; StorageCode = storage; }
    }

    // F2: Log severity is a result policy, while the destination is injected.
    // The live service always keeps Verse.Log.Error for real RecoveryRequired.
    internal enum ProfileOverwriteLogLevel { Message, Warning, Error }
    internal enum ProfileOverwriteLogScope { UserAction, IsolatedSelfTest }

    internal interface IProfileOverwriteLogSink
    {
        ProfileOverwriteLogScope Scope { get; }
        void Write(ProfileOverwriteLogLevel level, string message);
    }

    internal sealed class ProfileOverwriteVerseLogSink : IProfileOverwriteLogSink
    {
        internal static readonly ProfileOverwriteVerseLogSink Instance =
            new ProfileOverwriteVerseLogSink();
        private ProfileOverwriteVerseLogSink() { }
        public ProfileOverwriteLogScope Scope { get { return ProfileOverwriteLogScope.UserAction; } }
        public void Write(ProfileOverwriteLogLevel level, string message)
        {
            if (level == ProfileOverwriteLogLevel.Error) Log.Error(message);
            else if (level == ProfileOverwriteLogLevel.Warning) Log.Warning(message);
            else Log.Message(message);
        }
    }

    internal sealed class ProfileCurrentOverwriteService
    {
        internal const string Revision = "R6P1C2E-EXPLICIT-JSON-OVERWRITE-20261108";
        private readonly Func<ProfileFileRepository> repository;
        private readonly ProfileOverwritePreflightService preflight;
        private static bool executing;
        private readonly Func<bool> productionWriteBlocked;
        private readonly IProfileOverwriteLogSink logSink;

        internal ProfileCurrentOverwriteService(Func<ProfileFileRepository> repo,
            Func<ProfileEditorReadinessResult> readiness,
            Func<bool> productionWriteBlocked = null,
            IProfileOverwriteLogSink logSink = null)
        {
            if (repo == null || readiness == null) throw new ArgumentNullException("repo/readiness");
            repository = repo;
            this.logSink = logSink ?? ProfileOverwriteVerseLogSink.Instance;
            this.productionWriteBlocked = productionWriteBlocked ??
                delegate { return ProfileProductionWriteGate.IsUncertain ||
                    ProfileProductionWriteGate.IsBusy; };
            preflight = new ProfileOverwritePreflightService(repo, readiness, false,
                this.productionWriteBlocked);
        }

        internal static ProfileCurrentOverwriteService ForCurrentUser()
        {
            return new ProfileCurrentOverwriteService(
                delegate { return ProfileFileRepository.ForCurrentUser(); },
                delegate { return ProfileEditorReadinessService.Check(); });
        }

        // Regression checks may inspect the wiring without emitting a deliberate red error.
        internal bool UsesVerseLogSink
        { get { return Object.ReferenceEquals(logSink, ProfileOverwriteVerseLogSink.Instance); } }

        internal static ProfileOverwriteLogLevel LogLevelFor(ProfileCurrentOverwriteCode code)
        {
            if (code == ProfileCurrentOverwriteCode.RecoveryRequired)
                return ProfileOverwriteLogLevel.Error;
            if (code == ProfileCurrentOverwriteCode.Rejected ||
                code == ProfileCurrentOverwriteCode.Conflict ||
                code == ProfileCurrentOverwriteCode.StorageFailure)
                return ProfileOverwriteLogLevel.Warning;
            return ProfileOverwriteLogLevel.Message;
        }

        internal ProfileOverwritePreflightResult Prepare(ProfileDocument selected, string hash,
            XpCustomizerSettings live)
        { return preflight.Check(selected, hash, live); }

        // Must only be called by the confirmation button. Initial candidate is
        // compared against a fresh preflight, preventing stale editor state.
        internal ProfileCurrentOverwriteResult Confirm(ProfileDocument selected,
            ProfileOverwritePreflightResult initial, XpCustomizerSettings live)
        {
            string id = selected == null ? null : selected.ProfileId;
            if (executing || initial == null || !initial.Success || initial.Candidate == null ||
                live == null || !Object.ReferenceEquals(live, XpCustomizerMod.Settings))
                return Report(ProfileCurrentOverwriteCode.Rejected, id, "INVALID_CONFIRM_OR_BUSY");
            if (ProfileWriteSessionGate.IsBlocked ||
                RuntimeRecoverySessionGate.LiveIsBlocked || productionWriteBlocked())
                return Report(ProfileCurrentOverwriteCode.RecoveryRequired, id,
                    "LIVE_XML_RECOVERY_LOCK", ProfileStorageCode.RecoveryRequired);
            executing = true;
            bool committed = false, writeAttempted = false;
            try
            {
                ProfileOverwritePreflightResult fresh = preflight.Check(
                    selected, initial.ExpectedHash, live);
                if (!fresh.Success || fresh.Candidate == null)
                    return Report(fresh.Code == ProfileOverwritePreflightCode.Conflict ?
                        ProfileCurrentOverwriteCode.Conflict : ProfileCurrentOverwriteCode.Rejected,
                        id, "RECHECK_" + fresh.Code + ":" + fresh.Detail, fresh.StorageCode);
                if (!fresh.ContentDiffers)
                    return Report(ProfileCurrentOverwriteCode.NoChange, id, "SAME_NO_WRITE");
                // Normalize export timestamps: a different candidate after the
                // dialog opened is a stale selection rather than consent.
                ProfileDocument atOpen = ProfileJsonCodec.Deserialize(
                    ProfileJsonCodec.Serialize(initial.Candidate));
                ProfileDocument atConfirm = ProfileJsonCodec.Deserialize(
                    ProfileJsonCodec.Serialize(fresh.Candidate));
                atOpen.UpdatedUtc = atConfirm.UpdatedUtc;
                if (ProfileJsonCodec.Serialize(atOpen) != ProfileJsonCodec.Serialize(atConfirm))
                    return Report(ProfileCurrentOverwriteCode.Conflict, id,
                        "SETTINGS_CHANGED_AFTER_CONFIRM_DIALOG_OPEN");

                ProfileStorageResult old = repository().Load(initial.ProfileId);
                if (!old.Success || old.ContentHash != initial.ExpectedHash ||
                    old.Document.DisplayName != selected.DisplayName ||
                    old.Document.CreatedUtc != selected.CreatedUtc)
                    return Report(ProfileCurrentOverwriteCode.Conflict, id,
                        "SOURCE_FILE_CHANGED", old.Code);

                // The confirmation window may have been open while a different
                // Apply failed. Recheck immediately before the JSON write.
                if (ProfileWriteSessionGate.IsBlocked ||
                    RuntimeRecoverySessionGate.LiveIsBlocked || productionWriteBlocked())
                    return Report(ProfileCurrentOverwriteCode.RecoveryRequired, id,
                        "LIVE_XML_RECOVERY_LOCK_BEFORE_UPDATE", ProfileStorageCode.RecoveryRequired);
                writeAttempted = true;
                ProfileStorageResult written = repository().Update(fresh.Candidate,
                    initial.ExpectedHash);
                ProfileWriteSessionGate.Observe(written.Code);
                if (!written.Success)
                    return Report(written.Code == ProfileStorageCode.Conflict ?
                        ProfileCurrentOverwriteCode.Conflict :
                        written.Code == ProfileStorageCode.RecoveryRequired ?
                            ProfileCurrentOverwriteCode.RecoveryRequired :
                            ProfileCurrentOverwriteCode.StorageFailure,
                        id, "ATOMIC_UPDATE_" + written.DetailCode, written.Code);
                committed = true;

                ProfileStorageResult readback = repository().Load(initial.ProfileId);
                if (!readback.Success || readback.ContentHash != written.ContentHash ||
                    readback.Document == null ||
                    readback.Document.CreatedUtc != old.Document.CreatedUtc ||
                    readback.Document.DisplayName != old.Document.DisplayName ||
                    readback.Document.ProfileId != old.Document.ProfileId ||
                    readback.Document.UpdatedUtc <= old.Document.UpdatedUtc ||
                    readback.Document.UpdatedUtc != written.Document.UpdatedUtc)
                    return Uncertain(id, "READBACK_ID_SHA_OR_METADATA");
                ProfileDocument normalize = ProfileJsonCodec.Deserialize(
                    ProfileJsonCodec.Serialize(readback.Document));
                normalize.UpdatedUtc = fresh.Candidate.UpdatedUtc;
                if (ProfileJsonCodec.Serialize(normalize) !=
                    ProfileJsonCodec.Serialize(fresh.Candidate))
                    return Uncertain(id, "READBACK_SETTINGS_MISMATCH");
                return Report(ProfileCurrentOverwriteCode.Updated, id, "JSON_REPLACED_AND_VERIFIED");
            }
            catch (Exception ex)
            {
                // A throwing repository call may have reached File.Replace.
                // If a write was attempted, only restart/recovery may proceed.
                return committed || writeAttempted ?
                    Uncertain(id, "REPOSITORY_WRITE_STATE_UNKNOWN_" + ex.GetType().Name) :
                    Report(ProfileCurrentOverwriteCode.StorageFailure, id,
                        "EXCEPTION_BEFORE_WRITE_" + ex.GetType().Name);
            }
            finally { executing = false; }
        }

        private ProfileCurrentOverwriteResult Uncertain(string id, string reason)
        {
            ProfileWriteSessionGate.Observe(ProfileStorageCode.RecoveryRequired);
            return Report(ProfileCurrentOverwriteCode.RecoveryRequired, id, reason,
                ProfileStorageCode.RecoveryRequired);
        }

        private ProfileCurrentOverwriteResult Report(ProfileCurrentOverwriteCode code,
            string id, string reason, ProfileStorageCode storage = ProfileStorageCode.Ok)
        {
            string canonical;
            string prefix = ProfileStoragePaths.TryCanonicalId(id, out canonical) ?
                canonical.Substring(0, 8) : "INVALID";
            bool isolated = logSink.Scope == ProfileOverwriteLogScope.IsolatedSelfTest;
            string scope = isolated ? "ISOLATED_SELFTEST" : "USER_ACTION";
            string testResult = isolated ?
                "; testResult=" + (code == ProfileCurrentOverwriteCode.RecoveryRequired ?
                    "EXPECTED_REJECTION" : code == ProfileCurrentOverwriteCode.Updated ?
                    "ISOLATED_JSON_UPDATED" : "OBSERVED_STATUS") : "";
            string message = "[XPC-R6P1C2E][PROFILE_OVERWRITE] revision=" + Revision +
                "; status=" + code + "; idPrefix=" + prefix + "; reason=" + reason +
                "; storage=" + storage + "; currentXmlWrites=0; runtimeApplies=0" +
                "; sessionLocked=" + ProfileWriteSessionGate.IsBlocked +
                "; productionXmlUncertain=" + ProfileProductionWriteGate.IsUncertain +
                "; scope=" + scope + testResult + "; build=" + XpCustomizerMod.BuildId;
            // A synthetic rejection never reaches the global Verse logger.
            // The real user service uses ProfileOverwriteVerseLogSink unchanged.
            logSink.Write(LogLevelFor(code), message);
            return new ProfileCurrentOverwriteResult(code, id, reason, storage);
        }
    }
}
