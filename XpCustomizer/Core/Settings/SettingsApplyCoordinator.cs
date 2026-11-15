using System;
using System.Collections;
using System.Reflection;
using Verse;

namespace XpCustomizer
{
    internal enum SettingsApplyStatus
    {
        APPLY_PASS,
        APPLY_REJECTED,
        APPLY_ROLLBACK_PASS,
        APPLY_ROLLBACK_FAILED
    }

    internal sealed class SettingsApplyResult
    {
        internal readonly SettingsApplyStatus Status;
        internal readonly string Reason;
        internal readonly int FieldsWritten;
        internal readonly bool RollbackVerified;

        internal SettingsApplyResult(SettingsApplyStatus status, string reason,
            int fieldsWritten, bool rollbackVerified)
        {
            Status = status;
            Reason = reason;
            FieldsWritten = fieldsWritten;
            RollbackVerified = rollbackVerified;
        }
    }

    /// <summary>
    /// R6-P0-C: data-only application boundary, detached/self-test use until
    /// P0-D connects main-thread UI, caches, GeneDefs and persistence.
    /// Never replaces the target ModSettings instance; never calls Scribe,
    /// WriteSettings, Harmony, cache invalidation or gameplay services.
    /// No public user-facing entry point is exposed in this phase.
    /// </summary>
    internal static class SettingsApplyCoordinator
    {
        internal const string Revision = "R6P0C-DATA-TRANSACTION-20261106";
        private const BindingFlags ProfileFields = BindingFlags.Instance |
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        internal static SettingsApplyResult Apply(XpCustomizerSettings target,
            SettingsSnapshot snapshot)
        {
            return ApplyCore(target, snapshot, -1, true);
        }

#if DEBUG
        // Fault injection is test-only and cannot be compiled into Release.
        internal static SettingsApplyResult ApplyForTesting(XpCustomizerSettings target,
            SettingsSnapshot snapshot, int failAfterFields)
        {
            return ApplyCore(target, snapshot, failAfterFields, false);
        }
#endif

        private static SettingsApplyResult ApplyCore(XpCustomizerSettings target,
            SettingsSnapshot snapshot, int failAfterFields, bool emitLog)
        {
            if (target == null || snapshot == null)
                return Finish(SettingsApplyStatus.APPLY_REJECTED,
                    "NULL_TARGET_OR_SNAPSHOT", 0, false, emitLog);

            // A second copy isolates us even if the snapshot owner modifies
            // their own detached copy during this operation.
            XpCustomizerSettings staging;
            SettingsCandidateValidator.Result checkedCandidate;
            try
            {
                staging = snapshot.CreateDetachedCopy();
                checkedCandidate = SettingsCandidateValidator.Validate(staging);
            }
            catch (Exception ex)
            {
                return Finish(SettingsApplyStatus.APPLY_REJECTED,
                    "PRECHECK_EXCEPTION:" + ex.GetType().Name, 0, false, emitLog);
            }
            if (!checkedCandidate.Accepted)
                return Finish(SettingsApplyStatus.APPLY_REJECTED,
                    checkedCandidate.Reason, 0, false, emitLog);

            // The target may contain older/legacy raw data; do not normalize
            // or reject it. Only ensure that its entire state can be backed up.
            FieldInfo[] fields;
            object[] originals;
            XpCustomizerSettings originalValues;
            try
            {
                fields = SettingsSnapshotService.GetDeclaredSettingsFieldsForAudit();
                originalValues = SettingsSnapshotService.CloneIndependent(target);
                originals = new object[fields.Length];
                for (int i = 0; i < fields.Length; i++)
                    originals[i] = fields[i].GetValue(target);
            }
            catch (Exception ex)
            {
                return Finish(SettingsApplyStatus.APPLY_REJECTED,
                    "BACKUP_EXCEPTION:" + ex.GetType().Name, 0, false, emitLog);
            }

            int written = 0;
            try
            {
                // Do not call out to UI/Def/gameplay while fields are partially
                // assigned. It is the P0-D integration layer's responsibility
                // to update derived state after the full data transaction.
                for (int i = 0; i < fields.Length; i++)
                {
                    fields[i].SetValue(target, fields[i].GetValue(staging));
                    written++;
#if DEBUG
                    if (failAfterFields == written)
                        throw new InvalidOperationException("TEST_INJECTED_FAILURE");
#endif
                }

                string mismatch;
                if (!Equivalent(target, staging, out mismatch))
                    throw new InvalidOperationException("POST_APPLY_MISMATCH:" + mismatch);
                if (!DisjointReferenceFields(target, fields, originals, out mismatch))
                    throw new InvalidOperationException("STAGING_REUSED_ORIGINAL:" + mismatch);

                return Finish(SettingsApplyStatus.APPLY_PASS,
                    "PASS", written, false, emitLog);
            }
            catch (Exception ex)
            {
                // Restore the original references, not merely equivalent deep
                // copies. This preserves any external references held by UI or
                // other code when a transaction fails part way through.
                bool restored = true;
                string restoreError = "NONE";
                for (int i = 0; i < fields.Length; i++)
                {
                    try { fields[i].SetValue(target, originals[i]); }
                    catch (Exception rollbackEx)
                    {
                        restored = false;
                        restoreError = "RESTORE_FIELD:" + fields[i].Name +
                            ":" + rollbackEx.GetType().Name;
                    }
                }

                try
                {
                    string mismatch;
                    if (!Equivalent(target, originalValues, out mismatch))
                    {
                        restored = false;
                        restoreError = "VERIFY_MISMATCH:" + mismatch;
                    }
                    if (!OriginalReferencesRestored(target, fields, originals, out mismatch))
                    {
                        restored = false;
                        restoreError = "REFERENCE_MISMATCH:" + mismatch;
                    }
                }
                catch (Exception verifyEx)
                {
                    restored = false;
                    restoreError = "VERIFY_EXCEPTION:" + verifyEx.GetType().Name;
                }

                return Finish(restored ? SettingsApplyStatus.APPLY_ROLLBACK_PASS :
                    SettingsApplyStatus.APPLY_ROLLBACK_FAILED,
                    ex.GetType().Name + (restored ? ":" + ex.Message : ":" + restoreError),
                    written, restored, emitLog);
            }
        }

        /// <summary>
        /// Exact value comparison for the supported field inventory. No
        /// normalizing, allocating gameplay objects or touching user settings.
        /// </summary>
        internal static bool Equivalent(XpCustomizerSettings a,
            XpCustomizerSettings b, out string mismatch)
        {
            mismatch = "NONE";
            if (a == null || b == null) { mismatch = "NULL"; return false; }
            FieldInfo[] fields = SettingsSnapshotService.GetDeclaredSettingsFieldsForAudit();
            for (int i = 0; i < fields.Length; i++)
            {
                object left = fields[i].GetValue(a);
                object right = fields[i].GetValue(b);
                if (left == null || right == null)
                {
                    if (left != null || right != null)
                    { mismatch = fields[i].Name; return false; }
                    continue;
                }
                if (fields[i].FieldType.IsValueType)
                {
                    if (!left.Equals(right))
                    { mismatch = fields[i].Name; return false; }
                }
                else if (left is IDictionary)
                {
                    if (!EqualDictionaries((IDictionary)left, (IDictionary)right))
                    { mismatch = fields[i].Name; return false; }
                }
                else if (left is IList)
                {
                    if (!EqualLists((IList)left, (IList)right))
                    { mismatch = fields[i].Name; return false; }
                }
                else
                {
                    FieldInfo[] profileFields = left.GetType().GetFields(ProfileFields);
                    if (left.GetType() != right.GetType() || profileFields.Length != 12)
                    { mismatch = fields[i].Name; return false; }
                    for (int j = 0; j < profileFields.Length; j++)
                    {
                        object x = profileFields[j].GetValue(left);
                        object y = profileFields[j].GetValue(right);
                        if (!object.Equals(x, y))
                        { mismatch = fields[i].Name + "." + profileFields[j].Name; return false; }
                    }
                }
            }
            return true;
        }

        private static bool EqualDictionaries(IDictionary left, IDictionary right)
        {
            if (left.Count != right.Count) return false;
            foreach (DictionaryEntry entry in left)
            {
                if (!right.Contains(entry.Key) ||
                    !object.Equals(entry.Value, right[entry.Key])) return false;
            }
            // A dictionary's comparer is also part of its behavior.
            Type type = left.GetType();
            PropertyInfo comparer = type.GetProperty("Comparer", BindingFlags.Instance | BindingFlags.Public);
            return comparer == null || object.ReferenceEquals(comparer.GetValue(left, null),
                comparer.GetValue(right, null));
        }

        private static bool EqualLists(IList left, IList right)
        {
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
                if (!object.Equals(left[i], right[i])) return false;
            return true;
        }

        private static bool DisjointReferenceFields(XpCustomizerSettings assigned,
            FieldInfo[] fields, object[] originals, out string mismatch)
        {
            mismatch = "NONE";
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].FieldType.IsValueType) continue;
                object now = fields[i].GetValue(assigned);
                object previous = originals[i];
                if (now != null && object.ReferenceEquals(now, previous))
                { mismatch = fields[i].Name; return false; }
            }
            return true;
        }

        private static bool OriginalReferencesRestored(XpCustomizerSettings target,
            FieldInfo[] fields, object[] originals, out string mismatch)
        {
            mismatch = "NONE";
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].FieldType.IsValueType) continue;
                if (!object.ReferenceEquals(fields[i].GetValue(target), originals[i]))
                { mismatch = fields[i].Name; return false; }
            }
            return true;
        }

        private static SettingsApplyResult Finish(SettingsApplyStatus status,
            string reason, int fieldsWritten, bool rollbackVerified, bool emitLog)
        {
            SettingsApplyResult result = new SettingsApplyResult(status, reason,
                fieldsWritten, rollbackVerified);
            if (emitLog)
            {
                string summary = "[XPC-R6P0][APPLY] status=" + status +
                    "; reason=" + reason + "; fieldsWritten=" + fieldsWritten +
                    "; rollbackVerified=" + rollbackVerified +
                    "; effects=DATA_ONLY; scribeCalls=0; uiRefresh=0; geneDefWrites=0" +
                    "; build=" + XpCustomizerMod.BuildId;
                if (status == SettingsApplyStatus.APPLY_ROLLBACK_FAILED)
                    Log.Error(summary);
                else if (status == SettingsApplyStatus.APPLY_REJECTED ||
                         status == SettingsApplyStatus.APPLY_ROLLBACK_PASS)
                    Log.Warning(summary);
                else
                    Log.Message(summary);
            }
            return result;
        }
    }
}
