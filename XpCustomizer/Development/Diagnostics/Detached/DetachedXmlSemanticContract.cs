#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Verse;
using XpCustomizer.Gws;
using XpCustomizer.ConsciousnessLearning;

namespace XpCustomizer
{
    /// <summary>
    /// R6-P1-D2-C4-B2-A/B1. Strict evidence-only inventory for the *actual*
    /// third argument of every Scribe_Values.Look, never constructor defaults.
    /// Generated from the three fixed ExposeData sources. Do not modify a
    /// Scribe declaration without regenerating this file and its manifest.
    /// No disk writes, no live Scribe loader and no profile Apply.
    /// </summary>
    internal static class DetachedXmlSemanticContract
    {
        internal const string Revision = "R6P1D2C4B2AB1-SCRIBE-DEFAULT-CONTRACT-20261107";
        internal const string SourceFingerprint = "fdbb674116966a42";
        internal const int ScalarCount = 131;
        internal const int CollectionCount = 10;

        internal sealed class Result
        {
            internal bool Valid;
            internal string Failure = "NOT_CHECKED";
            internal int Emitted;
            internal int OmittedVerified;
            internal int Unresolved = ScalarCount;
            internal int Collections;
            internal string Fingerprint = SourceFingerprint;
        }

        // Deliberately not cached: schema drift is rechecked for every scan.
        internal static Dictionary<string, object> Defaults()
        {
            Dictionary<string, object> result = new Dictionary<string, object>(StringComparer.Ordinal);
            Add(result, "decayByLevelEnabled", false);
            Add(result, "decaySkillMultipliersEnabled", false);
            Add(result, "greatMemoryDecayReduction", 50f);
            Add(result, "perfectMemoryDecayReduction", 100f);
            Add(result, "careerXpMultiplier", XpCustomizerDefaults.CareerXpMultiplier);
            Add(result, "level20ReserveMultiplier", XpCustomizerDefaults.Level20ReserveMultiplier);
            Add(result, "skillSpecificXpEnabled", false);
            Add(result, "careerXpDistributionDataVersion", 0);
            Add(result, "careerXpUseCustomDistribution", XpCustomizerDefaults.CareerXpUseCustomDistribution);
            Add(result, "careerXpRoundingStep", XpCustomizerDefaults.CareerXpRoundingStep);
            Add(result, "skilltrainerXpMultiplier", XpCustomizerDefaults.SkilltrainerXpMultiplier);
            Add(result, "skilltrainerSpecificEnabled", false);
            Add(result, "gwsEnabled", false);
            Add(result, "gwsLowEnabled", true);
            Add(result, "gwsHighEnabled", true);
            Add(result, "gwsLowAnchor", GwsLearningService.DefaultLowAnchorGws);
            Add(result, "gwsHighAnchor", LegacyCurveXmlDefaults.GwsHighAnchor);
            Add(result, "gwsLowFactor", 0.70f);
            Add(result, "gwsHighFactor", LegacyCurveXmlDefaults.GwsHighFactor);
            Add(result, "gwsLowCurve", (int)GwsCurveMode.Linear);
            Add(result, "gwsHighCurve", LegacyCurveXmlDefaults.GwsHighCurve);
            Add(result, "gwsLowCurvature", 1f);
            Add(result, "gwsHighCurvature", LegacyCurveXmlDefaults.GwsHighCurvature);
            Add(result, "gwsHighDiminishingLimit", LegacyCurveXmlDefaults.GwsHighDiminishingLimit);
            Add(result, "gwsHighTechnicalLimit", GwsLearningService.DefaultAcceleratingLimit);
            Add(result, "gwsApplyToReading", false);
            Add(result, "bookXpSettingsVersion", 0);
            Add(result, "bookXpEnabled", false);
            Add(result, "bookIntellectualEnabled", false);
            Add(result, "bookIntellectualXpPerHour", 250f);
            Add(result, "bookQualityEnabled", false);
            Add(result, "bookSkillEnabled", false);
            Add(result, "learningCapacityDataVersion", 0);
            Add(result, "learningCapacityBaseDailyCap", XpCustomizerDefaults.LearningCapacityBaseDailyCap);
            Add(result, "learningCapacitySourceModifiersEnabled", XpCustomizerDefaults.LearningCapacitySourceModifiersEnabled);
            Add(result, "learningCapacitySourceStrength", XpCustomizerDefaults.LearningCapacitySourceStrength);
            Add(result, "learningCapacityUseCustomValues", false);
            Add(result, "learningCapacityBonusConversion", 0f);
            Add(result, "learningCapacityCustomSlowLearner", XpCustomizerDefaults.SlowLearnerCapacity);
            Add(result, "learningCapacityCustomFastLearner", XpCustomizerDefaults.FastLearnerCapacity);
            Add(result, "learningCapacityCustomTooSmart", XpCustomizerDefaults.TooSmartCapacity);
            Add(result, "learningCapacityCustomBlissLobotomy", XpCustomizerDefaults.BlissLobotomyCapacity);
            Add(result, "learningCapacityCustomSlowStudy", XpCustomizerDefaults.SlowStudyCapacity);
            Add(result, "learningCapacityCustomQuickStudy", XpCustomizerDefaults.QuickStudyCapacity);
            Add(result, "learningCapacityCustomNeuralSupercharge", XpCustomizerDefaults.NeuralSuperchargeCapacity);
            Add(result, "learningCapacityCustomLearningAssistant", XpCustomizerDefaults.LearningAssistantCapacity);
            Add(result, "learningCapacityCustomRanchingCentral", XpCustomizerDefaults.RanchingCentralCapacity);
            Add(result, "learningCapacityOverrideSlowLearner", false);
            Add(result, "learningCapacityOverrideFastLearner", false);
            Add(result, "learningCapacityOverrideTooSmart", false);
            Add(result, "learningCapacityOverrideBlissLobotomy", false);
            Add(result, "learningCapacityOverrideSlowStudy", false);
            Add(result, "learningCapacityOverrideQuickStudy", false);
            Add(result, "learningCapacityOverrideNeuralSupercharge", false);
            Add(result, "learningCapacityOverrideRanchingCentral", false);
            Add(result, "learningCapacityOverrideLearningAssistant", false);
            Add(result, "furtherLearningBaseFactor", XpCustomizerDefaults.FurtherLearningBase);
            Add(result, "furtherLearningMentalSettingsVersion", 0);
            Add(result, "furtherLearningMentalThresholdEnabled", XpCustomizerDefaults.FurtherLearningMentalThresholdEnabled);
            Add(result, "furtherLearningMentalThresholdStrength", XpCustomizerDefaults.FurtherLearningMentalStrength);
            Add(result, "furtherLearningTraitSettingsVersion", 0);
            Add(result, "furtherLearningTraitModifiersEnabled", XpCustomizerDefaults.FurtherLearningTraitModifiersEnabled);
            Add(result, "furtherLearningVolatileModifier", XpCustomizerDefaults.VolatileFurtherLearning);
            Add(result, "furtherLearningNervousModifier", XpCustomizerDefaults.NervousFurtherLearning);
            Add(result, "furtherLearningSteadfastModifier", XpCustomizerDefaults.SteadfastFurtherLearning);
            Add(result, "furtherLearningIronWilledModifier", XpCustomizerDefaults.IronWilledFurtherLearning);
            Add(result, "furtherLearningSlothfulModifier", XpCustomizerDefaults.SlothfulFurtherLearning);
            Add(result, "furtherLearningLazyModifier", XpCustomizerDefaults.LazyFurtherLearning);
            Add(result, "furtherLearningHardWorkerModifier", XpCustomizerDefaults.HardWorkerFurtherLearning);
            Add(result, "furtherLearningIndustriousModifier", XpCustomizerDefaults.IndustriousFurtherLearning);
            Add(result, "furtherLearningNeuroticModifier", XpCustomizerDefaults.NeuroticFurtherLearning);
            Add(result, "furtherLearningVeryNeuroticModifier", XpCustomizerDefaults.VeryNeuroticFurtherLearning);
            Add(result, "consciousnessAffectsLearning", XpCustomizerDefaults.ConsciousnessAffectsLearning);
            Add(result, "consciousnessAffectsLearningSpeed", XpCustomizerDefaults.ConsciousnessAffectsLearningSpeed);
            Add(result, "consciousnessAffectsDailyCapacity", XpCustomizerDefaults.ConsciousnessAffectsDailyCapacity);
            Add(result, "consciousnessAffectsFurtherLearning", XpCustomizerDefaults.ConsciousnessAffectsFurtherLearning);
            Add(result, "consciousnessFurtherPenaltyAt80", XpCustomizerDefaults.ConsciousnessFurtherPenaltyAt80);
            Add(result, "consciousnessFurtherMaximumPenalty", XpCustomizerDefaults.ConsciousnessFurtherMaximumPenalty);
            Add(result, "consciousnessFurtherBonusAt170", XpCustomizerDefaults.ConsciousnessFurtherBonusAt170);
            Add(result, "consciousnessFurtherMaximumBonus", XpCustomizerDefaults.ConsciousnessFurtherMaximumBonus);
            Add(result, "biotechCustomizationEnabled", XpCustomizerDefaults.BiotechCustomizationEnabled);
            Add(result, "biotechAwfulAptitude", XpCustomizerDefaults.BiotechAwfulAptitude);
            Add(result, "biotechPoorAptitude", XpCustomizerDefaults.BiotechPoorAptitude);
            Add(result, "biotechStrongAptitude", XpCustomizerDefaults.BiotechStrongAptitude);
            Add(result, "biotechGreatAptitude", XpCustomizerDefaults.BiotechGreatAptitude);
            Add(result, "biotechAwfulComplexity", XpCustomizerDefaults.BiotechAwfulComplexity);
            Add(result, "biotechPoorComplexity", XpCustomizerDefaults.BiotechPoorComplexity);
            Add(result, "biotechStrongComplexity", XpCustomizerDefaults.BiotechStrongComplexity);
            Add(result, "biotechGreatComplexity", XpCustomizerDefaults.BiotechGreatComplexity);
            Add(result, "biotechAwfulMetabolism", XpCustomizerDefaults.BiotechAwfulMetabolism);
            Add(result, "biotechPoorMetabolism", XpCustomizerDefaults.BiotechPoorMetabolism);
            Add(result, "biotechStrongMetabolism", XpCustomizerDefaults.BiotechStrongMetabolism);
            Add(result, "biotechGreatMetabolism", XpCustomizerDefaults.BiotechGreatMetabolism);
            Add(result, "consAnchorV2SpeedLowEnabled", true);
            Add(result, "consAnchorV2CapacityLowEnabled", true);
            Add(result, "consAnchorV2SpeedHighEnabled", true);
            Add(result, "consAnchorV2CapacityHighEnabled", true);
            Add(result, "consAnchorV2SpeedLowAnchor", ConsciousnessAnchorCurveService.DefaultLowAnchor);
            Add(result, "consAnchorV2CapacityLowAnchor", ConsciousnessAnchorCurveService.DefaultLowAnchor);
            Add(result, "consAnchorV2SpeedLowFactor", ConsciousnessAnchorCurveService.DefaultLowFactor);
            Add(result, "consAnchorV2CapacityLowFactor", ConsciousnessAnchorCurveService.DefaultLowFactor);
            Add(result, "consAnchorV2SpeedLowCurve", (int)ConsAnchorCurveMode.Linear);
            Add(result, "consAnchorV2CapacityLowCurve", (int)ConsAnchorCurveMode.Linear);
            Add(result, "consAnchorV2SpeedLowCurvature", ConsciousnessAnchorCurveService.DefaultCurvature);
            Add(result, "consAnchorV2CapacityLowCurvature", ConsciousnessAnchorCurveService.DefaultCurvature);
            Add(result, "consAnchorV2SpeedHighAnchor", LegacyCurveXmlDefaults.ConsSpeedCapacityHighAnchor);
            Add(result, "consAnchorV2CapacityHighAnchor", LegacyCurveXmlDefaults.ConsSpeedCapacityHighAnchor);
            Add(result, "consAnchorV2SpeedHighFactor", LegacyCurveXmlDefaults.ConsSpeedCapacityHighFactor);
            Add(result, "consAnchorV2CapacityHighFactor", LegacyCurveXmlDefaults.ConsSpeedCapacityHighFactor);
            Add(result, "consAnchorV2SpeedHighCurve", (int)ConsAnchorCurveMode.Diminishing);
            Add(result, "consAnchorV2CapacityHighCurve", (int)ConsAnchorCurveMode.Diminishing);
            Add(result, "consAnchorV2SpeedHighCurvature", ConsciousnessAnchorCurveService.DefaultCurvature);
            Add(result, "consAnchorV2CapacityHighCurvature", ConsciousnessAnchorCurveService.DefaultCurvature);
            Add(result, "consAnchorV2SpeedHighDiminishingLimit", ConsciousnessAnchorCurveService.DefaultHighDiminishingLimit);
            Add(result, "consAnchorV2CapacityHighDiminishingLimit", ConsciousnessAnchorCurveService.DefaultHighDiminishingLimit);
            Add(result, "consAnchorV2SpeedHighSafetyLimit", ConsciousnessAnchorCurveService.DefaultHighSafetyLimit);
            Add(result, "consAnchorV2CapacityHighSafetyLimit", ConsciousnessAnchorCurveService.DefaultHighSafetyLimit);
            Add(result, "consciousnessAnchorSettingsVersion", ConsciousnessAnchorV2SettingsStore.CurrentSchemaVersion);
            Add(result, "consFurtherV2LowEnabled", true);
            Add(result, "consFurtherV2HighEnabled", true);
            Add(result, "consFurtherV2LowAnchor", ConsciousnessFurtherV2Settings.DefaultLowAnchor);
            Add(result, "consFurtherV2LowFactor", ConsciousnessFurtherV2Settings.DefaultLowFactor);
            Add(result, "consFurtherV2LowCurve", (int)ConsAnchorCurveMode.Linear);
            Add(result, "consFurtherV2LowCurvature", ConsciousnessFurtherV2Settings.DefaultCurvature);
            Add(result, "consFurtherV2HighAnchor", LegacyCurveXmlDefaults.ConsFurtherHighAnchor);
            Add(result, "consFurtherV2HighFactor", LegacyCurveXmlDefaults.ConsFurtherHighFactor);
            Add(result, "consFurtherV2HighCurve", (int)ConsAnchorCurveMode.Diminishing);
            Add(result, "consFurtherV2HighCurvature", ConsciousnessFurtherV2Settings.DefaultCurvature);
            Add(result, "consFurtherV2HighDiminishingLimit", ConsciousnessFurtherV2Settings.DefaultHighLimit);
            Add(result, "consFurtherV2HighSafetyLimit", ConsciousnessFurtherV2Settings.DefaultSafetyCap);
            Add(result, "consciousnessFurtherV2Version", ConsciousnessFurtherV2SettingsStore.SchemaVersion);
            if (result.Count != ScalarCount)
                throw new InvalidOperationException("C4B2_DEFAULT_INVENTORY_DRIFT");
            return result;
        }

        private static void Add(Dictionary<string, object> map, string key, object value)
        {
            if (value == null || (value.GetType() != typeof(bool) &&
                value.GetType() != typeof(int) && value.GetType() != typeof(float)) ||
                map.ContainsKey(key))
                throw new InvalidOperationException("C4B2_INVALID_DEFAULT_CONTRACT:" + key);
            if (value is float && (Single.IsNaN((float)value) || Single.IsInfinity((float)value)))
                throw new InvalidOperationException("C4B2_NONFINITE_DEFAULT:" + key);
            map.Add(key, value);
        }

        /// <summary>
        /// B2-B raw XML semantic evidence only; detached Verse.LoadingVars and
        /// PostLoadInit are NOT run. Never set fullSemantic=PASS from this.
        /// </summary>
        internal static Result Inspect(byte[] xml, XpCustomizerSettings expected)
        {
            Result result = new Result();
            DetachedXmlStageAudit.Inspection structure = DetachedXmlStageAudit.InspectXml(xml, expected);
            if (!structure.Valid)
            {
                result.Failure = "B1_STRUCTURE:" + structure.Failure;
                return result;
            }
            try
            {
                Dictionary<string, object> fields = DetachedXmlStageAudit.BuildSerializedFields(expected);
                Dictionary<string, object> defaults = Defaults();
                if (fields.Count != ScalarCount + CollectionCount ||
                    defaults.Count != ScalarCount)
                    return Failed(result, "SCHEMA_COUNT_DRIFT");

                foreach (KeyValuePair<string, object> entry in fields)
                {
                    if (entry.Value is IList || entry.Value is IDictionary)
                    {
                        if (!CheckCollectionComparer(entry.Value))
                            return Failed(result, "NONORDINAL_DICTIONARY_OR_NULL_COLLECTION:" + entry.Key);
                        result.Collections++;
                        if (defaults.ContainsKey(entry.Key))
                            return Failed(result, "COLLECTION_IS_SCALAR:" + entry.Key);
                        continue;
                    }
                    object fallback;
                    if (!defaults.TryGetValue(entry.Key, out fallback))
                        return Failed(result, "NO_DECLARED_SCRIBE_DEFAULT:" + entry.Key);
                    if (entry.Value == null || entry.Value.GetType() != fallback.GetType() ||
                        (entry.Value is float && (Single.IsNaN((float)entry.Value) ||
                            Single.IsInfinity((float)entry.Value))))
                        return Failed(result, "BAD_SCALAR_TYPE_OR_VALUE:" + entry.Key);
                }
                foreach (string name in defaults.Keys)
                    if (!fields.ContainsKey(name) || fields[name] is IList || fields[name] is IDictionary)
                        return Failed(result, "ORPHAN_DEFAULT:" + name);

                XmlReaderSettings readerOptions = new XmlReaderSettings();
                readerOptions.DtdProcessing = DtdProcessing.Prohibit;
                readerOptions.XmlResolver = null;
                readerOptions.MaxCharactersInDocument = DetachedXmlStageAudit.MaximumBytes;
                XmlDocument doc = new XmlDocument();
                doc.XmlResolver = null;
                using (System.IO.MemoryStream stream = new System.IO.MemoryStream(xml, false))
                using (XmlReader reader = XmlReader.Create(stream, readerOptions)) doc.Load(reader);
                XmlElement data = doc.DocumentElement["ModSettings"];
                if (data == null) return Failed(result, "MISSING_MOD_SETTINGS");
                foreach (KeyValuePair<string, object> item in defaults)
                {
                    if (data[item.Key] != null)
                    {
                        result.Emitted++;
                    }
                    else
                    {
                        // This is the *Look(defaultValue)* contract, not the
                        // field initializer or current preset. Omitting an
                        // edited value is a hard failure, never an assumed default.
                        if (!item.Value.Equals(fields[item.Key]))
                            return Failed(result, "OMITTED_NONDEFAULT:" + item.Key);
                        result.OmittedVerified++;
                    }
                }
                if (result.Emitted != structure.ComparedScalars ||
                    result.OmittedVerified != structure.OmittedScalars ||
                    result.Collections != CollectionCount ||
                    result.Emitted + result.OmittedVerified != ScalarCount)
                    return Failed(result, "COUNT_OR_COVERAGE_MISMATCH");
                result.Valid = true;
                result.Unresolved = 0;
                result.Failure = "RAW_XML_SEMANTIC_MATCH_NO_VERSE_LOAD";
                return result;
            }
            catch (Exception ex)
            {
                return Failed(result, "CONTRACT_EXCEPTION:" + ex.GetType().Name);
            }
        }

        private static bool CheckCollectionComparer(object value)
        {
            if (value == null) return false;
            // Verse's Dictionary load builds a fresh Dictionary<string,T>;
            // comparer is Ordinal/default. Ignore ordering, not comparer semantics.
            Dictionary<string, bool> booleans = value as Dictionary<string, bool>;
            if (booleans != null) return IsOrdinal(booleans.Comparer);
            Dictionary<string, float> numbers = value as Dictionary<string, float>;
            if (numbers != null) return IsOrdinal(numbers.Comparer);
            return value is IList;
        }

        private static bool IsOrdinal(IEqualityComparer<string> comparer)
        {
            return Object.ReferenceEquals(comparer, EqualityComparer<string>.Default) ||
                Object.ReferenceEquals(comparer, StringComparer.Ordinal);
        }

        private static Result Failed(Result result, string cause)
        {
            result.Valid = false;
            result.Failure = cause;
            result.Unresolved = ScalarCount - result.Emitted - result.OmittedVerified;
            return result;
        }
    }
}
#endif
