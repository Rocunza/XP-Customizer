using System;
using Verse;

namespace XpCustomizer.Profiles
{
    /// <summary>
    /// R6-P1-D2-1. Separate, one-use consent gate for a FUTURE profile apply.
    /// This phase has no apply-capable output or callback: all outcomes are read-only.
    /// A D1 review session must never be used as an actual apply authorization.
    /// </summary>
    internal enum ProfileApplyGateCode
    {
        PreparedChanged,
        NoChange,
        EligibleReadOnly,
        Rejected,
        Duplicate,
        Conflict
    }

    internal sealed class ProfileApplyGateSession
    {
        private readonly ProfileApplyReviewSession review;
        private bool confirmationAttempted;

        internal ProfileApplyGateSession(ProfileApplyReviewSession reviewSession)
        {
            if (reviewSession == null || !reviewSession.ContentDiffers)
                throw new ArgumentException("A changed, validated review is required", "reviewSession");
            review = reviewSession;
        }

        // No ProfileDocument, SettingsSnapshot or apply delegate leaves the gate.
        internal ProfileApplyReviewSession ReviewForReadOnlyRecheck { get { return review; } }
        internal bool TryConsume()
        {
            if (confirmationAttempted) return false;
            confirmationAttempted = true; // consumed before ANY confirmation work
            return true;
        }
    }

    internal sealed class ProfileApplyGateResult
    {
        internal readonly ProfileApplyGateCode Code;
        internal readonly string Reason;
        internal readonly ProfileApplyGateSession Session;
        internal bool Prepared { get { return Code == ProfileApplyGateCode.PreparedChanged && Session != null; } }

        internal ProfileApplyGateResult(ProfileApplyGateCode code, string reason,
            ProfileApplyGateSession session)
        {
            Code = code;
            Reason = reason ?? "NONE";
            Session = session;
        }
    }

    internal sealed class ProfileApplyGateService
    {
        internal const string Revision = "R6P1D2A1-ONE-SHOT-READONLY-20261107";
        private readonly ProfileApplyPreflightService preflight;
        private readonly bool logging;

        internal ProfileApplyGateService(ProfileApplyPreflightService preflight, bool logging = true)
        {
            if (preflight == null) throw new ArgumentNullException("preflight");
            this.preflight = preflight;
            this.logging = logging;
        }

        // PREPARE is not a user click and does not count as consent.
        internal ProfileApplyGateResult Prepare(ProfileDocument selected, string selectedHash,
            XpCustomizerSettings live)
        {
            ProfileApplyReviewResult checkedReview = preflight.Open(selected, selectedHash, live);
            if (checkedReview.Code == ProfileApplyReviewCode.ReadyNoChange)
                return Report("PREPARE", ProfileApplyGateCode.NoChange, "NO_PORTABLE_CHANGES", null);
            if (checkedReview.Code != ProfileApplyReviewCode.ReadyChanged ||
                checkedReview.Session == null)
                return Report("PREPARE", ProfileApplyGateCode.Rejected,
                    "PREFLIGHT_" + checkedReview.Code, null);
            return Report("PREPARE", ProfileApplyGateCode.PreparedChanged,
                "EXPLICIT_CONFIRMATION_REQUIRED", new ProfileApplyGateSession(checkedReview.Session));
        }

        /// <summary>
        /// Rechecks SHA, profile metadata/schema, strict live baseline, editor state,
        /// and recovery lock through D1's proven read-only confirmation. Even after
        /// explicit consent this stage NEVER returns a live-apply token or candidate.
        /// The eventual write phase MUST revalidate and hold a transaction guard.
        /// </summary>
        internal ProfileApplyGateResult ConfirmForReadOnlyAudit(ProfileApplyGateSession session,
            XpCustomizerSettings live, bool explicitUserConfirmation)
        {
            if (session == null)
                return Report("CONFIRM", ProfileApplyGateCode.Rejected, "NULL_SESSION", null);
            if (!session.TryConsume())
                return Report("CONFIRM", ProfileApplyGateCode.Duplicate, "SESSION_ALREADY_CONSUMED", null);
            if (!explicitUserConfirmation)
                return Report("CONFIRM", ProfileApplyGateCode.Rejected, "USER_DID_NOT_CONFIRM", null);
            ProfileApplyReviewResult recheck = preflight.ConfirmDryRun(
                session.ReviewForReadOnlyRecheck, live);
            if (recheck.Code != ProfileApplyReviewCode.ReadyChanged || !recheck.Success)
                return Report("CONFIRM", recheck.Code == ProfileApplyReviewCode.Conflict ?
                    ProfileApplyGateCode.Conflict : ProfileApplyGateCode.Rejected,
                    "RECHECK_" + recheck.Code, null);
            return Report("CONFIRM", ProfileApplyGateCode.EligibleReadOnly,
                "VALIDATED_BUT_LIVE_APPLY_BLOCKED", null);
        }

        private ProfileApplyGateResult Report(string phase, ProfileApplyGateCode code,
            string reason, ProfileApplyGateSession session)
        {
            ProfileApplyGateResult result = new ProfileApplyGateResult(code, reason, session);
            if (logging)
                Log.Message("[XPC-R6P1D2A1][PROFILE_APPLY_GATE] revision=" + Revision +
                    "; phase=" + phase + "; status=" + code + "; reason=" + result.Reason +
                    "; consentRequiredForConfirm=True; oneShot=True; " +
                    "candidateExport=NO; liveApplyByReadOnlyGate=NOT_INVOKED; productionCommit=SEPARATE_ROUTE; profileFileWrites=0; " +
                    "liveSettingsWrites=0; geneDefWrites=0; pawnWrites=0; " +
                    "scribeCalls=0; build=" + XpCustomizerMod.BuildId);
            return result;
        }
    }
}
