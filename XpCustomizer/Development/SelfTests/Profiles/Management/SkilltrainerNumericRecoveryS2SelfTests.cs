#if DEBUG
using System;
using Verse;
using XpCustomizer.UI;

namespace XpCustomizer
{
    /// <summary>
    /// S2 detached policy and ticket checks, no live settings/game/UI/profile IO.
    /// </summary>
    internal static class SkilltrainerNumericRecoveryS2SelfTests
    {
        internal static void Run()
        {
            int checks = 0;
            Action<bool, string> test = delegate(bool ok, string label)
            {
                checks++;
                if (!ok) throw new InvalidOperationException("N1-S2 " + label);
            };
            test(NumericDraftRecoveryRegistry.HasSkilltrainerCapability(),
                "single registered Skilltrainer capability");
            NumericDraftRecoveryOwner[] owners = NumericDraftRecoveryRegistry.CopyOwners();
            int active = 0;
            foreach (NumericDraftRecoveryOwner owner in owners)
                if (owner.Family == NumericDraftRecoveryFamily.NumericSingleSkilltrainerActive)
                    active++;
            test(active == 1, "exactly one S2 active owner");
            XpCustomizerSettings settings = new XpCustomizerSettings();
            settings.skilltrainerXpMultiplier = 1.5f;
            NumericInputState input = new NumericInputState();
            const string control = "ZCN.XpCustomizer.ProfileFocus.Numeric.S2Trainer";
            input.FocusControlName = control;
            input.buffer = "";
            input.WasFocused = true;
            SkilltrainerNumericRecoveryTicket ticket;
            test(SkilltrainerNumericRecoveryTicket.TryCapture(input, settings,
                out ticket) && ticket != null, "empty draft capture");
            test(ticket.OwnedControlName == control, "owned control identity");
            test(input.buffer == "" && settings.skilltrainerXpMultiplier == 1.5f,
                "offer read-only");
            test(ticket.TryDiscard(input, settings), "confirmed discard");
            test(input.buffer == null && !input.WasFocused &&
                input.FocusControlName == control, "state reset preserves identity");
            test(settings.skilltrainerXpMultiplier == 1.5f,
                "accepted setting unchanged");
            test(!ticket.TryDiscard(input, settings), "one-use ticket");

            input.buffer = "-";
            test(SkilltrainerNumericRecoveryTicket.TryCapture(input, settings,
                out ticket), "partial dash captured");
            input.buffer = "1e";
            test(!ticket.TryDiscard(input, settings) && input.buffer == "1e",
                "draft changed -> no reset");
            test(SkilltrainerNumericRecoveryTicket.TryCapture(input, settings,
                out ticket), "second partial captured");
            settings.skilltrainerXpMultiplier = 2f;
            test(!ticket.TryDiscard(input, settings) && input.buffer == "1e",
                "accepted value changed -> no reset");
            settings.skilltrainerXpMultiplier = 1.5f;
            test(SkilltrainerNumericRecoveryTicket.TryCapture(input, settings,
                out ticket), "third partial captured");
            XpCustomizerSettings other = new XpCustomizerSettings();
            other.skilltrainerXpMultiplier = 1.5f;
            test(!ticket.TryDiscard(input, other) && input.buffer == "1e",
                "different settings root blocked");
            NumericInputState unrelated = new NumericInputState();
            unrelated.FocusControlName = control;
            unrelated.buffer = "1e";
            test(!ticket.TryDiscard(unrelated, settings),
                "other input object blocked");
            input.FocusControlName = "ForeignMod.Number";
            test(!ticket.TryDiscard(input, settings), "changed control rejected");
            test(!SkilltrainerNumericRecoveryTicket.TryCapture(input, settings,
                out ticket) && ticket == null, "foreign control never captures");
            input.FocusControlName = control;
            input.buffer = "1.5";
            test(!SkilltrainerNumericRecoveryTicket.TryCapture(input, settings,
                out ticket), "valid numeric not offered");
            input.buffer = ".";
            test(SkilltrainerNumericRecoveryTicket.TryCapture(input, settings,
                out ticket), "single dot captured");
            settings.skilltrainerXpMultiplier = float.NaN;
            test(!ticket.TryDiscard(input, settings) && input.buffer == ".",
                "non-finite new accepted value blocks");
            test(!SkilltrainerNumericRecoveryTicket.TryCapture(input, settings,
                out ticket), "non-finite capture blocked");
            settings.skilltrainerXpMultiplier = 1.5f;
            input.buffer = "";
            test(SkilltrainerNumericRecoveryTicket.TryCapture(input, settings,
                out ticket), "final valid ticket");
            test(ticket.TryDiscard(input, settings), "accepted final discard");

            NumericDraftRecoveryObservation eligible =
                new NumericDraftRecoveryObservation(
                    NumericDraftRecoveryObservationCode.OneOwnerPending,
                    "SkilltrainerSettingsUI", "SkilltrainerSettingsUI.multiplierInput",
                    true, 1, 0);
            test(SettingsNumericRecoveryController.IsSkilltrainerCandidate(
                SettingsTab.Progression, "PendingEdit_SkilltrainerSettingsUI", eligible),
                "one exact pending owner allowed");
            test(!SettingsNumericRecoveryController.IsSkilltrainerCandidate(
                SettingsTab.Learning, "PendingEdit_SkilltrainerSettingsUI", eligible),
                "other tab excluded");
            test(!SettingsNumericRecoveryController.IsSkilltrainerCandidate(
                SettingsTab.Progression, "Uncertain_SkilltrainerSettingsUI", eligible),
                "Uncertain excluded");
            test(!SettingsNumericRecoveryController.IsSkilltrainerCandidate(
                SettingsTab.Progression, "PendingEdit_CareerProgressionSettingsUI", eligible),
                "Career reason excluded");
            test(!SettingsNumericRecoveryController.IsSkilltrainerCandidate(
                SettingsTab.Progression, "PendingEdit_SkilltrainerSettingsUI", null),
                "null observation blocked");
            test(!SettingsNumericRecoveryController.IsSkilltrainerCandidate(
                SettingsTab.Progression, "PendingEdit_SkilltrainerSettingsUI",
                new NumericDraftRecoveryObservation(
                    NumericDraftRecoveryObservationCode.MultiplePending,
                    "NONE", null, false, 2, 0)), "multi pending blocked");
            test(!SettingsNumericRecoveryController.IsSkilltrainerCandidate(
                SettingsTab.Progression, "PendingEdit_SkilltrainerSettingsUI",
                new NumericDraftRecoveryObservation(
                    NumericDraftRecoveryObservationCode.Uncertain,
                    "NONE", null, false, 1, 1)), "uncertain blocked");
            test(!SettingsNumericRecoveryController.IsSkilltrainerCandidate(
                SettingsTab.Progression, "PendingEdit_SkilltrainerSettingsUI",
                new NumericDraftRecoveryObservation(
                    NumericDraftRecoveryObservationCode.OneOwnerPending,
                    "SkilltrainerSpecificXpSettingsUI",
                    "SkilltrainerSpecificXpSettingsUI.inputs[0]", true, 1, 0)),
                "per-skill editor excluded");
            test(!SettingsNumericRecoveryController.IsSkilltrainerCandidate(
                SettingsTab.Progression, "PendingEdit_SkilltrainerSettingsUI",
                new NumericDraftRecoveryObservation(
                    NumericDraftRecoveryObservationCode.OneOwnerPending,
                    "SkilltrainerSettingsUI", "SkilltrainerSettingsUI.multiplierInput",
                    false, 1, 0)), "unidentified control excluded");
            test(!SettingsNumericRecoveryController.IsSkilltrainerCandidate(
                SettingsTab.Progression, "PendingEdit_SkilltrainerSettingsUI",
                new NumericDraftRecoveryObservation(
                    NumericDraftRecoveryObservationCode.OneOwnerPending,
                    "SkilltrainerSettingsUI", "SkilltrainerSettingsUI.someOtherInput",
                    true, 1, 0)), "wrong field excluded");
            test(SettingsNumericRecoveryController.S2Version ==
                "XPC-N1-R2-S2-20261113-R1", "version identity");
            test(SettingsProfileNavigationController.MayUseRecoveryConsent(
                true, false, false, true, 3, 3,
                SettingsTab.Progression, SettingsTab.Progression),
                "shared one-modal consent valid");
            test(!SettingsProfileNavigationController.MayUseRecoveryConsent(
                true, true, false, true, 3, 3,
                SettingsTab.Progression, SettingsTab.Progression),
                "shared invalidated consent rejected");
            test(!SettingsProfileNavigationController.MayUseRecoveryConsent(
                true, false, false, false, 3, 3,
                SettingsTab.Progression, SettingsTab.Progression),
                "closed settings window rejected");
            Log.Message("[XPC-N1-R2R3][S2_SELFTEST] version=" +
                SettingsNumericRecoveryController.S2Version +
                "; status=PASS; checks=" + checks +
                "; owner=SkilltrainerSettingsUI; field=TOTAL_TRAINER_MULTIPLIER" +
                "; settingsWrites=0; profileJsonWrites=0; scribeCalls=0" +
                "; liveIMGUI=NOT_RUN; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
