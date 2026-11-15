using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    internal sealed partial class ProfileManagementSettingsUI
    {
        private void OpenOverwriteConfirmation()
        {
            if (!CanStartSelectedActiveProfileAction()) return;
            // Actual user-click audit: distinguish observed editor readiness
            // from startup-only pure tests. Do not log buffer text or values.
            ProfileEditorReadinessResult focus = ProfileEditorReadinessService.Check();
            Log.Message("[XPC-R6P1C2B2A1][PROFILE_EDITOR_RUNTIME] action=CHECK; " +
                "status=" + focus.Code + "; reason=" + focus.Source +
                "; groups=" + focus.CoveredGroups + "; buffers=" + focus.ProbedBuffers +
                "; untracked=" + focus.UntrackedFocusBuffers +
                "; inactiveDecayBuffers=" + focus.InactiveDecayBuffers +
                "; fileWrites=0; profileApplyByThisAction=NOT_INVOKED; build=" + XpCustomizerMod.BuildId);
            ProfileOverwritePreflightResult report =
                ProfileOverwritePreflightService.ForCurrentUser().Check(
                    state.Selected, state.SelectedHash, XpCustomizerMod.Settings);
            switch (report.Code)
            {
                case ProfileOverwritePreflightCode.Ready:
                    if (!report.ContentDiffers)
                    {
                        creationNotice = "XPC_Profile_OverwriteSame".Translate().ToString();
                        break;
                    }
                    Find.WindowStack.Add(new Dialog_OverwriteXpProfile(
                        state.Selected, report, OnProfileOverwritten));
                    creationNotice = "XPC_Profile_OverwriteReview".Translate().ToString();
                    break;
                case ProfileOverwritePreflightCode.PendingEdit:
                    creationNotice = "XPC_Profile_OverwriteEditPending".Translate().ToString();
                    break;
                case ProfileOverwritePreflightCode.Uncertain:
                    creationNotice = string.Format("XPC_Profile_OverwriteFocusUncertain".Translate().ToString(), report.Detail);
                    break;
                case ProfileOverwritePreflightCode.Conflict:
                    creationNotice = "XPC_Profile_OverwriteConflict".Translate().ToString();
                    break;
                default:
                    creationNotice = string.Format("XPC_Profile_OverwriteBlocked".Translate().ToString(), report.Code);
                    break;
            }
            Log.Message("[XPC-R6P1C2E][PROFILE_OVERWRITE_UI] action=OPEN_OR_REJECT; " +
                "status=" + report.Code + "; fileWrites=0; settingsWrites=0; " +
                "profileApplyByThisAction=NOT_INVOKED; build=" + XpCustomizerMod.BuildId);
        }

        private void OnProfileOverwritten(ProfileCurrentOverwriteResult result)
        {
            if (result == null) return;
            if (result.Success)
            {
                bool selectedAgain = RefreshAndReselect(result.ProfileId);
                creationNotice = selectedAgain
                    ? "XPC_Profile_OverwriteSuccess".Translate().ToString()
                    : "XPC_Profile_OverwriteRefreshUncertain".Translate().ToString();
            }
            else creationNotice = string.Format("XPC_Profile_OverwriteFailure".Translate().ToString(), result.Code);
        }

        private bool CanStartSelectedActiveProfileAction()
        {
            return !ProfileWriteSessionGate.IsBlocked && !state.InTrash &&
                state.ListingCode == ProfileStorageCode.Ok && selectedIndex >= 0 &&
                !state.HasErrorSelection && state.Selected != null &&
                !String.IsNullOrEmpty(state.SelectedHash) &&
                selectedIndex < state.VisibleEntries.Count &&
                state.VisibleEntries[selectedIndex].Result.Success &&
                state.VisibleEntries[selectedIndex].Result.Document.ProfileId == state.Selected.ProfileId;
        }

        // No paths are built from display names or list indexes. Active delete uses
        // selected GUID + SHA and is confirmed in a separate owned dialog.
        private void OpenNotesEditor()
        {
            if (!CanStartSelectedActiveProfileAction()) return;
            Find.WindowStack.Add(new Dialog_EditXpProfileNotes(state.Selected,
                state.SelectedHash, OnNotesUpdated));
            Log.Message("[XPC-PN1][NOTES_UI] action=OPEN; profileWrites=0; " +
                "settingsWrites=0; build=" + XpCustomizerMod.BuildId);
        }

        private void OnNotesUpdated(ProfileNotesResult result)
        {
            if (result == null) return;
            if (result.Code == ProfileNotesCode.NoChange)
            {
                creationNotice = "XPC_Profile_NotesUnchanged".Translate().ToString();
                return;
            }
            if (result.Code == ProfileNotesCode.Saved)
            {
                bool selectedAgain = RefreshAndReselect(result.ProfileId);
                creationNotice = selectedAgain ? "XPC_Profile_NotesSaved".Translate().ToString() :
                    "XPC_Profile_NotesRefreshFailed".Translate().ToString();
            }
        }

        private void OpenDeleteConfirmation()
        {
            if (!CanStartSelectedActiveProfileAction()) return;
            Find.WindowStack.Add(new Dialog_DeleteXpProfile(state.Selected,
                state.SelectedHash, OnDeleteConfirmed));
            LogTrashAction("OPEN_DELETE_CONFIRM", ProfileStorageCode.Ok, "NO_FILE_MUTATION");
        }

        private void OnDeleteConfirmed(string id, string expectedHash)
        {
            if (!CanStartSelectedActiveProfileAction() || state.Selected.ProfileId != id ||
                !String.Equals(state.SelectedHash, expectedHash, StringComparison.Ordinal))
            {
                creationNotice = "XPC_Profile_DeleteSelectionChanged".Translate().ToString();
                LogTrashAction("DELETE_REJECTED_SELECTION", ProfileStorageCode.Conflict, "NO_WRITE");
                return;
            }
            ProfileStorageResult result = trashActions.Delete(id, expectedHash);
            LogTrashAction("DELETE_TO_TRASH", result.Code,
                result.Success ? "MOVE_VERIFIED" : "NO_RETRY");
            if (result.Success)
            {
                state.Refresh();
                ResetSelectionVisual();
                creationNotice = state.ListingCode == ProfileStorageCode.Ok
                    ? "XPC_Profile_DeleteMoved".Translate().ToString()
                    : "XPC_Profile_DeleteMovedRefreshFailed".Translate().ToString();
            }
            else
            {
                creationNotice = string.Format("XPC_Profile_DeleteNotConfirmed".Translate().ToString(), ProfileManagementUiState.LocalizedExplain(result.Code));
            }
        }

        private bool CanRestore()
        {
            if (ProfileWriteSessionGate.IsBlocked || !state.InTrash ||
                state.ListingCode != ProfileStorageCode.Ok || selectedIndex < 0 ||
                state.HasErrorSelection || state.Selected == null ||
                String.IsNullOrEmpty(state.SelectedHash) ||
                String.IsNullOrEmpty(state.SelectedName) ||
                selectedIndex >= state.VisibleEntries.Count) return false;
            ProfileStorageEntry entry = state.VisibleEntries[selectedIndex];
            string tokenId;
            return entry.Result.Success &&
                String.Equals(entry.FileName, state.SelectedName, StringComparison.Ordinal) &&
                String.Equals(entry.Result.ContentHash, state.SelectedHash, StringComparison.Ordinal) &&
                ProfileStoragePaths.TryParseTrashToken(entry.FileName, out tokenId) &&
                tokenId == state.Selected.ProfileId;
        }

        private void RestoreSelected()
        {
            if (!CanRestore()) return;
            string token = state.SelectedName;
            string id = state.Selected.ProfileId;
            ProfileStorageResult result = trashActions.Restore(token, state.SelectedHash);
            LogTrashAction("RESTORE_FROM_TRASH", result.Code,
                result.Success ? "MOVE_VERIFIED" : "NO_RETRY");
            if (result.Success)
            {
                bool selectedAgain = RefreshAndReselect(id);
                creationNotice = selectedAgain
                    ? "XPC_Profile_RestoreSuccess".Translate().ToString()
                    : "XPC_Profile_RestoreRefreshUncertain".Translate().ToString();
            }
            else
            {
                creationNotice = string.Format("XPC_Profile_RestoreNotConfirmed".Translate().ToString(), ProfileManagementUiState.LocalizedExplain(result.Code));
            }
        }

        private static void LogTrashAction(string action, ProfileStorageCode code, string detail)
        {
            Log.Message("[XPC-R6P1C2C][PROFILE_TRASH_UI] action=" + action +
                "; status=" + code + "; result=" + detail +
                "; settingsWrites=0; profileApplyByThisAction=NOT_INVOKED; build=" + XpCustomizerMod.BuildId);
        }

        private void OpenRename()
        {
            if (!CanStartSelectedActiveProfileAction()) return;
            Find.WindowStack.Add(new Dialog_RenameXpProfile(state.Selected, state.SelectedHash,
                OnProfileRenamed));
            string id = state.Selected.ProfileId;
            Log.Message("[XPC-R6P1C2B1][PROFILE_RENAME_UI] action=OPEN; idPrefix=" +
                id.Substring(0, Math.Min(8, id.Length)) +
                "; writes=0; profileApplyByThisAction=NOT_INVOKED; build=" + XpCustomizerMod.BuildId);
        }

        private void OnProfileRenamed(ProfileRenameResult result)
        {
            // The rename dialog owns normal failure messages and only invokes this
            // callback after success or NoChange. This branch is a fail-closed guard
            // against an unexpected caller, not a second normal failure UI.
            if (result == null || (!result.Success && result.Code != ProfileRenameCode.NoChange))
            {
                creationNotice = string.Format("XPC_Profile_RenameUnexpected".Translate().ToString(), result == null ? "NULL" : result.Code.ToString());
                Log.Warning("[XPC-PM1-UX1][PROFILE_RENAME_UI] action=POST_RENAME_REFRESH; " +
                    "status=UNEXPECTED_RESULT; result=" +
                    (result == null ? "NULL" : result.Code.ToString()) +
                    "; refresh=NOT_ATTEMPTED; profileApplyByThisAction=NOT_INVOKED; build=" +
                    XpCustomizerMod.BuildId);
                return;
            }
            if (result.Code == ProfileRenameCode.NoChange)
            {
                creationNotice = "XPC_Profile_RenameNoChange".Translate().ToString();
                return;
            }
            bool selectedAgain = RefreshAndReselect(result.ProfileId);
            creationNotice = selectedAgain
                ? "XPC_Profile_RenameSuccess".Translate().ToString()
                : "XPC_Profile_RenameRefreshUncertain".Translate().ToString();
            Log.Message("[XPC-R6P1C2B1][PROFILE_RENAME_UI] action=POST_RENAME_REFRESH; " +
                "status=" + (selectedAgain ? "RESELECTED" : "NEEDS_REFRESH") +
                "; listing=" + state.ListingCode + "; profileApplyByThisAction=NOT_INVOKED; build=" +
                XpCustomizerMod.BuildId);
        }

        private void OnProfileCreated(ProfileCreationResult result)
        {
            // The create dialog currently invokes this callback only on success.
            // Keep null/failed results separate from a committed JSON whose UI
            // selection could not be refreshed. Never retry an already committed save.
            bool selectedAgain = false;
            string creationStatus;
            string refreshStatus = "NOT_ATTEMPTED";
            if (result == null)
            {
                creationStatus = "NULL";
                creationNotice = string.Format("XPC_Profile_CreateFailed".Translate().ToString(), "NULL");
            }
            else if (!result.Success)
            {
                creationStatus = "FAILED";
                creationNotice = string.Format("XPC_Profile_CreateFailed".Translate().ToString(), result.Code);
            }
            else
            {
                creationStatus = "CREATED";
                selectedAgain = RefreshAndReselect(result.ProfileId);
                refreshStatus = selectedAgain ? "OK" : "NEEDS_REFRESH";
                creationNotice = selectedAgain
                    ? "XPC_Profile_CreateSuccess".Translate().ToString()
                    : "XPC_Profile_CreateRefreshUncertain".Translate().ToString();
            }
            Log.Message("[XPC-R6P1C2A][PROFILE_CREATE_UI] action=POST_SAVE_REFRESH; " +
                "result=" + (result == null ? "NULL" : result.Code.ToString()) +
                "; creationStatus=" + creationStatus + "; refresh=" + refreshStatus +
                "; listing=" + state.ListingCode + "; reselected=" + selectedAgain +
                "; profileApplyByThisAction=NOT_INVOKED; build=" + XpCustomizerMod.BuildId);
        }
    }
}
