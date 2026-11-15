using System;
using System.Collections.Generic;

namespace XpCustomizer.Profiles
{
    internal enum ProfileTrashPurgeState
    {
        Ready,
        NoOp,
        Complete,
        Rejected,
        Partial,
        Uncertain
    }

    // Only the repository can create a plan. The UI never supplies file paths or names.
    internal sealed class ProfileTrashPurgePlan
    {
        internal readonly string OwnerRoot;
        internal readonly string Fingerprint;
        internal readonly ProfileTrashPurgeCandidate[] Candidates;
        internal int Count { get { return Candidates.Length; } }

        internal ProfileTrashPurgePlan(string ownerRoot, string fingerprint,
            ProfileTrashPurgeCandidate[] candidates)
        {
            OwnerRoot = ownerRoot;
            Fingerprint = fingerprint;
            Candidates = (ProfileTrashPurgeCandidate[])candidates.Clone();
        }
    }

    internal sealed class ProfileTrashPurgeCandidate
    {
        internal readonly string Token;
        internal readonly string ProfileId;
        internal readonly string Sha256;

        internal ProfileTrashPurgeCandidate(string token, string profileId, string sha256)
        { Token = token; ProfileId = profileId; Sha256 = sha256; }
    }

    internal sealed class ProfileTrashPurgeResult
    {
        internal readonly ProfileTrashPurgeState State;
        internal readonly ProfileStorageCode Code;
        internal readonly string Detail;
        internal readonly ProfileTrashPurgePlan Plan;
        internal readonly int Requested;
        internal readonly int DeletedVerified;
        internal readonly int Remaining; // -1 means state could not be determined reliably.
        internal bool IsComplete { get { return State == ProfileTrashPurgeState.Complete; } }

        internal ProfileTrashPurgeResult(ProfileTrashPurgeState state, ProfileStorageCode code,
            string detail, ProfileTrashPurgePlan plan, int requested, int deletedVerified, int remaining)
        {
            State = state;
            Code = code;
            Detail = detail;
            Plan = plan;
            Requested = requested;
            DeletedVerified = deletedVerified;
            Remaining = remaining;
        }
    }
}
