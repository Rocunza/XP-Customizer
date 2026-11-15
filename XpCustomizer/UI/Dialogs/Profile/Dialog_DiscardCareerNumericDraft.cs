using System;
using UnityEngine;
using Verse;

namespace XpCustomizer.UI
{
    /// <summary>
    /// N1-R1: explicit consent to discard ONLY an uncommitted numeric text
    /// buffer. No mutation occurs while this dialog is displayed or canceled.
    /// Callback is delivered exactly once from PostClose (X/ESC included).
    /// </summary>
    internal sealed class Dialog_DiscardCareerNumericDraft : Window
    {
        private readonly Action<bool> onDecision;
        private bool accepted;
        private bool reported;
        private Vector2 bodyScrollPosition;

        internal Dialog_DiscardCareerNumericDraft(Action<bool> decision)
        {
            if (decision == null) throw new ArgumentNullException("decision");
            onDecision = decision;
            doCloseX = true;
            closeOnCancel = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            draggable = true;
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(
                Mathf.Min(560f, Mathf.Max(340f, Verse.UI.screenWidth - 48f)),
                Mathf.Min(290f, Mathf.Max(235f, Verse.UI.screenHeight - 48f))); }
        }

        public override void DoWindowContents(Rect rect)
        {
            float width = rect.width;
            float buttonY = Mathf.Max(0f, rect.height - 37f);
            Rect viewport = new Rect(0f, 43f, width,
                Mathf.Max(1f, buttonY - 51f));
            float textWidth = Mathf.Max(1f, viewport.width - 20f);
            string title = "XPC_N1R1_RecoveryTitle".Translate().ToString();
            string warning = "XPC_N1R1_RecoveryBody".Translate().ToString();
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0f, 0f, width, 36f), title);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                float bodyHeight = Mathf.Max(32f,
                    Text.CalcHeight(warning, textWidth) + 8f);
                Rect content = new Rect(0f, 0f, textWidth,
                    Mathf.Max(viewport.height, bodyHeight));
                Widgets.BeginScrollView(viewport, ref bodyScrollPosition, content);
                try { Widgets.Label(new Rect(0f, 0f, textWidth, bodyHeight), warning); }
                finally { Widgets.EndScrollView(); }
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                Text.WordWrap = oldWrap;
            }

            float buttonWidth = Mathf.Min(184f, (width - 12f) * 0.48f);
            Rect confirm = new Rect(width - 2f * buttonWidth - 8f,
                buttonY, buttonWidth, 33f);
            Rect cancel = new Rect(width - buttonWidth, buttonY,
                buttonWidth, 33f);
            // Deliberate: Cancel remains the right-hand button.
            if (Widgets.ButtonText(cancel,
                "XPC_N1R1_RecoveryCancel".Translate().ToString()))
            { Close(); return; }
            if (Widgets.ButtonText(confirm,
                "XPC_N1R1_RecoveryConfirm".Translate().ToString()))
            { accepted = true; Close(); }
        }

        public override void PostClose()
        {
            base.PostClose();
            if (reported) return;
            reported = true;
            try { onDecision(accepted); }
            catch (Exception ex)
            {
                Log.Error("[XPC-N1-RECOVERY] action=DECISION_CALLBACK_FAILED; " +
                    "reason=" + ex.GetType().Name +
                    "; settingsWritesByDialog=0; build=" + XpCustomizerMod.BuildId);
            }
        }
    }
}
