#if DEBUG
using System;
using Verse;
using XpCustomizer.UI;

namespace XpCustomizer
{
    internal static class ProfileNumericNavigationN1SelfTests
    {
        internal static void Run()
        {
            int checks = 0;
            Action<bool, string> test = delegate(bool ok, string label)
            {
                checks++;
                if (!ok) throw new InvalidOperationException("N1 " + label);
            };
            string reason;
            test(ProfileNumericNavigation.ValidInteger("-6", -20, 0, out reason),
                "valid biotech negative");
            test(ProfileNumericNavigation.ValidInteger("+8", 0, 20, out reason),
                "valid biotech positive");
            test(!ProfileNumericNavigation.ValidInteger("-", -20, 0, out reason) &&
                reason == "INVALID_DRAFT", "partial minus rejected");
            test(!ProfileNumericNavigation.ValidInteger("", -20, 0, out reason) &&
                reason == "INVALID_DRAFT", "empty rejected");
            test(!ProfileNumericNavigation.ValidInteger("-21", -20, 0, out reason) &&
                reason == "OUT_OF_RANGE", "integer out of range rejected");
            test(!ProfileNumericNavigation.ValidInteger("2147483648", 0, 20, out reason) &&
                reason == "INVALID_DRAFT", "int overflow rejected");
            test(!ProfileNumericNavigation.ValidInteger("1.5", 0, 20, out reason),
                "fractional integer rejected");
            test(ProfileNumericNavigation.ValidFloat("1.35", 1f, 5f, out reason),
                "valid staged float");
            test(!ProfileNumericNavigation.ValidFloat("1.", 0f, 5f, out reason) &&
                reason == "INVALID_DRAFT", "unfinished decimal rejected");
            test(!ProfileNumericNavigation.ValidFloat("NaN", 0f, 5f, out reason),
                "NaN rejected");
            test(!ProfileNumericNavigation.ValidFloat("Infinity", 0f, 5f, out reason),
                "Infinity rejected");
            test(!ProfileNumericNavigation.ValidFloat("5.1", 0f, 5f, out reason) &&
                reason == "OUT_OF_RANGE", "float range rejected");
            test(ProfileNumericNavigation.IsOwnedControl("ZCN.XpCustomizer.Biotech.Awful.Aptitude"),
                "XP owned control recognized");
            test(!ProfileNumericNavigation.IsOwnedControl("SomeOtherMod.TextField"),
                "foreign focus not owned");
            test(ProfileNumericNavigation.IsActive("field", true, null),
                "last known focus requires validation");
            test(ProfileNumericNavigation.IsActive("field", false, "field"),
                "current focus requires validation");
            test(!ProfileNumericNavigation.IsActive("field", false, "other"),
                "unrelated editor not validated");
            test(BiotechSettingsUI.IsBiotechControl(
                "ZCN.XpCustomizer.Biotech.Awful.Aptitude"),
                "registered biotech name");
            test(!BiotechSettingsUI.IsBiotechControl(
                "ZCN.XpCustomizer.Biotech.Unknown"),
                "unknown biotech name fails closed");
            test(GwsSettingsUI.IsGwsControl("ZCN.XpCustomizer.GwsV2.HighAnchor"),
                "registered GWS name");
            test(!GwsSettingsUI.IsGwsControl("ZCN.XpCustomizer.GwsV2.Unknown"),
                "unknown GWS name fails closed");
            test(ConsciousnessAnchorV2SettingsUI.IsConsControl(
                "ZCN.XpCustomizer.ConsV2.Speed.LowAnchor"),
                "registered Cons name");
            test(ConsciousnessFurtherLearningSettingsUI.IsFurtherControl(
                "ZCN.XpCustomizer.FurtherV2.PreviewBase"),
                "registered Further preview name");
            test(!ProfileNumericFocus.ValidateNavigationDraft(
                "ZCN.XpCustomizer.ProfileFocus.MissingN1Probe", out reason) &&
                reason == "UNOBSERVED_NUMERIC_FOCUS", "unregistered numeric focus rejects");
            // R0 detached field-identification cases; no real input/reset/UI writes.
            NumericInputState total = new NumericInputState();
            total.FocusControlName = "ZCN.XpCustomizer.ProfileFocus.Numeric.R0Total";
            total.buffer = "";
            System.Collections.Generic.List<NumericInputState> weights =
                new System.Collections.Generic.List<NumericInputState>();
            NumericInputState weight = new NumericInputState();
            weight.FocusControlName = "ZCN.XpCustomizer.ProfileFocus.Numeric.R0Weight";
            weights.Add(weight);
            System.Collections.Generic.List<NumericInputState> locks =
                new System.Collections.Generic.List<NumericInputState>();
            NumericInputState locked = new NumericInputState();
            locked.FocusControlName = "ZCN.XpCustomizer.ProfileFocus.Numeric.R0Lock";
            locks.Add(locked);
            test(CareerProgressionSettingsUI.DiagnoseCareerNumericField(
                total, weights, locks, null) == "TOTAL_MULTIPLIER",
                "R0 empty total identified");
            test(CareerProgressionSettingsUI.DiagnoseCareerNumericField(
                total, weights, locks, weight.FocusControlName) == "STAGE_WEIGHT_0",
                "R0 rejected stage weight identified");
            test(CareerProgressionSettingsUI.DiagnoseCareerNumericField(
                total, weights, locks, locked.FocusControlName) == "STAGE_LOCK_0",
                "R0 rejected stage lock identified");
            test(CareerProgressionSettingsUI.DiagnoseCareerNumericField(
                total, weights, locks, "ForeignControl") == "TOTAL_MULTIPLIER",
                "R0 foreign focus never used as XP identity");
            test(CareerProgressionSettingsUI.DiagnoseCareerNumericField(
                total, weights, locks,
                "ZCN.XpCustomizer.ProfileFocus.Numeric.Unknown") ==
                "REJECTED_CONTROL_NOT_IN_CAREER",
                "R0 unknown owned identity not falsely attributed");
            weight.buffer = "-";
            test(CareerProgressionSettingsUI.DiagnoseCareerNumericField(
                total, weights, locks, null) == "MULTIPLE_CAREER_PENDING",
                "R0 multiple pending not misattributed");
            total.buffer = null;
            weight.buffer = null;
            test(CareerProgressionSettingsUI.DiagnoseCareerNumericField(
                total, weights, locks, null) == "UNRESOLVED",
                "R0 no pending reports unresolved");
            test(SettingsProfileNavigationController.NumericRecoveryDiagnosticVersion ==
                "XPC-N1-EDIT-RECOVERY-R0-20261112-R1",
                "R0 build identity");
            test(total.buffer == null && weight.buffer == null && locked.buffer == null,
                "R0 diagnostics preserve detached buffers");
            Log.Message("[XPC-N1][PROFILE_NUMERIC_NAV_SELFTEST] status=PASS; checks=" +
                checks + "; scope=PURE_NO_GAME_GUI; ownerValidation=PASS; " +
                "invalidDraft=BLOCKED; unknownFocus=BLOCKED; profileJsonWrites=0; " +
                "configXmlWrites=0; geneDefWrites=0; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
