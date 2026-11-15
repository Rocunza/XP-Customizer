using System.Collections.Generic;
using XpCustomizer.Progression;
using XpCustomizer.BookXp;

namespace XpCustomizer
{
    internal static partial class SettingsPresetService
    {
        // ================================================================
        // Progression
        // ================================================================


        public static void ResetProgressionToVanilla()
        {
            XpCustomizerSettings settings =
                XpCustomizerMod.Settings;

            if (settings == null)
            {
                return;
            }

            settings.careerXpMultiplier =
                XpCustomizerDefaults.CareerXpMultiplier;
            settings.skillSpecificXpEnabled = false;
            settings.skillSpecificXpMultipliers = new Dictionary<string, float>();

            settings.level20ReserveMultiplier =
                XpCustomizerDefaults.Level20ReserveMultiplier;

            settings.skilltrainerXpMultiplier =
                XpCustomizerDefaults.SkilltrainerXpMultiplier;
            settings.skilltrainerSpecificEnabled = false;
            settings.skilltrainerSpecificMultipliers = new Dictionary<string, float>();
            ResetBookXpToVanilla();

            settings.careerXpUseCustomDistribution =
                XpCustomizerDefaults.CareerXpUseCustomDistribution;

            settings.careerXpRoundingStep =
                XpCustomizerDefaults.CareerXpRoundingStep;

            settings.careerXpSplitLevels =
                new List<int>(
                    CareerXpDistributionService
                        .GetRecommendedSplitLevels());

            settings.careerXpStageWeights =
                new List<float>();

            for (int i = 0;
                 i < CareerXpDistributionService.RecommendedStageCount;
                 i++)
            {
                settings.careerXpStageWeights.Add(
                    XpCustomizerDefaults.CareerXpStageWeight);
            }
            settings.careerXpStageLockedShares = new List<float>();
            for (int i = 0; i < CareerXpDistributionService.RecommendedStageCount; i++)
                settings.careerXpStageLockedShares.Add(-1f);
        }


        public static void ResetBookXpToVanilla()
        {
            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            if (settings == null) return;
            settings.bookXpEnabled = false;
            settings.bookIntellectualEnabled = false;
            settings.bookIntellectualXpPerHour = BookXpService.VanillaIntellectualXpPerHour;
            settings.bookQualityEnabled = false;
            settings.bookQualityMultipliers = new Dictionary<string, float>();
            settings.bookSkillEnabled = false;
            settings.bookSkillMultipliers = new Dictionary<string, float>();
        }
    }
}
