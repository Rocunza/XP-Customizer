using System;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    /// <summary>
    /// R6-P1-C2-D: binds the separate destructive confirmation to the repository service.
    /// ProfileManagementSettingsUI only draws a button; never deletes files itself.
    /// </summary>
    internal sealed class ProfileTrashPurgeUiController
    {
        private readonly ProfileManagementUiState state;
        private readonly ProfileTrashActionService actions;
        private readonly Action resetSelection;
        private readonly Action<string> notice;

        internal ProfileTrashPurgeUiController(ProfileManagementUiState state,
            ProfileTrashActionService actions, Action resetSelection, Action<string> notice)
        {
            if (state == null || actions == null || resetSelection == null || notice == null)
                throw new ArgumentNullException("purge controller dependency");
            this.state = state;
            this.actions = actions;
            this.resetSelection = resetSelection;
            this.notice = notice;
        }

        internal bool CanOpen()
        {
            if (!state.InTrash || ProfileWriteSessionGate.IsBlocked ||
                state.ListingCode != ProfileStorageCode.Ok || state.VisibleEntries.Count == 0)
                return false;
            foreach (ProfileStorageEntry entry in state.VisibleEntries)
                if (entry == null || entry.Result == null || !entry.Result.Success)
                    return false;
            return true;
        }

        internal void Open()
        {
            if (!CanOpen()) return;
            ProfileTrashPurgeResult prepared = actions.PrepareEmptyTrash();
            LogPurge("PREPARE", prepared);
            if (prepared.State != ProfileTrashPurgeState.Ready || prepared.Plan == null)
            {
                notice(prepared.State == ProfileTrashPurgeState.NoOp
                    ? "XPC_Profile_TrashNoDelete".Translate().ToString()
                    : string.Format("XPC_Profile_TrashCannotEmpty".Translate().ToString(), ProfileManagementUiState.LocalizedExplain(prepared.Code)));
                state.Refresh();
                resetSelection();
                return;
            }
            Find.WindowStack.Add(new Dialog_EmptyXpProfileTrash(prepared.Plan, Confirm));
        }

        private void Confirm(ProfileTrashPurgePlan approved)
        {
            if (!state.InTrash || ProfileWriteSessionGate.IsBlocked ||
                state.ListingCode != ProfileStorageCode.Ok)
            {
                notice("XPC_Profile_TrashConfirmCancelled".Translate().ToString());
                return;
            }
            LogPurge("CONFIRM", new ProfileTrashPurgeResult(ProfileTrashPurgeState.Ready,
                ProfileStorageCode.Ok, "SECOND_EXPLICIT_CONFIRM", approved,
                approved.Count, 0, approved.Count));
            ProfileTrashPurgeResult result = actions.EmptyTrash(approved);
            LogPurge("FINISH", result);
            state.Refresh();
            resetSelection();
            if (result.IsComplete && state.ListingCode == ProfileStorageCode.Ok &&
                state.VisibleEntries.Count == 0)
                notice(string.Format("XPC_Profile_TrashDeleted".Translate().ToString(), result.DeletedVerified));
            else if (result.IsComplete)
                notice(string.Format("XPC_Profile_TrashDeletedRefreshUncertain".Translate().ToString(), result.DeletedVerified));
            else if (result.State == ProfileTrashPurgeState.Partial ||
                     result.State == ProfileTrashPurgeState.Uncertain)
                notice(string.Format("XPC_Profile_TrashStopped".Translate().ToString(), result.State, result.DeletedVerified, result.Requested));
            else
                notice(string.Format("XPC_Profile_TrashFailure".Translate().ToString(), ProfileManagementUiState.LocalizedExplain(result.Code)));
        }

        private static void LogPurge(string phase, ProfileTrashPurgeResult result)
        {
            string line = "[XPC-R6P1C2D][TRASH_PURGE] phase=" + phase +
                "; status=" + result.State + "; code=" + result.Code +
                "; reason=" + result.Detail + "; requested=" + result.Requested +
                "; deletedVerified=" + result.DeletedVerified +
                "; remaining=" + result.Remaining +
                "; fingerprint=" + (result.Plan == null ? "NONE" : result.Plan.Fingerprint) +
                "; ownedTrashOnly=True; configXmlWrites=0; profileApplyByThisAction=NOT_INVOKED; " +
                "noRetry=True; build=" + XpCustomizerMod.BuildId;
            if (result.State == ProfileTrashPurgeState.Partial ||
                result.State == ProfileTrashPurgeState.Uncertain)
                Log.Error(line);
            else Log.Message(line);
        }
    }
}
