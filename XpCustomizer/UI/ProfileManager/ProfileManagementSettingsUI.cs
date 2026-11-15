using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    /// <summary>
    /// R6-P1-C2-E detached profile browser; explicit Create, Rename, Overwrite JSON,
    /// Trash, Restore and Empty Trash. Profile Apply is a separate review flow.
    /// All UI buffers live in the owning Window, not in SettingsWindow.
    /// </summary>
    internal sealed partial class ProfileManagementSettingsUI
    {
        private readonly ProfileManagementUiState state;
        private readonly List<string> detailRows = new List<string>();
        private Vector2 listScroll = Vector2.zero;
        private Vector2 detailScroll = Vector2.zero;
        private int selectedIndex = -1;
        private bool logged;
        private string creationNotice;
        private readonly ProfileTrashActionService trashActions =
            ProfileTrashActionService.ForCurrentUser();
        private readonly ProfileTrashPurgeUiController purgeController;

        internal ProfileManagementSettingsUI() : this(new ProfileManagementUiState()) { }

        internal ProfileManagementSettingsUI(ProfileManagementUiState model)
        {
            if (model == null) throw new ArgumentNullException("model");
            state = model;
            purgeController = new ProfileTrashPurgeUiController(state, trashActions,
                ResetSelectionVisual, delegate(string message) { creationNotice = message; });
        }

        internal void Draw(Rect inRect, Action close)
        {
            if (!logged)
            {
                logged = true;
                Log.Message("[XPC-UI-PROFILE-FONT] version=XPC-UI-PROFILE-FONT-STAGE-A-20261112-R1; " +
                    "phase=STAGE_A; scope=DISPLAY_ONLY; headerListFooter=SMALL; " +
                    "dialogs=UNCHANGED; profileWritesByFontPatch=0; layout=" +
                    ProfileManagerLayout.Revision + "; build=" + XpCustomizerMod.BuildId);
                Log.Message("[XPC-L10N-L3E][KEYED_PROFILE_MANAGER] version=XPC-L10N-L3E1-PROFILE-MANAGER-20261112-R1; phase=E1; keys=" +
                    122 + "; sampleKey=XPC_Profile_Title; sample=" +
                    "XPC_Profile_Title".Translate().ToString()+ "; build=" + XpCustomizerMod.BuildId);
                Log.Message("[XPC-R6P1C15][PROFILE_WINDOW_REVISION] revision=" +
                    ProfileManagerLayout.Revision + "; mode=READ_CREATE_RENAME_OVERWRITE_TRASH_PURGE; " +
                    "layout=DUAL_OR_STACKED; model=" + ProfileManagementUiState.Revision +
                    "; windowUiWrites=0; bundleSeed=SEPARATE_PREOPEN; creation=EXPLICIT_ONLY; rename=EXPLICIT_METADATA_ONLY; " +
                    "overwrite=EXPLICIT_JSON_CONFIRM_ONLY; trashActions=EXPLICIT_DELETE_RESTORE_PURGE; " +
                    "trashRevision=" + ProfileTrashActionService.Revision +
                    "; activeActions=LEFT_OVERWRITE_RIGHT_DELETE; trashActionsLayout=RIGHT_RESTORE_UNCHANGED" +
                    "; liveProfileApply=SEPARATE_REVIEW_ONLY; build=" + XpCustomizerMod.BuildId);
            }
            // Cached after first opening. No repeated I/O on OnGUI Repaint/Layout.
            state.EnsureLoaded();
            ProfileManagerGeometry g = ProfileManagerLayout.Calculate(inRect);
            DrawHeader(g.Header);
            DrawToolbar(g.Toolbar);
            DrawList(g.List);
            DrawDetail(g.Detail);
            DrawFooter(g.Footer, close);
        }

        private void ChangeView(bool trash)
        {
            bool switched = state.InTrash != trash;
            state.SetTrashView(trash);
            if (switched)
            {
                ResetSelectionVisual();
                creationNotice = null;
            }
            LogAction(trash ? "VIEW_TRASH" : "VIEW_ACTIVE", state.ListingCode);
        }

        private void ResetSelectionVisual()
        {
            selectedIndex = -1;
            detailRows.Clear();
            listScroll = Vector2.zero;
            detailScroll = Vector2.zero;
        }

        // Shared GUID-based post-mutation selection. Never select by display name/index.
        private bool RefreshAndReselect(string profileId)
        {
            state.SetTrashView(false);
            state.Refresh();
            ResetSelectionVisual();
            if (state.ListingCode != ProfileStorageCode.Ok || String.IsNullOrEmpty(profileId))
                return false;
            IList<ProfileStorageEntry> rows = state.VisibleEntries;
            for (int i = 0; i < rows.Count; i++)
            {
                ProfileStorageEntry entry = rows[i];
                if (!entry.Result.Success || entry.Result.Document.ProfileId != profileId)
                    continue;
                selectedIndex = i;
                ProfileStorageCode selected = state.Select(i);
                BuildDetailLines();
                return selected == ProfileStorageCode.Ok &&
                    !state.HasErrorSelection && state.Selected != null;
            }
            return false;
        }
    }
}
