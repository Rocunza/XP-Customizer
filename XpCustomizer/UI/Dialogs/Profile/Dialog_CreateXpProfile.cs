using System;
using UnityEngine;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    /// <summary>
    /// Explicit create-only dialog. Save uses the currently committed settings snapshot.
    /// It does not change live settings, call WriteSettings, or invoke the profile apply gate.
    /// </summary>
    internal sealed class Dialog_CreateXpProfile : Window
    {
        private readonly Action<ProfileCreationResult> onCompleted;
        private readonly ProfileCreationService creator;
        private string enteredName = "";
        private string errorMessage = "";
        private bool busy;
        private Vector2 introductionScroll;
        private Vector2 statusScroll;

        internal Dialog_CreateXpProfile(Action<ProfileCreationResult> onCompleted,
            ProfileCreationService creator = null)
        {
            this.onCompleted = onCompleted;
            this.creator = creator ?? ProfileCreationService.ForCurrentUser();
            doCloseX = true;
            closeOnCancel = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            draggable = true;

#if DEBUG
            Log.Message("[XPC-L10N-L3E][KEYED_PROFILE_DIALOGS] " +
                "version=XPC-L10N-L3E2-PROFILE-DIALOGS-20261112-R1; " +
                "dialog=CREATE; keys=60; sample=" +
                "XPC_L2_CreateTitle".Translate().ToString() +
                "; build=" + XpCustomizerMod.BuildId);
#endif
            Log.Message("[XPC-UI-U2][PROFILE_DIALOG_BUTTON_ORDER] " +
                "version=XPC-UI-PROFILE-DIALOG-ORDER-U2-20261111-R1; " +
                "event=CONSTRUCTED; dialog=CREATE; primary=LEFT; cancel=RIGHT; " +
                "label=Cancel; build=" + XpCustomizerMod.BuildId);
#if DEBUG
            // L2: once per dialog instance, after language resources have loaded.
            // The translated title is evidence of the active Keyed lookup; it is not a PASS claim.
            Log.Message("[XPC-L10N-L2][KEYED_PILOT] " +
                "version=XPC-L10N-L2-KEYED-CREATE-20261112-R1; dialog=CREATE; keys=7; title=" +
                "XPC_L2_CreateTitle".Translate().ToString() +
                "; build=" + XpCustomizerMod.BuildId);
#endif
#if DEBUG
            Log.Message("[XPC-UI-PROFILE-FONT] version=XPC-UI-PROFILE-FONT-STAGE-B-20261112-R1; " +
                "phase=STAGE_B; dialog=CREATE; scope=DISPLAY_ONLY; bodyFont=Small; " +
                "profileWritesByFontPatch=0; build=" + XpCustomizerMod.BuildId);
#endif
        }

        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(Mathf.Min(530f, Mathf.Max(310f, Verse.UI.screenWidth - 48f)),
                    Mathf.Min(325f, Mathf.Max(230f, Verse.UI.screenHeight - 48f)));
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            float width = inRect.width;
            Widgets.Label(new Rect(0f, 0f, width, 28f), "XPC_L2_CreateTitle".Translate().ToString());
            // Introduction scrolls independently of the name editor; changing the
            // font must not move the input field or swallow its keyboard focus.
            string separate = "XPC_L2_CreateSeparateJson".Translate().ToString();
            string committed = "XPC_L2_CreateCommittedOnly".Translate().ToString();
            Rect introViewport = new Rect(0f, 32f, width, 55f);
            float introWidth = Mathf.Max(1f, width - 20f);
            GameFont old = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                float firstH = Mathf.Max(22f, Text.CalcHeight(separate, introWidth) + 3f);
                float secondH = Mathf.Max(22f, Text.CalcHeight(committed, introWidth) + 3f);
                Rect content = new Rect(0f, 0f, introWidth,
                    Mathf.Max(introViewport.height, firstH + secondH + 5f));
                Widgets.BeginScrollView(introViewport, ref introductionScroll, content);
                try
                {
                    Widgets.Label(new Rect(0f, 0f, introWidth, firstH), separate);
                    Widgets.Label(new Rect(0f, firstH + 5f, introWidth, secondH), committed);
                }
                finally { Widgets.EndScrollView(); }
            }
            finally
            {
                Text.WordWrap = oldWrap;
                Text.Anchor = oldAnchor;
                Text.Font = old;
            }

            Widgets.Label(new Rect(0f, 94f, width, 22f), "XPC_L2_CreateNameLabel".Translate().ToString());
            string edited = Widgets.TextField(new Rect(0f, 120f, width, 30f), enteredName);
            if (edited != enteredName) errorMessage = "";
            enteredName = edited;
            if (enteredName != null && enteredName.Length > 100)
                enteredName = enteredName.Substring(0, 100);

            string normalized;
            bool nameValid = ProfileCreationService.TryName(enteredName, out normalized);
            string status = ProfileWriteSessionGate.IsBlocked ?
                "XPC_ProfileDialog_CreateRecoveryLock".Translate().ToString() :
                !String.IsNullOrEmpty(errorMessage) ? errorMessage :
                !nameValid ? "XPC_L2_CreateNameHint".Translate().ToString() : "";
            float buttonY = Mathf.Max(0f, inRect.height - 38f);
            Rect statusViewport = new Rect(0f, 159f, width, Mathf.Max(1f, buttonY - 167f));
            if (status.Length > 0)
            {
                GameFont priorFont = Text.Font;
                TextAnchor priorAnchor = Text.Anchor;
                bool priorWrap = Text.WordWrap;
                try
                {
                    Text.Font = GameFont.Small;
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.WordWrap = true;
                    float textWidth = Mathf.Max(1f, width - 20f);
                    float textHeight = Mathf.Max(24f, Text.CalcHeight(status, textWidth) + 4f);
                    Rect content = new Rect(0f, 0f, textWidth,
                        Mathf.Max(statusViewport.height, textHeight + 4f));
                    Widgets.BeginScrollView(statusViewport, ref statusScroll, content);
                    try { Widgets.Label(new Rect(0f, 0f, textWidth, textHeight), status); }
                    finally { Widgets.EndScrollView(); }
                }
                finally
                {
                    Text.WordWrap = priorWrap;
                    Text.Anchor = priorAnchor;
                    Text.Font = priorFont;
                }
            }
            float buttonWidth = Mathf.Min(128f, (width - 10f) * 0.48f);
            Rect save = new Rect(width - buttonWidth * 2f - 8f, buttonY, buttonWidth, 34f);
            Rect cancel = new Rect(width - buttonWidth, buttonY, buttonWidth, 34f);
            if (Widgets.ButtonText(cancel, "XPC_L2_CreateCancel".Translate().ToString()))
            {
                Log.Message("[XPC-R6P1C2A][PROFILE_CREATE_UI] action=CANCEL; " +
                    "settingsWrites=0; applies=0; build=" + XpCustomizerMod.BuildId);
                Close();
                return;
            }
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && !ProfileWriteSessionGate.IsBlocked && !busy;
            bool saveClicked;
            try { saveClicked = Widgets.ButtonText(save, "XPC_L2_CreateSaveNew".Translate().ToString()); }
            finally { GUI.enabled = oldEnabled; }
            if (saveClicked)
            {
                if (!nameValid)
                {
                    errorMessage = "XPC_ProfileDialog_CreateInvalidInput".Translate().ToString();
                    return;
                }
                busy = true;
                ProfileCreationResult result = creator.Create(XpCustomizerMod.Settings,
                    normalized, ProfileCreationService.CurrentGameVersion());
                if (result.Success)
                {
                    if (onCompleted != null)
                    {
                        try { onCompleted(result); }
                        catch (Exception ex)
                        {
                            Log.Warning("[XPC-R6P1C2A][PROFILE_CREATE_UI] " +
                                "action=POST_SAVE_REFRESH; status=FAIL; exception=" +
                                ex.GetType().Name + "; profileFile=CREATED; build=" +
                                XpCustomizerMod.BuildId);
                        }
                    }
                    Close();
                    return;
                }
                errorMessage = LocalizedExplain(result);
                busy = false;
            }
        }

        // PM1 asserts Explain() English results. Only the actual UI uses this view.
        private static string LocalizedExplain(ProfileCreationResult result)
        {
            if (result == null) return "XPC_ProfileDialog_CreateNull".Translate().ToString();
            switch (result.Code)
            {
                case ProfileCreationCode.Created:
                    return "XPC_ProfileDialog_CreateSuccess".Translate().ToString();
                case ProfileCreationCode.InvalidName:
                    return "XPC_ProfileDialog_CreateInvalidName".Translate().ToString();
                case ProfileCreationCode.InvalidSettings:
                    return "XPC_ProfileDialog_CreateInvalidSettings".Translate().ToString();
                case ProfileCreationCode.InvalidProfile:
                    return "XPC_ProfileDialog_CreateInvalidProfile".Translate().ToString();
                default:
                    return result.StorageCode == ProfileStorageCode.RecoveryRequired
                        ? "XPC_ProfileDialog_CreateRecoveryLock".Translate().ToString()
                        : string.Format("XPC_ProfileDialog_CreateStorageFailure".Translate().ToString(),
                            ProfileManagementUiState.LocalizedExplain(result.StorageCode));
            }
        }

        internal static string Explain(ProfileCreationResult result)
        {
            if (result == null) return "Save failed. No success was reported.";
            switch (result.Code)
            {
                case ProfileCreationCode.Created: return "Profile created.";
                case ProfileCreationCode.InvalidName: return "Invalid profile name.";
                case ProfileCreationCode.InvalidSettings: return "Current settings are invalid; nothing was saved.";
                case ProfileCreationCode.InvalidProfile: return "Profile data validation failed; nothing was saved.";
                default:
                    if (result.StorageCode == ProfileStorageCode.RecoveryRequired)
                        return "Recovery required. Profile writes are now blocked until restart and backup inspection.";
                    return "Could not save profile: " +
                        ProfileManagementUiState.Explain(result.StorageCode) + ".";
            }
        }
    }
}
