#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// SETTINGS-SPLIT-SSR1: read-only contract audit plus DETACHED migration fixtures.
    /// No live settings, Scribe state, XML/JSON files, Defs, Pawn, or game UI are mutated.
    /// The separate existing XML/Scribe suites and actual game save/load are still required.
    /// </summary>
    internal static class SettingsSplitR1SelfTests
    {
        internal const string Revision = "SETTINGS-SPLIT-SSR1";
        // Frozen from 261110 0413(1).zip, before any source extraction.
        // Sorted by ordinal field name: name|C# type|visibility + LF.
        private const string ExpectedFieldSha256 = "495d88e81170986b93e8005aff6747cb71bd455c9b8bd57f38b36af51f8cfa3c";
        private const int ExpectedFields = 108;
        private const int ExpectedPublicFields = 96;
        private const int ExpectedXmlScalars = 131;
        private const int ExpectedXmlCollections = 10;
        private const int ExpectedProfileTopLevel = 81;
        private static int checks;

        internal static void Run()
        {
            checks = 0;
            Type owner = typeof(XpCustomizerSettings);
            Check(owner.BaseType == typeof(ModSettings), "BASE_TYPE_CHANGED");
            FieldInfo[] fields = owner.GetFields(BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Check(fields.Length == ExpectedFields, "INSTANCE_FIELD_COUNT");
            // Sort by field NAME, not by the concatenated descriptor; this also
            // preserves correct ordering if one future name prefixes another.
            Array.Sort(fields, delegate(FieldInfo a, FieldInfo b)
            {
                return StringComparer.Ordinal.Compare(a.Name, b.Name);
            });
            int visible = 0;
            var descriptors = new List<string>(fields.Length);
            foreach (FieldInfo field in fields)
            {
                if (field.IsPublic) ++visible;
                string visibility = field.IsPublic ? "public" :
                    field.IsAssembly ? "internal" : field.IsPrivate ? "private" : "other";
                descriptors.Add(field.Name + "|" + SourceTypeName(field.FieldType) +
                    "|" + visibility + "\n");
            }
            Check(visible == ExpectedPublicFields, "PUBLIC_FIELD_COUNT");
            string actualHash = Sha256(string.Concat(descriptors.ToArray()));
            Check(actualHash == ExpectedFieldSha256, "FIELD_SIGNATURE_SHA256:" + actualHash);

            // Scribe contract is checked independently in the pre-existing suites.
            // This inventory is detached and DOES NOT invoke ExposeData or WriteSettings.
            XpCustomizerSettings fresh = new XpCustomizerSettings();
            Check(DetachedXmlSemanticContract.Defaults().Count == ExpectedXmlScalars,
                "XML_DEFAULT_INVENTORY");
            Check(DetachedXmlStageAudit.BuildSerializedFields(fresh).Count ==
                ExpectedXmlScalars + ExpectedXmlCollections, "XML_FIELD_INVENTORY");
            Check(Profiles.ProfileSchemaV1.TopLevelCount == ExpectedProfileTopLevel &&
                Profiles.ProfileSchemaV1.Fingerprint == "0x07C6F415", "PROFILE_SCHEMA_CHANGED");
            Check(Math.Abs(fresh.gwsHighFactor - 1.30f) < 0.0001f &&
                DetachedXmlSemanticContract.Defaults()["gwsHighFactor"].Equals(1.20f),
                "GWS_HISTORICAL_FALLBACK");

            // The nine new private helpers must exist exactly once on the SAME CLR type.
            string[] helpers = {
                "ExposeDecayData", "ExposeCareerData", "ExposeSkilltrainerData",
                "ExposeGwsData", "ExposeBookXpData", "ExposeLearningCapacityData",
                "ExposeFurtherLearningData", "ExposeConsciousnessData", "ExposeBiotechData"
            };
            foreach (string name in helpers) VerifyPrivateNoArgVoidMethod(owner, name);
            VerifyPrivateNoArgVoidMethod(owner, "NormalizeLearningCapacitySettings");
            VerifyPrivateNoArgVoidMethod(owner, "NormalizeCareerXpDistributionSettings");

            // Deliberate empty Career stage list must remain a valid one-stage layout.
            XpCustomizerSettings oneStage = new XpCustomizerSettings();
            SetPrivateInt(oneStage, "careerXpDistributionDataVersion", 2);
            oneStage.careerXpSplitLevels = new List<int>();
            oneStage.careerXpStageWeights = new List<float> { 3f };
            oneStage.careerXpStageLockedShares = new List<float> { 1f };
            InvokePrivate(oneStage, "NormalizeCareerXpDistributionSettings");
            Check(oneStage.careerXpSplitLevels != null && oneStage.careerXpSplitLevels.Count == 0 &&
                oneStage.careerXpStageWeights.Count == 1 &&
                oneStage.careerXpStageLockedShares.Count == 1 &&
                oneStage.careerXpStageLockedShares[0] == -1f,
                "CAREER_EMPTY_SINGLE_STAGE");

            // Existing Book XP master OFF must NOT be re-enabled by a child switch.
            XpCustomizerSettings book = new XpCustomizerSettings();
            SetPrivateInt(book, "bookXpSettingsVersion", 1);
            book.bookXpEnabled = false;
            book.bookSkillEnabled = true;
            book.NormalizeBookXpMasterSettings();
            Check(!book.bookXpEnabled, "BOOK_MASTER_EXPLICIT_OFF");

            // V3 custom + strength is transformed once; subsequent V4 normalization
            // must preserve the final pp (not multiply the strength again).
            XpCustomizerSettings capacity = new XpCustomizerSettings();
            SetPrivateInt(capacity, "learningCapacityDataVersion", 3);
            capacity.learningCapacitySourceModifiersEnabled = true;
            capacity.learningCapacitySourceStrength = 1.30f;
            capacity.learningCapacityCustomFastLearner = 15f;
            InvokePrivate(capacity, "NormalizeLearningCapacitySettings");
            float converted = capacity.learningCapacityCustomFastLearner;
            Check(Math.Abs(converted - 19.5f) < 0.001f &&
                capacity.learningCapacityOverrideFastLearner,
                "LEARNING_CAPACITY_V4_ONCE");
            Check(GetPrivateInt(capacity, "learningCapacityDataVersion") == 4,
                "LEARNING_CAPACITY_VERSION");
            InvokePrivate(capacity, "NormalizeLearningCapacitySettings");
            Check(Math.Abs(capacity.learningCapacityCustomFastLearner - converted) < 0.001f,
                "LEARNING_CAPACITY_V4_IDEMPOTENT");

            Log.Message("[XPC-SS-SPLIT-R1][SETTINGS_SPLIT_AUDIT] status=PASS; " +
                "checks=" + checks + "; fields=108; publicFields=96; " +
                "fieldSha256=" + actualHash + "; helpers=9; xmlScalars=131; " +
                "xmlCollections=10; profileFingerprint=0x07C6F415; " +
                "scope=REFLECTION_AND_DETACHED_FIXTURES; " +
                "scribeRuntime=NOT_RUN; diskXmlRoundTrip=NOT_RUN; " +
                "realGameAndOtherMods=NOT_RUN; " +
                "build=" + XpCustomizerMod.BuildId);
        }

        private static string SourceTypeName(Type type)
        {
            if (type == typeof(bool)) return "bool";
            if (type == typeof(int)) return "int";
            if (type == typeof(float)) return "float";
            if (type == typeof(List<int>)) return "List<int>";
            if (type == typeof(List<float>)) return "List<float>";
            if (type == typeof(Dictionary<string, bool>)) return "Dictionary<string,bool>";
            if (type == typeof(Dictionary<string, float>)) return "Dictionary<string,float>";
            // Other original fields are the existing Consciousness V2 DTO types.
            return type.Name;
        }

        private static string Sha256(string value)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value)))
                    .Replace("-", "").ToLowerInvariant();
        }

        private static void VerifyPrivateNoArgVoidMethod(Type owner, string name)
        {
            MethodInfo m = owner.GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Check(m != null && m.IsPrivate && m.ReturnType == typeof(void) &&
                m.GetParameters().Length == 0, "METHOD_DRIFT:" + name);
        }

        private static void SetPrivateInt(XpCustomizerSettings s, string name, int value)
        {
            FieldInfo f = typeof(XpCustomizerSettings).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Check(f != null && f.FieldType == typeof(int), "VERSION_FIELD_MISSING:" + name);
            f.SetValue(s, value);
        }

        private static int GetPrivateInt(XpCustomizerSettings s, string name)
        {
            FieldInfo f = typeof(XpCustomizerSettings).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Check(f != null && f.FieldType == typeof(int), "VERSION_FIELD_MISSING:" + name);
            return (int)f.GetValue(s);
        }

        private static void InvokePrivate(XpCustomizerSettings s, string name)
        {
            MethodInfo m = typeof(XpCustomizerSettings).GetMethod(name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Check(m != null, "METHOD_MISSING:" + name);
            // No exception swallowing: a migration failure must fail the startup suite.
            m.Invoke(s, null);
        }

        private static void Check(bool condition, string label)
        {
            ++checks;
            if (!condition)
                throw new InvalidOperationException("SETTINGS_SPLIT_AUDIT_FAILED:" + label);
        }
    }
}
#endif
