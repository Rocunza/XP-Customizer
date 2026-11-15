using System;
using System.Collections.Generic;
using System.Reflection;
using XpCustomizer.ConsciousnessLearning;

namespace XpCustomizer
{
    /// <summary>
    /// R6-P0-C: non-mutating validation of a detached settings candidate.
    /// This is NOT Scribe migration and never normalizes user values.
    /// Legacy/inactive values are preserved, including intentionally empty
    /// career split lists and unknown (but nonblank) SkillDef names.
    /// </summary>
    internal static class SettingsCandidateValidator
    {
        internal const string Revision = "R6P0C-VALIDATOR-20261106";
        internal const int ExpectedSettingsFieldCount = 108;
        internal const uint ExpectedFieldNameFingerprint = 0x32E32440u;

        internal sealed class Result
        {
            internal readonly bool Accepted;
            internal readonly string Reason;

            internal Result(bool accepted, string reason)
            {
                Accepted = accepted;
                Reason = reason;
            }
        }

        internal static Result Validate(XpCustomizerSettings candidate)
        {
            if (candidate == null)
                return Reject("NULL_CANDIDATE");

            FieldInfo[] fields = SettingsSnapshotService.GetDeclaredSettingsFieldsForAudit();
            if (fields.Length != ExpectedSettingsFieldCount ||
                ComputeFieldFingerprint(fields) != ExpectedFieldNameFingerprint)
                return Reject("SETTINGS_SCHEMA_DRIFT");

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                string name = field.Name;
                Type type = field.FieldType;
                object value;
                try { value = field.GetValue(candidate); }
                catch (Exception) { return Reject("READ_FAILED:" + name); }

                if (type == typeof(float))
                {
                    if (!Finite((float)value)) return Reject("NONFINITE:" + name);
                }
                else if (type == typeof(bool)) { }
                else if (type == typeof(int))
                {
                    int integer = (int)value;
                    if ((name == "gwsLowCurve" || name == "gwsHighCurve") &&
                        (integer < 0 || integer > 2))
                        return Reject("UNSUPPORTED_CURVE:" + name);
                    int maxVersion;
                    if (TryGetSupportedVersion(name, out maxVersion) &&
                        (integer < 0 || integer > maxVersion))
                        return Reject("UNSUPPORTED_VERSION:" + name);
                }
                else if (type == typeof(Dictionary<string, float>))
                {
                    Dictionary<string, float> values = value as Dictionary<string, float>;
                    if (values == null) return Reject("NULL_COLLECTION:" + name);
                    foreach (KeyValuePair<string, float> item in values)
                    {
                        if (string.IsNullOrWhiteSpace(item.Key))
                            return Reject("INVALID_KEY:" + name);
                        if (!Finite(item.Value))
                            return Reject("NONFINITE_COLLECTION_VALUE:" + name);
                    }
                }
                else if (type == typeof(Dictionary<string, bool>))
                {
                    Dictionary<string, bool> values = value as Dictionary<string, bool>;
                    if (values == null) return Reject("NULL_COLLECTION:" + name);
                    foreach (KeyValuePair<string, bool> item in values)
                        if (string.IsNullOrWhiteSpace(item.Key))
                            return Reject("INVALID_KEY:" + name);
                }
                else if (type == typeof(List<float>))
                {
                    List<float> values = value as List<float>;
                    if (values == null) return Reject("NULL_COLLECTION:" + name);
                    for (int j = 0; j < values.Count; j++)
                        if (!Finite(values[j]))
                            return Reject("NONFINITE_LIST_VALUE:" + name);
                }
                else if (type == typeof(List<int>))
                {
                    List<int> values = value as List<int>;
                    if (values == null) return Reject("NULL_COLLECTION:" + name);
                    if (name == "careerXpSplitLevels")
                    {
                        // Empty is an intentional single-stage configuration.
                        // Reject unsupported boundaries, not older preset list
                        // lengths: a preset may temporarily hold empty locks.
                        for (int j = 0; j < values.Count; j++)
                            if (values[j] < 1 || values[j] > 19 ||
                                (j > 0 && values[j] <= values[j - 1]))
                                return Reject("INVALID_SPLIT_LEVEL:" + name);
                    }
                }
                else if (type == typeof(ConsAnchorV2ProfileSettings) ||
                         type == typeof(ConsciousnessFurtherV2Settings))
                {
                    if (value == null) return Reject("NULL_PROFILE:" + name);
                    Result profile = ValidateProfile(value, name);
                    if (!profile.Accepted) return profile;
                }
                else
                    return Reject("UNSUPPORTED_FIELD_TYPE:" + name);
            }

            return new Result(true, "PASS");
        }

        private static Result ValidateProfile(object profile, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo[] fields = profile.GetType().GetFields(flags);
            if (fields.Length != 12)
                return Reject("PROFILE_SCHEMA_DRIFT:" + name);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                object value = field.GetValue(profile);
                if (field.FieldType == typeof(float))
                {
                    if (!Finite((float)value))
                        return Reject("NONFINITE_PROFILE_VALUE:" + name + "." + field.Name);
                }
                else if (field.FieldType == typeof(int))
                {
                    if (field.Name == "LowCurve" || field.Name == "HighCurve")
                    {
                        int mode = (int)value;
                        if (mode < 0 || mode > 2)
                            return Reject("UNSUPPORTED_PROFILE_CURVE:" + name + "." + field.Name);
                    }
                    else
                        return Reject("UNSUPPORTED_PROFILE_INT:" + name + "." + field.Name);
                }
                else if (field.FieldType != typeof(bool))
                    return Reject("UNSUPPORTED_PROFILE_TYPE:" + name + "." + field.Name);
            }
            return new Result(true, "PASS");
        }

        private static bool TryGetSupportedVersion(string fieldName, out int maximum)
        {
            switch (fieldName)
            {
                case "careerXpDistributionDataVersion": maximum = 2; return true;
                case "bookXpSettingsVersion": maximum = 1; return true;
                case "learningCapacityDataVersion": maximum = 4; return true;
                case "furtherLearningMentalSettingsVersion": maximum = 1; return true;
                case "furtherLearningTraitSettingsVersion": maximum = 1; return true;
                case "consciousnessAnchorSettingsVersion":
                    maximum = ConsciousnessAnchorV2SettingsStore.CurrentSchemaVersion; return true;
                case "consciousnessFurtherV2Version":
                    maximum = ConsciousnessFurtherV2SettingsStore.SchemaVersion; return true;
                default: maximum = 0; return false;
            }
        }

        internal static uint ComputeFieldFingerprint(FieldInfo[] fields)
        {
            string[] names = new string[fields.Length];
            for (int i = 0; i < fields.Length; i++) names[i] = fields[i].Name;
            Array.Sort(names, StringComparer.Ordinal);
            uint hash = 2166136261u;
            unchecked
            {
                for (int i = 0; i < names.Length; i++)
                {
                    string token = names[i] + ";";
                    for (int j = 0; j < token.Length; j++)
                        hash = (hash ^ token[j]) * 16777619u;
                }
            }
            return hash;
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static Result Reject(string reason)
        {
            return new Result(false, reason);
        }
    }
}
