using System;
using UnityEngine;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    /// <summary>
    /// R6-P1-C2-B1: explicit metadata-only rename. The target is frozen by GUID
    /// and full SHA-256 at selection time. Cancel and closing never write files.
    /// </summary>
    internal sealed class Dialog_RenameXpProfile : Window
    {
        private readonly string profileId;
        private readonly string expectedHash;
        private readonly string oldName;
        private readonly Action<ProfileRenameResult> onCompleted;
        private readonly ProfileUpdateService service;
        private string newName;
        private string error = "";
        private bool busy;
        private bool terminalError;
        private Vector2 introductionScroll;
        private Vector2 hintScroll;

        internal Dialog_RenameXpProfile(ProfileDocument selected, string selectedHash,
            Action<ProfileRenameResult> onCompleted, ProfileUpdateService service = null)
        {
            if (selected == null) throw new ArgumentNullException("selected");
            profileId = selected.ProfileId;
            oldName = selected.DisplayName;
            expectedHash = selectedHash;
            this.onCompleted = onCompleted;
            this.service = service ?? ProfileUpdateService.ForCurrentUser();
            newName = oldName ?? "";
            doCloseX = true;
            closeOnCancel = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            draggable = true;

#if DEBUG
            Log.Message("[XPC-L10N-L3E][KEYED_PROFILE_DIALOGS] " +
                "version=XPC-L10N-L3E2-PROFILE-DIALOGS-20261112-R1; " +
                "dialog=RENAME; keys=60; sample=" +
                "XPC_ProfileDialog_RenameTitle".Translate().ToString() +
                "; build=" + XpCustomizerMod.BuildId);
#endif
            Log.Message("[XPC-UI-U2][PROFILE_DIALOG_BUTTON_ORDER] " +
                "version=XPC-UI-PROFILE-DIALOG-ORDER-U2-20261111-R1; " +
                "event=CONSTRUCTED; dialog=RENAME; primary=LEFT; cancel=RIGHT; " +
                "label=Cancel; build=" + XpCustomizerMod.BuildId);
#if DEBUG
            Log.Message("[XPC-UI-PROFILE-FONT] version=XPC-UI-PROFILE-FONT-STAGE-B-20261112-R1; " +
                "phase=STAGE_B; dialog=RENAME; scope=DISPLAY_ONLY; bodyFont=Small; " +
                "profileWritesByFontPatch=0; build=" + XpCustomizerMod.BuildId);
#endif
        }

        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(Mathf.Min(560f, Mathf.Max(325f, Verse.UI.screenWidth - 48f)),
                    Mathf.Min(330f, Mathf.Max(230f, Verse.UI.screenHeight - 48f)));
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            float width = inRect.width;
            Widgets.Label(new Rect(0f, 0f, width, 27f), "XPC_ProfileDialog_RenameTitle".Translate().ToString());
            string scope = "XPC_ProfileDialog_RenameScope".Translate().ToString();
            string original = string.Format("XPC_ProfileDialog_RenameOriginal".Translate().ToString(),
                ShortId(profileId), Clip(oldName, 42));
            Rect introViewport = new Rect(0f, 30f, width, 55f);
            float introWidth = Mathf.Max(1f, width - 20f);
            GameFont priorFont = Text.Font;
            TextAnchor priorAnchor = Text.Anchor;
            bool priorWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                float scopeH = Mathf.Max(22f, Text.CalcHeight(scope, introWidth) + 3f);
                float originalH = Mathf.Max(22f, Text.CalcHeight(original, introWidth) + 3f);
                Rect content = new Rect(0f, 0f, introWidth,
                    Mathf.Max(introViewport.height, scopeH + originalH + 5f));
                Widgets.BeginScrollView(introViewport, ref introductionScroll, content);
                try
                {
                    Widgets.Label(new Rect(0f, 0f, introWidth, scopeH), scope);
                    Widgets.Label(new Rect(0f, scopeH + 5f, introWidth, originalH), original);
                }
                finally { Widgets.EndScrollView(); }
            }
            finally
            {
                Text.WordWrap = priorWrap;
                Text.Anchor = priorAnchor;
                Text.Font = priorFont;
            }

            Widgets.Label(new Rect(0f, 94f, width, 22f), "XPC_ProfileDialog_RenameNewName".Translate().ToString());
            string edit = Widgets.TextField(new Rect(0f, 120f, width, 31f), newName);
            if (edit != newName && !terminalError) error = "";
            newName = edit ?? "";
            if (newName.Length > 100) newName = newName.Substring(0, 100);

            string canonical;
            bool valid = ProfileNamePolicy.TryCanonical(newName, out canonical);
            float y = Mathf.Max(0f, inRect.height - 39f);
            Rect hintViewport = new Rect(0f, 160f, width, Mathf.Max(1f, y - 168f));
            if (String.IsNullOrEmpty(error) && !valid)
                DrawHint(hintViewport, "XPC_ProfileDialog_RenameNameHint".Translate().ToString());
            else if (!String.IsNullOrEmpty(error))
                DrawHint(hintViewport, error);
            else
                DrawHint(hintViewport, "XPC_ProfileDialog_RenameNoApply".Translate().ToString());

            float buttonWidth = Mathf.Min(130f, (width - 10f) * 0.48f);
            Rect rename = new Rect(width - buttonWidth * 2f - 8f, y, buttonWidth, 34f);
            Rect cancel = new Rect(width - buttonWidth, y, buttonWidth, 34f);
            if (Widgets.ButtonText(cancel, "XPC_ProfileDialog_Cancel".Translate().ToString()))
            {
                Log.Message("[XPC-R6P1C2B1][PROFILE_RENAME_UI] action=CANCEL; " +
                    "writes=0; applies=0; build=" + XpCustomizerMod.BuildId);
                Close();
                return;
            }
            bool previous = GUI.enabled;
            try
            {
                GUI.enabled = previous && !busy && !terminalError &&
                    !ProfileWriteSessionGate.IsBlocked;
                if (Widgets.ButtonText(rename, "XPC_ProfileDialog_RenameButton".Translate().ToString()))
                {
                    if (!valid)
                    {
                        error = "XPC_ProfileDialog_RenameInvalidInput".Translate().ToString();
                        return;
                    }
                    busy = true;
                    ProfileRenameResult result = service.Rename(profileId, expectedHash, canonical);
                    if (result.Code == ProfileRenameCode.Renamed ||
                        result.Code == ProfileRenameCode.NoChange)
                    {
                        if (onCompleted != null)
                        {
                            try { onCompleted(result); }
                            catch (Exception ex)
                            {
                                Log.Warning("[XPC-R6P1C2B1][PROFILE_RENAME_UI] " +
                                    "action=POST_RENAME_REFRESH; status=FAIL; exception=" +
                                    ex.GetType().Name + "; fileState=" + result.Code +
                                    "; build=" + XpCustomizerMod.BuildId);
                            }
                        }
                        Close();
                        return;
                    }
                    error = LocalizedExplain(result);
                    terminalError = result.Code == ProfileRenameCode.Conflict ||
                        result.Code == ProfileRenameCode.RecoveryRequired ||
                        result.Code == ProfileRenameCode.WriteLocked ||
                        result.StorageCode == ProfileStorageCode.NotFound ||
                        result.StorageCode == ProfileStorageCode.AtomicReplaceUnavailable;
                    busy = false;
                }
            }
            finally { GUI.enabled = previous; }
        }

        // Do not alter Explain(): PM1 tests assert its invariant English contract.
        private static string LocalizedExplain(ProfileRenameResult result)
        {
            if (result == null) return "XPC_ProfileDialog_RenameNull".Translate().ToString();
            switch (result.Code)
            {
                case ProfileRenameCode.Renamed:
                    return "XPC_ProfileDialog_RenameSuccess".Translate().ToString();
                case ProfileRenameCode.NoChange:
                    return "XPC_ProfileDialog_RenameNoChange".Translate().ToString();
                case ProfileRenameCode.InvalidName:
                    return "XPC_ProfileDialog_RenameInvalidInput".Translate().ToString();
                case ProfileRenameCode.InvalidSelection:
                    return "XPC_ProfileDialog_RenameInvalidSelection".Translate().ToString();
                case ProfileRenameCode.Conflict:
                    return "XPC_ProfileDialog_RenameConflict".Translate().ToString();
                case ProfileRenameCode.RecoveryRequired:
                case ProfileRenameCode.WriteLocked:
                    return "XPC_ProfileDialog_RenameRecovery".Translate().ToString();
                default:
                    return string.Format("XPC_ProfileDialog_RenameStorageFailure".Translate().ToString(),
                        ProfileManagementUiState.LocalizedExplain(result.StorageCode));
            }
        }

        private void DrawHint(Rect viewport, string message)
        {
            GameFont old = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                float width = Mathf.Max(1f, viewport.width - 20f);
                float height = Mathf.Max(24f, Text.CalcHeight(message, width) + 4f);
                Rect content = new Rect(0f, 0f, width,
                    Mathf.Max(viewport.height, height + 4f));
                Widgets.BeginScrollView(viewport, ref hintScroll, content);
                try { Widgets.Label(new Rect(0f, 0f, width, height), message); }
                finally { Widgets.EndScrollView(); }
            }
            finally
            {
                Text.WordWrap = oldWrap;
                Text.Anchor = oldAnchor;
                Text.Font = old;
            }
        }

        internal static string Explain(ProfileRenameResult result)
        {
            if (result == null) return "Rename failed without a result.";
            switch (result.Code)
            {
                case ProfileRenameCode.Renamed: return "Profile renamed.";
                case ProfileRenameCode.NoChange: return "Name unchanged. No file was written.";
                case ProfileRenameCode.InvalidName: return "Invalid name (1-80 characters).";
                case ProfileRenameCode.InvalidSelection: return "Invalid selection. Close and reselect a profile.";
                case ProfileRenameCode.Conflict: return "File changed. Close, Refresh, and select it again.";
                case ProfileRenameCode.RecoveryRequired:
                case ProfileRenameCode.WriteLocked:
                    return "Recovery required! Writes blocked until restart and backup inspection.";
                default:
                    return "Rename failed: " + ProfileManagementUiState.Explain(result.StorageCode) +
                        ". Check the backup before retrying.";
            }
        }

        private static string ShortId(string id)
        { return String.IsNullOrEmpty(id) ? "INVALID" : id.Substring(0, Math.Min(8, id.Length)); }

        private static string Clip(string text, int max)
        {
            if (String.IsNullOrEmpty(text)) return "(empty)";
            return text.Length <= max ? text : text.Substring(0, max - 3) + "...";
        }
    }
}
