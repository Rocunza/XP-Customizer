using System;
using System.Collections.Generic;

namespace XpCustomizer.Profiles
{
    internal enum ProfileStorageCode
    {
        Ok,
        NotFound,
        AlreadyExists,
        Conflict,
        InvalidArgument,
        InvalidFormat,
        UnsupportedVersion,
        IdMismatch,
        FileTooLarge,
        SecurityRejected,
        AtomicReplaceUnavailable,
        IoError,
        RecoveryRequired,
        InjectedFailure
    }

    internal sealed class ProfileStorageResult
    {
        internal readonly ProfileStorageCode Code;
        internal readonly ProfileDocument Document;
        internal readonly string ContentHash;
        internal readonly string TrashToken;
        internal readonly string DetailCode;
        internal bool Success { get { return Code == ProfileStorageCode.Ok; } }

        internal ProfileStorageResult(ProfileStorageCode code, ProfileDocument document = null,
            string contentHash = null, string trashToken = null, string detailCode = null)
        {
            Code = code;
            Document = document;
            ContentHash = contentHash;
            TrashToken = trashToken;
            DetailCode = detailCode;
        }
    }

    internal sealed class ProfileStorageEntry
    {
        internal readonly string FileName;
        internal readonly ProfileStorageResult Result;
        internal ProfileStorageEntry(string fileName, ProfileStorageResult result)
        { FileName = fileName; Result = result; }
    }

    internal sealed class ProfileStorageListing
    {
        internal readonly ProfileStorageCode Code;
        internal readonly List<ProfileStorageEntry> Entries;
        internal ProfileStorageListing(ProfileStorageCode code, List<ProfileStorageEntry> entries)
        { Code = code; Entries = entries; }
    }

    // Debug-only fault injection sites are inert unless explicitly passed from self-tests.
    internal enum ProfileStorageFault
    {
        None,
        AfterTempWrite,
        BeforeCommit,
        BeforeReplace,
        BeforeSoftDelete,
        BeforeRestore
    }
}
