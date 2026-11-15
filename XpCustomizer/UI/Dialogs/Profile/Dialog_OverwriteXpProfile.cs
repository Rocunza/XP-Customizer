using System;
using UnityEngine;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    // R6-P1-C2-E. File replacement requires a separate, explicit confirmation.
    internal sealed class Dialog_OverwriteXpProfile : Window
    {
        private readonly ProfileDocument selected;
        private readonly ProfileOverwritePreflightResult prepared;
        private readonly ProfileCurrentOverwriteService service;
        private readonly Action<ProfileCurrentOverwriteResult> onCompleted;
        private bool attempted;
        private string note = "";
        private Vector2 bodyScrollPosition;

        internal Dialog_OverwriteXpProfile(ProfileDocument doc,
            ProfileOverwritePreflightResult initial,
            Action<ProfileCurrentOverwriteResult> callback,
            ProfileCurrentOverwriteService overwrite = null)
        {
            if (doc == null || initial == null || !initial.Success || !initial.ContentDiffers)
                throw new ArgumentException("Changed valid preflight is required");
            selected = doc;
            prepared = initial;
            onCompleted = callback;
            service = overwrite ?? ProfileCurrentOverwriteService.ForCurrentUser();
            doCloseX = true;
            closeOnCancel = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
#if DEBUG
            Log.Message("[XPC-L10N-L3E][KEYED_PROFILE_DIALOGS] " +
                "version=XPC-L10N-L3E2-PROFILE-DIALOGS-20261112-R1; " +
                "dialog=OVERWRITE; keys=60; sample=" +
                "XPC_ProfileDialog_OverwriteTitle".Translate().ToString() +
                "; build=" + XpCustomizerMod.BuildId);
#endif
#if DEBUG
            Log.Message("[XPC-UI-PROFILE-FONT] version=XPC-UI-PROFILE-FONT-STAGE-B-20261112-R1; " +
                "phase=STAGE_B; dialog=OVERWRITE; scope=DISPLAY_ONLY; bodyFont=Small; " +
                "profileWritesByFontPatch=0; build=" + XpCustomizerMod.BuildId);
#endif
        }

        public override Vector2 InitialSize
        { get { return new Vector2(Mathf.Min(570f, Mathf.Max(340f, Verse.UI.screenWidth - 48f)),
            Mathf.Min(355f, Mathf.Max(250f, Verse.UI.screenHeight - 48f))); } }

        public override void DoWindowContents(Rect rect)
        {
            Widgets.Label(new Rect(0f, 0f, rect.width, 32f), "XPC_ProfileDialog_OverwriteTitle".Translate().ToString());
            Widgets.Label(new Rect(0f, 39f, rect.width, 28f), string.Format("XPC_ProfileDialog_ProfileLabel".Translate().ToString(), selected.DisplayName));
            float y = Mathf.Max(0f, rect.height - 37f);
            Rect body = new Rect(0f, 76f, rect.width, Mathf.Max(1f, y - 84f));
            float textWidth = Mathf.Max(1f, rect.width - 20f);
            string warning = "XPC_ProfileDialog_OverwriteWarning".Translate().ToString();
            string scope = "XPC_ProfileDialog_OverwriteScope".Translate().ToString();
            GameFont old = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                float warningHeight = Mathf.Max(24f, Text.CalcHeight(warning, textWidth) + 4f);
                float scopeHeight = Mathf.Max(24f, Text.CalcHeight(scope, textWidth) + 4f);
                float noteHeight = note.Length == 0 ? 0f :
                    Mathf.Max(24f, Text.CalcHeight(note, textWidth) + 4f);
                float contentHeight = warningHeight + 10f + scopeHeight +
                    (noteHeight > 0f ? 10f + noteHeight : 0f) + 6f;
                Rect content = new Rect(0f, 0f, textWidth, Mathf.Max(body.height, contentHeight));
                Widgets.BeginScrollView(body, ref bodyScrollPosition, content);
                try
                {
                    Widgets.Label(new Rect(0f, 0f, textWidth, warningHeight), warning);
                    Widgets.Label(new Rect(0f, warningHeight + 10f, textWidth, scopeHeight), scope);
                    if (noteHeight > 0f)
                        Widgets.Label(new Rect(0f, warningHeight + 10f + scopeHeight + 10f,
                            textWidth, noteHeight), note);
                }
                finally { Widgets.EndScrollView(); }
            }
            finally
            {
                Text.WordWrap = oldWrap;
                Text.Anchor = oldAnchor;
                Text.Font = old;
            }
            float w = Mathf.Min(162f, (rect.width - 12f) * 0.49f);
            Rect ok = new Rect(rect.width - 2f * w - 8f, y, w, 32f);
            Rect cancel = new Rect(rect.width - w, y, w, 32f);
            if (Widgets.ButtonText(cancel, "XPC_ProfileDialog_Cancel".Translate().ToString())) { Close(); return; }
            bool enabled = GUI.enabled;
            try
            {
                GUI.enabled = enabled && !attempted;
                if (Widgets.ButtonText(ok, "XPC_ProfileDialog_OverwriteButton".Translate().ToString()))
                {
                    attempted = true;
                    ProfileCurrentOverwriteResult result = service.Confirm(
                        selected, prepared, XpCustomizerMod.Settings);
                    if (onCompleted != null)
                    {
                        try { onCompleted(result); }
                        catch (Exception ex) { Log.Warning("[XPC-R6P1C2E][PROFILE_OVERWRITE_UI] " +
                            "callback=" + ex.GetType().Name + "; build=" + XpCustomizerMod.BuildId); }
                    }
                    note = result.Success ? "XPC_ProfileDialog_OverwriteSuccess".Translate().ToString() :
                        string.Format("XPC_ProfileDialog_OverwriteFailure".Translate().ToString(),
                            result.Code, result.Reason);
                    if (result.Success || result.Code == ProfileCurrentOverwriteCode.NoChange) Close();
                }
            }
            finally { GUI.enabled = enabled; }
        }
    }
}
