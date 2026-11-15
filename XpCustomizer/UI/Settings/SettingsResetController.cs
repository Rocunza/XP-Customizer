using System;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    /// <summary>
    /// Owns both confirmation dialog latches and the current full Mod Defaults
    /// reset flow. The legacy numeric-only flow resides in a partial file.
    /// Constructors only store dependencies; they do not read live settings.
    /// </summary>
    internal sealed partial class SettingsResetController
    {
        // Avoid stacked confirmation windows on rapid clicks.
        // Close is driven by the original dialog on-close callback.
        internal sealed class ConfirmationOpenLatch
        {
            private bool isOpen;
            internal bool TryOpen()
            {
                if (isOpen) return false;
                isOpen = true;
                return true;
            }
            internal void Close() { isOpen = false; }
            internal bool IsOpen { get { return isOpen; } }
        }

        private readonly ConfirmationOpenLatch vanillaResetLatch = new ConfirmationOpenLatch();
        private readonly ConfirmationOpenLatch modNumericResetLatch = new ConfirmationOpenLatch();
        private readonly SettingsProfileNavigationController profileNavigation;
        private readonly Action disarmLiveAudit;

        internal SettingsResetController(SettingsProfileNavigationController profileNavigation,
            Action disarmLiveAudit)
        {
            if (profileNavigation == null) throw new ArgumentNullException("profileNavigation");
            this.profileNavigation = profileNavigation;
            this.disarmLiveAudit = disarmLiveAudit;
        }

        // R4: the sole footer reset opens the existing confirmed full-reset modal.
        // R1: a footer click only opens a modal; no settings, GeneDefs,
        // profile JSON or own XML are changed by this request.
        internal void RequestVanillaResetConfirmation()
        {
            if (!vanillaResetLatch.TryOpen()) return;
            try
            {
                Find.WindowStack.Add(new Dialog_ConfirmGlobalVanillaReset(
                    ApplyConfirmedVanillaReset,
                    delegate { vanillaResetLatch.Close(); }));
                Log.Message("[XPC-RESET][GLOBAL_RESET_UI] kind=MOD_DEFAULTS; action=OPEN; " +
                    "resetCalls=0; uiRefresh=0; profileJsonWrites=0; " +
                    "ownXmlWritesByDialog=0; uiRevision=RESET_CONFIRM_COPY_FONT_RU1; build=" + XpCustomizerMod.BuildId);
            }
            catch (Exception ex)
            {
                vanillaResetLatch.Close();
                Log.Warning("[XPC-RESET][GLOBAL_RESET_UI] kind=MOD_DEFAULTS; " +
                    "action=OPEN_FAILED; reason=" + ex.GetType().Name +
                    "; resetCalls=0; build=" + XpCustomizerMod.BuildId);
            }
        }

        // R4: the old global Vanilla reset confirmation is reused for one
        // install-default reset. The section-level Vanilla presets remain intact.
        private void ApplyConfirmedVanillaReset()
        {
            // Fail closed if a recovery lock becomes active after the dialog
            // was opened; do not bypass the existing settings persistence gate.
            if (ProfileProductionWriteGate.IsUncertain ||
                ProfileProductionWriteGate.IsBusy ||
                RuntimeRecoverySessionGate.LiveIsBlocked ||
                ProfileWriteSessionGate.IsBlocked)
                throw new GlobalVanillaResetBlockedException();
            if (disarmLiveAudit != null) disarmLiveAudit();
            SettingsPresetService.ResetAllToModDefaults();

            // Existing preset already reapplies owned Biotech GeneDefs. This
            // refresh only clears stale XP-owned editor buffers, once.
            SettingsRefreshCoordinator.NotifyAll(
                SettingsRefreshReason.GlobalVanillaReset);

            // R4-HF1: a confirmed full reset also discards unfinished XP-owned
            // drafts. R4 refreshed editor buffers but forgot the N1 focus/raw
            // observations, so Quick/Manage could reject stale INVALID_DRAFT or
            // UNRECOGNIZED_OWN_FOCUS after an otherwise successful reset.
            // Reuse the already-tested R3-B cleanup ONLY after a successful
            // refresh. Never blur vanilla/other-mod controls or weaken the
            // normal profile navigation validation path.
            ProfileNumericFocus.ForgetAfterConfirmedModNumericReset();
            string focusedAfter = GUI.GetNameOfFocusedControl();
            bool xpFocusClearRequested = ProfileNumericNavigation.IsOwnedControl(focusedAfter);
            if (xpFocusClearRequested)
                GUI.FocusControl(null);

            profileNavigation.BlockIfPending("SETTINGS_CHANGED_DURING_NAVIGATION");

            // BP4: optional disk side effect AFTER the full settings reset and
            // R4-HF1 editor-focus cleanup have completed successfully.
            // A missing/invalid shipped source cannot undo a successful settings
            // reset; report partial profile restoration independently.
            BundledProfileInstaller.RestoreReport bundle =
                BundledProfileInstaller.RestoreMissingForCurrentUser();
            if (!bundle.Success)
                Messages.Message("XPC_L3F1_BundledPartial".Translate().ToString(),
                    MessageTypeDefOf.RejectInput, false);

            Log.Message("[XPC-RESET][GLOBAL_RESET] kind=MOD_DEFAULTS; status=APPLIED; " +
                "mentalThresholdStrength=0.60; defaultCurves=GWS_LOW_LINEAR_HIGH_DIMINISHING+CONS6_LINEAR; " +
                "allSwitchesReset=True; uiRefresh=1; " +
                "numericFocusObservations=DISCARDED; unfinishedDrafts=DISCARDED_ON_CONFIRMED_SUCCESS; " +
                "xpOwnedFocusClearRequested=" + xpFocusClearRequested + "; foreignFocus=UNTOUCHED; " +
                "profileNavigationGate=UNCHANGED; " +
                "directOwnXmlWrites=0; profileJsonCreates=" + bundle.Created +
                "; bundlePreserved=" + bundle.Preserved + "; bundleRestoreFailed=" + bundle.Failed +
                "; build=" + XpCustomizerMod.BuildId);
        }

    }
}
