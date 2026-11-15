using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace XpCustomizer.Profiles
{
    /// <summary>
    /// R6-P1-C2-D. Permanently deletes ONLY validated, directly owned trash entries.
    /// Individual deletes are not a multi-file atomic transaction. Stop on first anomaly.
    /// No Config XML, active profile, backups, Scribe, or other mod access.
    /// </summary>
    internal sealed partial class ProfileFileRepository
    {
        internal ProfileTrashPurgeResult PrepareTrashPurge()
        {
            try
            {
                ProfileTrashPurgePlan plan;
                ProfileStorageCode code;
                string detail;
                if (!TrySnapshotTrash(out plan, out code, out detail))
                    return PurgeResult(ProfileTrashPurgeState.Rejected, code, detail, null, 0, 0, -1);
                return plan.Count == 0
                    ? PurgeResult(ProfileTrashPurgeState.NoOp, ProfileStorageCode.Ok,
                        "TRASH_EMPTY", plan, 0, 0, 0)
                    : PurgeResult(ProfileTrashPurgeState.Ready, ProfileStorageCode.Ok,
                        "SNAPSHOT_VERIFIED", plan, plan.Count, 0, plan.Count);
            }
            catch (Exception ex)
            {
                return PurgeResult(ProfileTrashPurgeState.Rejected, Classify(ex).Code,
                    "PREPARE_EXCEPTION", null, 0, 0, -1);
            }
        }

        internal ProfileTrashPurgeResult PurgeTrash(ProfileTrashPurgePlan approved
#if DEBUG
            , int injectBeforeIndex = -1, int injectAfterDeleteIndex = -1
#endif
            )
        {
            if (approved == null || approved.Count == 0 ||
                approved.OwnerRoot != paths.Root || !IsHash(approved.Fingerprint))
                return PurgeResult(ProfileTrashPurgeState.Rejected,
                    ProfileStorageCode.InvalidArgument, "INVALID_APPROVAL", approved,
                    approved == null ? 0 : approved.Count, 0, -1);
            int deleted = 0;
            bool deleteInFlight = false;
            try
            {
                // Never create a trash folder when a purge is requested.
                paths.GuardDirectories(false, false, false);
                if (!Directory.Exists(paths.Root) || !Directory.Exists(paths.Trash))
                    return PurgeResult(ProfileTrashPurgeState.Rejected, ProfileStorageCode.Conflict,
                        "DIRECTORY_CHANGED", approved, approved.Count, 0, -1);
                ProfileStoragePaths.GuardDirectoryIfExists(paths.Trash);
                using (AcquireWriteLock())
                {
                    ProfileTrashPurgePlan current;
                    ProfileStorageCode scanCode;
                    string scanDetail;
                    if (!TrySnapshotTrash(out current, out scanCode, out scanDetail))
                        return PurgeResult(ProfileTrashPurgeState.Rejected, scanCode,
                            "RECHECK_" + scanDetail, approved, approved.Count, 0, -1);
                    if (!IdenticalSnapshot(approved, current))
                        return PurgeResult(ProfileTrashPurgeState.Rejected, ProfileStorageCode.Conflict,
                            "LIST_CHANGED", approved, approved.Count, 0, current.Count);

                    for (int i = 0; i < approved.Count; i++)
                    {
                        ProfileTrashPurgeCandidate candidate = approved.Candidates[i];
                        // Restrict path to a token checked again at the point of use.
                        string id;
                        if (!ProfileStoragePaths.TryParseTrashToken(candidate.Token, out id) ||
                            id != candidate.ProfileId || !IsHash(candidate.Sha256))
                            return Aborted(approved, deleted, false, ProfileStorageCode.Conflict,
                                "TOKEN_CHANGED");
                        paths.GuardDirectories(false, false, false);
                        ProfileStoragePaths.GuardDirectoryIfExists(paths.Trash);
                        string file = Path.Combine(paths.Trash, candidate.Token);
                        ProfileStorageResult recheck = ReadFile(file, id);
                        if (!recheck.Success || recheck.ContentHash != candidate.Sha256)
                            return Aborted(approved, deleted, false, ProfileStorageCode.Conflict,
                                "FILE_CHANGED_OR_UNREADABLE");
                        ProfileStoragePaths.GuardRegularFile(file);
#if DEBUG
                        if (i == injectBeforeIndex)
                            return Aborted(approved, deleted, false, ProfileStorageCode.InjectedFailure,
                                "INJECT_BEFORE_DELETE");
#endif
                        // A failure after issuing File.Delete is uncertain even if it throws.
                        deleteInFlight = true;
                        File.Delete(file);
#if DEBUG
                        if (i == injectAfterDeleteIndex)
                            return Aborted(approved, deleted, true, ProfileStorageCode.InjectedFailure,
                                "INJECT_AFTER_DELETE_BEFORE_VERIFY");
#endif
                        // File.Exists(false) is not enough: verify absence via GetAttributes.
                        if (!ConfirmedAbsent(file))
                            return Aborted(approved, deleted, true, ProfileStorageCode.RecoveryRequired,
                                "DELETE_NOT_ABSENT");
                        deleteInFlight = false;
                        deleted++;
                    }
                    // Check the entire matching trash set after the operation, not just targets.
                    ProfileTrashPurgePlan final;
                    ProfileStorageCode finalCode;
                    string finalDetail;
                    if (!TrySnapshotTrash(out final, out finalCode, out finalDetail) || final.Count != 0)
                        return Aborted(approved, deleted, false, ProfileStorageCode.RecoveryRequired,
                            "FINAL_TRASH_NOT_EMPTY_OR_UNREADABLE");
                    return PurgeResult(ProfileTrashPurgeState.Complete, ProfileStorageCode.Ok,
                        "ALL_ABSENT_VERIFIED", approved, approved.Count, deleted, 0);
                }
            }
            catch (Exception ex)
            {
                return Aborted(approved, deleted, deleteInFlight,
                    Classify(ex).Code, "PURGE_EXCEPTION_" + ex.GetType().Name);
            }
        }

        private bool TrySnapshotTrash(out ProfileTrashPurgePlan plan,
            out ProfileStorageCode code, out string detail)
        {
            plan = null;
            code = ProfileStorageCode.Ok;
            detail = "NONE";
            ProfileStorageListing listing = ListTrash();
            if (listing == null || listing.Code != ProfileStorageCode.Ok)
            {
                code = listing == null ? ProfileStorageCode.IoError : listing.Code;
                detail = "LIST_FAILED";
                return false;
            }
            List<ProfileTrashPurgeCandidate> candidates = new List<ProfileTrashPurgeCandidate>();
            foreach (ProfileStorageEntry entry in listing.Entries)
            {
                string id;
                if (entry == null || !ProfileStoragePaths.TryParseTrashToken(entry.FileName, out id) ||
                    entry.Result == null || !entry.Result.Success ||
                    entry.Result.Document == null || entry.Result.Document.ProfileId != id ||
                    !IsHash(entry.Result.ContentHash))
                {
                    code = entry == null || entry.Result == null ? ProfileStorageCode.InvalidFormat :
                        (entry.Result.Success ? ProfileStorageCode.InvalidFormat : entry.Result.Code);
                    detail = "INVALID_TRASH_ENTRY";
                    return false;
                }
                candidates.Add(new ProfileTrashPurgeCandidate(entry.FileName, id,
                    entry.Result.ContentHash));
            }
            candidates.Sort(delegate(ProfileTrashPurgeCandidate a, ProfileTrashPurgeCandidate b)
            { return String.CompareOrdinal(a.Token, b.Token); });
            for (int i = 1; i < candidates.Count; i++)
                if (candidates[i - 1].Token == candidates[i].Token)
                {
                    code = ProfileStorageCode.InvalidFormat;
                    detail = "DUPLICATE_TOKEN";
                    return false;
                }
            // Prefix every tuple member with its length; no accidental delimiter collision.
            StringBuilder sb = new StringBuilder();
            foreach (ProfileTrashPurgeCandidate item in candidates)
            {
                sb.Append(item.Token.Length).Append(':').Append(item.Token);
                sb.Append(item.Sha256.Length).Append(':').Append(item.Sha256);
            }
            plan = new ProfileTrashPurgePlan(paths.Root, Hash(Encoding.UTF8.GetBytes(sb.ToString())),
                candidates.ToArray());
            return true;
        }

        private static bool IdenticalSnapshot(ProfileTrashPurgePlan approved,
            ProfileTrashPurgePlan current)
        {
            if (current == null || approved.Count != current.Count ||
                approved.Fingerprint != current.Fingerprint || approved.OwnerRoot != current.OwnerRoot)
                return false;
            for (int i = 0; i < approved.Count; i++)
                if (approved.Candidates[i].Token != current.Candidates[i].Token ||
                    approved.Candidates[i].Sha256 != current.Candidates[i].Sha256 ||
                    approved.Candidates[i].ProfileId != current.Candidates[i].ProfileId)
                    return false;
            return true;
        }

        private bool ConfirmedAbsent(string file)
        {
            paths.GuardDirectories(false, false, false);
            if (!Directory.Exists(paths.Trash)) return false;
            ProfileStoragePaths.GuardDirectoryIfExists(paths.Trash);
            try { File.GetAttributes(file); return false; }
            catch (FileNotFoundException) { return true; }
            // Parent removed/unreadable is UNCERTAIN, never an absence proof.
        }

        private ProfileTrashPurgeResult Aborted(ProfileTrashPurgePlan plan, int deleted,
            bool uncertain, ProfileStorageCode code, string detail)
        {
            ProfileTrashPurgeState state = uncertain ? ProfileTrashPurgeState.Uncertain :
                deleted > 0 ? ProfileTrashPurgeState.Partial : ProfileTrashPurgeState.Rejected;
            // After ANY confirmed deletion, no further profile writes in this game session.
            ProfileStorageCode effective = uncertain || deleted > 0
                ? ProfileStorageCode.RecoveryRequired : code;
            // External file changes can make the actual remaining count unknowable.
            return PurgeResult(state, effective, detail, plan, plan.Count, deleted, -1);
        }

        private static ProfileTrashPurgeResult PurgeResult(ProfileTrashPurgeState state,
            ProfileStorageCode code, string detail, ProfileTrashPurgePlan plan,
            int requested, int deleted, int remaining)
        { return new ProfileTrashPurgeResult(state, code, detail, plan, requested, deleted, remaining); }
    }
}
