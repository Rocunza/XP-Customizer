#if DEBUG
using System;
using System.Collections.Generic;
using Verse;

namespace XpCustomizer
{
    /// <summary>Pure candidate-only regression; never calls Verse Scribe/Log errors.</summary>
    internal static class DetachedNonDefaultVectorSelfTests
    {
        private static int checks;
        private static void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException("B2C3_PURE_CASE_FAILED:" + label);
            checks++;
        }

        internal static void Run()
        {
            checks = 0;
            XpCustomizerSettings original = new XpCustomizerSettings();
            XpCustomizerSettings baseline = SettingsSnapshotService.CloneIndependent(original);
            XpCustomizerSettings candidate = DetachedNonDefaultVector.Build(original);
            DetachedNonDefaultVector.Coverage coverage = DetachedNonDefaultVector.Assess(candidate);
            Check(coverage.Accepted && coverage.Reason == "NONE", "positive vector admitted");
            Check(coverage.NonDefaultScalars >= DetachedNonDefaultVector.MinimumNonDefaultScalars,
                "representative scalar changes");
            Check(coverage.NonEmptyDictionaries == 6 && coverage.NonEmptyLists == 4,
                "all collection families exercised");
            Check(coverage.NestedNonDefaultScalars >= 6, "three profile changes");
            Check(coverage.EscapedKeys >= 1 &&
                candidate.skillSpecificXpMultipliers.ContainsKey(DetachedNonDefaultVector.EscapedSkillKey),
                "escaped future SkillDef key");
            Check(candidate.careerXpSplitLevels.Count == 3 && candidate.careerXpSplitLevels[1] == 10,
                "ordered split levels");
            Check(candidate.careerXpStageWeights.Count == 4 &&
                candidate.careerXpStageWeights[2] == 1.75f, "ordered stage weights");
            Check(candidate.careerXpStageLockedShares.Count == 4 &&
                candidate.careerXpStageLockedShares[1] == 0.25f, "locked stage share");
            Check(candidate.decayDailyByLevel.Count == 21 &&
                candidate.decayDailyByLevel[12] == 150f, "non-default decay level value");
            Check(candidate.gwsSkillEnabled["Shooting"] &&
                !candidate.gwsSkillEnabled["Mining"], "two known GWS exceptions");
            Check(candidate.consciousnessAnchorSpeed.HighFactor == 1.25f &&
                candidate.consciousnessAnchorCapacity.HighFactor == 1.3125f &&
                candidate.consciousnessFurtherV2.HighFactor == 1.4375f,
                "distinct nested profile high factors");
            Check(candidate.gwsHighFactor == 1.1875f &&
                !DetachedXmlSemanticContract.Defaults()["gwsHighFactor"].Equals(
                    candidate.gwsHighFactor), "legacy Scribe default overridden");
            Check(candidate.learningCapacityOverrideFastLearner &&
                candidate.learningCapacityBaseDailyCap == 4200f,
                "V4 manual capacity override");
            Check(candidate.bookQualityMultipliers["Good"] == 1.25f &&
                candidate.bookSkillMultipliers["Shooting"] == 1.5f,
                "distinct book dictionaries");
            string diff;
            Check(SettingsApplyCoordinator.Equivalent(original, baseline, out diff),
                "source object untouched");
            Check(!Object.ReferenceEquals(candidate.skillSpecificXpMultipliers,
                original.skillSpecificXpMultipliers) &&
                !Object.ReferenceEquals(candidate.consciousnessAnchorSpeed,
                    original.consciousnessAnchorSpeed), "detached references");
            candidate.gwsHighFactor = 1.5f;
            Check(original.gwsHighFactor == baseline.gwsHighFactor,
                "candidate mutation isolated");
            XpCustomizerSettings empty = DetachedNonDefaultVector.Build(original);
            empty.careerXpSplitLevels = new List<int>();
            Check(!DetachedNonDefaultVector.Assess(empty).Accepted,
                "insufficient vector coverage rejected, pure only");
            Check(!DetachedNonDefaultVector.Assess(null).Accepted, "null rejected");
            Log.Message("[XPC-R6P1D2C4B2C3][NONDEFAULT_VECTOR_SELFTEST] status=PASS; checks=" +
                checks + "; scope=PURE_NO_SCRIBE; scalarThreshold=" +
                DetachedNonDefaultVector.MinimumNonDefaultScalars +
                "; dictionaries=6; lists=4; engineLoader=NOT_RUN" +
                "; liveSettingsWrites=0; scribeCalls=0; productionApply=NOT_EXERCISED_BY_THIS_TEST" +
                "; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
