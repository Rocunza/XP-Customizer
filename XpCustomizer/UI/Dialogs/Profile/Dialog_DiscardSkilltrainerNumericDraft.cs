using System;
using UnityEngine;
using Verse;

namespace XpCustomizer.UI
{
    /// <summary>
    /// S2 modal: X, Escape and Keep Editing all report false exactly once.
    /// No settings or text buffer is changed before explicit confirmation.
    /// </summary>
    internal sealed class Dialog_DiscardSkilltrainerNumericDraft : Window
    {
        private readonly Action<bool> onDecision;
        private bool accepted;
        private bool reported;
        private Vector2 scroll;

        internal Dialog_DiscardSkilltrainerNumericDraft(Action<bool> decision)
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
            string title = "XPC_N1R2S2_RecoveryTitle".Translate().ToString();
            string warning = "XPC_N1R2S2_RecoveryBody".Translate().ToString();
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
                float textHeight = Mathf.Max(32f,
                    Text.CalcHeight(warning, textWidth) + 8f);
                Rect content = new Rect(0f, 0f, textWidth,
                    Mathf.Max(viewport.height, textHeight));
                Widgets.BeginScrollView(viewport, ref scroll, content);
                try { Widgets.Label(new Rect(0f, 0f, textWidth, textHeight), warning); }
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
            if (Widgets.ButtonText(cancel,
                "XPC_N1R2S2_RecoveryCancel".Translate().ToString()))
            { Close(); return; }
            if (Widgets.ButtonText(confirm,
                "XPC_N1R2S2_RecoveryConfirm".Translate().ToString()))
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
                Log.Error("[XPC-N1-R2R3] stage=S2; action=DECISION_CALLBACK_FAILED" +
                    "; reason=" + ex.GetType().Name +
                    "; settingsWritesByDialog=0; build=" + XpCustomizerMod.BuildId);
            }
        }
    }
}
