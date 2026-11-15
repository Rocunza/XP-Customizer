using System;
using UnityEngine;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    /// <summary>Explicit, single-use confirmation for the entire frozen trash snapshot.</summary>
    internal sealed class Dialog_EmptyXpProfileTrash : Window
    {
        private readonly ProfileTrashPurgePlan approved;
        private readonly Action<ProfileTrashPurgePlan> onConfirm;
        private bool attempted;
        private Vector2 bodyScrollPosition;

        internal Dialog_EmptyXpProfileTrash(ProfileTrashPurgePlan prepared,
            Action<ProfileTrashPurgePlan> confirm)
        {
            if (prepared == null || prepared.Count <= 0) throw new ArgumentException("Empty plan.");
            if (confirm == null) throw new ArgumentNullException("confirm");
            approved = prepared;
            onConfirm = confirm;
            closeOnCancel = true;
            closeOnClickedOutside = false;
            doCloseX = true;
            absorbInputAroundWindow = true;
            draggable = true;
#if DEBUG
            Log.Message("[XPC-L10N-L3E][KEYED_PROFILE_DIALOGS] " +
                "version=XPC-L10N-L3E2-PROFILE-DIALOGS-20261112-R1; " +
                "dialog=TRASH_PURGE; keys=60; sample=" +
                "XPC_ProfileDialog_TrashTitle".Translate().ToString() +
                "; build=" + XpCustomizerMod.BuildId);
#endif
#if DEBUG
            Log.Message("[XPC-UI-PROFILE-FONT] version=XPC-UI-PROFILE-FONT-STAGE-B-20261112-R1; " +
                "phase=STAGE_B; dialog=TRASH_PURGE; scope=DISPLAY_ONLY; bodyFont=Small; " +
                "profileWritesByFontPatch=0; build=" + XpCustomizerMod.BuildId);
#endif
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(Mathf.Min(555f, Mathf.Max(340f, Verse.UI.screenWidth - 48f)),
                Mathf.Min(294f, Mathf.Max(214f, Verse.UI.screenHeight - 48f))); }
        }

        public override void PostClose()
        {
            base.PostClose();
            if (!attempted)
                Log.Message("[XPC-R6P1C2D][TRASH_PURGE] phase=CANCEL; " +
                    "status=CANCELLED_NO_WRITE; requested=" + approved.Count +
                    "; deletedVerified=0; configXmlWrites=0; profileApplyByThisAction=NOT_INVOKED; " +
                    "build=" + XpCustomizerMod.BuildId);
        }

        public override void DoWindowContents(Rect inRect)
        {
            float w = inRect.width;
            float y = Mathf.Max(0f, inRect.height - 37f);
            Rect body = new Rect(0f, 81f, w, Mathf.Max(1f, y - 89f));
            float textWidth = Mathf.Max(1f, w - 20f);
            string warning = "XPC_ProfileDialog_TrashWarning".Translate().ToString();
            string recheck = "XPC_ProfileDialog_TrashRecheck".Translate().ToString();
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0f, 0f, w, 32f), "XPC_ProfileDialog_TrashTitle".Translate().ToString());
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(0f, 45f, w, 28f),
                    string.Format("XPC_ProfileDialog_TrashCount".Translate().ToString(), approved.Count));
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                float warningHeight = Mathf.Max(24f, Text.CalcHeight(warning, textWidth) + 4f);
                float recheckHeight = Mathf.Max(24f, Text.CalcHeight(recheck, textWidth) + 4f);
                Rect content = new Rect(0f, 0f, textWidth,
                    Mathf.Max(body.height, warningHeight + 10f + recheckHeight + 6f));
                Widgets.BeginScrollView(body, ref bodyScrollPosition, content);
                try
                {
                    Widgets.Label(new Rect(0f, 0f, textWidth, warningHeight), warning);
                    Widgets.Label(new Rect(0f, warningHeight + 10f, textWidth, recheckHeight), recheck);
                }
                finally { Widgets.EndScrollView(); }
            }
            finally
            {
                Text.WordWrap = oldWrap;
                Text.Anchor = oldAnchor;
                Text.Font = oldFont;
            }
            float width = Mathf.Min(179f, (w - 12f) * 0.49f);
            Rect confirm = new Rect(w - width * 2f - 8f, y, width, 33f);
            Rect cancel = new Rect(w - width, y, width, 33f);
            bool old = GUI.enabled;
            bool pressed;
            try
            {
                GUI.enabled = old && !attempted && !ProfileWriteSessionGate.IsBlocked;
                pressed = Widgets.ButtonText(confirm, "XPC_ProfileDialog_TrashButton".Translate().ToString());
            }
            finally { GUI.enabled = old; }
            if (Widgets.ButtonText(cancel, "XPC_ProfileDialog_Cancel".Translate().ToString())) { Close(); return; }
            if (!pressed) return;
            attempted = true;
            Close();
            onConfirm(approved);
        }
    }
}
