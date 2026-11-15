using System;

namespace XpCustomizer.Progression
{
    /// <summary>
    /// Immutable result produced by CareerXpDistributionService.
    /// UI and gameplay can later consume the same calculated curve.
    /// </summary>
    internal sealed class CareerXpDistribution
    {
        private readonly float[] requirements;
        private readonly int[] boundaries;
        private readonly float[] stageWeights;
        private readonly float[] stageXp;
        private readonly float[] stageShares;


        public float TotalXp { get; }

        public int RequestedRoundingStep { get; }

        public int EffectiveRoundingStep { get; }

        public bool UsesRounding
        {
            get
            {
                return RequestedRoundingStep !=
                       CareerXpDistributionService.XpRoundingExact;
            }
        }

        public bool UsesRoundingFallback
        {
            get
            {
                return UsesRounding &&
                       EffectiveRoundingStep !=
                       RequestedRoundingStep;
            }
        }

        public int StageCount
        {
            get
            {
                return boundaries.Length - 1;
            }
        }


        internal CareerXpDistribution(
            float totalXp,
            float[] requirements,
            int[] boundaries,
            float[] stageWeights,
            float[] stageXp,
            float[] stageShares,
            int requestedRoundingStep,
            int effectiveRoundingStep)
        {
            TotalXp =
                totalXp;

            RequestedRoundingStep =
                requestedRoundingStep;

            EffectiveRoundingStep =
                effectiveRoundingStep;

            this.requirements =
                requirements;

            this.boundaries =
                boundaries;

            this.stageWeights =
                stageWeights;

            this.stageXp =
                stageXp;

            this.stageShares =
                stageShares;
        }


        public float GetRequirement(
            int startingLevel)
        {
            if (startingLevel < CareerXpDistributionService.MinLevel ||
                startingLevel >= CareerXpDistributionService.MaxLevel)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startingLevel));
            }

            return requirements[startingLevel];
        }


        public int GetStageStartLevel(
            int stageIndex)
        {
            ValidateStageIndex(
                stageIndex);

            return boundaries[stageIndex];
        }


        public int GetStageEndLevel(
            int stageIndex)
        {
            ValidateStageIndex(
                stageIndex);

            return boundaries[stageIndex + 1];
        }


        public float GetStageWeight(
            int stageIndex)
        {
            ValidateStageIndex(
                stageIndex);

            return stageWeights[stageIndex];
        }


        public float GetStageXp(
            int stageIndex)
        {
            ValidateStageIndex(
                stageIndex);

            return stageXp[stageIndex];
        }


        public float GetStageShare(
            int stageIndex)
        {
            ValidateStageIndex(
                stageIndex);

            return stageShares[stageIndex];
        }


        public int[] GetSplitLevels()
        {
            int splitCount =
                boundaries.Length - 2;

            int[] result =
                new int[splitCount];

            for (int i = 0;
                 i < splitCount;
                 i++)
            {
                result[i] =
                    boundaries[i + 1];
            }

            return result;
        }


        private void ValidateStageIndex(
            int stageIndex)
        {
            if (stageIndex < 0 ||
                stageIndex >= StageCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stageIndex));
            }
        }
    }
}
