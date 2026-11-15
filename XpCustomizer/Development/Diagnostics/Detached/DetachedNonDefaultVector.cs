#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using XpCustomizer.ConsciousnessLearning;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// R6-P1-D2-C4-B2-C3 representative POSITIVE vector. The input is an
    /// already-loaded settings SNAPSHOT; all mutations target a fresh clone.
    /// No Scribe, game state, Config writes, Harmony or global logging here.
    /// Values are selected inside the current PostLoadInit normalization domain,
    /// NOT to assert any pre-v4 migration/legacy conversion behavior.
    /// </summary>
    internal static class DetachedNonDefaultVector
    {
        internal const string Revision = "R6P1D2C4B2C3-NONDEFAULT-REPRESENTATIVE-20261107";
        internal const string VectorId = "REPRESENTATIVE_NONDEFAULT_V1";
        internal const int ExpectedCollectionCount = 10;
        internal const int ExpectedDictionaryCount = 6;
        internal const int ExpectedListCount = 4;
        internal const int MinimumNonDefaultScalars = 24;
        // Unknown but nonblank skill identifiers are deliberately preserved in
        // SkillSpecificXpService.NormalizeSettings, including XML special chars.
        internal const string EscapedSkillKey = "Future & <Skill> \"Quoted\"";

        internal sealed class Coverage
        {
            internal int NonDefaultScalars;
            internal int NonEmptyDictionaries;
            internal int NonEmptyLists;
            internal int NestedNonDefaultScalars;
            internal int EscapedKeys;
            internal bool Accepted;
            internal string Reason = "NOT_CHECKED";
        }

        internal static XpCustomizerSettings Build(XpCustomizerSettings original)
        {
            if (original == null) throw new ArgumentNullException("original");
            XpCustomizerSettings s = SettingsSnapshotService.CloneIndependent(original);
            // Career XP + independent skill multipliers; all values canonical.
            s.careerXpMultiplier = 1.25f;
            s.level20ReserveMultiplier = 1.125f;
            s.careerXpUseCustomDistribution = true;
            s.careerXpRoundingStep = 100;
            s.careerXpSplitLevels = new List<int> { 5, 10, 15 };
            s.careerXpStageWeights = new List<float> { 1.25f, 1.5f, 1.75f, 2f };
            s.careerXpStageLockedShares = new List<float> { -1f, 0.25f, -1f, -1f };
            s.skillSpecificXpEnabled = true;
            s.skillSpecificXpMultipliers = new Dictionary<string, float>
            {
                { "Mining", 1.25f }, { EscapedSkillKey, 1.5f }
            };
            s.skilltrainerXpMultiplier = 1.25f;
            s.skilltrainerSpecificEnabled = true;
            s.skilltrainerSpecificMultipliers = new Dictionary<string, float>
            {
                { "Shooting", 1.5f }
            };

            // Book quality + skill dictionaries are distinct from skill XP.
            s.bookXpEnabled = true;
            s.bookIntellectualEnabled = true;
            s.bookIntellectualXpPerHour = 375f;
            s.bookQualityEnabled = true;
            s.bookQualityMultipliers = new Dictionary<string, float>
            {
                { "Good", 1.25f }
            };
            s.bookSkillEnabled = true;
            s.bookSkillMultipliers = new Dictionary<string, float>
            {
                { "Shooting", 1.5f }
            };

            // GWS: known non-default skill exceptions survive its sanitizer.
            s.gwsEnabled = true;
            s.gwsLowAnchor = 0.5f;
            s.gwsHighAnchor = 2.25f;
            s.gwsLowFactor = 0.625f;
            s.gwsHighFactor = 1.1875f; // deliberate nondefault vs Scribe's legacy 1.20
            s.gwsHighCurve = 1; // Linear
            s.gwsHighDiminishingLimit = 1.5f;
            s.gwsHighTechnicalLimit = 2.5f;
            s.gwsSkillEnabled = new Dictionary<string, bool>
            {
                { "Shooting", true }, { "Mining", false }
            };

            // Learning Capacity V4: non-default strength plus manual override.
            s.learningCapacitySourceModifiersEnabled = true;
            s.learningCapacitySourceStrength = 0.875f;
            s.learningCapacityBaseDailyCap = 4200f;
            s.learningCapacityOverrideFastLearner = true;
            s.learningCapacityCustomFastLearner = 25f;
            s.furtherLearningBaseFactor = 0.25f;
            s.furtherLearningMentalThresholdEnabled = true;
            s.furtherLearningMentalThresholdStrength = 0.875f;

            // Six dictionary fields and four list fields: all nonempty.
            s.decayByLevelEnabled = true;
            s.decaySkillMultipliersEnabled = true;
            s.decayDailyByLevel = new List<float>(Decay.SkillDecayService.DefaultValues());
            s.decayDailyByLevel[12] = 150f;
            s.decayDailyByLevel[17] = 1300f;
            s.decaySkillMultipliers = new Dictionary<string, float>
            {
                { "Mining", 0.75f }
            };
            s.greatMemoryDecayReduction = 62.5f;

            // Three independent nested V2 profiles. All anchors/factors are
            // within the normalization domain; no deliberate migration here.
            s.consciousnessAffectsLearning = true;
            s.consciousnessAffectsLearningSpeed = true;
            s.consciousnessAffectsDailyCapacity = true;
            s.consciousnessAffectsFurtherLearning = true;
            s.consciousnessAnchorSpeed.LowAnchor = 0.75f;
            s.consciousnessAnchorSpeed.LowFactor = 0.625f;
            s.consciousnessAnchorSpeed.HighAnchor = 1.5f;
            s.consciousnessAnchorSpeed.HighFactor = 1.25f;
            s.consciousnessAnchorSpeed.HighDiminishingLimit = 1.75f;
            s.consciousnessAnchorSpeed.HighSafetyLimit = 2f;
            s.consciousnessAnchorCapacity.LowAnchor = 0.625f;
            s.consciousnessAnchorCapacity.LowFactor = 0.5625f;
            s.consciousnessAnchorCapacity.HighAnchor = 1.625f;
            s.consciousnessAnchorCapacity.HighFactor = 1.3125f;
            s.consciousnessAnchorCapacity.HighDiminishingLimit = 1.875f;
            s.consciousnessAnchorCapacity.HighSafetyLimit = 2f;
            s.consciousnessFurtherV2.LowAnchor = 0.75f;
            s.consciousnessFurtherV2.LowFactor = 0.6875f;
            s.consciousnessFurtherV2.HighAnchor = 1.5f;
            s.consciousnessFurtherV2.HighFactor = 1.4375f;
            s.consciousnessFurtherV2.HighDiminishingLimit = 2f;
            s.consciousnessFurtherV2.HighSafetyLimit = 2f;
            return s;
        }

        internal static Coverage Assess(XpCustomizerSettings value)
        {
            Coverage result = new Coverage();
            if (value == null) { result.Reason = "NULL"; return result; }
            SettingsCandidateValidator.Result validation = SettingsCandidateValidator.Validate(value);
            if (!validation.Accepted) { result.Reason = "VALIDATION:" + validation.Reason; return result; }
            try
            {
                Dictionary<string, object> fields = DetachedXmlStageAudit.BuildSerializedFields(value);
                Dictionary<string, object> defaults = DetachedXmlSemanticContract.Defaults();
                if (fields.Count != 141 || defaults.Count != 131)
                { result.Reason = "SCHEMA_DRIFT"; return result; }
                foreach (KeyValuePair<string, object> pair in fields)
                {
                    IDictionary dictionary = pair.Value as IDictionary;
                    IList list = pair.Value as IList;
                    if (dictionary != null)
                    {
                        if (dictionary.Count > 0) result.NonEmptyDictionaries++;
                        foreach (DictionaryEntry entry in dictionary)
                            if (entry.Key is string && ((string)entry.Key).Contains("&") &&
                                ((string)entry.Key).Contains("<")) result.EscapedKeys++;
                    }
                    else if (list != null)
                    {
                        if (list.Count > 0) result.NonEmptyLists++;
                    }
                    else
                    {
                        object fallback;
                        if (!defaults.TryGetValue(pair.Key, out fallback))
                        { result.Reason = "MISSING_DEFAULT"; return result; }
                        if (!Object.Equals(pair.Value, fallback))
                        {
                            result.NonDefaultScalars++;
                            if (pair.Key.StartsWith("consAnchorV2", StringComparison.Ordinal) ||
                                pair.Key.StartsWith("consFurtherV2", StringComparison.Ordinal))
                                result.NestedNonDefaultScalars++;
                        }
                    }
                }
                bool suitable = result.NonDefaultScalars >= MinimumNonDefaultScalars &&
                    result.NonEmptyDictionaries == ExpectedDictionaryCount &&
                    result.NonEmptyLists == ExpectedListCount &&
                    result.NestedNonDefaultScalars >= 6 && result.EscapedKeys >= 1 &&
                    value.careerXpSplitLevels.Count == 3 &&
                    value.careerXpStageWeights.Count == 4 &&
                    value.careerXpStageLockedShares.Count == 4 &&
                    value.decayDailyByLevel.Count == 21;
                result.Accepted = suitable;
                result.Reason = suitable ? "NONE" : "COVERAGE_INSUFFICIENT";
                return result;
            }
            catch (Exception ex) { result.Reason = "ASSESS_EXCEPTION_" + ex.GetType().Name; return result; }
        }
    }
}
#endif
