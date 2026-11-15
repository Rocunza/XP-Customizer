#if DEBUG
using System;
using System.Collections.Generic;
using Verse;
using XpCustomizer.UI;

namespace XpCustomizer
{
    /// <summary>
    /// N1-R1 detached recovery proofs. No real settings, GUI focus, profile,
    /// XML, GeneDefs, or other mod state is read or modified.
    /// </summary>
    internal static class CareerNumericRecoveryR1SelfTests
    {
        internal static void Run()
        {
            int checks = 0;
            Action<bool, string> test = delegate(bool ok, string label)
            {
                checks++;
                if (!ok) throw new InvalidOperationException("N1-R1 " + label);
            };
            XpCustomizerSettings live = new XpCustomizerSettings();
            live.careerXpMultiplier = 6f;
            NumericInputState total = new NumericInputState();
            total.FocusControlName = "ZCN.XpCustomizer.ProfileFocus.Numeric.N1R1Total";
            total.buffer = "";
            total.WasFocused = true;
            List<NumericInputState> weights = new List<NumericInputState> {
                new NumericInputState()
            };
            List<NumericInputState> locks = new List<NumericInputState> {
                new NumericInputState()
            };
            CareerTotalMultiplierRecovery ticket;
            test(CareerTotalMultiplierRecovery.TryCapture(total, weights, locks,
                live, out ticket) && ticket != null, "empty total offered");
            test(live.careerXpMultiplier == 6f && total.buffer == "",
                "opening offer is read only");
            test(ticket.OwnedControlName == total.FocusControlName,
                "stable owned identity");
            test(!SettingsProfileNavigationController.MayUseRecoveryConsent(
                true, true, false, true, 3, 3, SettingsTab.Progression,
                SettingsTab.Progression), "tab/reset invalidation blocks");
            test(!SettingsProfileNavigationController.MayUseRecoveryConsent(
                true, false, false, true, 3, 2, SettingsTab.Progression,
                SettingsTab.Progression), "old consent serial rejected");
            test(!SettingsProfileNavigationController.MayUseRecoveryConsent(
                true, false, true, true, 3, 3, SettingsTab.Progression,
                SettingsTab.Progression), "concurrent navigation blocked");
            test(!SettingsProfileNavigationController.MayUseRecoveryConsent(
                true, false, false, true, 3, 3, SettingsTab.Progression,
                SettingsTab.Learning), "other tab rejected");
            test(SettingsProfileNavigationController.MayUseRecoveryConsent(
                true, false, false, true, 3, 3, SettingsTab.Progression,
                SettingsTab.Progression), "valid consent eligible");
            test(!SettingsProfileNavigationController.MayUseRecoveryConsent(
                true, false, false, false, 3, 3, SettingsTab.Progression,
                SettingsTab.Progression), "closed owner window fails closed");
            test(ticket.TryDiscard(total, weights, locks, live),
                "approved malformed text discard");
            test(total.buffer == null && !total.WasFocused &&
                total.IdleObservations == 0 && total.FocusControlName ==
                "ZCN.XpCustomizer.ProfileFocus.Numeric.N1R1Total",
                "reset observation but preserve control name");
            test(live.careerXpMultiplier == 6f && weights[0].buffer == null &&
                locks[0].buffer == null, "committed data and siblings unchanged");
            test(!ticket.TryDiscard(total, weights, locks, live),
                "replay forbidden");

            // Candidate changes between modal open and confirmation must fail
            // without even clearing the edited text.
            total.buffer = "-";
            test(CareerTotalMultiplierRecovery.TryCapture(total, weights, locks,
                live, out ticket), "second ticket capture");
            total.buffer = "1e";
            test(!ticket.TryDiscard(total, weights, locks, live) &&
                total.buffer == "1e", "changed buffer not discarded");
            test(CareerTotalMultiplierRecovery.TryCapture(total, weights, locks,
                live, out ticket), "third ticket capture");
            live.careerXpMultiplier = 7f;
            test(!ticket.TryDiscard(total, weights, locks, live) &&
                total.buffer == "1e", "changed settings reject stale ticket");
            live.careerXpMultiplier = 6f;
            test(CareerTotalMultiplierRecovery.TryCapture(total, weights, locks,
                live, out ticket), "fourth ticket capture");
            weights[0].buffer = "-";
            test(!ticket.TryDiscard(total, weights, locks, live) &&
                total.buffer == "1e", "sibling draft not erased");
            weights[0].buffer = null;
            total.FocusControlName = "ForeignMod.Number";
            test(!CareerTotalMultiplierRecovery.TryCapture(total, weights, locks,
                live, out ticket) && ticket == null,
                "foreign control not eligible");
            total.FocusControlName = "ZCN.XpCustomizer.ProfileFocus.Numeric.N1R1Total";
            total.buffer = "1.5";
            test(!CareerTotalMultiplierRecovery.TryCapture(total, weights, locks,
                live, out ticket), "well formed text not auto-discardable");
            total.buffer = "-";
            weights[0].buffer = "4"; // not observed -> Uncertain, never auto-clear
            test(!CareerTotalMultiplierRecovery.TryCapture(total, weights, locks,
                live, out ticket), "unobserved sibling fails closed");
            weights[0].buffer = null;
            test(CareerTotalMultiplierRecovery.TryCapture(total, weights, locks,
                live, out ticket), "recovered safe candidate");
            XpCustomizerSettings other = new XpCustomizerSettings();
            other.careerXpMultiplier = 6f;
            test(!ticket.TryDiscard(total, weights, locks, other),
                "different settings instance blocked");
            test(total.buffer == "-" && live.careerXpMultiplier == 6f,
                "all rejected attempts are read only");
            // The single-control navigation cleanup must never erase another
            // XP-owned control's observation, much less a foreign mod's.
            string forgotten = "ZCN.XpCustomizer.ProfileFocus.Numeric.N1R1Forgotten";
            string preserved = "ZCN.XpCustomizer.ProfileFocus.Numeric.N1R1Preserved";
            ProfileNumericFocus.DebugSeedStaleNumericNavigationDraft(forgotten, "-");
            ProfileNumericFocus.DebugSeedStaleNumericNavigationDraft(preserved, "-");
            string invalidName, invalidReason;
            test(!ProfileNumericFocus.ValidateLastFocusedDraft(
                out invalidName, out invalidReason) && invalidName == preserved,
                "second draft observed before selective cleanup");
            ProfileNumericFocus.ForgetConfirmedNumericDraft(forgotten);
            test(!ProfileNumericFocus.ValidateLastFocusedDraft(
                out invalidName, out invalidReason) && invalidName == preserved,
                "selective cleanup keeps other owned draft");
            ProfileNumericFocus.ForgetConfirmedNumericDraft(preserved);
            test(ProfileNumericFocus.ValidateLastFocusedDraft(
                out invalidName, out invalidReason),
                "selected stale navigation draft fully cleared");
            bool foreignDenied = false;
            try { ProfileNumericFocus.ForgetConfirmedNumericDraft("OtherMod.Numeric"); }
            catch (ArgumentException) { foreignDenied = true; }
            test(foreignDenied, "foreign numeric cache cannot be targeted");
            test(SettingsProfileNavigationController.NumericRecoveryVersion ==
                "XPC-N1-EDIT-RECOVERY-R1-20261112-R1",
                "R1 version contract");
            Log.Message("[XPC-N1-RECOVERY][R1_SELFTEST] version=" +
                SettingsProfileNavigationController.NumericRecoveryVersion +
                "; hotfix=XPC-N1-R1-CS1501-HF1-20261112; status=PASS; checks=" + checks +
                "; mode=DETACHED_PURE; settingsWrites=0; profileJsonWrites=0; " +
                "geneDefWrites=0; scribeCalls=0; realIMGUI=NOT_RUN; " +
                "build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
