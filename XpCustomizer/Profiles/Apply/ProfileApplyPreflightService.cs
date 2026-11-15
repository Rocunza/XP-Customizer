using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace XpCustomizer.Profiles
{
    internal sealed class ProfileApplyPreflightService
    {
        internal const string Revision = "R6P1D1-FIX2-PORTABLE-DIFF-20261107";
        private readonly Func<ProfileFileRepository> repositoryFactory;
        private readonly Func<ProfileEditorReadinessResult> readiness;
        private readonly bool logging;

        internal ProfileApplyPreflightService(Func<ProfileFileRepository> repositoryFactory,
            Func<ProfileEditorReadinessResult> readiness, bool logging = true)
        {
            if (repositoryFactory == null) throw new ArgumentNullException("repositoryFactory");
            if (readiness == null) throw new ArgumentNullException("readiness");
            this.repositoryFactory = repositoryFactory;
            this.readiness = readiness;
            this.logging = logging;
        }

        internal static ProfileApplyPreflightService ForCurrentUser()
        {
            return new ProfileApplyPreflightService(
                delegate { return ProfileFileRepository.ForCurrentUser(); },
                delegate { return ProfileEditorReadinessService.Check(); });
        }

        internal ProfileApplyReviewResult Open(ProfileDocument selected, string expectedHash,
            XpCustomizerSettings currentSettings)
        {
            if (selected == null || !ValidHash(expectedHash))
                return Report(ProfileApplyReviewCode.InvalidSelection, ProfileStorageCode.InvalidArgument,
                    "INVALID_SELECTED_DOCUMENT", null, "INITIAL", false);
            string canonical;
            if (!ProfileStoragePaths.TryCanonicalId(selected.ProfileId, out canonical))
                return Report(ProfileApplyReviewCode.InvalidSelection, ProfileStorageCode.InvalidArgument,
                    "INVALID_PROFILE_ID", null, "INITIAL", false);
            return CheckCore(canonical, expectedHash, selected, null, currentSettings, "INITIAL");
        }

        /// <summary>One-shot, read-only confirmation. Does NOT return or invoke an apply delegate.</summary>
        internal ProfileApplyReviewResult ConfirmDryRun(ProfileApplyReviewSession session,
            XpCustomizerSettings currentSettings)
        {
            if (session == null)
                return Report(ProfileApplyReviewCode.InvalidSelection, ProfileStorageCode.InvalidArgument,
                    "MISSING_REVIEW_SESSION", null, "CONFIRM", false);
            if (!session.TryBeginConfirmation())
                return Report(ProfileApplyReviewCode.InvalidSelection, ProfileStorageCode.InvalidArgument,
                    "DUPLICATE_CONFIRMATION", null, "CONFIRM", false);
            return CheckCore(session.ProfileId, session.ExpectedHash, null, session,
                currentSettings, "CONFIRM", true);
        }

        // No file creates, replacement, Scribe, live value writes, runtime service calls or UI focus changes.
        private ProfileApplyReviewResult CheckCore(string id, string expectedHash,
            ProfileDocument listed, ProfileApplyReviewSession session,
            XpCustomizerSettings live, string phase, bool confirming = false)
        {
            if (ProfileWriteSessionGate.IsBlocked)
                return Report(ProfileApplyReviewCode.RecoveryRequired,
                    ProfileStorageCode.RecoveryRequired, "SESSION_RECOVERY_LOCK", null, phase, false);
            string canonical;
            if (!ProfileStoragePaths.TryCanonicalId(id, out canonical) ||
                !ValidHash(expectedHash) || live == null)
                return Report(live == null ? ProfileApplyReviewCode.InvalidSettings :
                    ProfileApplyReviewCode.InvalidSelection, ProfileStorageCode.InvalidArgument,
                    live == null ? "NULL_LIVE_SETTINGS" : "INVALID_ID_OR_HASH", null, phase, false);
            try
            {
                ProfileEditorReadinessResult editor = readiness();
                if (editor == null || editor.Code == ProfileEditorReadinessCode.Uncertain)
                    return Report(ProfileApplyReviewCode.Uncertain, ProfileStorageCode.Ok,
                        editor == null ? "EDITOR_RESULT_NULL" : "EDITOR_NOT_SETTLED", null, phase, false);
                if (editor.Code == ProfileEditorReadinessCode.PendingEdit)
                    return Report(ProfileApplyReviewCode.PendingEdit, ProfileStorageCode.Ok,
                        "EDITOR_PENDING", null, phase, false);

                ProfileStorageResult loaded = repositoryFactory().Load(canonical);
                if (loaded == null)
                    return Report(ProfileApplyReviewCode.StorageFailure, ProfileStorageCode.IoError,
                        "REPOSITORY_RESULT_NULL", null, phase, false);
                if (!loaded.Success)
                    return Report(MapStorage(loaded.Code, confirming), loaded.Code,
                        "PROFILE_LOAD_REJECTED", null, phase, false);
                if (!String.Equals(loaded.ContentHash, expectedHash, StringComparison.Ordinal))
                    return Report(ProfileApplyReviewCode.Conflict, ProfileStorageCode.Conflict,
                        "SHA256_CHANGED", null, phase, false);
                if (loaded.Document == null ||
                    !String.Equals(loaded.Document.ProfileId, canonical, StringComparison.Ordinal))
                    return Report(ProfileApplyReviewCode.InvalidProfile, ProfileStorageCode.IdMismatch,
                        "PROFILE_ID_MISMATCH", null, phase, false);
                ProfileDocument doc = loaded.Document;
                ProfileJsonCodec.Validate(doc);
                if (listed != null &&
                    (!String.Equals(listed.DisplayName, doc.DisplayName, StringComparison.Ordinal) ||
                     listed.CreatedUtc != doc.CreatedUtc || listed.UpdatedUtc != doc.UpdatedUtc))
                    return Report(ProfileApplyReviewCode.Conflict, ProfileStorageCode.Conflict,
                        "SELECTED_METADATA_CHANGED", null, phase, false);
                if (session != null &&
                    (!String.Equals(session.DisplayName, doc.DisplayName, StringComparison.Ordinal) ||
                     session.UpdatedUtc != doc.UpdatedUtc))
                    return Report(ProfileApplyReviewCode.Conflict, ProfileStorageCode.Conflict,
                        "REVIEW_METADATA_CHANGED", null, phase, false);

                XpCustomizerSettings baseline = SettingsSnapshotService.CloneIndependent(live);
                if (!SettingsCandidateValidator.Validate(baseline).Accepted)
                    return Report(ProfileApplyReviewCode.InvalidSettings, ProfileStorageCode.Ok,
                        "BASELINE_INVALID", null, phase, false);
                string baselineMismatch = "NONE";
                if (session != null && !session.BaselineMatches(baseline, out baselineMismatch))
                    return Report(ProfileApplyReviewCode.Conflict, ProfileStorageCode.Conflict,
                        "CURRENT_SETTINGS_CHANGED", null, phase, false, baselineMismatch);
                XpCustomizerSettings candidate = ProfileMapper.ImportDetached(doc, baseline);
                if (!SettingsCandidateValidator.Validate(candidate).Accepted)
                    return Report(ProfileApplyReviewCode.InvalidProfile, ProfileStorageCode.InvalidFormat,
                        "IMPORTED_SETTINGS_INVALID", null, phase, false);
                string mismatch;
                // D1 compares only the portable fields that a saved profile can
                // replace. The older transaction equivalence additionally checks
                // dictionary comparer object identity; JSON decodes to Ordinal
                // while ordinary settings dictionaries use Default (also ordinal).
                // That implementation detail is NOT a settings-value difference.
                bool differs = !ProfileApplySettingsComparison.PortableEquivalent(
                    baseline, candidate, out mismatch);
                if (session != null && differs != session.ContentDiffers)
                    return Report(ProfileApplyReviewCode.Conflict, ProfileStorageCode.Conflict,
                        "CHANGE_CLASSIFICATION_CHANGED", null, phase, false, mismatch);
                // Re-read the XP-owned editor state after detached transformations.
                ProfileEditorReadinessResult finalEditor = readiness();
                if (finalEditor == null || finalEditor.Code != ProfileEditorReadinessCode.Ready)
                    return Report(finalEditor != null && finalEditor.Code == ProfileEditorReadinessCode.PendingEdit
                        ? ProfileApplyReviewCode.PendingEdit : ProfileApplyReviewCode.Uncertain,
                        ProfileStorageCode.Ok, "EDITOR_CHANGED_DURING_REVIEW", null, phase, false);
                // Validate the source objects again after all detached work. This
                // rejects a concurrent settings or external file edit mid-review.
                string finalMismatch;
                if (!SettingsApplyCoordinator.Equivalent(baseline, live, out finalMismatch))
                    return Report(ProfileApplyReviewCode.Conflict, ProfileStorageCode.Conflict,
                        "SETTINGS_CHANGED_DURING_REVIEW", null, phase, false, finalMismatch);
                ProfileStorageResult finalFile = repositoryFactory().Load(canonical);
                if (finalFile == null || !finalFile.Success ||
                    !String.Equals(finalFile.ContentHash, expectedHash, StringComparison.Ordinal))
                    return Report(ProfileApplyReviewCode.Conflict, ProfileStorageCode.Conflict,
                        "FILE_CHANGED_DURING_REVIEW", null, phase, false);
                ProfileApplyReviewSession review = session ??
                    new ProfileApplyReviewSession(doc, loaded.ContentHash, baseline, differs);
                return Report(differs ? ProfileApplyReviewCode.ReadyChanged :
                    ProfileApplyReviewCode.ReadyNoChange, ProfileStorageCode.Ok,
                    "DETACHED_IMPORT_VALID", review, phase, true, mismatch);
            }
            catch (ProfileContractException ex)
            {
                return Report(ProfileApplyReviewCode.InvalidProfile, ProfileStorageCode.InvalidFormat,
                    ex.Code, null, phase, false);
            }
            catch (Exception ex)
            {
                return Report(ProfileApplyReviewCode.InternalFailure, ProfileStorageCode.IoError,
                    ex.GetType().Name, null, phase, false);
            }
        }

        private static ProfileApplyReviewCode MapStorage(ProfileStorageCode code, bool confirming)
        {
            switch (code)
            {
                case ProfileStorageCode.NotFound:
                    return confirming ? ProfileApplyReviewCode.Conflict : ProfileApplyReviewCode.StorageFailure;
                case ProfileStorageCode.InvalidFormat:
                case ProfileStorageCode.UnsupportedVersion:
                case ProfileStorageCode.IdMismatch:
                case ProfileStorageCode.FileTooLarge:
                    return ProfileApplyReviewCode.InvalidProfile;
                case ProfileStorageCode.Conflict: return ProfileApplyReviewCode.Conflict;
                case ProfileStorageCode.RecoveryRequired: return ProfileApplyReviewCode.RecoveryRequired;
                default: return ProfileApplyReviewCode.StorageFailure;
            }
        }

        private static bool ValidHash(string hash)
        {
            if (hash == null || hash.Length != 64) return false;
            foreach (char c in hash)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }

        private ProfileApplyReviewResult Report(ProfileApplyReviewCode code,
            ProfileStorageCode storage, string detail, ProfileApplyReviewSession session,
            string phase, bool hashMatched, string firstMismatch = "NOT_CHECKED")
        {
            ProfileApplyReviewResult result = new ProfileApplyReviewResult(code, storage, detail, session);
            if (logging)
                Log.Message("[XPC-R6P1D1][PROFILE_APPLY_PREFLIGHT] phase=" + phase +
                    "; status=" + code + "; storage=" + storage + "; reason=" + result.Detail +
                    "; firstMismatchField=" + firstMismatch +
                    "; selectedHashMatched=" + (hashMatched ? "True" : "FalseOrNotChecked") +
                    "; baselineMatched=" + (phase == "INITIAL" ? "NOT_APPLICABLE" :
                        (result.Success ? "True" : "NOT_CONFIRMED")) +
                    "; result=READ_ONLY; fileWritesByD1=0; liveSettingsWrites=0; " +
                    "geneDefWrites=0; scribeCalls=0; applyByReadOnlyPreflight=NOT_INVOKED; productionCommit=SEPARATE_ROUTE; build=" + XpCustomizerMod.BuildId);
            return result;
        }
    }

}
