using System;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    /// <summary>Explicit notes-only editor. Save never calls profile Apply or settings persistence.</summary>
    internal sealed class Dialog_EditXpProfileNotes : Window
    {
        private readonly string profileId;
        private readonly string expectedHash;
        private readonly string displayName;
        private readonly Action<ProfileNotesResult> onCompleted;
        private readonly ProfileNotesService service = ProfileNotesService.ForCurrentUser();
        private string notes;
        private string message = "";
        private bool busy;
        private bool terminal;

        internal Dialog_EditXpProfileNotes(ProfileDocument selected, string hash,
            Action<ProfileNotesResult> completed)
        {
            if (selected == null || hash == null) throw new ArgumentNullException("selected/hash");
            profileId = selected.ProfileId;
            expectedHash = hash;
            displayName = selected.DisplayName;
            notes = selected.Notes ?? "";
            onCompleted = completed;
            doCloseX = true;
            closeOnCancel = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            draggable = true;
        }

        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(Mathf.Min(680f, Mathf.Max(340f, Verse.UI.screenWidth - 48f)),
                    Mathf.Min(440f, Mathf.Max(260f, Verse.UI.screenHeight - 48f)));
            }
        }

        public override void DoWindowContents(Rect rect)
        {
            GameFont oldFont = Text.Font;
            try
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0f, 0f, rect.width, 32f),
                    "XPC_ProfileDialog_NotesTitle".Translate().ToString());
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(0f, 37f, rect.width, 25f), displayName);
                Widgets.Label(new Rect(0f, 66f, rect.width, 23f),
                    "XPC_ProfileDialog_NotesLabel".Translate().ToString());
                float footerY = rect.height - 38f;
                float editorH = Mathf.Max(50f, footerY - 92f - 31f);
                bool oldEnabled = GUI.enabled;
                try
                {
                    GUI.enabled = oldEnabled && !busy && !terminal;
                    string input = Widgets.TextArea(new Rect(0f, 92f, rect.width, editorH), notes);
                    if (input != null && input != notes)
                    {
                        notes = input.Length > ProfileFormatV2.MaxNotesCharacters ?
                            input.Substring(0, ProfileFormatV2.MaxNotesCharacters) : input;
                        message = "";
                    }
                }
                finally { GUI.enabled = oldEnabled; }
                Widgets.Label(new Rect(0f, 95f + editorH, rect.width, 24f),
                    message.Length > 0 ? message :
                    String.Format("XPC_ProfileDialog_NotesCount".Translate().ToString(),
                        notes.Length, ProfileFormatV2.MaxNotesCharacters));

                float width = Mathf.Min(155f, Mathf.Max(100f, (rect.width - 8f) / 2f));
                Rect save = new Rect(rect.width - 2f * width - 8f, footerY, width, 33f);
                Rect cancel = new Rect(rect.width - width, footerY, width, 33f);
                if (Widgets.ButtonText(cancel, "XPC_ProfileDialog_Cancel".Translate().ToString()))
                { Close(); return; }
                try
                {
                    GUI.enabled = oldEnabled && !busy && !terminal &&
                        !ProfileWriteSessionGate.IsBlocked;
                    if (Widgets.ButtonText(save, "XPC_ProfileDialog_NotesSave".Translate().ToString()))
                    {
                        busy = true;
                        ProfileNotesResult result = service.Save(profileId, expectedHash, notes);
                        if (result.Success)
                        {
                            if (onCompleted != null)
                            {
                                try { onCompleted(result); }
                                catch (Exception ex)
                                {
                                    Log.Warning("[XPC-PN1][NOTES_UI] refresh callback failed; " +
                                        "exception=" + ex.GetType().Name +
                                        "; saved=" + result.Code + "; build=" + XpCustomizerMod.BuildId);
                                }
                            }
                            Close();
                            return;
                        }
                        terminal = result.Code == ProfileNotesCode.Conflict ||
                            result.Code == ProfileNotesCode.RecoveryRequired;
                        message = result.Code == ProfileNotesCode.Conflict ?
                            "XPC_ProfileDialog_NotesConflict".Translate().ToString() :
                            String.Format("XPC_ProfileDialog_NotesFailure".Translate().ToString(), result.Code);
                        busy = false;
                    }
                }
                finally { GUI.enabled = oldEnabled; }
            }
            finally { Text.Font = oldFont; }
        }
    }
}
