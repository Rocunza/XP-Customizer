using System;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    /// <summary>
    /// R6-P1-D2-D4: normal user review and explicit single-click Apply.
    /// The safe Release-capable service owns all validation and persistence.
    /// The historical Debug-only ARM/RUN services remain compiled for
    /// diagnostics but are no longer displayed in normal Profile Review UI.
    /// </summary>
    internal sealed class Dialog_ReviewXpProfileApply : Window
    {
        internal const string Revision = "P1-REVIEW-READABILITY-20261109";
        internal const GameFont BodyFont = GameFont.Small;
        private Vector2 reviewScrollPosition;
        private readonly ProfileApplyReviewSession session;
        private readonly ProfileProductionApplyService apply = new ProfileProductionApplyService();
        private bool attempted;
        private string status = "";

        internal Dialog_ReviewXpProfileApply(ProfileApplyReviewSession selected)
        {
            if (selected == null)
                throw new ArgumentNullException("selected");
            session = selected;
            doCloseX = true;
            closeOnCancel = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            draggable = true;

#if DEBUG
            Log.Message("[XPC-L10N-L3E][KEYED_PROFILE_DIALOGS] " +
                "version=XPC-L10N-L3E2-PROFILE-DIALOGS-20261112-R1; " +
                "dialog=APPLY_REVIEW; keys=60; sample=" +
                "XPC_ProfileDialog_ReviewTitle".Translate().ToString() +
                "; build=" + XpCustomizerMod.BuildId);
#endif
            Log.Message("[XPC-UI-U2][PROFILE_DIALOG_BUTTON_ORDER] " +
                "version=XPC-UI-PROFILE-DIALOG-ORDER-U2-20261111-R1; " +
                "event=CONSTRUCTED; dialog=APPLY_REVIEW; primary=LEFT; cancel=RIGHT; " +
                "label=Cancel; build=" + XpCustomizerMod.BuildId);
        }

        public override Vector2 InitialSize
        {
            get { return ProfileReviewLayoutP1.InitialSize(Verse.UI.screenWidth, Verse.UI.screenHeight); }
        }

        public override void DoWindowContents(Rect rect)
        {
            Rect viewport, left, right;
            ProfileReviewLayoutP1.Calculate(rect, out viewport, out left, out right);
            float contentWidth = Mathf.Max(1f, viewport.width - 20f);
            // Measure and draw with the same font/width. Scroll only the text;
            // confirmation buttons are always visible and keep their original order.
            float requiredHeight = DrawReviewText(contentWidth, false);
            Rect content = new Rect(0f, 0f, contentWidth,
                Mathf.Max(viewport.height, requiredHeight + 3f));
            Widgets.BeginScrollView(viewport, ref reviewScrollPosition, content);
            try { DrawReviewText(contentWidth, true); }
            finally { Widgets.EndScrollView(); }

            if (Widgets.ButtonText(right, "XPC_ProfileDialog_Cancel".Translate().ToString())) { Close(); return; }
            bool enabled = GUI.enabled;
            try
            {
                GUI.enabled = enabled && session.ContentDiffers && !attempted &&
                    (Find.TickManager == null || Find.TickManager.Paused) &&
                    !ProfileProductionWriteGate.IsUncertain && !ProfileProductionWriteGate.IsBusy &&
                    !RuntimeRecoverySessionGate.LiveIsBlocked &&
                    !ProfileWriteSessionGate.IsBlocked;
                if (Widgets.ButtonText(left,
                    session.ContentDiffers ? "XPC_ProfileDialog_ReviewApplyButton".Translate().ToString() :
                        "XPC_ProfileDialog_ReviewAlreadyButton".Translate().ToString()))
                {
                    // Prepare is strictly read-only; a definite preflight
                    // rejection must not consume this review's Apply attempt.
                    ProfileProductionApplyResult prepared = apply.Prepare(session, XpCustomizerMod.Settings);
                    attempted = prepared.Prepared; // after consent, single-use forever
                    ProfileProductionApplyResult result = prepared.Prepared ?
                        apply.ConfirmAndApply(prepared.Session, XpCustomizerMod.Settings) : prepared;
                    bool succeeded = result.Code == ProfileProductionApplyCode.WrittenVerifiedNoRestart;
                    status = succeeded ? "XPC_ProfileDialog_ReviewSuccess".Translate().ToString() :
                        string.Format("XPC_ProfileDialog_ReviewFailure".Translate().ToString(),
                            LocalizedApplyCode(result.Code), result.Reason);
                    Messages.Message(status,
                        succeeded ? MessageTypeDefOf.NeutralEvent : MessageTypeDefOf.RejectInput, false);
                    Log.Message("[XPC-R6P1D2D4][PROFILE_APPLY_UI] revision=" + Revision +
                        "; action=APPLY; status=" + result.Code +
                        "; profileJsonWrites=0; build=" + XpCustomizerMod.BuildId);
                    if (succeeded) Close();
                }
            }
            finally { GUI.enabled = enabled; }
        }

        // Render a localized explanation; retain the raw reason token for diagnostics.
        // Do not use translated text to decide Apply readiness or success.
        private static string LocalizedApplyCode(ProfileProductionApplyCode code)
        {
            switch (code)
            {
                case ProfileProductionApplyCode.Prepared: return "XPC_ProfileDialog_ApplyPrepared".Translate().ToString();
                case ProfileProductionApplyCode.NoChange: return "XPC_ProfileDialog_ApplyNoChange".Translate().ToString();
                case ProfileProductionApplyCode.Rejected: return "XPC_ProfileDialog_ApplyRejected".Translate().ToString();
                case ProfileProductionApplyCode.Conflict: return "XPC_ProfileDialog_ApplyConflict".Translate().ToString();
                case ProfileProductionApplyCode.RuntimeRecoveredNoWrite: return "XPC_ProfileDialog_ApplyRecoveredNoWrite".Translate().ToString();
                case ProfileProductionApplyCode.RuntimeRecoveryRequired: return "XPC_ProfileDialog_ApplyRecoveryRequired".Translate().ToString();
                case ProfileProductionApplyCode.PersistenceUncertain: return "XPC_ProfileDialog_ApplyPersistenceUncertain".Translate().ToString();
                case ProfileProductionApplyCode.WrittenVerifiedNoRestart: return "XPC_ProfileDialog_ReviewSuccess".Translate().ToString();
                default: return code.ToString();
            }
        }

        private float DrawReviewText(float width, bool draw)
        {
            GameFont previous = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;
            bool previousWrap = Text.WordWrap;
            try
            {
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                float y = 0f;
                y = DrawLine(width, y, "XPC_ProfileDialog_ReviewTitle".Translate().ToString(), GameFont.Medium, 31f, 8f, draw);
                y = DrawLine(width, y, string.Format("XPC_ProfileDialog_ProfileLabel".Translate().ToString(), Clip(session.DisplayName, 60)),
                    BodyFont, 24f, 12f, draw);
                y = DrawLine(width, y,
                    string.Format("XPC_ProfileDialog_ReviewSavedGame".Translate().ToString(),
                        session.UpdatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                        Clip(session.SourceGameVersion, 28)),
                    BodyFont, 24f, 10f, draw);
                if (!String.IsNullOrEmpty(session.Notes))
                {
                    y = DrawLine(width, y,
                        "XPC_ProfileDialog_NotesLabel".Translate().ToString(),
                        BodyFont, 24f, 4f, draw);
                    y = DrawLine(width, y,
                        BundledProfileNotesLocalization.Display(session.ProfileId, session.Notes),
                        BodyFont, 24f, 15f, draw);
                }
                y = DrawLine(width, y, session.ContentDiffers
                    ? "XPC_ProfileDialog_ReviewReplace".Translate().ToString()
                    : "XPC_ProfileDialog_ReviewAlready".Translate().ToString(),
                    BodyFont, 24f, 5f, draw);
                if (session.ContentDiffers && Find.TickManager != null &&
                    !Find.TickManager.Paused)
                    y = DrawLine(width, y,
                        "XPC_ProfileDialog_ReviewPauseNeeded".Translate().ToString(),
                        BodyFont, 24f, 5f, draw);
                if (status.Length > 0)
                    y = DrawLine(width, y, Clip(status, 220), BodyFont, 24f, 5f, draw);
                return y;
            }
            finally
            {
                Text.Font = previous;
                Text.Anchor = previousAnchor;
                Text.WordWrap = previousWrap;
            }
        }

        private static float DrawLine(float width, float y, string text,
            GameFont font, float minHeight, float bottomGap, bool draw)
        {
            Text.Font = font;
            float height = Mathf.Max(minHeight, Text.CalcHeight(text, width) + 2f);
            if (draw) Widgets.Label(new Rect(0f, y, width, height), text);
            return y + height + bottomGap;
        }

        public override void PostClose()
        {
            base.PostClose();
            if (!attempted)
                Log.Message("[XPC-R6P1D2D4][PROFILE_APPLY_UI] action=CLOSE_NO_COMMIT; " +
                    "settingsWritesByUi=0; profileJsonWrites=0; build=" + XpCustomizerMod.BuildId);
        }

        private static string Clip(string value, int limit)
        {
            if (String.IsNullOrEmpty(value)) return "(unknown)";
            string result = value.Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
            return result.Length <= limit ? result : result.Substring(0, limit - 3) + "...";
        }
    }
    /// <summary>Pure window/button/viewport geometry for P1; no side effects.</summary>
    internal static class ProfileReviewLayoutP1
    {
        internal static Vector2 InitialSize(float screenWidth, float screenHeight)
        {
            return new Vector2(
                Mathf.Min(675f, Mathf.Max(300f, screenWidth - 48f)),
                Mathf.Min(440f, Mathf.Max(300f, screenHeight - 48f)));
        }

        internal static void Calculate(Rect rect, out Rect viewport,
            out Rect apply, out Rect cancel)
        {
            float width = rect.width;
            float buttonWidth = Mathf.Min(173f, Mathf.Max(75f, (width - 8f) / 2f));
            float buttonY = rect.height - 37f;
            apply = new Rect(width - 2f * buttonWidth - 8f, buttonY, buttonWidth, 32f);
            cancel = new Rect(width - buttonWidth, buttonY, buttonWidth, 32f);
            viewport = new Rect(0f, 0f, Mathf.Max(1f, width),
                Mathf.Max(1f, buttonY - 12f));
        }
    }

}
