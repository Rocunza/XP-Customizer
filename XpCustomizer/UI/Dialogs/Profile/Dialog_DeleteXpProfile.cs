using System;
using UnityEngine;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    /// <summary>
    /// R6-P1-C2-C: explicit confirmation only. Does not delete a file itself.
    /// The profile ID and selected SHA are frozen when this dialog is opened.
    /// </summary>
    internal sealed class Dialog_DeleteXpProfile : Window
    {
        private readonly string profileId;
        private readonly string expectedHash;
        private readonly string displayName;
        private readonly Action<string, string> onConfirm;
        private bool attempted;
        private Vector2 bodyScrollPosition;

        internal Dialog_DeleteXpProfile(ProfileDocument document, string selectedHash,
            Action<string, string> confirmed)
        {
            if (document == null) throw new ArgumentNullException("document");
            if (confirmed == null) throw new ArgumentNullException("confirmed");
            profileId = document.ProfileId;
            expectedHash = selectedHash;
            displayName = document.DisplayName ?? "XPC_ProfileDialog_Unnamed".Translate().ToString();
            onConfirm = confirmed;
            closeOnCancel = true;
            closeOnClickedOutside = false;
            doCloseX = true;
            absorbInputAroundWindow = true;
            draggable = true;
#if DEBUG
            Log.Message("[XPC-L10N-L3E][KEYED_PROFILE_DIALOGS] " +
                "version=XPC-L10N-L3E2-PROFILE-DIALOGS-20261112-R1; " +
                "dialog=DELETE; keys=60; sample=" +
                "XPC_ProfileDialog_DeleteTitle".Translate().ToString() +
                "; build=" + XpCustomizerMod.BuildId);
#endif
#if DEBUG
            Log.Message("[XPC-UI-PROFILE-FONT] version=XPC-UI-PROFILE-FONT-STAGE-B-20261112-R1; " +
                "phase=STAGE_B; dialog=DELETE; scope=DISPLAY_ONLY; bodyFont=Small; " +
                "profileWritesByFontPatch=0; build=" + XpCustomizerMod.BuildId);
#endif
        }

        public override Vector2 InitialSize
        {
            get { return new Vector2(Mathf.Min(520f, Mathf.Max(340f, Verse.UI.screenWidth - 48f)),
                Mathf.Min(280f, Mathf.Max(205f, Verse.UI.screenHeight - 48f))); }
        }

        public override void DoWindowContents(Rect inRect)
        {
            float w = inRect.width;
            // B: keep approval controls fixed; make every warning readable without
            // ever drawing under the buttons, even for narrow Korean layouts.
            float y = Mathf.Max(0f, inRect.height - 37f);
            Rect body = new Rect(0f, 78f, w, Mathf.Max(1f, y - 86f));
            float textWidth = Mathf.Max(1f, w - 20f);
            string warning = "XPC_ProfileDialog_DeleteWarning".Translate().ToString();
            string identity = string.Format("XPC_ProfileDialog_ProfileId".Translate().ToString(),
                profileId ?? "INVALID");
            GameFont before = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;
            bool previousWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0f, 0f, w, 32f), "XPC_ProfileDialog_DeleteTitle".Translate().ToString());
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(0f, 43f, w, 27f),
                    string.Format("XPC_ProfileDialog_ProfileLabel".Translate().ToString(), Clip(displayName, 56)));
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                float warningHeight = Mathf.Max(24f, Text.CalcHeight(warning, textWidth) + 4f);
                float identityHeight = Mathf.Max(24f, Text.CalcHeight(identity, textWidth) + 4f);
                Rect view = new Rect(0f, 0f, textWidth,
                    Mathf.Max(body.height, warningHeight + 10f + identityHeight + 6f));
                Widgets.BeginScrollView(body, ref bodyScrollPosition, view);
                try
                {
                    Widgets.Label(new Rect(0f, 0f, textWidth, warningHeight), warning);
                    Widgets.Label(new Rect(0f, warningHeight + 10f, textWidth, identityHeight), identity);
                }
                finally { Widgets.EndScrollView(); }
            }
            finally
            {
                Text.WordWrap = previousWrap;
                Text.Anchor = previousAnchor;
                Text.Font = before;
            }

            float button = Mathf.Min(160f, (w - 12f) * 0.48f);
            Rect delete = new Rect(w - 2f * button - 8f, y, button, 33f);
            Rect cancel = new Rect(w - button, y, button, 33f);
            bool enabled = GUI.enabled;
            bool clicked = false;
            try
            {
                GUI.enabled = enabled && !attempted && !ProfileWriteSessionGate.IsBlocked;
                clicked = Widgets.ButtonText(delete, "XPC_ProfileDialog_DeleteButton".Translate().ToString());
            }
            finally { GUI.enabled = enabled; }
            if (Widgets.ButtonText(cancel, "XPC_ProfileDialog_Cancel".Translate().ToString())) { Close(); return; }
            if (!clicked) return;
            attempted = true;
            Close();
            // Parent window validates its *current* selection again before writing.
            onConfirm(profileId, expectedHash);
        }

        private static string Clip(string text, int limit)
        {
            string safe = (text ?? "").Replace('\r', ' ').Replace('\n', ' ');
            return safe.Length <= limit ? safe : safe.Substring(0, limit - 3) + "...";
        }
    }
}
