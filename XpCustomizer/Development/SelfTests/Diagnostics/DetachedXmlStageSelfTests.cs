#if DEBUG
using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Xml;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// Pure synthetic XML fixtures only: zero Scribe calls and zero disk I/O.
    /// Real Scribe serialization occurs only via the explicit DebugAction.
    /// </summary>
    internal static class DetachedXmlStageSelfTests
    {
        private static int scenarios;
        private static int checks;

        internal static void Run()
        {
            scenarios = 0;
            checks = 0;
            XpCustomizerSettings candidate = new XpCustomizerSettings();
            candidate.gwsHighFactor = 1.125f;
            candidate.furtherLearningMentalThresholdStrength = 0.75f;
            candidate.decaySkillMultipliers["Future_Mining"] = 0.35f;
            candidate.gwsSkillEnabled["Social"] = false;
            candidate.careerXpSplitLevels.Clear();
            candidate.consciousnessFurtherV2.HighFactor = 1.5f;
            string xml = Fixture(candidate);

            DetachedXmlStageAudit.Inspection valid = Scan(xml, candidate);
            Case(valid.Valid && valid.VerifiedCollections == 10 && valid.ComparedScalars == 3 &&
                valid.VerifiedItems > 0 && valid.Sha.Length == 64,
                "synthetic emitted scalars + all 10 collection values");
            Case(!Scan(null, candidate).Valid, "null bytes rejected");
            Case(!Scan("", candidate).Valid, "empty XML rejected");
            Case(!Scan("<broken", candidate).Valid, "malformed XML rejected");
            Case(!Scan(xml, null).Valid, "null expected rejected");
            Case(!DetachedXmlStageAudit.InspectXml(
                new byte[DetachedXmlStageAudit.MaximumBytes + 1], candidate).Valid,
                "oversize XML rejected");
            Case(!Scan(xml.Replace("SettingsBlock", "WrongRoot"), candidate).Valid,
                "incorrect root rejected");
            Case(!Scan(xml.Replace(DetachedXmlStageAudit.ExpectedClass, "Foreign.Mod"),
                candidate).Valid, "foreign class rejected");
            Case(!Scan(xml.Replace("<ModSettings Class=", "<ForeignSettings Class=")
                .Replace("</ModSettings>", "</ForeignSettings>"), candidate).Valid,
                "foreign settings element rejected");
            Case(!Scan(xml.Replace("</SettingsBlock>",
                "<ModSettings Class=\"XpCustomizer.XpCustomizerSettings\" /></SettingsBlock>"),
                candidate).Valid, "two settings roots rejected");
            Case(!Scan("<!DOCTYPE SettingsBlock [<!ENTITY x SYSTEM 'file:///etc/passwd'>]>" +
                xml, candidate).Valid, "DTD prohibited");
            Case(!Scan(xml.Replace("<gwsHighFactor>1.125</gwsHighFactor>",
                "<gwsHighFactor>1.25</gwsHighFactor>"), candidate).Valid,
                "mismatched scalar rejected");
            Case(!Scan(xml.Replace("<gwsHighFactor>1.125</gwsHighFactor>",
                "<gwsHighFactor>NaN</gwsHighFactor>"), candidate).Valid,
                "non-finite scalar rejected");
            Case(!Scan(xml.Replace("<gwsHighFactor>1.125</gwsHighFactor>",
                "<gwsHighFactor>1.125</gwsHighFactor>" +
                "<gwsHighFactor>1.125</gwsHighFactor>"), candidate).Valid,
                "duplicate scalar rejected");
            // Missing scalar is legitimate default elision, not corruption.
            DetachedXmlStageAudit.Inspection omitted =
                Scan(xml.Replace("<gwsHighFactor>1.125</gwsHighFactor>", ""), candidate);
            Case(omitted.Valid && omitted.ComparedScalars == 2,
                "omitted scalar is partial evidence, not made-up equality");
            Case(!Scan(xml.Replace("<furtherLearningMentalThresholdStrength>0.75" +
                "</furtherLearningMentalThresholdStrength>", "" )
                .Replace("<gwsHighFactor>1.125</gwsHighFactor>", ""), candidate).Valid,
                "only one emitted scalar insufficient");
            Case(!Scan(RemoveDirect(xml, "careerXpSplitLevels"), candidate).Valid,
                "missing empty list rejected");
            Case(!Scan(InjectListItem(xml, "careerXpSplitLevels", "5"), candidate).Valid,
                "tampered empty list rejected");
            Case(!Scan(xml.Replace("<gwsHighFactor>1.125</gwsHighFactor>",
                "<gwsHighFactor>1.125</gwsHighFactor><unknown>1</unknown>"), candidate).Valid,
                "unknown emitted key rejected");
            XpCustomizerSettings wrong = SettingsSnapshotService.CloneIndependent(candidate);
            wrong.decaySkillMultipliers["Future_Mining"] = 0.99f;
            Case(!Scan(xml, wrong).Valid, "dictionary value mismatch rejected");
            wrong = SettingsSnapshotService.CloneIndependent(candidate);
            wrong.careerXpStageWeights.Add(0.25f);
            Case(!Scan(xml, wrong).Valid, "list count mismatch rejected");
            wrong = SettingsSnapshotService.CloneIndependent(candidate);
            wrong.consciousnessFurtherV2.HighFactor = 1.55f;
            Case(!Scan(xml, wrong).Valid, "nested profile mismatch rejected");

            // F1 regression: normal XML text is allowed ONLY in scalar leaves.
            // Value types are checked against the detached candidate, not just
            // against syntax or a hard-coded XML literal.
            string typed = Mutate(xml, delegate(XmlDocument d)
            {
                XmlElement mod = RequireMod(d);
                AddScalar(d, mod, "gwsLowEnabled", candidate.gwsLowEnabled);
                AddScalar(d, mod, "gwsHighCurve",
                    candidate.gwsHighCurve);
            });
            DetachedXmlStageAudit.Inspection typedScan = Scan(typed, candidate);
            Case(typedScan.Valid && typedScan.ComparedScalars == 5,
                "bool/int/float emitted leaves accepted");
            Case(!Scan(Mutate(typed, delegate(XmlDocument d)
            {
                RequireMod(d)["gwsLowEnabled"].InnerText = "INVALID_BOOL";
            }), candidate).Valid, "invalid boolean leaf rejected");
            Case(!Scan(Mutate(typed, delegate(XmlDocument d)
            {
                RequireMod(d)["gwsHighCurve"].InnerText = "1.25";
            }), candidate).Valid, "invalid integer leaf rejected");

            XpCustomizerSettings escaped = SettingsSnapshotService.CloneIndependent(candidate);
            escaped.decaySkillMultipliers["Mining & <Research> \"quoted\""] = 0.625f;
            escaped.gwsSkillEnabled["  Social & <Special>  "] = true;
            string escapedXml = Fixture(escaped);
            Case(escapedXml.Contains("&amp;") && escapedXml.Contains("&lt;") &&
                Scan(escapedXml, escaped).Valid,
                "escaped dictionary string keys retain exact whitespace and meaning");
            Case(Scan(xml, candidate).Valid,
                "simple dictionary keys and list items remain valid after leaf fix");
            Case(!Scan(Mutate(xml, delegate(XmlDocument d)
            {
                RequireMod(d)["gwsHighFactor"].SetAttribute("unexpected", "1");
            }), candidate).Valid, "scalar leaf attributes rejected");
            Case(FailureIs(Mutate(xml, delegate(XmlDocument d)
            {
                RequireMod(d)["gwsHighFactor"].InnerXml = "<nested>1.125</nested>";
            }), candidate, "SCALAR_MISMATCH:gwsHighFactor"),
                "nested scalar element rejected as mismatch without parser crash");
            Case(!Scan(Mutate(xml, delegate(XmlDocument d)
            {
                RequireMod(d)["gwsHighFactor"].InnerXml = "1.125<nested/>";
            }), candidate).Valid, "scalar mixed content rejected");
            Case(!Scan(Mutate(xml, delegate(XmlDocument d)
            {
                RequireMod(d)["gwsHighFactor"].InnerXml = "<![CDATA[1.125]]>";
            }), candidate).Valid, "CDATA leaf rejected (noncanonical Scribe output)");
            Case(!Scan(Mutate(xml, delegate(XmlDocument d)
            {
                RequireMod(d)["gwsHighFactor"].InnerXml = "<!--comment-->1.125";
            }), candidate).Valid, "comment in scalar leaf rejected");
            Case(!Scan(Mutate(xml, delegate(XmlDocument d)
            {
                RequireMod(d)["gwsHighFactor"].InnerXml = "<?item test?>1.125";
            }), candidate).Valid, "processing instruction in scalar leaf rejected");
            Case(!Scan(Mutate(xml, delegate(XmlDocument d)
            {
                RequireMod(d)["careerXpStageWeights"]["li"].InnerXml = "<nested>1</nested>";
            }), candidate).Valid, "nested list value rejected");
            Case(!Scan(Mutate(xml, delegate(XmlDocument d)
            {
                GetDictionaryKey(d, "gwsSkillEnabled").SetAttribute("test", "1");
            }), candidate).Valid, "dictionary key attributes rejected");
            Case(FailureIs(Mutate(xml, delegate(XmlDocument d)
            {
                GetDictionaryKey(d, "gwsSkillEnabled").InnerXml = "<nested>Social</nested>";
            }), candidate, "DICTIONARY_MISMATCH:gwsSkillEnabled"),
                "nested dictionary key rejected without parser crash");
            Case(!Scan(Mutate(xml, delegate(XmlDocument d)
            {
                GetDictionaryKey(d, "gwsSkillEnabled").InnerXml = "Social<extra/>";
            }), candidate).Valid, "dictionary key mixed content rejected");
            Case(!Scan(Mutate(xml, delegate(XmlDocument d)
            {
                RequireMod(d)["gwsSkillEnabled"]["values"]["li"].InnerXml = "<nested>False</nested>";
            }), candidate).Valid, "nested dictionary value rejected");
            Case(!Scan(Mutate(xml, delegate(XmlDocument d)
            {
                RequireMod(d)["gwsHighFactor"].InnerText = "1.125";
                RequireMod(d).AppendChild(d.CreateTextNode("UNEXPECTED_MIXED_CONTENT"));
            }), candidate).Valid, "ModSettings mixed content rejected");
            Case(!Scan(Mutate(xml, delegate(XmlDocument d)
            {
                RequireMod(d)["careerXpStageWeights"].AppendChild(d.CreateTextNode("BAD"));
            }), candidate).Valid, "list container mixed content rejected");
            Case(!Scan(Mutate(xml, delegate(XmlDocument d)
            {
                RequireMod(d)["gwsSkillEnabled"]["keys"].AppendChild(d.CreateTextNode("BAD"));
            }), candidate).Valid, "dictionary keys container mixed content rejected");
            Case(!Scan(Mutate(xml, delegate(XmlDocument d)
            {
                RequireMod(d)["gwsSkillEnabled"]["values"].AppendChild(d.CreateTextNode("BAD"));
            }), candidate).Valid, "dictionary values container mixed content rejected");
            Case(!Scan(Mutate(xml, delegate(XmlDocument d)
            {
                d.DocumentElement.AppendChild(d.CreateTextNode("BAD"));
            }), candidate).Valid, "root mixed content rejected");
            Case(!Scan(Mutate(xml, delegate(XmlDocument d)
            {
                GetDictionaryKey(d, "gwsSkillEnabled").InnerText = "UnknownSkill";
            }), candidate).Valid, "unknown dictionary key rejected");
            XpCustomizerSettings duplicates = SettingsSnapshotService.CloneIndependent(candidate);
            duplicates.gwsSkillEnabled["SecondSkill"] = true;
            string duplicateXml = Mutate(Fixture(duplicates), delegate(XmlDocument d)
            {
                XmlElement keys = RequireMod(d)["gwsSkillEnabled"]["keys"];
                keys.LastChild.InnerText = keys.FirstChild.InnerText;
            });
            Case(!Scan(duplicateXml, duplicates).Valid,
                "duplicate dictionary key rejected even if item count matches");
            Case(!Scan(Mutate(xml, delegate(XmlDocument d)
            {
                RequireMod(d)["careerXpStageWeights"].SetAttribute("IsNull", "True");
            }), candidate).Valid, "unexpected collection attribute rejected");
            Case(!Scan(Mutate(xml, delegate(XmlDocument d)
            {
                XmlElement mod = RequireMod(d);
                mod.AppendChild(d.CreateElement("unknownFutureField"));
            }), candidate).Valid, "unknown scalar element still rejected");

            Log.Message("[XPC-R6P1D2C4B1F1][DETACHED_XML_STAGE_SELFTEST] status=PASS; " +
                "revision=" + DetachedXmlStageAudit.Revision +
                "; scenarios=" + scenarios + "; checks=" + checks +
                "; scope=PURE_SYNTHETIC_XML; actualModXmlReads=0; actualModXmlWrites=0" +
                "; scribeCalls=0; leafTextFix=STRICT_CONTAINER_SEPARATE; productionApply=NOT_EXERCISED_BY_THIS_TEST; build=" +
                XpCustomizerMod.BuildId);
        }

        private static void Case(bool condition, string label)
        {
            scenarios++;
            checks++;
            if (!condition)
                throw new InvalidOperationException("C4B1F1_PURE_CASE_FAILED:" + label);
        }

        private static DetachedXmlStageAudit.Inspection Scan(string xml,
            XpCustomizerSettings expected)
        {
            return DetachedXmlStageAudit.InspectXml(
                xml == null ? null : Encoding.UTF8.GetBytes(xml), expected);
        }

        private static string RemoveDirect(string xml, string label)
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml(xml);
            XmlNode child = document.DocumentElement["ModSettings"][label];
            if (child == null) throw new InvalidOperationException("FIXTURE_MISSING:" + label);
            child.ParentNode.RemoveChild(child);
            return document.OuterXml;
        }

        private static string InjectListItem(string xml, string label, string text)
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml(xml);
            XmlElement child = document.DocumentElement["ModSettings"][label];
            if (child == null) throw new InvalidOperationException("FIXTURE_MISSING:" + label);
            XmlElement li = document.CreateElement("li");
            li.InnerText = text;
            child.AppendChild(li);
            return document.OuterXml;
        }

        // Pure stand-in fixture: does NOT call Scribe, WriteSettings or touch disk.
        private static string Fixture(XpCustomizerSettings expected)
        {
            XmlDocument doc = new XmlDocument();
            XmlElement root = doc.CreateElement("SettingsBlock");
            doc.AppendChild(root);
            XmlElement mod = doc.CreateElement("ModSettings");
            mod.SetAttribute("Class", DetachedXmlStageAudit.ExpectedClass);
            root.AppendChild(mod);
            AddScalar(doc, mod, "gwsHighFactor", expected.gwsHighFactor);
            AddScalar(doc, mod, "furtherLearningMentalThresholdStrength",
                expected.furtherLearningMentalThresholdStrength);
            AddScalar(doc, mod, "consFurtherV2HighFactor",
                expected.consciousnessFurtherV2.HighFactor);
            FieldInfo[] fields = SettingsSnapshotService.GetDeclaredSettingsFieldsForAudit();
            foreach (FieldInfo f in fields)
            {
                object value = f.GetValue(expected);
                if (value is IList)
                {
                    XmlElement list = doc.CreateElement(f.Name);
                    mod.AppendChild(list);
                    foreach (object item in (IList)value)
                        AddScalar(doc, list, "li", item);
                }
                else if (value is IDictionary)
                {
                    XmlElement map = doc.CreateElement(f.Name);
                    mod.AppendChild(map);
                    XmlElement keys = doc.CreateElement("keys");
                    XmlElement vals = doc.CreateElement("values");
                    map.AppendChild(keys);
                    map.AppendChild(vals);
                    foreach (DictionaryEntry item in (IDictionary)value)
                    {
                        AddScalar(doc, keys, "li", item.Key);
                        AddScalar(doc, vals, "li", item.Value);
                    }
                }
            }
            return doc.OuterXml;
        }

        private static string Mutate(string xml, Action<XmlDocument> edit)
        {
            XmlDocument doc = new XmlDocument();
            doc.XmlResolver = null;
            doc.LoadXml(xml);
            edit(doc);
            return doc.OuterXml;
        }

        private static XmlElement RequireMod(XmlDocument doc)
        {
            XmlElement mod = doc.DocumentElement["ModSettings"];
            if (mod == null) throw new InvalidOperationException("FIXTURE_MOD_MISSING");
            return mod;
        }

        private static XmlElement GetDictionaryKey(XmlDocument doc, string name)
        {
            XmlElement dict = RequireMod(doc)[name];
            if (dict == null || dict["keys"] == null || dict["keys"]["li"] == null)
                throw new InvalidOperationException("FIXTURE_DICTIONARY_KEY_MISSING");
            return dict["keys"]["li"];
        }

        private static bool FailureIs(string xml, XpCustomizerSettings expected,
            string failure)
        {
            DetachedXmlStageAudit.Inspection result = Scan(xml, expected);
            return !result.Valid && result.Failure == failure;
        }

        private static void AddScalar(XmlDocument doc, XmlElement parent,
            string name, object value)
        {
            XmlElement element = doc.CreateElement(name);
            if (value is float)
                element.InnerText = ((float)value).ToString("G9", CultureInfo.InvariantCulture);
            else element.InnerText = Convert.ToString(value, CultureInfo.InvariantCulture);
            parent.AppendChild(element);
        }
    }
}
#endif
