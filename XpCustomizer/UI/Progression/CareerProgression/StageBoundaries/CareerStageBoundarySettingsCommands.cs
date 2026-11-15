using System.Collections.Generic;

namespace XpCustomizer.UI
{
    /// <summary>
    /// Only the three stage-boundary settings are written here. No cache,
    /// GeneDef, profile JSON, Scribe or game state mutation.
    /// </summary>
    internal static class CareerStageBoundarySettingsCommands
    {
        internal static void ApplyNormalizedBoundaries(
            XpCustomizerSettings settings,
            int[] normalized)
        {
            settings.careerXpSplitLevels =
                new List<int>(normalized);

            ResetWeights(settings, normalized.Length + 1);
            settings.careerXpStageLockedShares = new List<float>();
            for (int i = 0; i < normalized.Length + 1; i++)
                settings.careerXpStageLockedShares.Add(-1f);
        }

        internal static void ResetWeights(
            XpCustomizerSettings settings,
            int stageCount)
        {
            if (settings == null)
            {
                return;
            }

            settings.careerXpStageWeights =
                new List<float>(stageCount);

            for (int i = 0;
                 i < stageCount;
                 i++)
            {
                settings.careerXpStageWeights.Add(
                    1f);
            }
        }
    }
}
