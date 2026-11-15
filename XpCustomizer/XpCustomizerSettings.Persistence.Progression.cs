using System.Collections.Generic;
using Verse;
using XpCustomizer.Progression;
using XpCustomizer.FurtherLearning;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.Decay;
using XpCustomizer.Skilltrainer;
using XpCustomizer.BookXp;
using XpCustomizer.Gws;


namespace XpCustomizer
{
    public partial class XpCustomizerSettings
    {
        private void ExposeDecayData()
        {
            Scribe_Values.Look(ref decayByLevelEnabled, "decayByLevelEnabled", false);
            Scribe_Collections.Look(ref decayDailyByLevel, "decayDailyByLevel", LookMode.Value);
            Scribe_Values.Look(ref decaySkillMultipliersEnabled, "decaySkillMultipliersEnabled", false);
            Scribe_Collections.Look(ref decaySkillMultipliers,
                "decaySkillMultipliers", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref greatMemoryDecayReduction,
                "greatMemoryDecayReduction", 50f);
            Scribe_Values.Look(ref perfectMemoryDecayReduction,
                "perfectMemoryDecayReduction", 100f);

        }

        private void ExposeCareerData()
        {
            // ============================================================
            // Career progression
            // ============================================================

            Scribe_Values.Look(
                ref careerXpMultiplier,
                "careerXpMultiplier",
                XpCustomizerDefaults.CareerXpMultiplier);

            Scribe_Values.Look(
                ref level20ReserveMultiplier,
                "level20ReserveMultiplier",
                XpCustomizerDefaults.Level20ReserveMultiplier);

            Scribe_Values.Look(ref skillSpecificXpEnabled, "skillSpecificXpEnabled", false);
            Scribe_Collections.Look(ref skillSpecificXpMultipliers,
                "skillSpecificXpMultipliers", LookMode.Value, LookMode.Value);


            // Dynamic-stage XP distribution settings.
            //
            // Missing keys from older settings files are restored to the
            // recommended 0 -> 3 -> 7 -> 14 -> 17 -> 19 -> 20 layout in
            // PostLoadInit without touching the existing multiplier values.
            Scribe_Values.Look(
                ref careerXpDistributionDataVersion,
                "careerXpDistributionDataVersion",
                0);

            Scribe_Values.Look(
                ref careerXpUseCustomDistribution,
                "careerXpUseCustomDistribution",
                XpCustomizerDefaults.CareerXpUseCustomDistribution);

            Scribe_Values.Look(
                ref careerXpRoundingStep,
                "careerXpRoundingStep",
                XpCustomizerDefaults.CareerXpRoundingStep);

            Scribe_Collections.Look(
                ref careerXpSplitLevels,
                "careerXpSplitLevels",
                LookMode.Value);

            Scribe_Collections.Look(
                ref careerXpStageWeights,
                "careerXpStageWeights",
                LookMode.Value);

            Scribe_Collections.Look(
                ref careerXpStageLockedShares,
                "careerXpStageLockedShares",
                LookMode.Value);


        }

        private void ExposeSkilltrainerData()
        {
            // ============================================================
            // Skilltrainer
            // ============================================================

            Scribe_Values.Look(
                ref skilltrainerXpMultiplier,
                "skilltrainerXpMultiplier",
                XpCustomizerDefaults.SkilltrainerXpMultiplier);
            Scribe_Values.Look(ref skilltrainerSpecificEnabled,
                "skilltrainerSpecificEnabled", false);
            Scribe_Collections.Look(ref skilltrainerSpecificMultipliers,
                "skilltrainerSpecificMultipliers", LookMode.Value, LookMode.Value);


        }

    }
}
