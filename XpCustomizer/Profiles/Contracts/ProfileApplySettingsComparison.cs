using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace XpCustomizer.Profiles
{
    /// <summary>
    /// D1-FIX2: compare the v1 portable settings contract by values, not by
    /// dictionary comparer reference identity. The live session's baseline
    /// integrity check deliberately continues using the stricter coordinator.
    /// No mutations, I/O, or runtime apply operations.
    /// </summary>
    internal static class ProfileApplySettingsComparison
    {
        private const BindingFlags CurveFlags = BindingFlags.Instance |
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        internal static bool PortableEquivalent(XpCustomizerSettings left,
            XpCustomizerSettings right, out string firstMismatch)
        {
            firstMismatch = "NONE";
            if (left == null || right == null)
            { firstMismatch = "NULL_SETTINGS"; return false; }
            ProfileSchemaV1.VerifyContract();
            foreach (ProfileFieldSpec spec in ProfileSchemaV1.GetFields())
            {
                object a = spec.SettingField.GetValue(left);
                object b = spec.SettingField.GetValue(right);
                if (!SamePortableField(spec.FieldType, a, b))
                {
                    firstMismatch = spec.Section + "." + spec.Name;
                    return false;
                }
            }
            if (!SameCurve(left.consciousnessAnchorSpeed,
                right.consciousnessAnchorSpeed, "speedProfile", out firstMismatch)) return false;
            if (!SameCurve(left.consciousnessAnchorCapacity,
                right.consciousnessAnchorCapacity, "capacityProfile", out firstMismatch)) return false;
            if (!SameCurve(left.consciousnessFurtherV2,
                right.consciousnessFurtherV2, "furtherProfile", out firstMismatch)) return false;
            return true;
        }

        private static bool SamePortableField(Type type, object a, object b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (type == typeof(Dictionary<string, float>))
                return SameMap((Dictionary<string, float>)a, (Dictionary<string, float>)b);
            if (type == typeof(Dictionary<string, bool>))
                return SameMap((Dictionary<string, bool>)a, (Dictionary<string, bool>)b);
            if (type == typeof(List<float>))
                return SameList((List<float>)a, (List<float>)b);
            if (type == typeof(List<int>))
                return SameList((List<int>)a, (List<int>)b);
            return object.Equals(a, b);
        }

        private static bool SameMap<T>(Dictionary<string, T> a, Dictionary<string, T> b)
        {
            if (a.Count != b.Count || !SameComparer(a.Comparer, b.Comparer)) return false;
            foreach (KeyValuePair<string, T> entry in a)
            {
                T other;
                if (!b.TryGetValue(entry.Key, out other) ||
                    !EqualityComparer<T>.Default.Equals(entry.Value, other)) return false;
            }
            return true;
        }

        private static bool SameComparer(IEqualityComparer<string> a, IEqualityComparer<string> b)
        {
            if (object.ReferenceEquals(a, b)) return true;
            // JSON v1 deliberately uses StringComparer.Ordinal while the
            // vanilla/default dictionaries use EqualityComparer<string>.Default.
            // Both perform case-sensitive ordinal key matching. Do not equate
            // custom/ignore-case/culture comparers: their behavior can change.
            bool aOrdinal = object.ReferenceEquals(a, StringComparer.Ordinal) ||
                object.ReferenceEquals(a, EqualityComparer<string>.Default);
            bool bOrdinal = object.ReferenceEquals(b, StringComparer.Ordinal) ||
                object.ReferenceEquals(b, EqualityComparer<string>.Default);
            return aOrdinal && bOrdinal;
        }

        private static bool SameList<T>(List<T> a, List<T> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (!EqualityComparer<T>.Default.Equals(a[i], b[i])) return false;
            return true;
        }

        private static bool SameCurve(object a, object b, string name, out string mismatch)
        {
            mismatch = "NONE";
            if (a == null || b == null || a.GetType() != b.GetType())
            { mismatch = "consciousness." + name; return false; }
            foreach (string fieldName in ProfileSchemaV1.ProfileFieldNames)
            {
                FieldInfo field = a.GetType().GetField(fieldName, CurveFlags);
                if (field == null || !object.Equals(field.GetValue(a), field.GetValue(b)))
                {
                    mismatch = "consciousness." + name + "." + fieldName;
                    return false;
                }
            }
            return true;
        }
    }
}
