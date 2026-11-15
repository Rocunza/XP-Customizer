using System;
using System.Reflection;

namespace XpCustomizer
{
    /// <summary>
    /// R6-P0-D3: captures exact pre-apply field references as well as their
    /// independent values. An external refresh error must not be recovered by
    /// cloning back into a new collection: existing UI/other code may still
    /// hold references to the original collections.
    /// </summary>
    internal static class SettingsRuntimeRecovery
    {
        internal const string Revision = "R6P0D3-SETTINGS-REFERENCE-ROLLBACK-20261106";

        internal sealed class Backup
        {
            internal readonly XpCustomizerSettings Values;
            internal readonly FieldInfo[] Fields;
            internal readonly object[] References;

            internal Backup(XpCustomizerSettings values, FieldInfo[] fields, object[] references)
            {
                Values = values;
                Fields = fields;
                References = references;
            }
        }

        internal static Backup Capture(XpCustomizerSettings current)
        {
            if (current == null) throw new ArgumentNullException("current");
            FieldInfo[] fields = SettingsSnapshotService.GetDeclaredSettingsFieldsForAudit();
            XpCustomizerSettings values = SettingsSnapshotService.CloneIndependent(current);
            object[] original = new object[fields.Length];
            for (int i = 0; i < fields.Length; i++)
                original[i] = fields[i].GetValue(current);
            return new Backup(values, fields, original);
        }

        internal static bool Restore(XpCustomizerSettings current, Backup backup, out string reason)
        {
            reason = "NONE";
            if (current == null || backup == null)
            {
                reason = "MISSING_SETTINGS_BACKUP";
                return false;
            }

            bool restored = true;
            for (int i = 0; i < backup.Fields.Length; i++)
            {
                try
                {
                    backup.Fields[i].SetValue(current, backup.References[i]);
                }
                catch (Exception ex)
                {
                    restored = false;
                    reason = "RESTORE_FIELD:" + backup.Fields[i].Name + ":" + ex.GetType().Name;
                }
            }

            try
            {
                string mismatch;
                if (!SettingsApplyCoordinator.Equivalent(current, backup.Values, out mismatch))
                {
                    restored = false;
                    reason = "VALUE_MISMATCH:" + mismatch;
                }
                for (int i = 0; i < backup.Fields.Length; i++)
                {
                    if (backup.Fields[i].FieldType.IsValueType) continue;
                    if (!object.ReferenceEquals(backup.Fields[i].GetValue(current), backup.References[i]))
                    {
                        restored = false;
                        reason = "REFERENCE_MISMATCH:" + backup.Fields[i].Name;
                    }
                }
            }
            catch (Exception ex)
            {
                restored = false;
                reason = "VERIFY_EXCEPTION:" + ex.GetType().Name;
            }

            return restored;
        }
    }
}
