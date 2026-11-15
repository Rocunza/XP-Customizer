#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using XpCustomizer.Profiles;
using XpCustomizer.UI;

namespace XpCustomizer
{
    /// <summary>
    /// Detached S1 registry/policy checks; no live input, GUI, save, XML or
    /// profile action. The real per-owner scan is additionally read-only.
    /// </summary>
    internal static class NumericRecoveryR2S1SelfTests
    {
        internal static void Run()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool condition, string label)
            {
                checks++;
                if (!condition) throw new InvalidOperationException("N1-R2-S1 " + label);
            };

            Type[] known = ProfileEditorReadinessService.CopyKnownSourceTypes();
            NumericDraftRecoveryOwner[] entries = NumericDraftRecoveryRegistry.CopyOwners();
            check(known.Length == 15 && entries.Length == 15, "15 explicit owners");
            HashSet<Type> unique = new HashSet<Type>();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < known.Length; i++)
            {
                check(entries[i].OwnerType == known[i], "registry order " + i);
                check(unique.Add(entries[i].OwnerType) && ids.Add(entries[i].OwnerId),
                    "unique type and ID " + i);
                check(entries[i].OwnerId == entries[i].OwnerType.Name,
                    "stable named source " + i);
            }
            check(entries[0].Family == NumericDraftRecoveryFamily.ExistingCareerTotalOnly,
                "only career total has previous R1 path");
            for (int i = 1; i < entries.Length; i++)
                check(entries[i].Family != NumericDraftRecoveryFamily.ExistingCareerTotalOnly,
                    "other owners not promoted " + i);
            check(SettingsNumericRecoveryController.Version ==
                "XPC-N1-R2R3-S1-20261113-R1", "version tag");
            check(SettingsNumericRecoveryController.SafeStaticFieldLabel(
                "SkilltrainerSettingsUI.multiplierInput") ==
                "SkilltrainerSettingsUI.multiplierInput", "static field label");
            check(SettingsNumericRecoveryController.SafeStaticFieldLabel(
                "SkillSpecificXpSettingsUI.inputs[Foreign.Text]") ==
                "UNRESOLVED_OR_DYNAMIC", "dynamic key not logged");
            check(SettingsNumericRecoveryController.SafeStaticFieldLabel(null) ==
                "UNRESOLVED_OR_DYNAMIC", "null field not logged");

            ProfileEditorOwnerObservation[] observations =
                new ProfileEditorOwnerObservation[known.Length];
            for (int i = 0; i < known.Length; i++)
                observations[i] = new ProfileEditorOwnerObservation(
                    known[i], 0, 0, 1, null, null, true);
            check(NumericDraftRecoveryRegistry.Assess(observations).Code ==
                NumericDraftRecoveryObservationCode.AllReady, "all ready");

            int trainer = Array.IndexOf(known, typeof(SkilltrainerSettingsUI));
            int gws = Array.IndexOf(known, typeof(GwsSettingsUI));
            check(trainer >= 0 && gws >= 0, "distinct Skilltrainer/GWS owners");
            string owned = "ZCN.XpCustomizer.ProfileFocus.Numeric.S1Test";
            observations[trainer] = new ProfileEditorOwnerObservation(
                known[trainer], 1, 0, 1,
                "SkilltrainerSettingsUI.multiplierInput", owned, true);
            NumericDraftRecoveryObservation result =
                NumericDraftRecoveryRegistry.Assess(observations);
            check(result.Code == NumericDraftRecoveryObservationCode.OneOwnerPending &&
                result.OwnerId == "SkilltrainerSettingsUI" &&
                result.FieldId == "SkilltrainerSettingsUI.multiplierInput" &&
                result.IdentifiedOwnedNumericControl,
                "single precise owned candidate, diagnostic only");
            observations[gws] = new ProfileEditorOwnerObservation(
                known[gws], 1, 0, 1, "GwsSettingsUI.anchor", null, true);
            result = NumericDraftRecoveryRegistry.Assess(observations);
            check(result.Code == NumericDraftRecoveryObservationCode.MultiplePending &&
                result.OwnerId == "NONE" && result.FieldId == null &&
                result.PendingFields == 2, "multiple owners not misattributed");
            observations[gws] = new ProfileEditorOwnerObservation(
                known[gws], 0, 1, 1, null, null, true);
            result = NumericDraftRecoveryRegistry.Assess(observations);
            check(result.Code == NumericDraftRecoveryObservationCode.Uncertain &&
                result.OwnerId == "NONE", "uncertain beats single pending");
            observations[gws] = new ProfileEditorOwnerObservation(
                known[gws], 0, 0, 0, null, null, false);
            check(NumericDraftRecoveryRegistry.Assess(observations).Code ==
                NumericDraftRecoveryObservationCode.Uncertain,
                "reflection failure fails closed");
            observations[gws] = new ProfileEditorOwnerObservation(
                known[gws], 0, 0, 1, null, null, true);
            observations[trainer] = new ProfileEditorOwnerObservation(
                known[trainer], 2, 0, 2, "stale ambiguous", owned, true);
            result = NumericDraftRecoveryRegistry.Assess(observations);
            check(result.Code == NumericDraftRecoveryObservationCode.MultiplePending &&
                result.OwnerId == "NONE", "two fields same owner never single");
            observations[trainer] = new ProfileEditorOwnerObservation(
                known[trainer], 1, 0, 1, "malformed", "OtherMod.Numeric", true);
            result = NumericDraftRecoveryRegistry.Assess(observations);
            check(result.Code == NumericDraftRecoveryObservationCode.OneOwnerPending &&
                !result.IdentifiedOwnedNumericControl, "foreign control not identified");
            observations[trainer] = new ProfileEditorOwnerObservation(
                known[trainer], 0, 0, 1, null, null, true);
            check(NumericDraftRecoveryRegistry.Assess(null).Code ==
                NumericDraftRecoveryObservationCode.InvalidRegistry,
                "null inventory fails closed");
            check(NumericDraftRecoveryRegistry.Assess(
                new ProfileEditorOwnerObservation[0]).Code ==
                NumericDraftRecoveryObservationCode.InvalidRegistry,
                "missing owner fails closed");
            observations[gws] = observations[trainer];
            check(NumericDraftRecoveryRegistry.Assess(observations).Code ==
                NumericDraftRecoveryObservationCode.InvalidRegistry,
                "duplicate owner fails closed");
            observations[gws] = new ProfileEditorOwnerObservation(
                typeof(string), 0, 0, 1, null, null, true);
            check(NumericDraftRecoveryRegistry.Assess(observations).Code ==
                NumericDraftRecoveryObservationCode.InvalidRegistry,
                "foreign owner fails closed");

            ProfileEditorOwnerObservation[] live =
                ProfileEditorReadinessService.InspectOwnersReadOnly();
            check(live.Length == known.Length, "live probe covers same owner count");
            for (int i = 0; i < live.Length; i++)
                check(live[i] != null && live[i].OwnerType == known[i],
                    "read-only scan identity " + i);
            check(NumericDraftRecoveryRegistry.OwnerCount ==
                ProfileEditorReadinessService.KnownSourceCount,
                "readiness contract coverage unchanged");
            check(NumericDraftRecoveryRegistry.Version ==
                SettingsNumericRecoveryController.Version,
                "log identifier matches registry version");

            Log.Message("[XPC-N1-R2R3][S1_SELFTEST] version=" +
                NumericDraftRecoveryRegistry.Version + "; status=PASS; checks=" +
                checks + "; owners=15; multiplePending=FAIL_CLOSED" +
                "; uncertain=FAIL_CLOSED; ownedControl=READ_ONLY" +
                "; automaticDiscard=DISABLED; navigationGate=UNCHANGED" +
                "; settingsWrites=0; profileJsonWrites=0; geneDefWrites=0" +
                "; scribeCalls=0; liveIMGUI=NOT_RUN; build=" +
                XpCustomizerMod.BuildId);
        }
    }
}
#endif
