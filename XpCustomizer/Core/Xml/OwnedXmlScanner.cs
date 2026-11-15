using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using XpCustomizer.Gws;
using XpCustomizer.ConsciousnessLearning;

namespace XpCustomizer
{
    /// <summary>Release-capable read-only XML verifier. Same semantic
    /// inventory and raw XML parsing as the tested D2-C4-B2 contract.
    /// No Scribe, no XML writes, no other mod settings accessed.</summary>
    internal static class OwnedXmlScanner
    {
        internal const int MaximumBytes = 4 * 1024 * 1024;
        internal const string ExpectedClass = "XpCustomizer.XpCustomizerSettings";
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        internal sealed class Inspection
        {
            internal string Failure = "NONE";
            internal int ComparedScalars;
            internal int OmittedScalars;
            internal int VerifiedCollections;
            internal int VerifiedItems;
            internal string Sha = "NONE";
            internal long Bytes;
            internal bool Valid;
        }
        internal static string Sha(byte[] bytes)
        {
            using (SHA256 algo = SHA256.Create())
                return BitConverter.ToString(algo.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        internal static Inspection InspectXml(byte[] bytes, XpCustomizerSettings expected)
        {
            Inspection result = new Inspection();
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumBytes ||
                expected == null)
            {
                result.Failure = "INVALID_INPUT";
                return result;
            }
            result.Bytes = bytes.Length;
            result.Sha = Sha(bytes);
            try
            {
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.DtdProcessing = DtdProcessing.Prohibit;
                settings.XmlResolver = null;
                settings.MaxCharactersInDocument = MaximumBytes;
                XmlDocument document = new XmlDocument();
                document.XmlResolver = null;
                using (MemoryStream stream = new MemoryStream(bytes, false))
                using (XmlReader reader = XmlReader.Create(stream, settings))
                    document.Load(reader);
                XmlElement root = document.DocumentElement;
                if (root == null || root.Name != "SettingsBlock" || root.HasAttributes ||
                    DirectElements(root).Count != 1)
                    return Fail(result, "BAD_ROOT");
                XmlElement node = DirectElements(root)[0];
                if (node.Name != "ModSettings" || node.Attributes.Count != 1 ||
                    node.GetAttribute("Class") != ExpectedClass)
                    return Fail(result, "BAD_OWNER_CLASS");
                if (node.HasAttribute("IsNull")) return Fail(result, "NULL_SETTINGS");

                Dictionary<string, object> fields = BuildSerializedFields(expected);

                HashSet<string> found = new HashSet<string>(StringComparer.Ordinal);
                foreach (XmlElement entry in DirectElements(node))
                {
                    if (!found.Add(entry.Name)) return Fail(result, "DUPLICATE_FIELD");
                    object exp;
                    if (!fields.TryGetValue(entry.Name, out exp))
                        return Fail(result, "UNKNOWN_OR_UNEXPECTED_FIELD");
                    if (exp is IList)
                    {
                        if (!CheckList(entry, (IList)exp, result))
                            return Fail(result, "LIST_MISMATCH:" + entry.Name);
                        result.VerifiedCollections++;
                    }
                    else if (exp is IDictionary)
                    {
                        if (!CheckDictionary(entry, (IDictionary)exp, result))
                            return Fail(result, "DICTIONARY_MISMATCH:" + entry.Name);
                        result.VerifiedCollections++;
                    }
                    else if (!CheckScalar(entry, exp))
                        return Fail(result, "SCALAR_MISMATCH:" + entry.Name);
                    else result.ComparedScalars++;
                }
                foreach (KeyValuePair<string, object> entry in fields)
                {
                    if (entry.Value is IList || entry.Value is IDictionary)
                    {
                        if (!found.Contains(entry.Key))
                            return Fail(result, "COLLECTION_MISSING:" + entry.Key);
                    }
                    else if (!found.Contains(entry.Key)) result.OmittedScalars++;
                }
                // Expected full collection inventory: 4 lists + 6 dictionaries.
                // Future field inventory drift must fail closed, not silently
                // lower the test coverage.
                if (result.VerifiedCollections != 10 || result.ComparedScalars < 2)
                    return Fail(result, "INSUFFICIENT_EVIDENCE");
                result.Valid = true;
                return result;
            }
            catch (XmlException)
            {
                return Fail(result, "XML_MALFORMED");
            }
            catch (InvalidOperationException)
            {
                // Container mixed-content / invalid node: never treated as a
                // harmless scalar text node or an unspecified parser failure.
                return Fail(result, "STRUCTURE_INVALID");
            }
            catch (Exception ex)
            {
                return Fail(result, "PARSE_EXCEPTION:" + ex.GetType().Name);
            }
        }

        private static Inspection Fail(Inspection result, string why)
        {
            result.Valid = false;
            result.Failure = why;
            return result;
        }

        /// <summary>
        /// Shared inventory of actual C# field values. The B2 Scribe-default
        /// contract independently lists each XML key + the Look fallback.
        /// </summary>
        internal static Dictionary<string, object> BuildSerializedFields(XpCustomizerSettings expected)
        {
            if (expected == null) throw new ArgumentNullException("expected");
            Dictionary<string, object> fields = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (FieldInfo f in SettingsSnapshotService.GetDeclaredSettingsFieldsForAudit())
            {
                object value = f.GetValue(expected);
                if (f.FieldType == typeof(bool) || f.FieldType == typeof(int) ||
                    f.FieldType == typeof(float) || typeof(IList).IsAssignableFrom(f.FieldType) ||
                    typeof(IDictionary).IsAssignableFrom(f.FieldType))
                    fields.Add(SerializedName(f.Name), value);
            }
            AddProfileFields(fields, expected.consciousnessAnchorSpeed, "consAnchorV2Speed");
            AddProfileFields(fields, expected.consciousnessAnchorCapacity, "consAnchorV2Capacity");
            AddProfileFields(fields, expected.consciousnessFurtherV2, "consFurtherV2");
            return fields;
        }

        private static string SerializedName(string fieldName)
        {
            // Two retained legacy fields intentionally use their original
            // XML names instead of the in-memory migration field names.
            if (fieldName == "legacyLearningCapacityUseCustomValues")
                return "learningCapacityUseCustomValues";
            if (fieldName == "legacyLearningCapacityBonusConversion")
                return "learningCapacityBonusConversion";
            return fieldName;
        }

        private static void AddProfileFields(Dictionary<string, object> fields,
            object profile, string prefix)
        {
            if (profile == null) throw new InvalidOperationException("NULL_PROFILE");
            foreach (FieldInfo field in profile.GetType().GetFields(Fields))
                fields.Add(prefix + field.Name, field.GetValue(profile));
        }

        private static List<XmlElement> DirectElements(XmlElement parent)
        {
            List<XmlElement> items = new List<XmlElement>();
            foreach (XmlNode child in parent.ChildNodes)
            {
                XmlElement element = child as XmlElement;
                if (element != null) items.Add(element);
                else if (child.NodeType != XmlNodeType.Whitespace &&
                    child.NodeType != XmlNodeType.SignificantWhitespace &&
                    child.NodeType != XmlNodeType.Comment)
                    throw new InvalidOperationException("UNEXPECTED_XML_NODE");
            }
            return items;
        }

        /// <summary>
        /// Read a Scribe_Values leaf. Unlike DirectElements (strictly for XML
        /// containers), a leaf MUST accept ordinary Text nodes. Do not use
        /// InnerText alone: it would hide nested elements and mixed content.
        /// Reject attributes, CDATA, comments, PI and nested elements; none
        /// is part of Scribe_Values' canonical scalar/list-key output.
        /// </summary>
        private static bool TryReadLeafText(XmlElement element, out string token)
        {
            token = null;
            if (element == null || element.HasAttributes) return false;
            StringBuilder value = new StringBuilder();
            foreach (XmlNode child in element.ChildNodes)
            {
                switch (child.NodeType)
                {
                    case XmlNodeType.Text:
                    case XmlNodeType.Whitespace:
                    case XmlNodeType.SignificantWhitespace:
                        value.Append(child.Value);
                        break;
                    default:
                        return false;
                }
            }
            token = value.ToString();
            return true;
        }

        private static bool CheckScalar(XmlElement element, object expected)
        {
            string raw;
            if (expected == null || !TryReadLeafText(element, out raw))
                return false;
            string token = raw.Trim();
            if (expected is bool)
            {
                bool value;
                return Boolean.TryParse(token, out value) && value == (bool)expected;
            }
            if (expected is int)
            {
                int value;
                return Int32.TryParse(token, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out value) && value == (int)expected;
            }
            if (expected is float)
            {
                float value;
                return Single.TryParse(token, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out value) &&
                    !Single.IsNaN(value) && !Single.IsInfinity(value) &&
                    value.Equals((float)expected);
            }
            return false;
        }

        private static bool CheckList(XmlElement element, IList expected, Inspection result)
        {
            if (element.HasAttributes || expected == null) return false;
            List<XmlElement> values = DirectElements(element);
            if (values.Count != expected.Count) return false;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i].Name != "li" || !CheckScalar(values[i], expected[i]))
                    return false;
                result.VerifiedItems++;
            }
            return true;
        }

        private static bool CheckDictionary(XmlElement element, IDictionary expected, Inspection result)
        {
            if (element.HasAttributes || expected == null) return false;
            List<XmlElement> containers = DirectElements(element);
            if (containers.Count != 2 || containers[0].Name != "keys" ||
                containers[1].Name != "values" || containers[0].HasAttributes ||
                containers[1].HasAttributes) return false;
            List<XmlElement> keys = DirectElements(containers[0]);
            List<XmlElement> vals = DirectElements(containers[1]);
            if (keys.Count != expected.Count || vals.Count != expected.Count) return false;
            HashSet<string> observed = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                string key;
                if (keys[i].Name != "li" || vals[i].Name != "li" ||
                    !TryReadLeafText(keys[i], out key))
                    return false;
                // Dictionary<string, T> keys are exact, untrimmed strings.
                // XML entities are decoded by XmlDocument before comparing.
                if (!observed.Add(key) || !expected.Contains(key) ||
                    !CheckScalar(vals[i], expected[key])) return false;
                result.VerifiedItems++;
            }
            return true;
        }
    }

}
