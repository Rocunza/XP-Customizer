#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Xml;
using Verse;

namespace XpCustomizer
{
    /// <summary>Pure synthetic B2-A/B1 contract regressions; NO Scribe/disk writes.</summary>
    internal static class DetachedXmlSemanticSelfTests
    {
        private static int checks;
        internal static void Run()
        {
            checks = 0;
            XpCustomizerSettings source = new XpCustomizerSettings();
            source.gwsHighFactor = 1.11f;
            source.furtherLearningMentalThresholdStrength = 0.80f;
            source.consciousnessFurtherV2.HighFactor = 1.65f;
            source.decaySkillMultipliers["Mining & <Future>"] = 0.32f;
            source.gwsSkillEnabled["Social"] = false;
            Check(DetachedXmlSemanticContract.Defaults().Count == 131, "all 131 defaults registered");
            Check(DetachedXmlStageAudit.BuildSerializedFields(source).Count == 141,
                "131 scalars plus ten collections");
            Check(DetachedXmlSemanticContract.Defaults()["gwsHighFactor"].Equals(1.20f),
                "legacy Scribe fallback remains 1.20 not new preset 1.15");
            Check(!DetachedXmlSemanticContract.Defaults()["gwsHighFactor"].Equals(
                new XpCustomizerSettings().gwsHighFactor),
                "constructor default explicitly differs from Scribe fallback");
            Check(DetachedXmlSemanticContract.Defaults().ContainsKey("learningCapacityUseCustomValues") &&
                DetachedXmlSemanticContract.Defaults().ContainsKey("learningCapacityBonusConversion"),
                "legacy XML aliases retained");
            Check(!DetachedXmlSemanticContract.Defaults().ContainsKey(
                "legacyLearningCapacityUseCustomValues"), "private field name is not XML alias");
            Check(DetachedXmlSemanticContract.Defaults()["consciousnessAnchorSettingsVersion"]
                .Equals(ConsciousnessLearning.ConsciousnessAnchorV2SettingsStore.CurrentSchemaVersion),
                "anchor schema version uses Look default");
            Check(DetachedXmlSemanticContract.Defaults()["consciousnessFurtherV2Version"]
                .Equals(ConsciousnessLearning.ConsciousnessFurtherV2SettingsStore.SchemaVersion),
                "further schema version uses Look default");

            string canonical = Fixture(source);
            DetachedXmlSemanticContract.Result pass = Scan(canonical, source);
            Check(pass.Valid && pass.Emitted + pass.OmittedVerified == 131 &&
                pass.Unresolved == 0 && pass.Collections == 10,
                "fresh nondefault canonical semantic fixture matches all paths");
            Check(Scan(Fixture(new XpCustomizerSettings()), new XpCustomizerSettings()).Valid,
                "new default settings follow actual Scribe omission rules");
            Check(!Scan(Remove(canonical, "gwsHighFactor"), source).Valid,
                "missing nondefault gwsHighFactor rejected");
            Check(!Scan(Remove(canonical, "furtherLearningMentalThresholdStrength"), source).Valid,
                "missing nondefault mental value rejected");
            Check(!Scan(Remove(canonical, "consFurtherV2HighFactor"), source).Valid,
                "missing nested V2 nondefault rejected");
            XpCustomizerSettings legacy = SettingsSnapshotService.CloneIndependent(source);
            legacy.gwsHighFactor = 1.20f;
            Check(Scan(Fixture(legacy), legacy).Valid,
                "legacy old 1.20 value safely elided");
            Check(Scan(Remove(canonical, "gwsHighFactor"), legacy).Valid,
                "legacy omitted 1.20 is equivalent to explicit edited 1.11 fixture without key");
            Check(!Scan(ChangeScalar(canonical, "gwsHighFactor", "1.20"), source).Valid,
                "explicit wrong scalar rejected");
            Check(!Scan(ChangeScalar(canonical, "gwsHighFactor", "NaN"), source).Valid,
                "nonfinite scalar rejected");
            Check(!Scan(DuplicateScalar(canonical, "gwsHighFactor"),source).Valid,
                "duplicate scalar rejected");
            Check(!Scan(Replace(canonical,"gwsHighFactor", "UnexpectedField"),source).Valid,
                "unknown replacement key rejected");
            XpCustomizerSettings badComparer = SettingsSnapshotService.CloneIndependent(source);
            badComparer.gwsSkillEnabled = new Dictionary<string,bool>(
                badComparer.gwsSkillEnabled, StringComparer.OrdinalIgnoreCase);
            Check(!Scan(Fixture(badComparer), badComparer).Valid,
                "case-insensitive dictionary comparer rejected for Verse ordinal load");
            XpCustomizerSettings shuffled = SettingsSnapshotService.CloneIndependent(source);
            shuffled.gwsSkillEnabled["Mining"] = true;
            shuffled.gwsSkillEnabled["Cooking"] = false;
            Check(Scan(Fixture(shuffled),shuffled).Valid,
                "valid multikey dictionary accepted without key ordering requirement");
            Check(!Scan(Remove(canonical,"careerXpSplitLevels"),source).Valid,
                "missing collection rejected");
            Check(!Scan(canonical.Replace("SettingsBlock", "OtherRoot"),source).Valid,
                "wrong root rejected");
            Check(!Scan(canonical.Replace(DetachedXmlStageAudit.ExpectedClass,
                "Other.ModSettings"),source).Valid,"wrong owner class rejected");
            Check(!Scan("<!DOCTYPE SettingsBlock [<!ENTITY x SYSTEM 'file:///etc/passwd'>]>" +
                canonical,source).Valid,"external DTD rejected");
            XpCustomizerSettings edited = SettingsSnapshotService.CloneIndependent(source);
            edited.consciousnessAnchorSpeed.HighFactor += 0.1f;
            Check(!Scan(canonical,edited).Valid,
                "altered omitted profile value rejected");
            Check(!Scan(canonical, null).Valid, "null expected rejected");
            Check(!Scan("<broken", source).Valid, "malformed document rejected");

            // Exhaustive per-key default-elision regression: force a distinct
            // *non-default* value into every one of 131 reflected scalar paths,
            // then make sure removing that key is REJECTED. A source field
            // renamed without updating the manifest fails this test on boot.
            foreach (KeyValuePair<string, object> entry in DetachedXmlSemanticContract.Defaults())
            {
                XpCustomizerSettings single = SettingsSnapshotService.CloneIndependent(source);
                ForceNonDefault(single, entry.Key, entry.Value);
                string one = Fixture(single);
                Check(Scan(one, single).Valid, "emitted scalar path:" + entry.Key);
                Check(!Scan(Remove(one, entry.Key), single).Valid,
                    "nondefault omission rejected:" + entry.Key);
            }

            Log.Message("[XPC-R6P1D2C4B2AB1][XML_SEMANTIC_SELFTEST] status=PASS; " +
                "checks=" + checks + "; registeredScalars=131; collections=10" +
                "; contractFingerprint=" + DetachedXmlSemanticContract.SourceFingerprint +
                "; scope=PURE_SYNTHETIC_XML; engineLoader=NOT_RUN" +
                "; scribeCalls=0; actualModXmlWrites=0; productionApply=NOT_EXERCISED_BY_THIS_TEST; build=" +
                XpCustomizerMod.BuildId);
        }

        private static void Check(bool pass, string label)
        {
            ++checks;
            if (!pass) throw new InvalidOperationException("C4B2_PURE_CASE_FAILED:"+label);
        }

        private static DetachedXmlSemanticContract.Result Scan(string xml,XpCustomizerSettings expected)
        {
            return DetachedXmlSemanticContract.Inspect(xml==null?null:
                Encoding.UTF8.GetBytes(xml),expected);
        }

        private static void ForceNonDefault(XpCustomizerSettings settings,
            string xmlKey, object fallback)
        {
            object owner = settings;
            string field = xmlKey;
            if (xmlKey.StartsWith("consAnchorV2Speed", StringComparison.Ordinal))
            {
                owner = settings.consciousnessAnchorSpeed;
                field = xmlKey.Substring("consAnchorV2Speed".Length);
            }
            else if (xmlKey.StartsWith("consAnchorV2Capacity", StringComparison.Ordinal))
            {
                owner = settings.consciousnessAnchorCapacity;
                field = xmlKey.Substring("consAnchorV2Capacity".Length);
            }
            else if (xmlKey.StartsWith("consFurtherV2", StringComparison.Ordinal) &&
                xmlKey != "consciousnessFurtherV2Version")
            {
                owner = settings.consciousnessFurtherV2;
                field = xmlKey.Substring("consFurtherV2".Length);
            }
            else if (xmlKey == "learningCapacityUseCustomValues")
                field = "legacyLearningCapacityUseCustomValues";
            else if (xmlKey == "learningCapacityBonusConversion")
                field = "legacyLearningCapacityBonusConversion";
            FieldInfo target = owner.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly);
            if (target == null) throw new InvalidOperationException("MISSING_FIELD:" + xmlKey);
            object edited;
            if (fallback is bool) edited = !(bool)fallback;
            else if (fallback is int) edited = (int)fallback + 1;
            else if (fallback is float) edited = (float)fallback + 0.375f;
            else throw new InvalidOperationException("UNKNOWN_DEFAULT_TYPE:" + xmlKey);
            if (Object.Equals(edited, fallback))
                throw new InvalidOperationException("NONDEFAULT_MUTATION_FAILED:" + xmlKey);
            target.SetValue(owner, edited);
        }

        private static string ChangeScalar(string xml,string field,string content)
        {
            XmlDocument doc=new XmlDocument();doc.XmlResolver=null;doc.LoadXml(xml);
            XmlElement node=doc.DocumentElement["ModSettings"][field];
            if(node==null)throw new InvalidOperationException("FIXTURE_SCALAR_MISSING:"+field);
            node.InnerText=content;
            return doc.OuterXml;
        }
        private static string DuplicateScalar(string xml,string field)
        {
            XmlDocument doc=new XmlDocument();doc.XmlResolver=null;doc.LoadXml(xml);
            XmlElement mod=doc.DocumentElement["ModSettings"];
            XmlElement node=mod[field];
            if(node==null)throw new InvalidOperationException("FIXTURE_SCALAR_MISSING:"+field);
            mod.AppendChild(node.CloneNode(true));
            return doc.OuterXml;
        }
        private static string Replace(string original,string from,string to)
        {
            if (!original.Contains(from)) throw new InvalidOperationException("FIXTURE_REPLACEMENT_MISSING");
            return original.Replace(from,to);
        }
        private static string Remove(string xml,string name)
        {
            XmlDocument doc=new XmlDocument(); doc.XmlResolver=null; doc.LoadXml(xml);
            XmlElement mod=doc.DocumentElement["ModSettings"];
            XmlElement child=mod[name];
            if(child==null)throw new InvalidOperationException("FIXTURE_FIELD_MISSING:"+name);
            mod.RemoveChild(child);
            return doc.OuterXml;
        }
        internal static string Fixture(XpCustomizerSettings expected)
        {
            Dictionary<string,object> defaults=DetachedXmlSemanticContract.Defaults();
            Dictionary<string,object> fields=DetachedXmlStageAudit.BuildSerializedFields(expected);
            XmlDocument doc=new XmlDocument();
            XmlElement root=doc.CreateElement("SettingsBlock"); doc.AppendChild(root);
            XmlElement mod=doc.CreateElement("ModSettings");
            mod.SetAttribute("Class",DetachedXmlStageAudit.ExpectedClass); root.AppendChild(mod);
            foreach(KeyValuePair<string,object> field in fields)
            {
                object fallback;
                if(defaults.TryGetValue(field.Key,out fallback))
                {
                    if(!field.Value.Equals(fallback)) AddValue(doc,mod,field.Key,field.Value);
                }
                else if(field.Value is IList)
                {
                    XmlElement arr=doc.CreateElement(field.Key); mod.AppendChild(arr);
                    foreach(object item in (IList)field.Value)AddValue(doc,arr,"li",item);
                }
                else if(field.Value is IDictionary)
                {
                    XmlElement map=doc.CreateElement(field.Key);mod.AppendChild(map);
                    XmlElement keys=doc.CreateElement("keys"),values=doc.CreateElement("values");
                    map.AppendChild(keys);map.AppendChild(values);
                    foreach(DictionaryEntry entry in (IDictionary)field.Value)
                    {
                        AddValue(doc,keys,"li",entry.Key);
                        AddValue(doc,values,"li",entry.Value);
                    }
                }
                else throw new InvalidOperationException("FIXTURE_UNSUPPORTED:"+field.Key);
            }
            return doc.OuterXml;
        }
        private static void AddValue(XmlDocument doc, XmlElement parent,string name,object value)
        {
            XmlElement element=doc.CreateElement(name);
            element.InnerText=value is float ? ((float)value).ToString("G9",CultureInfo.InvariantCulture) :
                Convert.ToString(value,CultureInfo.InvariantCulture);
            parent.AppendChild(element);
        }
    }
}
#endif
