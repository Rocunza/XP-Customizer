using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace XpCustomizer.Profiles
{
    /// <summary>R6-P1-D1. Read-only profile import/review; never calls a live apply or file writer.</summary>
    internal enum ProfileApplyReviewCode
    {
        ReadyChanged, ReadyNoChange, InvalidSelection, Conflict, PendingEdit,
        Uncertain, InvalidProfile, InvalidSettings, StorageFailure, RecoveryRequired,
        InternalFailure, Cancelled
    }

    /// <summary>Pure user-facing explanations for read-only review failures.</summary>
    internal static class ProfileApplyReviewMessage
    {
        internal static string Explain(ProfileApplyReviewCode code)
        {
            switch (code)
            {
                case ProfileApplyReviewCode.InvalidSelection: return "The selected profile is invalid";
                case ProfileApplyReviewCode.Conflict: return "The profile or current settings changed; reselect it";
                case ProfileApplyReviewCode.PendingEdit: return "Finish editing the numeric field first";
                case ProfileApplyReviewCode.Uncertain: return "An editor is not ready; close and reopen the settings";
                case ProfileApplyReviewCode.InvalidProfile: return "The profile data failed validation";
                case ProfileApplyReviewCode.InvalidSettings: return "Current XP settings failed validation";
                case ProfileApplyReviewCode.StorageFailure: return "Could not safely read the saved profile";
                case ProfileApplyReviewCode.RecoveryRequired: return "Recovery lock is active; restart after preserving backups";
                case ProfileApplyReviewCode.InternalFailure: return "An internal review error occurred";
                case ProfileApplyReviewCode.Cancelled: return "Review was cancelled";
                default: return "Review could not be completed";
            }
        }
    }

    internal sealed class ProfileApplyReviewResult
    {
        internal readonly ProfileApplyReviewCode Code;
        internal readonly ProfileStorageCode StorageCode;
        internal readonly string Detail;
        internal readonly ProfileApplyReviewSession Session;
        internal bool Success { get { return Code == ProfileApplyReviewCode.ReadyChanged ||
            Code == ProfileApplyReviewCode.ReadyNoChange; } }

        internal ProfileApplyReviewResult(ProfileApplyReviewCode code, ProfileStorageCode storage,
            string detail, ProfileApplyReviewSession session)
        { Code = code; StorageCode = storage; Detail = detail ?? "NONE"; Session = session; }
    }

}
