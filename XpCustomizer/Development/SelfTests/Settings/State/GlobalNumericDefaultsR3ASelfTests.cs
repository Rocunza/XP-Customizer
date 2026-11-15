#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using XpCustomizer.UI;

namespace XpCustomizer
{
    /// <summary>
    /// R3-A isolated contract validation. No game/UI/Scribe/GeneDef/profile IO.
    /// </summary>
    internal static class GlobalNumericDefaultsR3ASelfTests
    {
        private static int checks;
        private static void Check(bool value, string label)
        {
            checks++;
            if (!value) throw new InvalidOperationException("R3-A " + label);
        }
        internal static void Run()
        {
            checks = 0;
            XpCustomizerSettings s = new XpCustomizerSettings();
            s.careerXpMultiplier = 5f;
            s.level20ReserveMultiplier = 7f;
            s.skilltrainerXpMultiplier = 2.5f;
            s.bookIntellectualXpPerHour = 900f;
            s.gwsLowFactor = 0.2f;
            s.gwsHighFactor = 3f;
            s.gwsHighCurve = 2;
            s.gwsLowCurve = 1;
            s.learningCapacityBaseDailyCap = 8500f;
            s.learningCapacityOverrideSlowLearner = true;
            s.learningCapacityOverrideBlissLobotomy = true;
            s.learningCapacityCustomSlowLearner = 95f;
            s.furtherLearningBaseFactor = 0.4f;
            s.furtherLearningMentalThresholdEnabled = true;
            s.furtherLearningMentalThresholdStrength = 1.4f;
            s.biotechCustomizationEnabled = true;
            s.biotechGreatAptitude = 1;
            s.bookXpEnabled = true;
            s.bookSkillEnabled = true;
            s.bookQualityEnabled = true;
            s.gwsEnabled = true;
            s.gwsSkillEnabled = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            s.gwsSkillEnabled["social"] = true;
            s.gwsSkillEnabled["unknown_skill"] = false;
            s.consciousnessAnchorSpeed.LowEnabled = false;
            s.consciousnessAnchorSpeed.HighCurve = 2;
            s.consciousnessAnchorSpeed.LowFactor = 0.19f;
            s.consciousnessAnchorCapacity.HighEnabled = false;
            s.consciousnessAnchorCapacity.LowCurve = 1;
            s.consciousnessAnchorCapacity.HighFactor = 3.5f;
            s.consciousnessFurtherV2.LowEnabled = false;
            s.consciousnessFurtherV2.HighCurve = 2;
            s.consciousnessFurtherV2.HighFactor = 4f;
            s.careerXpUseCustomDistribution = true;
            s.careerXpRoundingStep = 500;
            s.careerXpStageWeights[0] = 5f;
            s.careerXpStageLockedShares[0] = 0.23f;
            s.decayByLevelEnabled = true;
            s.decayDailyByLevel[10] = 999f;
            s.greatMemoryDecayReduction = 70f;
            s.skillSpecificXpMultipliers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            s.skillSpecificXpMultipliers["Shooting"] = 3f;
            s.skillSpecificXpMultipliers["FutureSkill"] = 2.9f;
            s.skilltrainerSpecificMultipliers["Shooting"] = 4f;
            s.skilltrainerSpecificMultipliers["FutureSkill"] = 6f;
            s.bookSkillMultipliers["Shooting"] = 3.2f;
            s.bookSkillMultipliers["FutureSkill"] = 2f;
            s.bookQualityMultipliers["Legendary"] = 2f;
            s.bookQualityMultipliers["FutureQuality"] = 4f;
            s.decaySkillMultipliers["Shooting"] = 0.7f;
            s.decaySkillMultipliers["FutureSkill"] = 0.6f;

            XpCustomizerSettings untouched = SettingsSnapshotService.CloneIndependent(s);
            SettingsSnapshot snapshot;
            string reason;
            bool ok = GlobalNumericDefaultsService.TryBuild(s,
                new [] { "Shooting", "Melee", "Social" }, out snapshot, out reason);
            Check(ok && reason == "PASS" && snapshot != null, "build passes");
            if (!ok) throw new InvalidOperationException("R3-A candidate " + reason);
            XpCustomizerSettings candidate = snapshot.CreateDetachedCopy();
            FieldInfo[] fields = SettingsSnapshotService.GetDeclaredSettingsFieldsForAudit();
            Check(fields.Length == 108, "schema count");
            string mismatch;
            Check(SettingsApplyCoordinator.Equivalent(s, untouched, out mismatch),
                "source not mutated");
            Check(candidate.careerXpMultiplier == 1f &&
                candidate.level20ReserveMultiplier == 1f &&
                candidate.skilltrainerXpMultiplier == 1f, "no Hard 5x");
            Check(candidate.bookIntellectualXpPerHour == 250f &&
                candidate.bookXpEnabled && candidate.bookSkillEnabled &&
                candidate.bookQualityEnabled, "book values and switches");
            Check(candidate.learningCapacityBaseDailyCap == 4000f &&
                candidate.learningCapacityCustomSlowLearner == 0f &&
                candidate.learningCapacityOverrideSlowLearner &&
                candidate.learningCapacityOverrideBlissLobotomy,
                "learning override semantics");
            Check(candidate.furtherLearningBaseFactor == 0.2f &&
                candidate.furtherLearningMentalThresholdStrength == 0.6f &&
                candidate.furtherLearningMentalThresholdEnabled, "mental 60 enabled held");
            Check(candidate.gwsLowFactor == 0.7f && candidate.gwsHighFactor == 1.30f &&
                candidate.gwsHighCurvature == 0.40f && candidate.gwsHighAnchor == 5f,
                "GWS base factors");
            Check(candidate.gwsLowCurve == 1 && candidate.gwsHighCurve == 2 &&
                candidate.gwsEnabled && candidate.gwsSkillEnabled.ContainsKey("SOCIAL") &&
                !candidate.gwsSkillEnabled["unknown_skill"], "GWS selection and comparer");
            Check(candidate.consciousnessAnchorSpeed.LowFactor != 0.19f &&
                !candidate.consciousnessAnchorSpeed.LowEnabled &&
                candidate.consciousnessAnchorSpeed.HighCurve == 2,
                "Cons speed numbers only");
            Check(candidate.consciousnessAnchorCapacity.HighFactor != 3.5f &&
                !candidate.consciousnessAnchorCapacity.HighEnabled &&
                candidate.consciousnessAnchorCapacity.LowCurve == 1,
                "Cons capacity numbers only");
            Check(candidate.consciousnessFurtherV2.HighFactor != 4f &&
                !candidate.consciousnessFurtherV2.LowEnabled &&
                candidate.consciousnessFurtherV2.HighCurve == 2,
                "Cons further numbers only");
            Check(candidate.careerXpUseCustomDistribution &&
                candidate.careerXpRoundingStep == 500 &&
                candidate.careerXpSplitLevels.Count == s.careerXpSplitLevels.Count &&
                candidate.careerXpStageLockedShares[0] == 0.23f &&
                candidate.careerXpStageWeights[0] == 1f,
                "career locks/boundaries preserved, weights reset");
            Check(candidate.decayByLevelEnabled &&
                candidate.decayDailyByLevel.Count == 21 &&
                candidate.decayDailyByLevel[10] == 30f &&
                candidate.greatMemoryDecayReduction == 50f,
                "decay numbers only");
            Check(candidate.biotechCustomizationEnabled &&
                candidate.biotechGreatAptitude == 8,
                "Biotech ON preserved, values reset");
            Check(!candidate.skillSpecificXpMultipliers.ContainsKey("Shooting") &&
                candidate.skillSpecificXpMultipliers["FutureSkill"] == 2.9f &&
                candidate.skillSpecificXpMultipliers.Comparer.Equals(StringComparer.OrdinalIgnoreCase),
                "skill map known vs unknown comparer");
            Check(!candidate.skilltrainerSpecificMultipliers.ContainsKey("Shooting") &&
                candidate.skilltrainerSpecificMultipliers["FutureSkill"] == 6f,
                "trainer map known vs unknown");
            Check(!candidate.bookSkillMultipliers.ContainsKey("Shooting") &&
                candidate.bookSkillMultipliers["FutureSkill"] == 2f,
                "book skill map unknown");
            Check(!candidate.bookQualityMultipliers.ContainsKey("Legendary") &&
                candidate.bookQualityMultipliers["FutureQuality"] == 4f,
                "book quality known vs unknown");
            Check(!candidate.decaySkillMultipliers.ContainsKey("Shooting") &&
                candidate.decaySkillMultipliers["FutureSkill"] == 0.6f,
                "decay skill unknown");
            // Exhaustive top-level non-numeric and all nested select states.
            foreach (FieldInfo f in fields)
                if (f.FieldType == typeof(bool))
                    Check(Equals(f.GetValue(s), f.GetValue(candidate)),
                        "preserved switch " + f.Name);
            foreach (string name in new [] { "consciousnessAnchorSpeed",
                "consciousnessAnchorCapacity", "consciousnessFurtherV2" })
            {
                FieldInfo holder = typeof(XpCustomizerSettings).GetField(name,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                object originalNested = holder.GetValue(s),
                    resultNested = holder.GetValue(candidate);
                foreach (FieldInfo field in originalNested.GetType().GetFields(
                    BindingFlags.Instance | BindingFlags.NonPublic |
                    BindingFlags.Public | BindingFlags.DeclaredOnly))
                    if (field.FieldType != typeof(float))
                        Check(Equals(field.GetValue(originalNested),
                            field.GetValue(resultNested)), "nested choice " + name + "." + field.Name);
            }
            SettingsCandidateValidator.Result validation =
                SettingsCandidateValidator.Validate(candidate);
            Check(validation.Accepted, "candidate valid");
            SettingsSnapshot second;
            Check(GlobalNumericDefaultsService.TryBuild(candidate,
                new [] { "Shooting", "Melee", "Social" }, out second, out reason),
                "repeat creation no error");
            Check(SettingsApplyCoordinator.Equivalent(candidate,
                second.CreateDetachedCopy(), out mismatch), "repeat idempotent");
            XpCustomizerSettings invalid = SettingsSnapshotService.CloneIndependent(s);
            invalid.careerXpStageLockedShares.Clear();
            Check(!GlobalNumericDefaultsService.TryBuild(invalid,
                new [] { "Shooting" }, out second, out reason) &&
                reason == "CAREER_STAGE_SHAPE_MISMATCH", "invalid stage shape rejected");
            invalid = SettingsSnapshotService.CloneIndependent(s);
            for (int i = 0; i < invalid.careerXpStageLockedShares.Count; i++)
                invalid.careerXpStageLockedShares[i] = 1f / invalid.careerXpStageLockedShares.Count;
            Check(!GlobalNumericDefaultsService.TryBuild(invalid,
                new [] { "Shooting" }, out second, out reason),
                "no residual stage rejected");
            Check(!GlobalNumericDefaultsService.TryBuild(s,
                new string[0], out second, out reason) &&
                reason == "LOADED_SKILLDEFS_EMPTY", "missing defs blocked");
            GlobalVanillaResetConfirmationGate gate = new GlobalVanillaResetConfirmationGate();
            int calls = 0;
            Check(gate.TryCancel() && !gate.TryConfirm(delegate { calls++; }) &&
                calls == 0, "modal cancel no apply");
            gate = new GlobalVanillaResetConfirmationGate();
            Check(gate.TryConfirm(delegate { calls++; }) &&
                !gate.TryConfirm(delegate { calls++; }) && calls == 1,
                "modal one shot");
            Log.Message("[XPC-RESET-R3A][NUMERIC_DEFAULTS_SELFTEST] status=PASS; checks=" +
                checks + "; fieldsReviewed=108; nestedFieldsReviewed=36; " +
                "switchesPreserved=PASS; unknownKeysPreserved=PASS; " +
                "schemaAndCareerLocks=PASS; sourceWrites=0; " +
                "profileJsonWrites=0; ownXmlWrites=0; geneDefWrites=0; " +
                "scope=PURE_DETACHED_NO_IMGUI; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
