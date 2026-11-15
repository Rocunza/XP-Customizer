#if DEBUG
using System;
using Verse;
using XpCustomizer.UI;

namespace XpCustomizer
{
    /// <summary>R1 pure consent tests. No game GUI, Scribe, GeneDefs or disk IO.</summary>
    internal static class GlobalVanillaResetR1SelfTests
    {
        internal static void Run()
        {
            int checks = 0;
            int resetCalls = 0;
            Action fakeReset = delegate { resetCalls++; };
            GlobalVanillaResetConfirmationGate cancelled =
                new GlobalVanillaResetConfirmationGate();
            Check(cancelled.IsPending, "new gate pending", ref checks);
            Check(resetCalls == 0, "open must not apply", ref checks);
            Check(cancelled.TryCancel(), "cancel accepted", ref checks);
            Check(!cancelled.IsPending, "cancel closes", ref checks);
            Check(!cancelled.TryConfirm(fakeReset), "cancel prevents yes", ref checks);
            Check(!cancelled.TryCancel(), "repeated cancel rejected", ref checks);
            Check(resetCalls == 0, "cancel does not apply", ref checks);

            GlobalVanillaResetConfirmationGate accepted =
                new GlobalVanillaResetConfirmationGate();
            Check(accepted.TryConfirm(fakeReset), "first yes accepted", ref checks);
            Check(accepted.Attempted && accepted.Succeeded, "yes status", ref checks);
            Check(!accepted.TryConfirm(fakeReset), "duplicate yes rejected", ref checks);
            Check(!accepted.TryCancel(), "cancel after yes rejected", ref checks);
            Check(resetCalls == 1, "yes executes once", ref checks);

            GlobalVanillaResetConfirmationGate failed =
                new GlobalVanillaResetConfirmationGate();
            bool caught = false;
            int failedCalls = 0;
            try
            {
                failed.TryConfirm(delegate
                {
                    failedCalls++;
                    throw new InvalidOperationException("synthetic test error");
                });
            }
            catch (InvalidOperationException) { caught = true; }
            Check(caught, "fault surfaced", ref checks);
            Check(failed.Attempted && !failed.Succeeded, "failed != success", ref checks);
            Check(!failed.TryConfirm(fakeReset), "failure cannot retry", ref checks);
            Check(!failed.TryCancel(), "failure cannot relabel cancel", ref checks);
            Check(failedCalls == 1 && resetCalls == 1, "fault single attempt", ref checks);

            GlobalVanillaResetConfirmationGate unused =
                new GlobalVanillaResetConfirmationGate();
            Check(unused.IsPending && !unused.Attempted && !unused.Succeeded,
                "new gate no effects", ref checks);
            bool rejectedNull = false;
            try { unused.TryConfirm(null); }
            catch (ArgumentNullException) { rejectedNull = true; }
            Check(rejectedNull && unused.IsPending, "null callback fails closed", ref checks);
            Check(unused.TryCancel(), "cancel after null", ref checks);
            Check(resetCalls == 1, "no other reset calls", ref checks);

            // FLR1: the existing per-tab Vanilla-labelled button now uses the
            // SAME 60% Mod Defaults route as the full reset. The legacy
            // internal ResetFurtherLearningToVanilla() remains at 100%.
            int r4Checks = 0;
            XpCustomizerSettings modDefaults = new XpCustomizerSettings();
            modDefaults.furtherLearningBaseFactor = 0.37f;
            modDefaults.furtherLearningMentalThresholdEnabled = true;
            modDefaults.furtherLearningMentalThresholdStrength = 1.8f;
            modDefaults.furtherLearningTraitModifiersEnabled = true;
            modDefaults.furtherLearningVolatileModifier = -17f;
            modDefaults.learningCapacityBaseDailyCap = 5432f;
            SettingsPresetService.ResetFurtherLearningToModDefaults(modDefaults);
            Check(modDefaults.furtherLearningBaseFactor ==
                XpCustomizerDefaults.FurtherLearningBase,
                "R4 Further Learning rate is default", ref r4Checks);
            Check(modDefaults.furtherLearningMentalThresholdStrength ==
                XpCustomizerDefaults.FurtherLearningMentalStrength &&
                modDefaults.furtherLearningMentalThresholdStrength == 0.60f,
                "R4 full Mod Defaults restores 60%", ref r4Checks);
            Check(!modDefaults.furtherLearningMentalThresholdEnabled,
                "R4 Mental Threshold master OFF", ref r4Checks);
            Check(!modDefaults.furtherLearningTraitModifiersEnabled,
                "R4 trait modifiers OFF", ref r4Checks);
            Check(modDefaults.furtherLearningVolatileModifier == 0f,
                "R4 legacy trait values neutral", ref r4Checks);
            Check(modDefaults.learningCapacityBaseDailyCap == 5432f,
                "FLR1 section reset does not touch daily capacity", ref r4Checks);
            SettingsPresetService.ResetFurtherLearningToModDefaults(modDefaults);
            Check(modDefaults.furtherLearningMentalThresholdStrength == 0.60f &&
                !modDefaults.furtherLearningMentalThresholdEnabled,
                "FLR1 section reset repeated is idempotent", ref r4Checks);

            XpCustomizerSettings vanillaReference = new XpCustomizerSettings();
            SettingsPresetService.ApplyFurtherLearningBaseline(vanillaReference,
                XpCustomizerDefaults.FurtherLearningMentalVanillaStrength);
            Check(vanillaReference.furtherLearningMentalThresholdStrength == 1f,
                "R4 internal Vanilla reference stays 100%", ref r4Checks);
            Check(new XpCustomizerSettings().furtherLearningMentalThresholdStrength ==
                modDefaults.furtherLearningMentalThresholdStrength,
                "R4 global strength equals a fresh settings instance", ref r4Checks);
            Log.Message("[XPC-R4][SINGLE_MOD_DEFAULTS_SELFTEST] status=PASS; " +
                "checks=" + r4Checks + "; mentalThreshold=60%; uiButtonLabel=VANILLA_UNCHANGED; " +
                "uiButtonPolicy=MOD_DEFAULTS_60; internalVanillaReference=100%; " +
                "dialogConsent=R1_ONE_SHOT; oldNumericReset=UI_HIDDEN_ONLY; " +
                "profileJsonWrites=0; ownXmlWrites=0; geneDefWrites=0; liveSettingsWrites=0; " +
                "build=" + XpCustomizerMod.BuildId);

            // R4-HF1: reproduce the actual post-reset navigation symptom in
            // isolation: stale owned draft blocks Profile navigation; clearing
            // the confirmed-reset observations makes normal N1 validation ready.
            // The real IMGUI focus-clearing call still needs a game click test.
            int hf1Checks = 0;
            string syntheticOwned = "ZCN.XpCustomizer.ProfileFocus.R4HF1Test";
            string owner, reason;
            try
            {
                ProfileNumericFocus.DebugSeedStaleNumericNavigationDraft(
                    syntheticOwned, "-");
                ProfileNumericFocus.ObserveInactiveCore(
                    "R4HF1.syntheticRaw", "-", "0", true);
                Check(ProfileNumericFocus.IsInactiveRaw("R4HF1.syntheticRaw"),
                    "HF1 stale raw exists before reset", ref hf1Checks);
                Check(!ProfileNumericNavigation.ValidateBeforeBlur(
                    SettingsTab.Progression, new XpCustomizerSettings(), "",
                    out owner, out reason) &&
                    owner == "ProfileNumericFocus" && reason == "INVALID_DRAFT",
                    "HF1 stale numeric draft blocks before reset", ref hf1Checks);
                ProfileNumericFocus.ForgetAfterConfirmedModNumericReset();
                Check(!ProfileNumericFocus.IsInactiveRaw("R4HF1.syntheticRaw"),
                    "HF1 raw observations cleared", ref hf1Checks);
                Check(ProfileNumericFocus.ValidateLastFocusedDraft(
                    out owner, out reason) && reason == "NONE",
                    "HF1 stale last-focused draft cleared", ref hf1Checks);
                Check(ProfileNumericNavigation.ValidateBeforeBlur(
                    SettingsTab.Progression, new XpCustomizerSettings(), "",
                    out owner, out reason) && reason == "NONE",
                    "HF1 normal navigation validation ready after reset", ref hf1Checks);
                Check(ProfileNumericNavigation.IsOwnedControl(syntheticOwned) &&
                    !ProfileNumericNavigation.IsOwnedControl("RimWorld.ForeignControl") &&
                    !ProfileNumericNavigation.IsOwnedControl(null),
                    "HF1 focus ownership is strictly XP-scoped", ref hf1Checks);
            }
            finally
            {
                // Never leak synthetic observations into subsequent test suites.
                ProfileNumericFocus.ForgetAfterConfirmedModNumericReset();
            }
            Log.Message("[XPC-R4HF1][RESET_FOCUS_SELFTEST] status=PASS; checks=" +
                hf1Checks + "; before=INVALID_DRAFT; after=NAVIGATION_READY; " +
                "rawAndLastFocused=CLEARED; foreignFocus=NOT_OWNED; " +
                "scope=ISOLATED_NO_IMGUI; liveResetClick=MANUAL; " +
                "settingsWrites=0; profileJsonWrites=0; geneDefWrites=0; " +
                "build=" + XpCustomizerMod.BuildId);

            Log.Message("[XPC-RESET-R1][CONFIRM_GATE_SELFTEST] status=PASS; checks=" +
                checks + "; scope=PURE_CONSENT_NO_IMGUI; cancelNoApply=PASS; " +
                "yesOneShot=PASS; failedAttemptNoRetry=PASS; " +
                "realSettingsWrites=0; geneDefWrites=0; profileJsonWrites=0; " +
                "build=" + XpCustomizerMod.BuildId);
        }

        private static void Check(bool value, string label, ref int count)
        {
            count++;
            if (!value)
                throw new InvalidOperationException("R1 confirmation gate: " + label);
        }
    }
}
#endif
