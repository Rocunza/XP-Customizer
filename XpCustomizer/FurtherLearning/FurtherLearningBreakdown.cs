using System.Collections.Generic;

namespace XpCustomizer.FurtherLearning
{
    /// <summary>
    /// One typed Further Learning contribution: either an additive pp
    /// Mental adjustment or an independent Consciousness multiplier.
    ///
    /// Example: Mental threshold 17%  +10pp at 100% strength.
    ///
    /// This is display-ready calculation data.
    /// It does not perform gameplay logic by itself.
    /// </summary>
    public sealed class FurtherLearningContribution
    {
        public string Label { get; private set; }

        public float PercentagePoints { get; private set; }

        public bool IsMultiplier { get; private set; }
        public float Multiplier { get; private set; }


        public FurtherLearningContribution(
            string label,
            float percentagePoints)
        {
            Label =
                label ?? string.Empty;

            PercentagePoints = percentagePoints;
            IsMultiplier = false;
            Multiplier = 1f;
        }

        private FurtherLearningContribution(string label, float multiplier, bool multiplierKind)
        {
            Label = label ?? string.Empty;
            IsMultiplier = multiplierKind;
            Multiplier = multiplier;
            PercentagePoints = 0f;
        }

        public static FurtherLearningContribution CreateMultiplier(string label, float multiplier)
        {
            return new FurtherLearningContribution(label, multiplier, true);
        }
    }


    /// <summary>
    /// Complete explanation of a pawn's Further Learning factor.
    ///
    /// The gameplay factor and tooltip breakdown are intentionally
    /// produced from the same calculation result.
    /// </summary>
    public sealed class FurtherLearningBreakdown
    {
        public float BaseFactor { get; private set; }

        public List<FurtherLearningContribution> Contributions
        {
            get;
            private set;
        }

        public float RawFactor { get; private set; }

        public float FinalFactor { get; private set; }


        /// <summary>
        /// True when negative modifiers pushed the raw result below 0%
        /// and the gameplay minimum clamp was applied.
        /// </summary>
        public bool WasClampedToZero
        {
            get
            {
                return RawFactor < 0f;
            }
        }


        public FurtherLearningBreakdown(
            float baseFactor,
            List<FurtherLearningContribution> contributions,
            float rawFactor,
            float finalFactor)
        {
            BaseFactor =
                baseFactor;

            Contributions =
                contributions ??
                new List<FurtherLearningContribution>();

            RawFactor =
                rawFactor;

            FinalFactor =
                finalFactor;
        }
    }
}