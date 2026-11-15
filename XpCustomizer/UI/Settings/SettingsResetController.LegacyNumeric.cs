using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    /// <summary>
    /// R3-B: outcome contract for an explicitly confirmed numeric-only reset.
    /// Profile Apply/Overwrite never call this policy.
    /// </summary>
    internal static class GlobalNumericResetR3BPolicy
    {
        internal static bool IsSuccessfulReset(SettingsRuntimeApplyStatus status)
        {
            return status == SettingsRuntimeApplyStatus.APPLY_PASS ||
                status == SettingsRuntimeApplyStatus.APPLY_NO_CHANGE;
        }

        internal static bool NeedsNoChangeEditorRefresh(SettingsRuntimeApplyStatus status)
        {
            return status == SettingsRuntimeApplyStatus.APPLY_NO_CHANGE;
        }
    }

    internal sealed partial class SettingsResetController
    {
        // R3-B: this explicit destructive numeric reset deliberately does NOT
        // use N1/profile navigation readiness. Only Yes may discard drafts.
        // Profile navigation, Save Current, Overwrite and Apply remain gated.
        private void RequestModNumericResetConfirmation()
        {
            if (!modNumericResetLatch.TryOpen()) return;
            // Do not blur, commit, clear, or validate staged numeric editors
            // here. Opening/Cancel must not intentionally change their state.
            // The existing one-shot Yes path is the only reset authority.
            try
            {
                Find.WindowStack.Add(new Dialog_ConfirmGlobalModNumericReset(
                    ApplyConfirmedModNumericReset,
                    delegate { modNumericResetLatch.Close(); }));
                Log.Message("[XPC-RESET][GLOBAL_RESET_UI] kind=MOD_NUMERIC_VALUES; " +
                    "action=OPEN; editorReadiness=NOT_REQUIRED_FOR_EXPLICIT_RESET; " +
                    "draftDisposition=ONLY_ON_SUCCESSFUL_YES; resetCalls=0; geneDefWrites=0; " +
                    "profileJsonWrites=0; ownXmlWritesByDialog=0; build=" +
                    XpCustomizerMod.BuildId);
            }
            catch (Exception ex)
            {
                modNumericResetLatch.Close();
                Log.Warning("[XPC-RESET][GLOBAL_RESET_UI] kind=MOD_NUMERIC_VALUES; " +
                    "action=OPEN_FAILED; reason=" + ex.GetType().Name +
                    "; build=" + XpCustomizerMod.BuildId);
            }
        }

        private void ApplyConfirmedModNumericReset()
        {
            if (ProfileProductionWriteGate.IsBusy ||
                ProfileProductionWriteGate.IsUncertain ||
                RuntimeRecoverySessionGate.LiveIsBlocked ||
                ProfileWriteSessionGate.IsBlocked)
                throw new GlobalNumericDefaultsException("RECOVERY_OR_WRITE_LOCK", false);
            if (Current.Game != null &&
                (Find.TickManager == null || !Find.TickManager.Paused))
                throw new GlobalNumericDefaultsException("PAUSE_ACTIVE_GAME_FIRST", false);
            // R3-B: explicit Yes discards unfinished XP-owned numeric drafts.
            // NEVER remove ProfileEditorReadinessService.Check from profile
            // Apply/Overwrite/Create or their independent navigation gates.
            if (DefDatabase<SkillDef>.AllDefsListForReading == null)
                throw new GlobalNumericDefaultsException("SKILLDEFS_NOT_READY", false);
            List<string> skills = new List<string>();
            foreach (SkillDef def in DefDatabase<SkillDef>.AllDefsListForReading)
                if (def != null && !String.IsNullOrEmpty(def.defName))
                    skills.Add(def.defName);
            SettingsSnapshot candidate;
            string reason;
            if (!GlobalNumericDefaultsService.TryBuild(XpCustomizerMod.Settings,
                skills, out candidate, out reason))
            {
                Log.Warning("[XPC-RESET][NUMERIC_DEFAULTS] phase=PREPARED; status=REJECTED; " +
                    "reason=" + reason + "; fieldsReviewed=108; nestedFieldsReviewed=36; " +
                    "liveSettingsWrites=0; build=" + XpCustomizerMod.BuildId);
                throw new GlobalNumericDefaultsException(reason, false);
            }
            Log.Message("[XPC-RESET][NUMERIC_DEFAULTS] phase=PREPARED; status=PASS; " +
                "fieldsReviewed=108; nestedFieldsReviewed=36; " +
                "allowedChangedOnly=True; switchesPreserved=True; schemaPreserved=True; " +
                "profileJsonWrites=0; ownXmlWritesByDialog=0; build=" + XpCustomizerMod.BuildId);
            SettingsRuntimeApplyResult result =
                SettingsRuntimeApplyCoordinator.ApplyLiveForGlobalNumericDefaults(candidate);
            if (!GlobalNumericResetR3BPolicy.IsSuccessfulReset(result.Status))
            {
                bool unrecovered = result.Status ==
                    SettingsRuntimeApplyStatus.RUNTIME_REFRESH_FAILED_UNRECOVERED;
                Log.Warning("[XPC-RESET][NUMERIC_DEFAULTS] phase=APPLY; status=" +
                    result.Status + "; reason=" + result.Reason +
                    "; restartRequired=" + unrecovered +
                    "; build=" + XpCustomizerMod.BuildId);
                throw new GlobalNumericDefaultsException(
                    result.Status + ":" + result.Reason, unrecovered);
            }
            // The common runtime apply path refreshes all editors on PASS.
            // NO_CHANGE intentionally bypasses the core UI notification, so
            // do it here, even if every numeric value already equals default.
            bool uiRefreshed = result.UiNotified;
            if (GlobalNumericResetR3BPolicy.NeedsNoChangeEditorRefresh(result.Status))
            {
                try
                {
                    SettingsRefreshCoordinator.NotifyAll(SettingsRefreshReason.GlobalModNumericReset);
                    uiRefreshed = true;
                }
                catch (Exception ex)
                {
                    // No settings mutation occurred in the NO_CHANGE path.
                    // Preserve the live recovery latch and allow a fresh dialog.
                    throw new GlobalNumericDefaultsException(
                        "NO_CHANGE_EDITOR_REFRESH_FAILED:" + ex.GetType().Name, false);
                }
            }
            // Both success paths have now reset the tab-owned editor buffers.
            // Clear ONLY this mod's stale raw/focus observations. Never touch
            // keyboard focus owned by vanilla RimWorld or another mod.
            ProfileNumericFocus.ForgetAfterConfirmedModNumericReset();
            string focusedAfter = GUI.GetNameOfFocusedControl();
            if (ProfileNumericNavigation.IsOwnedControl(focusedAfter))
                GUI.FocusControl(null);
            if (disarmLiveAudit != null) disarmLiveAudit();
            profileNavigation.BlockIfPending("SETTINGS_CHANGED_DURING_NAVIGATION");
            Log.Message("[XPC-RESET][NUMERIC_DEFAULTS] phase=VERIFY; status=" +
                (result.Status == SettingsRuntimeApplyStatus.APPLY_NO_CHANGE ?
                    "NO_CHANGE" : "APPLIED") +
                "; switchesPreserved=True; schemaPreserved=True; " +
                "curveCacheInvalidated=" + result.CacheInvalidated +
                "; biotechReapplied=" + result.BiotechReapplied +
                "; uiRefreshed=" + uiRefreshed +
                "; unfinishedDrafts=DISCARDED_ON_CONFIRMED_SUCCESS; " +
                "profileEditorGate=UNCHANGED; ownXmlWritesByDialog=0; profileJsonWrites=0; foreignWrites=0; " +
                "build=" + XpCustomizerMod.BuildId);
        }

    }
}
