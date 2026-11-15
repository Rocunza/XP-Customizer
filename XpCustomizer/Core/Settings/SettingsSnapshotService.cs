using System;
using System.Collections.Generic;
using System.Reflection;
using XpCustomizer.ConsciousnessLearning;

namespace XpCustomizer
{
    /// <summary>
    /// R6-P0-A/B: read-only, version-conscious settings snapshot boundary.
    /// No Scribe access, no gameplay callbacks, no UI refresh and no writes to
    /// the existing settings instance. Apply and validation are R6-P0-C work.
    ///
    /// Declared instance fields include the private migration/version fields;
    /// new reference types fail closed until a deliberate copy rule is added.
    /// This code runs only when a snapshot is explicitly requested, never in
    /// the game's SkillRecord/XP calculation hot paths.
    /// </summary>
    internal static class SettingsSnapshotService
    {
        internal const string Revision = "R6P0AB-SNAPSHOT-ISOLATED-20261106";

        private const BindingFlags InstanceFields =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        internal static SettingsSnapshot Capture(XpCustomizerSettings source)
        {
            return new SettingsSnapshot(CloneIndependent(source));
        }

        internal static XpCustomizerSettings CloneIndependent(XpCustomizerSettings source)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            // Constructor defaults are deliberately overwritten field-by-field,
            // including migration fields that are not public.
            XpCustomizerSettings result = new XpCustomizerSettings();
            FieldInfo[] fields = typeof(XpCustomizerSettings).GetFields(InstanceFields);

            // Validate *all* field types before writing to the detached result.
            // An unsupported future field must not be shallow-copied silently.
            for (int i = 0; i < fields.Length; i++)
                EnsureSupportedSettingsField(fields[i]);

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                object value = field.GetValue(source);
                field.SetValue(result, CloneValue(field.FieldType, value));
            }

            return result;
        }

        internal static FieldInfo[] GetDeclaredSettingsFieldsForAudit()
        {
            return typeof(XpCustomizerSettings).GetFields(InstanceFields);
        }

        private static void EnsureSupportedSettingsField(FieldInfo field)
        {
            Type type = field.FieldType;
            if (type == typeof(bool) || type == typeof(int) || type == typeof(float) ||
                type == typeof(Dictionary<string, float>) ||
                type == typeof(Dictionary<string, bool>) ||
                type == typeof(List<float>) || type == typeof(List<int>) ||
                type == typeof(ConsAnchorV2ProfileSettings) ||
                type == typeof(ConsciousnessFurtherV2Settings))
                return;

            throw new NotSupportedException(
                "[XPC-R6P0] SNAPSHOT_UNSUPPORTED_FIELD; field=" + field.Name +
                "; type=" + type.FullName + "; no shallow-copy fallback");
        }

        private static object CloneValue(Type type, object value)
        {
            if (value == null)
                return null; // Null is preserved; normalization belongs to P0-C.

            if (type == typeof(bool) || type == typeof(int) || type == typeof(float))
                return value;

            if (type == typeof(Dictionary<string, float>))
            {
                Dictionary<string, float> original = (Dictionary<string, float>)value;
                return new Dictionary<string, float>(original, original.Comparer);
            }
            if (type == typeof(Dictionary<string, bool>))
            {
                Dictionary<string, bool> original = (Dictionary<string, bool>)value;
                return new Dictionary<string, bool>(original, original.Comparer);
            }
            if (type == typeof(List<float>))
                return new List<float>((List<float>)value);
            if (type == typeof(List<int>))
                return new List<int>((List<int>)value);
            if (type == typeof(ConsAnchorV2ProfileSettings))
                return CopyPrimitiveProfile(value, new ConsAnchorV2ProfileSettings());
            if (type == typeof(ConsciousnessFurtherV2Settings))
                return CopyPrimitiveProfile(value, new ConsciousnessFurtherV2Settings());

            throw new NotSupportedException("[XPC-R6P0] Unsupported copy type " + type.FullName);
        }

        private static object CopyPrimitiveProfile(object source, object destination)
        {
            FieldInfo[] fields = source.GetType().GetFields(InstanceFields);
            for (int i = 0; i < fields.Length; i++)
            {
                Type type = fields[i].FieldType;
                if (type != typeof(bool) && type != typeof(int) && type != typeof(float))
                    throw new NotSupportedException(
                        "[XPC-R6P0] SNAPSHOT_UNSUPPORTED_PROFILE_FIELD; " +
                        source.GetType().Name + "." + fields[i].Name);
            }

            for (int i = 0; i < fields.Length; i++)
                fields[i].SetValue(destination, fields[i].GetValue(source));

            return destination;
        }
    }
}
