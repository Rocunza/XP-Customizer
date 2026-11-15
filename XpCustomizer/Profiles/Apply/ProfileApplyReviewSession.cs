using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace XpCustomizer.Profiles
{
    /// <summary>
    /// Frozen selection consent. The baseline is a private detached snapshot;
    /// a session can be confirmed only once and is never an apply authorization.
    /// </summary>
    internal sealed class ProfileApplyReviewSession
    {
        internal readonly string ProfileId;
        internal readonly string ExpectedHash;
        internal readonly string DisplayName;
        internal readonly string Notes;
        internal readonly DateTime UpdatedUtc;
        internal readonly string SourceGameVersion;
        internal readonly bool ContentDiffers;
        private readonly XpCustomizerSettings baseline;
        private bool confirmationAttempted;

        internal ProfileApplyReviewSession(ProfileDocument doc, string hash,
            XpCustomizerSettings snapshot, bool differs)
        {
            ProfileId = doc.ProfileId;
            ExpectedHash = hash;
            DisplayName = doc.DisplayName;
            Notes = doc.Notes ?? "";
            UpdatedUtc = doc.UpdatedUtc;
            SourceGameVersion = doc.SourceGameVersion;
            ContentDiffers = differs;
            baseline = SettingsSnapshotService.CloneIndependent(snapshot);
        }

        internal bool TryBeginConfirmation()
        {
            if (confirmationAttempted) return false;
            confirmationAttempted = true;
            return true;
        }

        internal bool BaselineMatches(XpCustomizerSettings current, out string mismatch)
        {
            // A review session must still reject ANY change to the live baseline,
            // including non-portable settings and dictionary comparer identity.
            return SettingsApplyCoordinator.Equivalent(baseline, current, out mismatch);
        }
    }

}
