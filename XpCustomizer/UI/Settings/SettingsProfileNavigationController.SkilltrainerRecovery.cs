using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace XpCustomizer.UI
{
    /// <summary>
    /// S2 modal bridge. Uses the SAME one-at-a-time consent state and original
    /// Request()/Complete() navigation gate as the already proven Career R1.
    /// </summary>
    internal sealed partial class SettingsProfileNavigationController
    {
        private bool TryOfferSkilltrainerRecovery(ProfileNavigationTarget target,
            SettingsTab sourceTab, string reason)
        {
            if (recoveryDialogOpen || sourceTab != SettingsTab.Progression ||
                lastObservedTab != sourceTab ||
                reason != "PendingEdit_SkilltrainerSettingsUI")
                return false;
            SkilltrainerNumericRecoveryTicket ticket;
            if (!SettingsNumericRecoveryController.TryPrepareSkilltrainerRecovery(
                sourceTab, reason, out ticket)) return false;

            recoveryDialogOpen = true;
            recoveryInvalidated = false;
            recoverySourceTab = sourceTab;
            int serial = ++recoveryOfferSerial;
            try
            {
                Find.WindowStack.Add(new Dialog_DiscardSkilltrainerNumericDraft(
                    delegate(bool approved)
                    {
                        ResolveSkilltrainerRecovery(serial, target, sourceTab,
                            ticket, approved);
                    }));
                LogSkilltrainerRecovery("OFFER", target, "CONSENT_REQUIRED");
                return true;
            }
            catch (Exception ex)
            {
                recoveryDialogOpen = false;
                LogSkilltrainerRecovery("OFFER_FAILED", target, ex.GetType().Name);
                return false;
            }
        }

        private void ResolveSkilltrainerRecovery(int serial,
            ProfileNavigationTarget target, SettingsTab sourceTab,
            SkilltrainerNumericRecoveryTicket ticket, bool approved)
        {
            bool recentlyDrawn = lastSettingsDrawFrame >= 0 &&
                Time.frameCount - lastSettingsDrawFrame <= 2;
            bool eligible = MayUseRecoveryConsent(recoveryDialogOpen,
                recoveryInvalidated, HasPending, recentlyDrawn,
                recoveryOfferSerial, serial, sourceTab, lastObservedTab);
            if (!recoveryDialogOpen || serial != recoveryOfferSerial) return;
            recoveryDialogOpen = false;
            recoveryInvalidated = false;
            if (!approved)
            {
                LogSkilltrainerRecovery("CANCEL", target, "NO_CHANGE");
                return;
            }
            if (!eligible ||
                !SettingsNumericRecoveryController.TryDiscardSkilltrainerRecovery(ticket))
            {
                LogSkilltrainerRecovery("REJECTED", target,
                    eligible ? "DRAFT_CHANGED_OR_OTHER_OWNER_PENDING" : "STALE_CONSENT");
                Messages.Message("XPC_N1R2S2_RecoveryStale".Translate().ToString(),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            // Forget exactly the discarded XP-owned numeric control, NEVER a
            // global reset, another editor, vanilla focus, or other mod state.
            string control = ticket.OwnedControlName;
            ProfileNumericFocus.ForgetConfirmedNumericDraft(control);
            if (String.Equals(GUI.GetNameOfFocusedControl(), control,
                StringComparison.Ordinal)) GUI.FocusControl(null);
            LogSkilltrainerRecovery("CONFIRMED_DISCARD", target,
                "TEXT_ONLY_ACCEPTED_VALUE_UNCHANGED");
            Request(target, sourceTab);
            LogSkilltrainerRecovery("RETRY_REQUESTED", target,
                HasPending ? "AWAIT_ORIGINAL_GATE" : "PRE_BLUR_REJECTED");
        }

        private static void LogSkilltrainerRecovery(string action,
            ProfileNavigationTarget target, string result)
        {
            Log.Message("[XPC-N1-R2R3] version=" +
                SettingsNumericRecoveryController.S2Version +
                "; stage=S2; action=" + action + "; target=" + target +
                "; owner=SkilltrainerSettingsUI; field=TOTAL_TRAINER_MULTIPLIER" +
                "; result=" + result + "; ownSettingsWritesByRecovery=0" +
                "; profileJsonWrites=0; foreignFocusChanges=0; build=" +
                XpCustomizerMod.BuildId);
        }
    }
}
