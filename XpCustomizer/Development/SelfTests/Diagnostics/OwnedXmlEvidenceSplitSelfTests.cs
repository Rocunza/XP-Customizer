#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// OXE1: read-only split audit. Both independently maintained default
    /// inventories are evaluated, then a detached fixture exercises the
    /// production scanner and semantic evaluator. No Scribe or disk writes.
    /// </summary>
    internal static class OwnedXmlEvidenceSplitSelfTests
    {
        private static int checks;

        private static void Check(bool condition, string name)
        {
            checks++;
            if (!condition)
                throw new InvalidOperationException("OXE1_XML_SPLIT:" + name);
        }

        internal static void Run()
        {
            checks = 0;
            Type scanner = typeof(OwnedXmlScanner);
            Type semantic = typeof(OwnedXmlSemanticContract);
            const BindingFlags allStatic = BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic;
            Check(scanner.Namespace == "XpCustomizer" &&
                scanner.IsAbstract && scanner.IsSealed && scanner.IsNotPublic,
                "SCANNER_TYPE_IDENTITY");
            Check(semantic.Namespace == "XpCustomizer" &&
                semantic.IsAbstract && semantic.IsSealed && semantic.IsNotPublic,
                "SEMANTIC_TYPE_IDENTITY");
            Check(OwnedXmlScanner.MaximumBytes == 4 * 1024 * 1024 &&
                OwnedXmlScanner.ExpectedClass == "XpCustomizer.XpCustomizerSettings",
                "SCANNER_SECURITY_CONSTANTS");
            Check(semantic.GetMethod("Inspect", allStatic) != null &&
                scanner.GetMethod("InspectXml", allStatic) != null &&
                scanner.GetMethod("BuildSerializedFields", allStatic) != null &&
                scanner.GetMethod("Sha", allStatic) != null,
                "PRESERVED_ENTRY_POINTS");
            Check(scanner.GetNestedType("Inspection", BindingFlags.NonPublic) != null &&
                semantic.GetNestedType("Result", BindingFlags.NonPublic) != null,
                "PRESERVED_RESULT_TYPES");
            Check(!String.IsNullOrEmpty(OwnedXmlSemanticContract.Revision) &&
                OwnedXmlSemanticContract.SourceFingerprint ==
                    DetachedXmlSemanticContract.SourceFingerprint &&
                OwnedXmlSemanticContract.ScalarCount ==
                    DetachedXmlSemanticContract.ScalarCount &&
                OwnedXmlSemanticContract.CollectionCount ==
                    DetachedXmlSemanticContract.CollectionCount,
                "INDEPENDENT_CONTRACT_METADATA");

            // Both inventories must remain independently authored. Evaluate
            // their actual boxed values, not merely names/counts or source text.
            Dictionary<string, object> release = OwnedXmlSemanticContract.Defaults();
            Dictionary<string, object> detached = DetachedXmlSemanticContract.Defaults();
            Check(release.Count == OwnedXmlSemanticContract.ScalarCount &&
                detached.Count == DetachedXmlSemanticContract.ScalarCount,
                "CONTRACT_COUNTS");
            Check(release.Comparer.Equals(StringComparer.Ordinal) &&
                detached.Comparer.Equals(StringComparer.Ordinal),
                "ORDINAL_KEY_COMPARERS");
            foreach (KeyValuePair<string, object> entry in release)
            {
                object expected;
                Check(detached.TryGetValue(entry.Key, out expected),
                    "MISSING_DETACHED_KEY:" + entry.Key);
                Check(entry.Value != null && expected != null &&
                    entry.Value.GetType() == expected.GetType(),
                    "VALUE_TYPE_DRIFT:" + entry.Key);
                Check(Object.Equals(entry.Value, expected),
                    "DEFAULT_VALUE_DRIFT:" + entry.Key);
            }
            Check(release.Count == detached.Count, "NO_UNEXPECTED_DETACHED_KEYS");

            XpCustomizerSettings current = new XpCustomizerSettings();
            Dictionary<string, object> fields =
                OwnedXmlScanner.BuildSerializedFields(current);
            Check(fields.Count == release.Count +
                OwnedXmlSemanticContract.CollectionCount,
                "SERIALIZED_FIELD_INVENTORY");
            foreach (string key in release.Keys)
                Check(fields.ContainsKey(key), "DEFAULT_KEY_NOT_SERIALIZED:" + key);

            // Read-only fixture; no File.*, no Scribe.*, no Mod.WriteSettings().
            current.gwsHighFactor = 1.11f;
            byte[] xml = Encoding.UTF8.GetBytes(
                DetachedXmlSemanticSelfTests.Fixture(current));
            OwnedXmlScanner.Inspection raw = OwnedXmlScanner.InspectXml(xml, current);
            OwnedXmlSemanticContract.Result verified =
                OwnedXmlSemanticContract.Inspect(xml, current);
            Check(raw.Valid && raw.Failure == "NONE" &&
                raw.VerifiedCollections == OwnedXmlSemanticContract.CollectionCount,
                "RELEASE_SCANNER_VALID_FIXTURE");
            Check(verified.Valid && verified.Failure == "RAW_XML_SEMANTIC_MATCH_NO_VERSE_LOAD" &&
                verified.Emitted + verified.OmittedVerified ==
                    OwnedXmlSemanticContract.ScalarCount,
                "RELEASE_SEMANTIC_VALID_FIXTURE");
            Check(raw.Sha == OwnedXmlScanner.Sha(xml) &&
                raw.Bytes == xml.Length,
                "RAW_SHA_PRESERVED");
            XpCustomizerSettings mismatch =
                SettingsSnapshotService.CloneIndependent(current);
            mismatch.gwsHighFactor = 1.18f;
            Check(!OwnedXmlScanner.InspectXml(xml, mismatch).Valid &&
                !OwnedXmlSemanticContract.Inspect(xml, mismatch).Valid,
                "REJECT_CHANGED_LOADED_VALUE");
            Check(!OwnedXmlScanner.InspectXml(
                Encoding.UTF8.GetBytes("<broken"), current).Valid,
                "REJECT_MALFORMED_XML");
            Check(!OwnedXmlScanner.InspectXml(null, current).Valid,
                "REJECT_ABSENT_XML");
            string foreign = Encoding.UTF8.GetString(xml).Replace(
                "XpCustomizer.XpCustomizerSettings", "Other.ModSettings");
            Check(!OwnedXmlScanner.InspectXml(
                Encoding.UTF8.GetBytes(foreign), current).Valid,
                "REJECT_FOREIGN_OWNER");

            Log.Message("[XPC-OXE1][OWNED_XML_EVIDENCE_SPLIT_AUDIT] " +
                "status=PASS; checks=" + checks +
                "; defaultPairs=" + release.Count +
                "; fieldInventory=" + fields.Count +
                "; typeAndApi=PRESERVED; defaultsParity=VALUE_AND_TYPE" +
                "; fixture=PRODUCTION_SCANNER_AND_SEMANTIC_READ_ONLY" +
                "; sourceMoveParity=EXTERNAL_SOURCE_AUDIT" +
                "; liveScribe=NOT_RUN; actualProductionApply=NOT_RUN" +
                "; configXmlWrites=0; foreignWrites=0; testScribeCalls=0" +
                "; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
