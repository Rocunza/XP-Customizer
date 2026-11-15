using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;
using XpCustomizer.BookXp;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.Decay;

namespace XpCustomizer
{
    /// <summary>
    /// R3-A: detached, schema-checked numerical defaults. Does not call
    /// Scribe, mutate source settings, change any enable/choice/lock state,
    /// read/write profile JSON, or interact with GeneDefs/UI.
    /// Hard/Progressive values are deliberately never referenced.
    /// </summary>
    internal static class GlobalNumericDefaultsService
    {
        internal const string Revision = "R3A-108FIELDS-36NESTED-20261108";
        private const BindingFlags Fields = BindingFlags.Instance |
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // Explicit whitelist. A new settings field requires an intentional
        // contract review, never automatic reflection-based mass resetting.
        private static readonly string[] NumericFieldNames = {
            "careerXpMultiplier",
            "level20ReserveMultiplier",
            "skilltrainerXpMultiplier",
            "bookIntellectualXpPerHour",
            "gwsLowAnchor",
            "gwsHighAnchor",
            "gwsLowFactor",
            "gwsHighFactor",
            "gwsLowCurvature",
            "gwsHighCurvature",
            "gwsHighDiminishingLimit",
            "gwsHighTechnicalLimit",
            "learningCapacityBaseDailyCap",
            "learningCapacitySourceStrength",
            "learningCapacityCustomSlowLearner",
            "learningCapacityCustomFastLearner",
            "learningCapacityCustomTooSmart",
            "learningCapacityCustomBlissLobotomy",
            "learningCapacityCustomSlowStudy",
            "learningCapacityCustomQuickStudy",
            "learningCapacityCustomNeuralSupercharge",
            "learningCapacityCustomRanchingCentral",
            "learningCapacityCustomLearningAssistant",
            "furtherLearningBaseFactor",
            "furtherLearningMentalThresholdStrength",
            "biotechAwfulAptitude",
            "biotechPoorAptitude",
            "biotechStrongAptitude",
            "biotechGreatAptitude",
            "biotechAwfulComplexity",
            "biotechPoorComplexity",
            "biotechStrongComplexity",
            "biotechGreatComplexity",
            "biotechAwfulMetabolism",
            "biotechPoorMetabolism",
            "biotechStrongMetabolism",
            "biotechGreatMetabolism",
            "greatMemoryDecayReduction",
            "perfectMemoryDecayReduction",
        };
        private static readonly string[] NestedNames = {
            "consciousnessFurtherV2", "consciousnessAnchorSpeed", "consciousnessAnchorCapacity"
        };
        private static readonly string[] KnownFloatMaps = {
            "skillSpecificXpMultipliers", "skilltrainerSpecificMultipliers",
            "bookQualityMultipliers", "bookSkillMultipliers", "decaySkillMultipliers"
        };

        internal static bool TryBuild(XpCustomizerSettings live,
            IEnumerable<string> loadedSkills, out SettingsSnapshot snapshot,
            out string reason)
        {
            snapshot = null;
            reason = "NONE";
            if (live == null || loadedSkills == null)
            { reason = "SETTINGS_OR_SKILLDEFS_UNAVAILABLE"; return false; }
            try
            {
                if (NumericFieldNames.Length != 39 || NestedNames.Length != 3 ||
                    KnownFloatMaps.Length != 5)
                { reason = "RESET_CONTRACT_COUNT_MISMATCH"; return false; }
                FieldInfo[] fields = SettingsSnapshotService.GetDeclaredSettingsFieldsForAudit();
                if (fields.Length != SettingsCandidateValidator.ExpectedSettingsFieldCount ||
                    SettingsCandidateValidator.ComputeFieldFingerprint(fields) !=
                    SettingsCandidateValidator.ExpectedFieldNameFingerprint)
                { reason = "SETTINGS_SCHEMA_DRIFT"; return false; }
                XpCustomizerSettings candidate = SettingsSnapshotService.CloneIndependent(live);
                XpCustomizerSettings defaults = new XpCustomizerSettings();
                Type settingsType = typeof(XpCustomizerSettings);
                HashSet<string> allowed = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < NumericFieldNames.Length; i++)
                {
                    string name = NumericFieldNames[i];
                    if (!allowed.Add(name)) throw new InvalidOperationException("DUPLICATE_NUMERIC_FIELD");
                    FieldInfo field = settingsType.GetField(name, Fields);
                    if (field == null || (field.FieldType != typeof(float) &&
                                          field.FieldType != typeof(int)))
                        throw new InvalidOperationException("UNSUPPORTED_NUMERIC_FIELD:" + name);
                    field.SetValue(candidate, field.GetValue(defaults));
                }
                // Decay is an explicit 21-element reference table, NOT a
                // topology selection (unlike Career stage boundaries).
                if (candidate.decayDailyByLevel == null ||
                    SkillDecayService.DefaultValues().Count != 21)
                { reason = "DECAY_SCHEMA_INVALID"; return false; }
                candidate.decayDailyByLevel = SkillDecayService.DefaultValues();
                allowed.Add("decayDailyByLevel");

                int stageCount = candidate.careerXpSplitLevels == null ? -1 :
                    candidate.careerXpSplitLevels.Count + 1;
                if (!ValidCareerLocks(candidate, stageCount, out reason)) return false;
                candidate.careerXpStageWeights = new List<float>(stageCount);
                for (int i = 0; i < stageCount; i++)
                    candidate.careerXpStageWeights.Add(XpCustomizerDefaults.CareerXpStageWeight);
                allowed.Add("careerXpStageWeights");

                HashSet<string> skillNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (string skill in loadedSkills)
                    if (!String.IsNullOrWhiteSpace(skill)) skillNames.Add(skill);
                if (skillNames.Count == 0)
                { reason = "LOADED_SKILLDEFS_EMPTY"; return false; }
                HashSet<string> bookSkillNames = new HashSet<string>(
                    BookXpService.VanillaSkillNames, StringComparer.Ordinal);
                HashSet<string> qualityNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (QualityCategory quality in Enum.GetValues(typeof(QualityCategory)))
                    qualityNames.Add(BookXpService.QualityKey(quality));

                ResetKnown(candidate.skillSpecificXpMultipliers, skillNames);
                ResetKnown(candidate.skilltrainerSpecificMultipliers, skillNames);
                ResetKnown(candidate.decaySkillMultipliers, skillNames);
                ResetKnown(candidate.bookSkillMultipliers, bookSkillNames);
                ResetKnown(candidate.bookQualityMultipliers, qualityNames);
                foreach (string key in KnownFloatMaps) allowed.Add(key);

                for (int i = 0; i < NestedNames.Length; i++)
                {
                    string name = NestedNames[i];
                    FieldInfo field = settingsType.GetField(name, Fields);
                    if (field == null) throw new InvalidOperationException("NESTED_FIELD_MISSING:" + name);
                    object current = field.GetValue(candidate);
                    object originalDefault = field.GetValue(defaults);
                    if (current == null || originalDefault == null ||
                        current.GetType() != originalDefault.GetType())
                    { reason = "NESTED_PROFILE_MISSING:" + name; return false; }
                    CopyNumericProfileFields(current, originalDefault);
                    allowed.Add(name);
                }
                if (allowed.Count != 49)
                { reason = "RESET_ALLOWLIST_DRIFT"; return false; }

                // Independent, exhaustive audit: 108 top-level fields and 36
                // nested fields; every non-whitelisted field must be identical.
                if (!PreservedContract(live, candidate, fields, allowed, out reason))
                    return false;
                SettingsCandidateValidator.Result validated =
                    SettingsCandidateValidator.Validate(candidate);
                if (!validated.Accepted)
                { reason = "CANDIDATE_" + validated.Reason; return false; }
                snapshot = SettingsSnapshotService.Capture(candidate);
                reason = "PASS";
                return true;
            }
            catch (Exception ex)
            {
                reason = "PREPARE_EXCEPTION:" + ex.GetType().Name + ":" + ex.Message;
                return false;
            }
        }

        private static void ResetKnown(Dictionary<string, float> values,
            HashSet<string> recognized)
        {
            if (values == null) throw new InvalidOperationException("NULL_NUMERIC_MAP");
            List<string> toRemove = new List<string>();
            foreach (KeyValuePair<string, float> item in values)
            {
                // Interpret a known key using this saved map's own comparer;
                // an OrdinalIgnoreCase map may store "shooting" for Shooting.
                foreach (string known in recognized)
                    if (values.Comparer.Equals(item.Key, known))
                    { toRemove.Add(item.Key); break; }
            }
            // Missing keys have neutral x1 semantics. Preserve comparer,
            // unknown modded keys, and exact capitalization of unknown keys.
            for (int i = 0; i < toRemove.Count; i++) values.Remove(toRemove[i]);
        }

        private static bool ValidCareerLocks(XpCustomizerSettings s,
            int stageCount, out string reason)
        {
            reason = "NONE";
            if (stageCount < 1 || stageCount > 20 ||
                s.careerXpStageWeights == null ||
                s.careerXpStageWeights.Count != stageCount ||
                s.careerXpStageLockedShares == null ||
                s.careerXpStageLockedShares.Count != stageCount)
            { reason = "CAREER_STAGE_SHAPE_MISMATCH"; return false; }
            double totalLocked = 0;
            int free = 0;
            for (int i = 0; i < stageCount; i++)
            {
                float locked = s.careerXpStageLockedShares[i];
                if (locked == -1f) { free++; continue; }
                if (float.IsNaN(locked) || float.IsInfinity(locked) ||
                    locked < 0f || locked > 1f)
                { reason = "CAREER_INVALID_LOCK"; return false; }
                totalLocked += locked;
            }
            if (free < 1 || totalLocked >= 1.0)
            { reason = "CAREER_NO_RESIDUAL_STAGE"; return false; }
            return true;
        }

        private static void CopyNumericProfileFields(object target, object defaults)
        {
            FieldInfo[] profileFields = target.GetType().GetFields(Fields);
            if (profileFields.Length != 12) throw new InvalidOperationException("PROFILE_SCHEMA_DRIFT");
            int numeric = 0, preserved = 0;
            foreach (FieldInfo field in profileFields)
            {
                if (field.FieldType == typeof(float))
                { field.SetValue(target, field.GetValue(defaults)); numeric++; }
                else if (field.FieldType == typeof(bool) || field.FieldType == typeof(int))
                    preserved++;
                else throw new InvalidOperationException("UNSUPPORTED_PROFILE_FIELD");
            }
            if (numeric != 8 || preserved != 4)
                throw new InvalidOperationException("NESTED_PROFILE_CONTRACT_DRIFT");
        }

        private static bool PreservedContract(XpCustomizerSettings before,
            XpCustomizerSettings after, FieldInfo[] fields,
            HashSet<string> permitted, out string reason)
        {
            reason = "NONE";
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                if (permitted.Contains(f.Name)) continue;
                if (!SameValue(f.GetValue(before), f.GetValue(after)))
                { reason = "PRESERVED_FIELD_CHANGED:" + f.Name; return false; }
            }
            // Nested enabled/curve selection switches can NEVER be reset.
            foreach (string name in NestedNames)
            {
                FieldInfo f = typeof(XpCustomizerSettings).GetField(name, Fields);
                object x = f.GetValue(before), y = f.GetValue(after);
                if (x == null || y == null)
                { reason = "NULL_NESTED:" + name; return false; }
                foreach (FieldInfo sub in x.GetType().GetFields(Fields))
                {
                    if (sub.FieldType == typeof(float)) continue;
                    if (!Equals(sub.GetValue(x), sub.GetValue(y)))
                    { reason = "NESTED_SELECTION_CHANGED:" + name + "." + sub.Name; return false; }
                }
            }
            return true;
        }

        private static bool SameValue(object x, object y)
        {
            if (x == null || y == null) return x == y;
            if (x is List<int>)
            {
                List<int> a = (List<int>)x, b = y as List<int>;
                if (b == null || a.Count != b.Count) return false;
                for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
                return true;
            }
            if (x is List<float>)
            {
                List<float> a = (List<float>)x, b = y as List<float>;
                if (b == null || a.Count != b.Count) return false;
                for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
                return true;
            }
            if (x is Dictionary<string, bool>)
            {
                Dictionary<string, bool> a = (Dictionary<string, bool>)x;
                Dictionary<string, bool> b = y as Dictionary<string, bool>;
                if (b == null || a.Count != b.Count ||
                    !Equals(a.Comparer, b.Comparer)) return false;
                foreach (KeyValuePair<string, bool> item in a)
                {
                    bool value;
                    if (!b.TryGetValue(item.Key, out value) || value != item.Value) return false;
                }
                return true;
            }
            return Equals(x, y);
        }
    }
}

namespace XpCustomizer
{
    internal sealed class GlobalNumericDefaultsException : System.Exception
    {
        internal readonly string Reason;
        internal readonly bool RequiresRestart;
        internal GlobalNumericDefaultsException(string reason, bool requiresRestart)
            : base(reason)
        {
            Reason = reason ?? "UNKNOWN";
            RequiresRestart = requiresRestart;
        }
    }
}
