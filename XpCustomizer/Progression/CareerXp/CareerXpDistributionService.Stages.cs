using System;
using System.Collections.Generic;

namespace XpCustomizer.Progression
{
    internal static partial class CareerXpDistributionService
    {

        // Stage boundaries and weights. Methods moved without behavioral changes.

        public static float NormalizeStageWeight(
            float weight)
        {
            if (float.IsNaN(weight) ||
                float.IsInfinity(weight))
            {
                return 1f;
            }

            if (weight < MinStageWeight)
            {
                return MinStageWeight;
            }

            if (weight > MaxStageWeight)
            {
                return MaxStageWeight;
            }

            return weight;
        }


        public static int[] GetRecommendedSplitLevels()
        {
            return (int[])RecommendedSplitLevels.Clone();
        }


        public static int[] GetRecommendedBoundaries()
        {
            return BuildBoundaries(
                RecommendedSplitLevels);
        }


        public static int[] NormalizeSplitLevels(
            IList<int> splitLevels)
        {
            if (splitLevels == null ||
                splitLevels.Count == 0)
            {
                return new int[0];
            }


            List<int> normalized =
                new List<int>();

            for (int i = 0;
                 i < splitLevels.Count;
                 i++)
            {
                int splitLevel =
                    splitLevels[i];

                // 0 and 20 are fixed endpoints, not editable split points.
                if (splitLevel <= MinLevel ||
                    splitLevel >= MaxLevel)
                {
                    continue;
                }

                if (!normalized.Contains(splitLevel))
                {
                    normalized.Add(splitLevel);
                }
            }


            normalized.Sort();

            return normalized.ToArray();
        }


        private static int[] BuildBoundaries(
            IList<int> normalizedSplitLevels)
        {
            int splitCount =
                normalizedSplitLevels != null
                    ? normalizedSplitLevels.Count
                    : 0;

            int[] boundaries =
                new int[splitCount + 2];

            boundaries[0] =
                MinLevel;

            for (int i = 0;
                 i < splitCount;
                 i++)
            {
                boundaries[i + 1] =
                    normalizedSplitLevels[i];
            }

            boundaries[boundaries.Length - 1] =
                MaxLevel;

            return boundaries;
        }


        private static float[] ResolveStageWeights(
            int stageCount,
            bool useCustomDistribution,
            IList<float> stageWeights)
        {
            float[] resolved =
                new float[stageCount];

            for (int stageIndex = 0;
                 stageIndex < stageCount;
                 stageIndex++)
            {
                if (!useCustomDistribution)
                {
                    resolved[stageIndex] = 1f;
                    continue;
                }

                float requestedWeight = 1f;

                if (stageWeights != null &&
                    stageIndex < stageWeights.Count)
                {
                    requestedWeight =
                        stageWeights[stageIndex];
                }

                resolved[stageIndex] =
                    NormalizeStageWeight(
                        requestedWeight);
            }

            return resolved;
        }
    }
}
