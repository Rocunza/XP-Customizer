#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using XpCustomizer.ConsciousnessLearning;

namespace XpCustomizer
{
    /// <summary>
    /// R6-P0-A/B: detached-only snapshot tests. They cannot mutate the live
    /// settings singleton, the save file, any Pawn, or any gameplay Def.
    /// </summary>
    internal static class SettingsSnapshotSelfTests
    {
        private const BindingFlags ProfileFields =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        internal static void Run()
        {
            FieldInfo[] fields = SettingsSnapshotService.GetDeclaredSettingsFieldsForAudit();
            int scalarCount = 0, dictionaryCount = 0, listCount = 0, profileCount = 0;
            for (int i = 0; i < fields.Length; i++)
            {
                Type type = fields[i].FieldType;
                if (type == typeof(float) || type == typeof(int) || type == typeof(bool))
                    scalarCount++;
                else if (type == typeof(Dictionary<string, float>) ||
                         type == typeof(Dictionary<string, bool>))
                    dictionaryCount++;
                else if (type == typeof(List<float>) || type == typeof(List<int>))
                    listCount++;
                else if (type == typeof(ConsAnchorV2ProfileSettings) ||
                         type == typeof(ConsciousnessFurtherV2Settings))
                    profileCount++;
                else
                    throw new InvalidOperationException("R6P0 unsupported field: " + fields[i].Name);
            }

            // R6-P0-A contract lock: update deliberately when adding persisted fields.
            Check(scalarCount == 95 && dictionaryCount == 6 &&
                  listCount == 4 && profileCount == 3 && fields.Length == 108,
                "R6P0 settings field inventory drift; review Scribe contract first");

            // Names are pinned too: a rename is a save-contract review event
            // even when field counts and types remain unchanged.
            string[] fieldNames = new string[fields.Length];
            for (int i = 0; i < fields.Length; i++)
                fieldNames[i] = fields[i].Name;
            Array.Sort(fieldNames, StringComparer.Ordinal);
            uint fieldNameFingerprint = 2166136261u;
            unchecked
            {
                for (int i = 0; i < fieldNames.Length; i++)
                {
                    string token = fieldNames[i] + ";";
                    for (int j = 0; j < token.Length; j++)
                        fieldNameFingerprint = (fieldNameFingerprint ^ token[j]) * 16777619u;
                }
            }
            Check(fieldNameFingerprint == 0x32E32440u,
                "R6P0 settings field name inventory drift; inspect persistence contract");

            XpCustomizerSettings original = new XpCustomizerSettings();
            // Exercise all scalar fields (including private version/migration fields),
            // not just a handful of default values.
            for (int i = 0; i < fields.Length; i++)
            {
                Type type = fields[i].FieldType;
                if (type == typeof(bool))
                    fields[i].SetValue(original, !(bool)fields[i].GetValue(original));
                else if (type == typeof(int))
                    fields[i].SetValue(original, (int)fields[i].GetValue(original) + 11);
                else if (type == typeof(float))
                    fields[i].SetValue(original, (float)fields[i].GetValue(original) + 0.125f);
            }

            // Deliberately empty career boundaries are valid, not 'missing'.
            original.careerXpSplitLevels.Clear();
            original.careerXpStageWeights.Clear();
            original.careerXpStageWeights.Add(0.5f);
            original.skillSpecificXpMultipliers["FutureCustomSkill"] = 2.75f;
            original.skilltrainerSpecificMultipliers["Shooting"] = 0f;
            original.bookQualityMultipliers["Legendary"] = 1.7f;
            original.bookSkillMultipliers["Plants"] = 1.8f;
            // Preserve the dictionary's comparer, not only its entries.
            original.gwsSkillEnabled =
                new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            original.gwsSkillEnabled["Social"] = true;
            original.decaySkillMultipliers["Mining"] = 0.4f;
            original.consciousnessAnchorSpeed.LowFactor = 0f;
            original.consciousnessAnchorCapacity.HighFactor = 1.75f;
            original.consciousnessFurtherV2.LowFactor = 0.68f;

            SettingsSnapshot snapshotA = SettingsSnapshotService.Capture(original);
            XpCustomizerSettings copyA = snapshotA.CreateDetachedCopy();
            XpCustomizerSettings copyB = snapshotA.CreateDetachedCopy();
            CheckEquivalent(original, copyA, fields);
            CheckEquivalent(original, copyB, fields);
            Check(!ReferenceEquals(original, copyA) &&
                  !ReferenceEquals(copyA, copyB), "R6P0 settings instances shared");

            // Every reference field must be disjoint across A, B and source.
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].FieldType.IsValueType) continue;
                object sourceValue = fields[i].GetValue(original);
                Check(!ReferenceEquals(sourceValue, fields[i].GetValue(copyA)),
                    "R6P0 source shared reference: " + fields[i].Name);
                Check(!ReferenceEquals(fields[i].GetValue(copyA), fields[i].GetValue(copyB)),
                    "R6P0 snapshot copies shared reference: " + fields[i].Name);
            }

            // Changing detached settings cannot alter the source or snapshot A.
            copyA.skillSpecificXpMultipliers["FutureCustomSkill"] = 777f;
            copyA.bookSkillMultipliers.Clear();
            copyA.careerXpSplitLevels.Add(19);
            copyA.consciousnessAnchorSpeed.LowFactor = 0.99f;
            copyA.consciousnessAnchorCapacity.HighFactor = 19f;
            copyA.consciousnessFurtherV2.LowFactor = 0.12f;
            CheckEquivalent(original, copyB, fields);
            CheckEquivalent(original, snapshotA.CreateDetachedCopy(), fields);
            Check(original.careerXpSplitLevels.Count == 0,
                "R6P0 valid empty boundaries changed");

            Check(copyB.gwsSkillEnabled.ContainsKey("social"),
                "R6P0 dictionary comparer was lost");

            // Null is preserved rather than silently normalized; P0-C validates
            // candidates before applying them.
            XpCustomizerSettings withNull = new XpCustomizerSettings();
            withNull.bookSkillMultipliers = null;
            withNull.consciousnessFurtherV2 = null;
            XpCustomizerSettings nullCopy = SettingsSnapshotService.Capture(withNull)
                .CreateDetachedCopy();
            Check(nullCopy.bookSkillMultipliers == null &&
                  nullCopy.consciousnessFurtherV2 == null,
                  "R6P0 missing-value semantics changed");

            bool rejectedNull = false;
            try { SettingsSnapshotService.Capture(null); }
            catch (ArgumentNullException) { rejectedNull = true; }
            Check(rejectedNull, "R6P0 null source accepted");

            Log.Message("[XPC-R6P0][SNAPSHOT_AUDIT] status=PASS; revision=" +
                SettingsSnapshotService.Revision +
                "; settingsFields=" + fields.Length +
                "; fieldsFingerprint=0x" + fieldNameFingerprint.ToString("X8") +
                "; scalar=" + scalarCount + "; dictionaries=" + dictionaryCount +
                "; lists=" + listCount + "; consProfiles=" + profileCount +
                "; profileFieldsEach=12; independentCopies=2; " +
                "privateVersions=PRESERVED; disabledValues=PRESERVED; " +
                "emptySplits=PRESERVED; nulls=PRESERVED; " +
                "sourceWrites=0; liveSettingsWrites=0; pawnWrites=0; " +
                "scribeCalls=0; applyCalls=0; build=" + XpCustomizerMod.BuildId);
        }

        private static void CheckEquivalent(XpCustomizerSettings expected,
            XpCustomizerSettings actual, FieldInfo[] fields)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                object a = fields[i].GetValue(expected);
                object b = fields[i].GetValue(actual);
                if (a == null || b == null)
                {
                    Check(a == null && b == null, "R6P0 null mismatch: " + fields[i].Name);
                    continue;
                }

                if (fields[i].FieldType.IsValueType)
                    Check(a.Equals(b), "R6P0 scalar mismatch: " + fields[i].Name);
                else if (a is IDictionary)
                    CheckDictionary((IDictionary)a, (IDictionary)b, fields[i].Name);
                else if (a is IList)
                    CheckList((IList)a, (IList)b, fields[i].Name);
                else
                    CheckProfile(a, b, fields[i].Name);
            }
        }

        private static void CheckDictionary(IDictionary a, IDictionary b, string name)
        {
            Check(a.Count == b.Count, "R6P0 dictionary count: " + name);
            foreach (DictionaryEntry entry in a)
                Check(b.Contains(entry.Key) && entry.Value.Equals(b[entry.Key]),
                    "R6P0 dictionary item: " + name + "/" + entry.Key);
        }

        private static void CheckList(IList a, IList b, string name)
        {
            Check(a.Count == b.Count, "R6P0 list count: " + name);
            for (int i = 0; i < a.Count; i++)
                Check(a[i].Equals(b[i]), "R6P0 list item: " + name + "/" + i);
        }

        private static void CheckProfile(object a, object b, string name)
        {
            FieldInfo[] fields = a.GetType().GetFields(ProfileFields);
            Check(fields.Length == 12, "R6P0 profile schema drift: " + name);
            for (int i = 0; i < fields.Length; i++)
                Check(fields[i].GetValue(a).Equals(fields[i].GetValue(b)),
                    "R6P0 profile item: " + name + "/" + fields[i].Name);
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("[XPC-R6P0] " + message);
        }
    }
}
#endif
