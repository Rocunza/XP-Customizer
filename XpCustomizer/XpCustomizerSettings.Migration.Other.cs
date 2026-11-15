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
        // ================================================================
        // Book XP pre-master migration. NEVER recalculate the master after V1:
        // a deliberate OFF must remain OFF even if child settings stay enabled.
        // ================================================================
        internal void NormalizeBookXpMasterSettings()
        {
            if (bookXpSettingsVersion < CurrentBookXpSettingsVersion)
            {
                bookXpEnabled = bookIntellectualEnabled || bookQualityEnabled || bookSkillEnabled;
            }
            bookXpSettingsVersion = CurrentBookXpSettingsVersion;
        }


        // ================================================================
        // Further Learning trait-switch migration
        // ================================================================

        internal void NormalizeFurtherLearningTraitSettings()
        {
            if (furtherLearningTraitSettingsVersion <
                CurrentFurtherLearningTraitSettingsVersion)
            {
                MigrateLegacyFurtherLearningTraitModifiers();
            }

            furtherLearningTraitSettingsVersion =
                CurrentFurtherLearningTraitSettingsVersion;
        }


        internal void MigrateLegacyFurtherLearningTraitModifiers()
        {
            // Previously all stored trait values applied unconditionally.
            // Only enable the new switch if any was actually non-zero.
            furtherLearningTraitModifiersEnabled =
                FurtherLearningService.HasNonzeroTraitModifiers(this);
        }


        // ================================================================
        // Mental threshold V1 migration
        // ================================================================

        internal void NormalizeFurtherLearningMentalSettings()
        {
            if (furtherLearningMentalSettingsVersion <
                CurrentFurtherLearningMentalSettingsVersion)
            {
                // The previous trait effects cannot be mapped one-to-one
                // onto a final stat threshold; preserve them for rollback,
                // but keep the new model explicitly OFF until opted in.
                furtherLearningMentalThresholdEnabled = false;
                if (furtherLearningTraitModifiersEnabled &&
                    FurtherLearningService.HasNonzeroTraitModifiers(this))
                    Log.Warning("[XP Customizer] MTH-MIGRATION-V1: Legacy Further Learning trait bonuses are inactive under the new Mental Threshold model; stored values retained for rollback. Enable the new switch to opt in.");
            }

            furtherLearningMentalThresholdStrength =
                FurtherLearningService.NormalizeThresholdStrength(
                    furtherLearningMentalThresholdStrength);
            furtherLearningMentalSettingsVersion =
                CurrentFurtherLearningMentalSettingsVersion;
        }


        // ================================================================
        // Career XP distribution data integrity
        // ================================================================

        private void NormalizeCareerXpDistributionSettings()
        {
            // Version 0 means these keys did not exist in an older settings
            // file. Initialize the new system to the recommended layout once.
            // This also lets a deliberately empty split list remain valid in
            // every later version.
            if (careerXpDistributionDataVersion <= 0)
            {
                careerXpSplitLevels =
                    new List<int>(
                        CareerXpDistributionService
                            .GetRecommendedSplitLevels());

                careerXpStageWeights =
                    CreateDefaultCareerXpStageWeights(
                        careerXpSplitLevels.Count + 1);

                careerXpUseCustomDistribution =
                    false;

                careerXpDistributionDataVersion =
                    CurrentCareerXpDistributionDataVersion;
            }


            // A null list can still occur after a malformed or manually
            // edited settings file. An intentionally empty list is valid and
            // represents one single 0 -> 20 stage, so it must not be replaced.
            if (careerXpSplitLevels == null)
            {
                careerXpSplitLevels =
                    new List<int>(
                        CareerXpDistributionService
                            .GetRecommendedSplitLevels());
            }
            else
            {
                careerXpSplitLevels =
                    new List<int>(
                        CareerXpDistributionService
                            .NormalizeSplitLevels(
                                careerXpSplitLevels));
            }


            careerXpRoundingStep =
                CareerXpDistributionService
                    .NormalizeRoundingStep(
                        careerXpRoundingStep);


            int stageCount =
                careerXpSplitLevels.Count + 1;

            List<float> normalizedWeights =
                new List<float>(
                    stageCount);

            for (int stageIndex = 0;
                 stageIndex < stageCount;
                 stageIndex++)
            {
                float weight = 1f;

                if (careerXpStageWeights != null &&
                    stageIndex < careerXpStageWeights.Count)
                {
                    weight =
                        careerXpStageWeights[stageIndex];
                }

                normalizedWeights.Add(
                    CareerXpDistributionService
                        .NormalizeStageWeight(
                            weight));
            }

            careerXpStageWeights =
                normalizedWeights;

            List<float> normalizedLocks =
                CreateDefaultCareerXpStageLocks(stageCount);
            int countLocked = 0;
            for (int i = 0; i < stageCount; i++)
            {
                if (careerXpStageLockedShares != null &&
                    i < careerXpStageLockedShares.Count)
                {
                    normalizedLocks[i] =
                        CareerXpDistributionService.NormalizeLockedStageShare(
                            careerXpStageLockedShares[i]);
                }
                if (normalizedLocks[i] >= 0f) countLocked++;
            }
            if (countLocked == stageCount)
                normalizedLocks[stageCount - 1] = -1f;
            careerXpStageLockedShares = normalizedLocks;

            careerXpDistributionDataVersion =
                CurrentCareerXpDistributionDataVersion;
        }


        private static List<float> CreateDefaultCareerXpStageLocks(int stageCount)
        {
            List<float> locks = new List<float>(stageCount);
            for (int i = 0; i < stageCount; i++) locks.Add(-1f);
            return locks;
        }

        private static List<float> CreateDefaultCareerXpStageWeights(
            int stageCount)
        {
            List<float> weights =
                new List<float>(
                    stageCount);

            for (int i = 0;
                 i < stageCount;
                 i++)
            {
                weights.Add(
                    1f);
            }

            return weights;
        }
    }
}
