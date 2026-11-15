using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Verse;

namespace XpCustomizer.Profiles
{
    /// <summary>
    /// R6-P1-B: file persistence ONLY. No Scribe, live settings, GeneDef, pawn or caches.
    /// The caller must explicitly request mutations; nothing runs at startup except isolated tests.
    /// </summary>
    internal sealed partial class ProfileFileRepository
    {
        private const int MaxBytes = 4 * 1024 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly ProfileStoragePaths paths;
        private readonly bool operationLogs;

        internal ProfileFileRepository(ProfileStoragePaths paths, bool operationLogs = true)
        {
            if (paths == null) throw new ArgumentNullException("paths");
            this.paths = paths;
            this.operationLogs = operationLogs;
        }

        internal static ProfileFileRepository ForCurrentUser()
        { return new ProfileFileRepository(ProfileStoragePaths.ForCurrentUser()); }

        internal ProfileStorageResult Create(ProfileDocument document,
            ProfileStorageFault fault = ProfileStorageFault.None)
        {
            string id = document == null ? null : document.ProfileId;
            string stage = null;
            try
            {
                string final = paths.FileForId(id);
                byte[] bytes = EncodeAndVerify(document);
                paths.GuardDirectories(true, false, false);
                using (AcquireWriteLock())
                {
                    if (File.Exists(final)) return Report("CREATE", id, Fail(ProfileStorageCode.AlreadyExists));
                    stage = WriteVerifiedStage(bytes, id);
                    Inject(fault, ProfileStorageFault.AfterTempWrite);
                    Inject(fault, ProfileStorageFault.BeforeCommit);
                    ProfileStoragePaths.GuardRegularFile(final);
                    File.Move(stage, final); // never Delete+Move; refuses existing destination
                    stage = null;
                    ProfileStorageResult verified = ReadFile(final, id);
                    if (!verified.Success || verified.ContentHash != Hash(bytes))
                        return Report("CREATE", id, Fail(ProfileStorageCode.RecoveryRequired, "COMMIT_VERIFY"));
                    return Report("CREATE", id, verified);
                }
            }
            catch (Exception ex) { return Report("CREATE", id, Classify(ex)); }
            finally { RemoveOwnedStage(stage); }
        }

        // Optimistic concurrency: callers must supply the hash returned by Load/List.
        // Backups are intentionally retained. Failed replacement never falls back to delete/move.
        internal ProfileStorageResult Update(ProfileDocument replacement, string expectedHash,
            ProfileStorageFault fault = ProfileStorageFault.None)
        { return UpdateCore(replacement, null, expectedHash, fault, false); }

        // Notes-only: the repository creates the replacement from the freshly loaded
        // on-disk document UNDER the lock, preventing any settings/metadata writes.
        internal ProfileStorageResult UpdateNotes(string profileId, string notes, string expectedHash,
            ProfileStorageFault fault = ProfileStorageFault.None)
        {
            if (!ProfileFormatV2.ValidNotes(notes))
                return Fail(ProfileStorageCode.InvalidArgument, "INVALID_NOTES");
            return UpdateCore(new ProfileDocument { ProfileId = profileId }, notes,
                expectedHash, fault, true);
        }

        private ProfileStorageResult UpdateCore(ProfileDocument replacement, string notes,
            string expectedHash, ProfileStorageFault fault, bool notesOnly)
        {
            string id = replacement == null ? null : replacement.ProfileId;
            string stage = null;
            bool replaceEntered = false;
            try
            {
                if (!IsHash(expectedHash)) return Fail(ProfileStorageCode.InvalidArgument, "EXPECTED_HASH_REQUIRED");
                string final = paths.FileForId(id);
                paths.GuardDirectories(true, false, true);
                using (AcquireWriteLock())
                {
                    ProfileStorageResult old = ReadFile(final, id);
                    if (!old.Success) return Report("UPDATE", id, old);
                    if (old.ContentHash != expectedHash) return Report("UPDATE", id, Fail(ProfileStorageCode.Conflict));
                    ProfileDocument detached = notesOnly ?
                        ProfileJsonCodec.Deserialize(ProfileJsonCodec.Serialize(old.Document)) :
                        ProfileJsonCodec.Deserialize(ProfileJsonCodec.Serialize(replacement));
                    detached.CreatedUtc = old.Document.CreatedUtc; // immutable identity
                    if (notesOnly)
                    {
                        detached.FormatVersion = ProfileFormatV2.FormatVersion;
                        detached.Notes = notes;
                        // UpdatedUtc means snapshot Saved time, NOT notes edit time.
                        detached.UpdatedUtc = old.Document.UpdatedUtc;
                    }
                    else
                    {
                        DateTime now = DateTime.UtcNow;
                        if (now <= old.Document.UpdatedUtc)
                        {
                            if (old.Document.UpdatedUtc == DateTime.MaxValue)
                                return Report("UPDATE", id, Fail(ProfileStorageCode.InvalidArgument, "TIMESTAMP_OVERFLOW"));
                            now = old.Document.UpdatedUtc.AddTicks(1);
                        }
                        detached.UpdatedUtc = now;
                    }
                    byte[] bytes = EncodeAndVerify(detached);
                    stage = WriteVerifiedStage(bytes, id);
                    Inject(fault, ProfileStorageFault.AfterTempWrite);
                    Inject(fault, ProfileStorageFault.BeforeCommit);
                    // Best-effort external editor detection right before File.Replace.
                    ProfileStorageResult recheck = ReadFile(final, id);
                    if (!recheck.Success) return Report("UPDATE", id, recheck);
                    if (recheck.ContentHash != expectedHash) return Report("UPDATE", id, Fail(ProfileStorageCode.Conflict));
                    Inject(fault, ProfileStorageFault.BeforeReplace);
                    string backup = paths.NewBackupName(id);
                    try { replaceEntered = true; File.Replace(stage, final, backup); }
                    catch (PlatformNotSupportedException)
                    { return Report("UPDATE", id, Fail(ProfileStorageCode.AtomicReplaceUnavailable)); }
                    catch (NotSupportedException)
                    { return Report("UPDATE", id, Fail(ProfileStorageCode.AtomicReplaceUnavailable)); }
                    stage = null;
                    ProfileStorageResult verified = ReadFile(final, id);
                    if (!verified.Success || verified.ContentHash != Hash(bytes))
                        return Report("UPDATE", id,
                            Fail(ProfileStorageCode.RecoveryRequired, "BACKUP_RETAINED"));
                    // Keep the immutable backup for manual recovery after a crash or future failure.
                    return Report("UPDATE", id, verified);
                }
            }
            catch (Exception ex)
            {
                // File.Replace may have changed the destination even when
                // it throws. Never report a definite no-write result here.
                if (replaceEntered)
                    return Report("UPDATE", id, Fail(ProfileStorageCode.RecoveryRequired,
                        "FILE_REPLACE_THROWN_UNCERTAIN_" + ex.GetType().Name));
                return Report("UPDATE", id, Classify(ex));
            }
            finally { RemoveOwnedStage(stage); }
        }

        internal ProfileStorageResult Load(string id)
        {
            try
            {
                string final = paths.FileForId(id);
                paths.GuardDirectories(false, false, false);
                return Report("LOAD", id, ReadFile(final, id));
            }
            catch (Exception ex) { return Report("LOAD", id, Classify(ex)); }
        }

        internal ProfileStorageListing List()
        {
            List<ProfileStorageEntry> results = new List<ProfileStorageEntry>();
            try
            {
                paths.GuardDirectories(false, false, false);
                if (!Directory.Exists(paths.Root))
                    return new ProfileStorageListing(ProfileStorageCode.Ok, results);
                foreach (string file in Directory.GetFiles(paths.Root, "*.json", SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileName(file);
                    string id = name.Substring(0, name.Length - 5);
                    string canonical;
                    ProfileStorageResult read = ProfileStoragePaths.TryCanonicalId(id, out canonical)
                        ? ReadFile(file, id) : Fail(ProfileStorageCode.InvalidFormat, "INVALID_FILENAME");
                    results.Add(new ProfileStorageEntry(name, read));
                }
                results.Sort(delegate(ProfileStorageEntry a, ProfileStorageEntry b)
                {
                    if (a.Result.Success != b.Result.Success) return a.Result.Success ? -1 : 1;
                    if (a.Result.Success)
                    {
                        int date = b.Result.Document.UpdatedUtc.CompareTo(a.Result.Document.UpdatedUtc);
                        if (date != 0) return date;
                        return String.CompareOrdinal(a.Result.Document.ProfileId, b.Result.Document.ProfileId);
                    }
                    return String.CompareOrdinal(a.FileName, b.FileName);
                });
                return new ProfileStorageListing(ProfileStorageCode.Ok, results);
            }
            catch (Exception ex)
            { return new ProfileStorageListing(Classify(ex).Code, results); }
        }

        // Enables P1-C to discover recoverable deletions after a full game restart.
        // Listing is read-only and never purges malformed trash entries.
        internal ProfileStorageListing ListTrash()
        {
            List<ProfileStorageEntry> results = new List<ProfileStorageEntry>();
            try
            {
                paths.GuardDirectories(false, false, false);
                if (!Directory.Exists(paths.Trash))
                    return new ProfileStorageListing(ProfileStorageCode.Ok, results);
                ProfileStoragePaths.GuardDirectoryIfExists(paths.Trash);
                foreach (string file in Directory.GetFiles(paths.Trash, "*.deleted",
                    SearchOption.TopDirectoryOnly))
                {
                    string token = Path.GetFileName(file);
                    string id;
                    ProfileStorageResult read = ProfileStoragePaths.TryParseTrashToken(token, out id)
                        ? ReadFile(file, id) : Fail(ProfileStorageCode.InvalidFormat, "INVALID_TRASH_FILENAME");
                    results.Add(new ProfileStorageEntry(token, read));
                }
                results.Sort(delegate(ProfileStorageEntry a, ProfileStorageEntry b)
                { return String.CompareOrdinal(b.FileName, a.FileName); });
                return new ProfileStorageListing(ProfileStorageCode.Ok, results);
            }
            catch (Exception ex)
            { return new ProfileStorageListing(Classify(ex).Code, results); }
        }

        internal ProfileStorageResult SoftDelete(string id, string expectedHash,
            ProfileStorageFault fault = ProfileStorageFault.None)
        {
            try
            {
                if (!IsHash(expectedHash)) return Fail(ProfileStorageCode.InvalidArgument, "EXPECTED_HASH_REQUIRED");
                string final = paths.FileForId(id);
                paths.GuardDirectories(true, true, false);
                using (AcquireWriteLock())
                {
                    ProfileStorageResult old = ReadFile(final, id);
                    if (!old.Success) return Report("DELETE_SOFT", id, old);
                    if (old.ContentHash != expectedHash) return Report("DELETE_SOFT", id, Fail(ProfileStorageCode.Conflict));
                    Inject(fault, ProfileStorageFault.BeforeSoftDelete);
                    string token = paths.NewTrashToken(id);
                    string trash = Path.Combine(paths.Trash, token);
                    ProfileStoragePaths.GuardRegularFile(trash);
                    File.Move(final, trash);
                    // A successful Move alone is not sufficient evidence of a recoverable profile.
                    // Keep the trash copy intact and block further writes on uncertain readback.
                    ProfileStorageResult verified = ReadFile(trash, id);
                    if (!verified.Success || verified.ContentHash != old.ContentHash)
                        return Report("DELETE_SOFT", id,
                            Fail(ProfileStorageCode.RecoveryRequired, "TRASH_VERIFY"));
                    return Report("DELETE_SOFT", id, new ProfileStorageResult(ProfileStorageCode.Ok,
                        old.Document, old.ContentHash, token));
                }
            }
            catch (Exception ex) { return Report("DELETE_SOFT", id, Classify(ex)); }
        }

        // Existing internal/test API preserved. User-facing UI uses the SHA-guarded overload.
        internal ProfileStorageResult Restore(string trashToken,
            ProfileStorageFault fault = ProfileStorageFault.None)
        { return RestoreCore(trashToken, null, fault); }

        internal ProfileStorageResult Restore(string trashToken, string expectedHash,
            ProfileStorageFault fault = ProfileStorageFault.None)
        {
            if (!IsHash(expectedHash))
                return Fail(ProfileStorageCode.InvalidArgument, "EXPECTED_HASH_REQUIRED");
            return RestoreCore(trashToken, expectedHash, fault);
        }

        private ProfileStorageResult RestoreCore(string trashToken, string expectedHash,
            ProfileStorageFault fault)
        {
            string id;
            if (!ProfileStoragePaths.TryParseTrashToken(trashToken, out id))
                return Fail(ProfileStorageCode.InvalidArgument, "INVALID_TRASH_TOKEN");
            try
            {
                string final = paths.FileForId(id);
                string trash = Path.Combine(paths.Trash, trashToken);
                paths.GuardDirectories(true, true, false);
                using (AcquireWriteLock())
                {
                    if (File.Exists(final)) return Report("RESTORE", id, Fail(ProfileStorageCode.AlreadyExists));
                    ProfileStorageResult old = ReadFile(trash, id);
                    if (!old.Success) return Report("RESTORE", id, old);
                    if (expectedHash != null && old.ContentHash != expectedHash)
                        return Report("RESTORE", id, Fail(ProfileStorageCode.Conflict));
                    Inject(fault, ProfileStorageFault.BeforeRestore);
                    File.Move(trash, final);
                    ProfileStorageResult verified = ReadFile(final, id);
                    if (!verified.Success || verified.ContentHash != old.ContentHash)
                        return Report("RESTORE", id, Fail(ProfileStorageCode.RecoveryRequired, "RESTORE_VERIFY"));
                    return Report("RESTORE", id, verified);
                }
            }
            catch (Exception ex) { return Report("RESTORE", id, Classify(ex)); }
        }

        private byte[] EncodeAndVerify(ProfileDocument doc)
        {
            string json = ProfileJsonCodec.Serialize(doc);
            byte[] bytes = Utf8.GetBytes(json); // strict encoder rejects lone surrogates
            if (bytes.Length > MaxBytes) throw new ProfileStorageException(ProfileStorageCode.FileTooLarge);
            string decoded = Utf8.GetString(bytes);
            if (ProfileJsonCodec.Deserialize(decoded).ProfileId != doc.ProfileId)
                throw new ProfileStorageException(ProfileStorageCode.IdMismatch);
            return bytes;
        }

        private ProfileStorageResult ReadFile(string file, string expectedId)
        {
            try
            {
                if (!File.Exists(file)) return Fail(ProfileStorageCode.NotFound);
                ProfileStoragePaths.GuardRegularFile(file);
                byte[] bytes;
                using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length > MaxBytes) return Fail(ProfileStorageCode.FileTooLarge);
                    bytes = new byte[(int)stream.Length];
                    int count = 0;
                    while (count < bytes.Length)
                    {
                        int got = stream.Read(bytes, count, bytes.Length - count);
                        if (got <= 0) return Fail(ProfileStorageCode.IoError, "TRUNCATED_READ");
                        count += got;
                    }
                    if (stream.ReadByte() != -1) return Fail(ProfileStorageCode.FileTooLarge);
                }
                if (bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf)
                    return Fail(ProfileStorageCode.InvalidFormat, "BOM_REJECTED");
                ProfileDocument doc = ProfileJsonCodec.Deserialize(Utf8.GetString(bytes));
                if (doc.ProfileId != expectedId) return Fail(ProfileStorageCode.IdMismatch);
                return new ProfileStorageResult(ProfileStorageCode.Ok, doc, Hash(bytes));
            }
            catch (Exception ex) { return Classify(ex); }
        }

        private string WriteVerifiedStage(byte[] bytes, string expectedId)
        {
            string path = paths.NewTempName();
            // Caller owns the stage even if a write throws. Clean it locally on failure.
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                ProfileStorageResult recheck = ReadFile(path, expectedId);
                if (!recheck.Success || recheck.ContentHash != Hash(bytes))
                    throw new ProfileStorageException(ProfileStorageCode.IoError);
                return path;
            }
            catch { RemoveOwnedStage(path); throw; }
        }

        private FileStream AcquireWriteLock()
        {
            ProfileStoragePaths.GuardRegularFile(paths.LockFile);
            return new FileStream(paths.LockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder(64);
                foreach (byte b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        private static bool IsHash(string hash)
        {
            if (hash == null || hash.Length != 64) return false;
            foreach (char c in hash)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }

        private static void RemoveOwnedStage(string stage)
        {
            if (String.IsNullOrEmpty(stage)) return;
            try
            {
                if (File.Exists(stage) && (File.GetAttributes(stage) & FileAttributes.ReparsePoint) == 0)
                    File.Delete(stage);
            }
            catch { /* original failure has precedence; stale stage never auto-reused */ }
        }

        private static void Inject(ProfileStorageFault actual, ProfileStorageFault site)
        {
            if (actual == site) throw new ProfileStorageException(ProfileStorageCode.InjectedFailure);
        }

        private static ProfileStorageResult Fail(ProfileStorageCode code, string detail = null)
        { return new ProfileStorageResult(code, detailCode: detail); }

        private static ProfileStorageResult Classify(Exception ex)
        {
            ProfileStorageException pse = ex as ProfileStorageException;
            if (pse != null) return Fail(pse.Code);
            ProfileContractException pce = ex as ProfileContractException;
            if (pce != null)
            {
                if (pce.Code == "UNSUPPORTED_VERSION") return Fail(ProfileStorageCode.UnsupportedVersion);
                return Fail(ProfileStorageCode.InvalidFormat, pce.Code);
            }
            if (ex is DecoderFallbackException || ex is EncoderFallbackException)
                return Fail(ProfileStorageCode.InvalidFormat, "UTF8_INVALID");
            if (ex is ArgumentException || ex is OverflowException) return Fail(ProfileStorageCode.InvalidArgument);
            if (ex is UnauthorizedAccessException || ex is SecurityException)
                return Fail(ProfileStorageCode.SecurityRejected);
            if (ex is PlatformNotSupportedException || ex is NotSupportedException)
                return Fail(ProfileStorageCode.AtomicReplaceUnavailable);
            if (ex is IOException) return Fail(ProfileStorageCode.IoError, ex.GetType().Name);
            // No exception messages: they can reveal absolute paths or user-controlled filenames.
            return Fail(ProfileStorageCode.IoError, ex.GetType().Name);
        }

        private ProfileStorageResult Report(string action, string id, ProfileStorageResult result)
        {
            if (operationLogs)
            {
                string canonical;
                string hint = ProfileStoragePaths.TryCanonicalId(id, out canonical)
                    ? canonical.Substring(0, 8) : "INVALID";
                Log.Message("[XPC-R6P1B][PROFILE_STORE] action=" + action +
                    "; status=" + result.Code.ToString().ToUpperInvariant() +
                    "; profileIdPrefix=" + hint + "; detail=" + (result.DetailCode ?? "NONE") +
                    "; build=" + XpCustomizerMod.BuildId);
            }
            return result;
        }

        private sealed class ProfileStorageException : Exception
        {
            internal readonly ProfileStorageCode Code;
            internal ProfileStorageException(ProfileStorageCode code) { Code = code; }
        }
    }
}
