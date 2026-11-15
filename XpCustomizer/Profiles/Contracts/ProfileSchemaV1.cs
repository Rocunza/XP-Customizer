using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using XpCustomizer.ConsciousnessLearning;

namespace XpCustomizer.Profiles
{
    internal sealed class ProfileFieldSpec
    {
        internal readonly string Section;
        internal readonly string Name;
        internal readonly Type FieldType;
        internal FieldInfo SettingField;
        internal ProfileFieldSpec(string section, string name, Type fieldType)
        { Section=section; Name=name; FieldType=fieldType; }
    }

    /// <summary>Frozen, explicitly reviewed v1 portable contract. Independent of Scribe.</summary>
    internal static class ProfileSchemaV1
    {
        internal const string FormatId = "zcn.xpcustomizer.profile";
        internal const int FormatVersion = 1;
        internal const int SettingsSchemaVersion = 1;
        internal const string Fingerprint = "0x07C6F415";
        internal const int TopLevelCount = 81;
        internal const int ProfileFieldCount = 12;
        internal const int MaxJsonChars = 1048576;
        internal const int MaxCollectionItems = 4096;

        internal static readonly string[] ProfileFieldNames = { "LowEnabled", "HighEnabled", "LowAnchor", "LowFactor", "LowCurve", "LowCurvature", "HighAnchor", "HighFactor", "HighCurve", "HighCurvature", "HighDiminishingLimit", "HighSafetyLimit" };
        internal static readonly string[] LegacyExcluded = {
            "consciousnessFurtherBonusAt170",
            "consciousnessFurtherMaximumBonus",
            "consciousnessFurtherMaximumPenalty",
            "consciousnessFurtherPenaltyAt80",
            "furtherLearningHardWorkerModifier",
            "furtherLearningIndustriousModifier",
            "furtherLearningIronWilledModifier",
            "furtherLearningLazyModifier",
            "furtherLearningNervousModifier",
            "furtherLearningNeuroticModifier",
            "furtherLearningSlothfulModifier",
            "furtherLearningSteadfastModifier",
            "furtherLearningTraitModifiersEnabled",
            "furtherLearningVeryNeuroticModifier",
            "furtherLearningVolatileModifier"
        };
        private static readonly ProfileFieldSpec[] Fields = {
            new ProfileFieldSpec("career", "careerXpMultiplier", typeof(float)), 
            new ProfileFieldSpec("career", "level20ReserveMultiplier", typeof(float)), 
            new ProfileFieldSpec("career", "skillSpecificXpEnabled", typeof(bool)), 
            new ProfileFieldSpec("career", "skillSpecificXpMultipliers", typeof(Dictionary<string, float>)), 
            new ProfileFieldSpec("career", "careerXpUseCustomDistribution", typeof(bool)), 
            new ProfileFieldSpec("career", "careerXpRoundingStep", typeof(int)), 
            new ProfileFieldSpec("career", "careerXpSplitLevels", typeof(List<int>)), 
            new ProfileFieldSpec("career", "careerXpStageWeights", typeof(List<float>)), 
            new ProfileFieldSpec("career", "careerXpStageLockedShares", typeof(List<float>)), 
            new ProfileFieldSpec("trainer", "skilltrainerXpMultiplier", typeof(float)), 
            new ProfileFieldSpec("trainer", "skilltrainerSpecificEnabled", typeof(bool)), 
            new ProfileFieldSpec("trainer", "skilltrainerSpecificMultipliers", typeof(Dictionary<string, float>)), 
            new ProfileFieldSpec("bookXp", "bookXpEnabled", typeof(bool)), 
            new ProfileFieldSpec("bookXp", "bookIntellectualEnabled", typeof(bool)), 
            new ProfileFieldSpec("bookXp", "bookIntellectualXpPerHour", typeof(float)), 
            new ProfileFieldSpec("bookXp", "bookQualityEnabled", typeof(bool)), 
            new ProfileFieldSpec("bookXp", "bookQualityMultipliers", typeof(Dictionary<string, float>)), 
            new ProfileFieldSpec("bookXp", "bookSkillEnabled", typeof(bool)), 
            new ProfileFieldSpec("bookXp", "bookSkillMultipliers", typeof(Dictionary<string, float>)), 
            new ProfileFieldSpec("gws", "gwsEnabled", typeof(bool)), 
            new ProfileFieldSpec("gws", "gwsLowEnabled", typeof(bool)), 
            new ProfileFieldSpec("gws", "gwsHighEnabled", typeof(bool)), 
            new ProfileFieldSpec("gws", "gwsLowAnchor", typeof(float)), 
            new ProfileFieldSpec("gws", "gwsHighAnchor", typeof(float)), 
            new ProfileFieldSpec("gws", "gwsLowFactor", typeof(float)), 
            new ProfileFieldSpec("gws", "gwsHighFactor", typeof(float)), 
            new ProfileFieldSpec("gws", "gwsLowCurve", typeof(int)), 
            new ProfileFieldSpec("gws", "gwsHighCurve", typeof(int)), 
            new ProfileFieldSpec("gws", "gwsLowCurvature", typeof(float)), 
            new ProfileFieldSpec("gws", "gwsHighCurvature", typeof(float)), 
            new ProfileFieldSpec("gws", "gwsHighDiminishingLimit", typeof(float)), 
            new ProfileFieldSpec("gws", "gwsHighTechnicalLimit", typeof(float)), 
            new ProfileFieldSpec("gws", "gwsApplyToReading", typeof(bool)), 
            new ProfileFieldSpec("gws", "gwsSkillEnabled", typeof(Dictionary<string, bool>)), 
            new ProfileFieldSpec("learningCapacity", "learningCapacityBaseDailyCap", typeof(float)), 
            new ProfileFieldSpec("learningCapacity", "learningCapacitySourceModifiersEnabled", typeof(bool)), 
            new ProfileFieldSpec("learningCapacity", "learningCapacitySourceStrength", typeof(float)), 
            new ProfileFieldSpec("learningCapacity", "learningCapacityCustomSlowLearner", typeof(float)), 
            new ProfileFieldSpec("learningCapacity", "learningCapacityCustomFastLearner", typeof(float)), 
            new ProfileFieldSpec("learningCapacity", "learningCapacityCustomTooSmart", typeof(float)), 
            new ProfileFieldSpec("learningCapacity", "learningCapacityCustomBlissLobotomy", typeof(float)), 
            new ProfileFieldSpec("learningCapacity", "learningCapacityCustomSlowStudy", typeof(float)), 
            new ProfileFieldSpec("learningCapacity", "learningCapacityCustomQuickStudy", typeof(float)), 
            new ProfileFieldSpec("learningCapacity", "learningCapacityCustomNeuralSupercharge", typeof(float)), 
            new ProfileFieldSpec("learningCapacity", "learningCapacityCustomRanchingCentral", typeof(float)), 
            new ProfileFieldSpec("learningCapacity", "learningCapacityCustomLearningAssistant", typeof(float)), 
            new ProfileFieldSpec("learningCapacity", "learningCapacityOverrideSlowLearner", typeof(bool)), 
            new ProfileFieldSpec("learningCapacity", "learningCapacityOverrideFastLearner", typeof(bool)), 
            new ProfileFieldSpec("learningCapacity", "learningCapacityOverrideTooSmart", typeof(bool)), 
            new ProfileFieldSpec("learningCapacity", "learningCapacityOverrideBlissLobotomy", typeof(bool)), 
            new ProfileFieldSpec("learningCapacity", "learningCapacityOverrideSlowStudy", typeof(bool)), 
            new ProfileFieldSpec("learningCapacity", "learningCapacityOverrideQuickStudy", typeof(bool)), 
            new ProfileFieldSpec("learningCapacity", "learningCapacityOverrideNeuralSupercharge", typeof(bool)), 
            new ProfileFieldSpec("learningCapacity", "learningCapacityOverrideRanchingCentral", typeof(bool)), 
            new ProfileFieldSpec("learningCapacity", "learningCapacityOverrideLearningAssistant", typeof(bool)), 
            new ProfileFieldSpec("furtherLearning", "furtherLearningBaseFactor", typeof(float)), 
            new ProfileFieldSpec("furtherLearning", "furtherLearningMentalThresholdEnabled", typeof(bool)), 
            new ProfileFieldSpec("furtherLearning", "furtherLearningMentalThresholdStrength", typeof(float)), 
            new ProfileFieldSpec("consciousness", "consciousnessAffectsLearning", typeof(bool)), 
            new ProfileFieldSpec("consciousness", "consciousnessAffectsLearningSpeed", typeof(bool)), 
            new ProfileFieldSpec("consciousness", "consciousnessAffectsDailyCapacity", typeof(bool)), 
            new ProfileFieldSpec("consciousness", "consciousnessAffectsFurtherLearning", typeof(bool)), 
            new ProfileFieldSpec("biotech", "biotechCustomizationEnabled", typeof(bool)), 
            new ProfileFieldSpec("biotech", "biotechAwfulAptitude", typeof(int)), 
            new ProfileFieldSpec("biotech", "biotechPoorAptitude", typeof(int)), 
            new ProfileFieldSpec("biotech", "biotechStrongAptitude", typeof(int)), 
            new ProfileFieldSpec("biotech", "biotechGreatAptitude", typeof(int)), 
            new ProfileFieldSpec("biotech", "biotechAwfulComplexity", typeof(int)), 
            new ProfileFieldSpec("biotech", "biotechPoorComplexity", typeof(int)), 
            new ProfileFieldSpec("biotech", "biotechStrongComplexity", typeof(int)), 
            new ProfileFieldSpec("biotech", "biotechGreatComplexity", typeof(int)), 
            new ProfileFieldSpec("biotech", "biotechAwfulMetabolism", typeof(int)), 
            new ProfileFieldSpec("biotech", "biotechPoorMetabolism", typeof(int)), 
            new ProfileFieldSpec("biotech", "biotechStrongMetabolism", typeof(int)), 
            new ProfileFieldSpec("biotech", "biotechGreatMetabolism", typeof(int)), 
            new ProfileFieldSpec("decay", "decayByLevelEnabled", typeof(bool)), 
            new ProfileFieldSpec("decay", "decayDailyByLevel", typeof(List<float>)), 
            new ProfileFieldSpec("decay", "decaySkillMultipliersEnabled", typeof(bool)), 
            new ProfileFieldSpec("decay", "decaySkillMultipliers", typeof(Dictionary<string, float>)), 
            new ProfileFieldSpec("decay", "greatMemoryDecayReduction", typeof(float)), 
            new ProfileFieldSpec("decay", "perfectMemoryDecayReduction", typeof(float)), 
        };
        internal static ProfileFieldSpec[] GetFields() { return (ProfileFieldSpec[])Fields.Clone(); }

        internal static void VerifyContract()
        {
            FieldInfo[] actual = SettingsSnapshotService.GetDeclaredSettingsFieldsForAudit();
            if (actual.Length != SettingsCandidateValidator.ExpectedSettingsFieldCount ||
                SettingsCandidateValidator.ComputeFieldFingerprint(actual) !=
                SettingsCandidateValidator.ExpectedFieldNameFingerprint)
                throw new InvalidOperationException("PROFILE_SOURCE_SCHEMA_DRIFT");
            Dictionary<string, FieldInfo> source = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
            foreach (FieldInfo f in actual) source.Add(f.Name, f);
            HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
            int publicCount=0;
            foreach (FieldInfo f in actual) if (f.IsPublic) publicCount++;
            if (publicCount != 96 || Fields.Length != TopLevelCount || LegacyExcluded.Length != 15)
                throw new InvalidOperationException("PROFILE_FIELD_COUNT_DRIFT");
            foreach (ProfileFieldSpec spec in Fields)
            {
                FieldInfo field;
                if (!source.TryGetValue(spec.Name, out field) || !field.IsPublic ||
                    field.FieldType != spec.FieldType || !used.Add(field.Name))
                    throw new InvalidOperationException("PROFILE_FIELD_DRIFT:"+spec.Name);
                spec.SettingField=field;
            }
            foreach (string legacy in LegacyExcluded)
            {
                FieldInfo field;
                if (!source.TryGetValue(legacy, out field) || !field.IsPublic || !used.Add(legacy))
                    throw new InvalidOperationException("PROFILE_LEGACY_DRIFT:"+legacy);
            }
            if (used.Count != publicCount) throw new InvalidOperationException("PROFILE_PUBLIC_FIELDS_UNACCOUNTED");
            CheckProfile(typeof(ConsAnchorV2ProfileSettings));
            CheckProfile(typeof(ConsciousnessFurtherV2Settings));
            if (ComputeFingerprint() != Fingerprint) throw new InvalidOperationException("PROFILE_FINGERPRINT_DRIFT");
        }

        private static void CheckProfile(Type t)
        {
            FieldInfo[] actual = t.GetFields(BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (actual.Length != ProfileFieldCount) throw new InvalidOperationException("PROFILE_CURVE_COUNT_DRIFT:"+t.Name);
            foreach (string name in ProfileFieldNames)
            {
                FieldInfo f=t.GetField(name, BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                Type want=name.EndsWith("Enabled",StringComparison.Ordinal) ? typeof(bool) :
                    name.EndsWith("Curve",StringComparison.Ordinal) ? typeof(int) : typeof(float);
                if (f==null || f.FieldType != want) throw new InvalidOperationException("PROFILE_CURVE_FIELD_DRIFT:"+name);
            }
        }

        private static string ComputeFingerprint()
        {
            List<string> canonical = new List<string>();
            foreach (ProfileFieldSpec f in Fields)
                canonical.Add(f.Section+"/"+f.Name+":"+TypeName(f.FieldType));
            string[] names={"speedProfile","capacityProfile","furtherProfile"};
            foreach (string name in names)
                foreach (string field in ProfileFieldNames)
                    canonical.Add("consciousness/"+name+"/"+field+":"+
                        (field.EndsWith("Enabled",StringComparison.Ordinal)?"bool":
                        field.EndsWith("Curve",StringComparison.Ordinal)?"int":"float"));
            canonical.Sort(StringComparer.Ordinal);
            byte[] bytes=Encoding.UTF8.GetBytes(string.Join("\n",canonical.ToArray()));
            uint hash=2166136261u;
            unchecked { foreach (byte b in bytes) hash=(hash^b)*16777619u; }
            return "0x"+hash.ToString("X8",System.Globalization.CultureInfo.InvariantCulture);
        }
        internal static string TypeName(Type t)
        {
            if(t==typeof(bool))return "bool"; if(t==typeof(int))return "int";
            if(t==typeof(float))return "float";
            if(t==typeof(Dictionary<string,float>))return "Dictionary<string, float>";
            if(t==typeof(Dictionary<string,bool>))return "Dictionary<string, bool>";
            if(t==typeof(List<int>))return "List<int>";
            if(t==typeof(List<float>))return "List<float>";
            throw new InvalidOperationException("PROFILE_UNSUPPORTED_TYPE:"+t.FullName);
        }
    }
}
