using System.Collections.Generic;
using UnityEngine;
using Verse;
using XpCustomizer.Progression;

namespace XpCustomizer.UI
{
    // CPS1: same CLR type, original method bodies moved without behavioral changes.
    internal static partial class CareerProgressionSettingsUI
    {

        private static void DrawCareerDistribution(
            Listing_Standard listing,
            XpCustomizerSettings settings)
        {
            SettingsUIHelpers.DrawSectionTitle(
                listing,
                "XPC_L3B_DistributionTitle".Translate().ToString());

            SettingsUIHelpers.DrawDescription(
                listing,
                "XPC_L3B_DistributionDescription".Translate().ToString());

            SettingsUIHelpers.DrawReferenceText(
                listing,
                "XPC_L3B_BoundariesApplyNotice".Translate().ToString());

            listing.Gap(6f);


            bool useCustomDistribution =
                settings.careerXpUseCustomDistribution;

            SettingsUIHelpers.DrawRightAdjacentToggle(
                listing, "XPC_L3B_UseCustomDistribution".Translate().ToString(),
                ref useCustomDistribution);

            settings.careerXpUseCustomDistribution =
                useCustomDistribution;


            int[] normalizedSplits =
                CareerXpDistributionService.NormalizeSplitLevels(
                    settings.careerXpSplitLevels);


            SettingsUIHelpers.DrawReferenceText(
                listing,
                string.Format("XPC_L3B_CurrentBoundaries".Translate().ToString(),
                    FormatBoundaries(normalizedSplits)));

            bool weightsReset;

            bool boundariesChanged =
                CareerXpStageBoundaryEditorUI.Draw(
                    listing,
                    settings,
                    normalizedSplits,
                    out weightsReset);

            if (weightsReset)
            {
                ResetStageWeightInputStates();
            }

            if (boundariesChanged)
            {
                ResetStageLockedShareInputStates();
                normalizedSplits =
                    CareerXpDistributionService.NormalizeSplitLevels(
                        settings.careerXpSplitLevels);
            }


            int stageCount =
                normalizedSplits.Length + 1;

            EnsureStageWeightStorage(
                settings,
                stageCount);

            EnsureLockedShareStorage(settings, stageCount);
            EnsureStageWeightInputStates(stageCount);
            EnsureStageLockedShareInputStates(stageCount);


            SettingsUIHelpers.DrawReferenceText(
                listing,
                "XPC_L3B_WeightsHelp".Translate().ToString());

            listing.Gap(6f);


            CareerXpDistribution distribution =
                CareerXpDistributionService.Build(
                    settings.careerXpMultiplier,
                    settings.careerXpUseCustomDistribution,
                    normalizedSplits,
                    settings.careerXpStageWeights,
                    settings.careerXpRoundingStep,
                    settings.careerXpStageLockedShares);


            listing.Gap(4f);


            DrawStageTableHeader(
                listing);


            for (int stageIndex = 0;
                 stageIndex < distribution.StageCount;
                 stageIndex++)
            {
                NumericInputState inputState =
                    stageWeightInputs[stageIndex];
                NumericInputState lockedInput =
                    stageLockedShareInputs[stageIndex];

                float value =
                    settings.careerXpStageWeights[stageIndex];
                float lockedShare =
                    settings.careerXpStageLockedShares[stageIndex];
                if (!float.IsNaN(lockedInput.lastValue) &&
                    !Mathf.Approximately(lockedShare, lockedInput.lastValue))
                    lockedInput.buffer = null;


                if (!float.IsNaN(
                        inputState.lastValue) &&
                    !Mathf.Approximately(
                        value,
                        inputState.lastValue))
                {
                    inputState.buffer = null;
                }


                int startLevel =
                    distribution.GetStageStartLevel(stageIndex);

                int endLevel =
                    distribution.GetStageEndLevel(stageIndex);

                string label =
                    string.Format("XPC_L3B_LevelRange".Translate().ToString(),
                        startLevel, endLevel);

                // Always compare against the same user-selected boundaries.
                // Vanilla references intentionally ignore the custom total,
                // stage weights and rounding options.
                float vanillaXp =
                    CareerXpDistributionService.GetVanillaXpForRange(
                        startLevel, endLevel);

                float vanillaShare =
                    CareerXpDistributionService.GetVanillaShareForRange(
                        startLevel, endLevel);


                float maxLockedSharePct = GetMaxLockedSharePct(
                    settings.careerXpStageLockedShares, stageIndex);
                bool canStartLock = CountShareLocks(
                    settings.careerXpStageLockedShares) < stageCount - 1;

                value = DrawStageDistributionRow(
                    listing, label, value, inputState,
                    settings.careerXpUseCustomDistribution,
                    distribution.GetStageShare(stageIndex), vanillaShare,
                    distribution.GetStageXp(stageIndex), vanillaXp,
                    ref lockedShare, lockedInput,
                    canStartLock, maxLockedSharePct);


                settings.careerXpStageWeights[stageIndex] =
                    CareerXpDistributionService.NormalizeStageWeight(
                        value);

                inputState.lastValue =
                    settings.careerXpStageWeights[stageIndex];
                settings.careerXpStageLockedShares[stageIndex] =
                    lockedShare;
                lockedInput.lastValue = lockedShare;

                // Refresh the preview after every accepted field value.
                // The next row therefore sees the latest distribution,
                // and the whole table is fully synchronized by the next GUI frame.
                distribution =
                    CareerXpDistributionService.Build(
                        settings.careerXpMultiplier,
                        settings.careerXpUseCustomDistribution,
                        normalizedSplits,
                        settings.careerXpStageWeights,
                        settings.careerXpRoundingStep,
                        settings.careerXpStageLockedShares);
            }


            if (!settings.careerXpUseCustomDistribution)
            {
                listing.Gap(4f);

                SettingsUIHelpers.DrawReferenceText(
                    listing,
                    "XPC_L3B_VanillaDistributionActive".Translate().ToString());
            }


            listing.Gap(10f);

            DrawXpRoundingControls(
                listing,
                settings);
        }


        private static string FormatBoundaries(
            int[] splitLevels)
        {
            string result =
                "0";


            if (splitLevels != null)
            {
                for (int i = 0;
                     i < splitLevels.Length;
                     i++)
                {
                    result +=
                        " | " +
                        splitLevels[i];
                }
            }


            result +=
                " | 20";

            return result;
        }
    }
}
