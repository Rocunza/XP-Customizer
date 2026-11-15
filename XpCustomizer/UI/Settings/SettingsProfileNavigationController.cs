using System;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    // Existing N1 entry targets. No change to toolbar choice or destination.
    internal enum ProfileNavigationTarget { None, Quick, Manage }

    /// <summary>
    /// Owns the settings toolbar's request -> focus-settle -> open state.
    /// Construction is state-only: XpCustomizerMod.Settings is not ready yet.
    /// Does not save, apply or seed profiles while validation is pending.
    /// </summary>
    internal sealed partial class SettingsProfileNavigationController
    {
        private ProfileNavigationTarget navigationTarget;
        private SettingsTab navigationSourceTab;
        private int navigationDrawChecks;
        private int navigationReadyChecks;
        private string navigationOwner = "NONE";
        private const int MaxNavigationDrawChecks = 32;

        // R1 consent is independent of the original N1 pending-navigation state.
        // No settings or profile mutation occurs while an offer is displayed.
        internal const string NumericRecoveryVersion =
            "XPC-N1-EDIT-RECOVERY-R1-20261112-R1";
        private bool recoveryDialogOpen;
        private bool recoveryInvalidated;
        private int recoveryOfferSerial;
        private SettingsTab recoverySourceTab;
        private SettingsTab lastObservedTab;
        private int lastSettingsDrawFrame = -1;

        internal bool HasPending { get { return navigationTarget != ProfileNavigationTarget.None; } }

        private enum NavigationStep { Waiting, Settled, TimedOut }

        private static bool IsCompletionEvent(EventType type)
        {
            return type == EventType.Repaint;
        }

        private void ClearPending()
        {
            navigationTarget = ProfileNavigationTarget.None;
            navigationReadyChecks = 0;
        }

        private bool TryBeginPending(ProfileNavigationTarget target, SettingsTab currentTab, string owner)
        {
            if (navigationTarget != ProfileNavigationTarget.None) return false;
            navigationTarget = target;
            navigationSourceTab = currentTab;
            navigationOwner = owner;
            navigationDrawChecks = 0;
            navigationReadyChecks = 0;
            return true;
        }

        // Evaluated once per Repaint by Complete(). The 2nd Ready at the 32nd
        // check wins over timeout, exactly as in the pre-split window.
        private void BeginRepaintCheck()
        {
            navigationDrawChecks++;
        }

        private NavigationStep AdvancePending(bool ready)
        {
            if (ready) navigationReadyChecks++;
            else navigationReadyChecks = 0;
            if (navigationReadyChecks >= 2) return NavigationStep.Settled;
            if (navigationDrawChecks >= MaxNavigationDrawChecks) return NavigationStep.TimedOut;
            return NavigationStep.Waiting;
        }

        internal void CancelIfTabChanged(SettingsTab currentTab)
        {
            lastObservedTab = currentTab;
            lastSettingsDrawFrame = Time.frameCount;
            if (recoveryDialogOpen && recoverySourceTab != currentTab)
                recoveryInvalidated = true;
            if (HasPending && navigationSourceTab != currentTab)
                BlockNavigation("TAB_CHANGED");
        }

        internal void BlockIfPending(string reason)
        {
            // A settings reset invalidates any outstanding recovery consent.
            if (recoveryDialogOpen) recoveryInvalidated = true;
            if (HasPending) BlockNavigation(reason);
        }

        // UI-only transition; never writes profiles/XML or bypasses the
        // existing preflight/explicit confirmation in destination windows.
        internal void Request(ProfileNavigationTarget target, SettingsTab currentTab)
        {
            if (navigationTarget != ProfileNavigationTarget.None || recoveryDialogOpen) return;
            string focused = GUI.GetNameOfFocusedControl();
            string owner, reason;
            bool valid = ProfileNumericNavigation.ValidateBeforeBlur(currentTab,
                XpCustomizerMod.Settings, focused, out owner, out reason);
            if (!valid)
            {
                // The toolbar click may already have taken focus. Restore ONLY
                // the rejected XP-owned editor so its original blur handler does
                // not silently revert/clamp a draft we explicitly rejected.
                string rejected = ProfileNumericNavigation.RejectedControl;
                if (ProfileNumericNavigation.IsOwnedControl(rejected))
                    GUI.FocusControl(rejected);
                LogNavigation("BLOCKED", target, owner, reason);
#if DEBUG
                LogBlockedNumericDiagnostic("PRE_BLUR", target, currentTab,
                    owner, reason, ProfileNumericNavigation.RejectedControl);
                SettingsNumericRecoveryController.ObserveBlockedReadOnly(
                    "PRE_BLUR", target, currentTab, reason);
#endif
                Messages.Message("XPC_L3F1_NumericInvalid".Translate().ToString(),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (!TryBeginPending(target, currentTab, owner)) return;
            LogNavigation("REQUEST", target, owner, "AWAIT_EDITOR_SETTLEMENT");
            // Only an XP-owned named control may lose focus here. Never clear
            // the keyboard focus belonging to RimWorld or another mod.
            if (ProfileNumericNavigation.IsOwnedControl(focused))
                GUI.FocusControl(null);
        }

        internal void Complete(SettingsTab currentTab)
        {
            if (navigationTarget == ProfileNavigationTarget.None) return;
            // Check settlement once per rendered frame, not once per Layout,
            // MouseUp and Repaint event. Keep window creation off MouseUp.
            if (Event.current == null || !IsCompletionEvent(Event.current.type))
                return;
            if (navigationSourceTab != currentTab)
            {
                BlockNavigation("TAB_CHANGED");
                return;
            }
            BeginRepaintCheck();
            ProfileEditorReadinessResult readiness = ProfileEditorReadinessService.Check();
            NavigationStep step = AdvancePending(readiness.Code == ProfileEditorReadinessCode.Ready);

            // Never turn a single transient blur into readiness. Full existing
            // readiness policy has the final word, including hidden Decay fields.
            if (step == NavigationStep.Settled)
            {
                ProfileNavigationTarget destination = navigationTarget;
                string owner = navigationOwner;
                ClearPending();
                LogNavigation("SETTLED", destination, owner, "READINESS_READY_TWICE");
                if (destination == ProfileNavigationTarget.Quick)
                    QuickProfileMenuController.OpenMenu();
                else if (destination == ProfileNavigationTarget.Manage)
                    ProfileManagerWindow.OpenOnce();
                LogNavigation("OPEN", destination, owner, "EXPLICIT_NAVIGATION_ONLY");
                return;
            }
            if (step == NavigationStep.TimedOut)
                BlockNavigation(readiness.Code + "_" + readiness.Source);
        }

        private void BlockNavigation(string reason)
        {
            ProfileNavigationTarget destination = navigationTarget;
            string owner = navigationOwner;
            SettingsTab sourceTab = navigationSourceTab;
            ClearPending();
            LogNavigation("BLOCKED", destination, owner, reason);
#if DEBUG
            if (reason.StartsWith("PendingEdit_", StringComparison.Ordinal) ||
                reason.StartsWith("Uncertain_", StringComparison.Ordinal))
                LogBlockedNumericDiagnostic("POST_SETTLE", destination, sourceTab,
                    owner, reason, null);
            SettingsNumericRecoveryController.ObserveBlockedReadOnly(
                "POST_SETTLE", destination, sourceTab, reason);
#endif
            // Only individually proven, post-settle numeric text owners may
            // receive consent. R1 and S2 share ONE modal latch. Other owners,
            // including Uncertain and staged drafts, remain fail-closed.
            if (!TryOfferTotalMultiplierRecovery(destination, sourceTab, reason) &&
                !TryOfferSkilltrainerRecovery(destination, sourceTab, reason))
                Messages.Message("XPC_L3F1_NumericPending".Translate().ToString(),
                    MessageTypeDefOf.RejectInput, false);
        }

        private bool TryOfferTotalMultiplierRecovery(ProfileNavigationTarget target,
            SettingsTab sourceTab, string reason)
        {
            if (recoveryDialogOpen || sourceTab != SettingsTab.Progression ||
                lastObservedTab != sourceTab ||
                reason != "PendingEdit_CareerProgressionSettingsUI")
                return false;

            CareerTotalMultiplierRecovery ticket;
            if (!CareerProgressionSettingsUI.TryPrepareTotalMultiplierRecovery(out ticket))
                return false;

            recoveryDialogOpen = true;
            recoveryInvalidated = false;
            recoverySourceTab = sourceTab;
            int serial = ++recoveryOfferSerial;
            try
            {
                Find.WindowStack.Add(new Dialog_DiscardCareerNumericDraft(
                    delegate(bool approved)
                    {
                        ResolveTotalMultiplierRecovery(serial, target, sourceTab,
                            ticket, approved);
                    }));
                LogRecovery("OFFER", target, "TOTAL_MULTIPLIER", "CONSENT_REQUIRED");
                return true;
            }
            catch (Exception ex)
            {
                recoveryDialogOpen = false;
                LogRecovery("OFFER_FAILED", target, "TOTAL_MULTIPLIER",
                    ex.GetType().Name);
                return false;
            }
        }

        // Pure gate is tested independently; the dialog callback may run after
        // the tab changed or settings were reset while confirmation was open.
        internal static bool MayUseRecoveryConsent(bool open, bool invalidated,
            bool anotherNavigationPending, bool ownerWindowRecentlyDrawn,
            int currentSerial, int offeredSerial,
            SettingsTab originalTab, SettingsTab latestTab)
        {
            return open && !invalidated && !anotherNavigationPending &&
                ownerWindowRecentlyDrawn && currentSerial == offeredSerial &&
                originalTab == latestTab;
        }

        private void ResolveTotalMultiplierRecovery(int serial,
            ProfileNavigationTarget target, SettingsTab sourceTab,
            CareerTotalMultiplierRecovery ticket, bool approved)
        {
            // The containing mod-settings window must still be drawn. A modal
            // surviving after settings were closed cannot revoke old drafts.
            bool ownerWindowRecentlyDrawn = lastSettingsDrawFrame >= 0 &&
                Time.frameCount - lastSettingsDrawFrame <= 2;
            bool eligible = MayUseRecoveryConsent(recoveryDialogOpen,
                recoveryInvalidated, HasPending, ownerWindowRecentlyDrawn,
                recoveryOfferSerial, serial, sourceTab, lastObservedTab);
            if (!recoveryDialogOpen || recoveryOfferSerial != serial) return;
            recoveryDialogOpen = false;
            recoveryInvalidated = false;
            if (!approved)
            {
                LogRecovery("CANCEL", target, "TOTAL_MULTIPLIER", "NO_CHANGE");
                return;
            }
            if (!eligible)
            {
                LogRecovery("REJECTED", target, "TOTAL_MULTIPLIER", "STALE_CONSENT");
                Messages.Message("XPC_N1R1_RecoveryStale".Translate().ToString(),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            // The full allowlisted readiness gate must still implicate the
            // same Career owner, not an Uncertain or a newly changed editor.
            ProfileEditorReadinessResult before = ProfileEditorReadinessService.Check();
            if (before.Code != ProfileEditorReadinessCode.PendingEdit ||
                before.Source != "CareerProgressionSettingsUI" ||
                !CareerProgressionSettingsUI.TryDiscardTotalMultiplierRecovery(ticket))
            {
                LogRecovery("REJECTED", target, "TOTAL_MULTIPLIER", "DRAFT_CHANGED");
                Messages.Message("XPC_N1R1_RecoveryStale".Translate().ToString(),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            // Clear the one stale owned navigation cache entry, never the raw
            // states of another editor or another mod. Preserve stable widget ID.
            string control = ticket.OwnedControlName;
            ProfileNumericFocus.ForgetConfirmedNumericDraft(control);
            if (String.Equals(GUI.GetNameOfFocusedControl(), control,
                StringComparison.Ordinal))
                GUI.FocusControl(null);
            LogRecovery("CONFIRMED_DISCARD", target, "TOTAL_MULTIPLIER",
                "TEXT_ONLY_COMMITTED_VALUE_UNCHANGED");

            // Retry through the ORIGINAL pre-blur and 2x Ready/32x Repaint
            // state machine. This never directly opens or applies a profile.
            Request(target, sourceTab);
            LogRecovery("RETRY_REQUESTED", target, "TOTAL_MULTIPLIER",
                HasPending ? "AWAIT_ORIGINAL_GATE" : "PRE_BLUR_REJECTED");
        }

        private static void LogRecovery(string action, ProfileNavigationTarget target,
            string field, string result)
        {
            Log.Message("[XPC-N1-RECOVERY] version=" + NumericRecoveryVersion +
                "; phase=R1; action=" + action + "; target=" + target +
                "; field=" + field + "; result=" + result +
                "; ownSettingsWritesByRecovery=0; profileJsonWrites=0; " +
                "foreignFocusChanges=0; build=" + XpCustomizerMod.BuildId);
        }

        private static void LogNavigation(string action, ProfileNavigationTarget target,
            string owner, string reason)
        {
            // One entry per user click/settle/reject; no per-frame log spam.
            Log.Message("[XPC-N1][PROFILE_NUMERIC_NAV] action=" + action +
                "; target=" + target + "; owner=" + owner +
                "; reason=" + reason + "; profileJsonWrites=0; " +
                "configXmlWrites=0; build=" + XpCustomizerMod.BuildId);
        }


#if DEBUG
        internal const string NumericRecoveryDiagnosticVersion =
            "XPC-N1-EDIT-RECOVERY-R0-20261112-R1";

        // R0 intentionally makes no recovery offer and never invokes a reset.
        // The field candidate is identified by XP-owned state identity only.
        private static void LogBlockedNumericDiagnostic(string phase,
            ProfileNavigationTarget target, SettingsTab tab, string owner,
            string reason, string rejectedControl)
        {
            string field = "OUT_OF_SCOPE";
            bool careerEvidence = phase == "PRE_BLUR"
                ? owner == "ProfileNumericFocus"
                : reason == "PendingEdit_CareerProgressionSettingsUI";
            if (tab == SettingsTab.Progression && careerEvidence)
            {
                try
                {
                    field = CareerProgressionSettingsUI.DiagnoseBlockedNumericField(
                        rejectedControl);
                }
                catch (Exception)
                {
                    field = "PROBE_FAILED";
                }
            }
            Log.Message("[XPC-N1-RECOVERY] version=" +
                NumericRecoveryDiagnosticVersion + "; phase=" + phase +
                "; target=" + target + "; tab=" + tab +
                "; owner=" + owner + "; reason=" + reason +
                "; field=" + field + "; action=READ_ONLY_DIAGNOSTIC" +
                "; settingsWrites=0; profileWrites=0; foreignFocusChanges=0" +
                "; build=" + XpCustomizerMod.BuildId);
        }

        // The EXACT production transitions above are exercised on detached
        // controllers. No GUI, Profile IO, settings, GeneDefs or messages.
        internal bool DebugStart(ProfileNavigationTarget target, SettingsTab sourceTab, string owner)
        {
            return TryBeginPending(target, sourceTab, owner);
        }
        internal string DebugStep(bool ready)
        {
            BeginRepaintCheck();
            return AdvancePending(ready).ToString();
        }
        internal bool DebugShouldCheckEvent(EventType eventType)
        {
            return IsCompletionEvent(eventType);
        }
        internal void DebugClear()
        {
            ClearPending();
        }
        internal bool DebugTabChanged(SettingsTab tab)
        {
            return HasPending && navigationSourceTab != tab;
        }
        internal int DebugDrawChecks { get { return navigationDrawChecks; } }
        internal int DebugReadyChecks { get { return navigationReadyChecks; } }
        internal ProfileNavigationTarget DebugTarget { get { return navigationTarget; } }
#endif
    }
}
