using System.Collections.Generic;

namespace XpCustomizer.LearningCapacity
{
    /// <summary>
    /// Describes how the final Daily Learning Capacity was produced.
    ///
    /// Calculation order:
    ///
    /// Base Capacity
    /// -> Dynamic source modifiers
    /// -> Dynamic Capacity
    /// -> Consciousness multiplier
    /// -> Final Capacity
    ///
    /// Contributions describe only the Dynamic source modifiers.
    /// Consciousness remains a separate multiplier.
    /// </summary>
    public sealed class LearningCapacityBreakdown
    {
        public float BaseCapacity
        {
            get;
        }


        /// <summary>
        /// Total Dynamic Capacity modifier.
        ///
        /// Example:
        /// +0.30 = +30%.
        /// </summary>
        public float DynamicModifier
        {
            get;
        }


        /// <summary>
        /// Individual sources that produced DynamicModifier.
        ///
        /// Example:
        /// Fast learner +15%
        /// Too smart +15%
        /// </summary>
        public List<LearningCapacityContribution> Contributions
        {
            get;
        }


        public float RawDynamicCapacity
        {
            get;
        }


        public float DynamicCapacity
        {
            get;
        }


        /// <summary>
        /// Consciousness multiplier applied after Dynamic Capacity.
        ///
        /// Example:
        /// 1.10 = x110%.
        /// </summary>
        public float ConsciousnessFactor
        {
            get;
        }


        public float FinalCapacity
        {
            get;
        }


        public bool WasDynamicCapacityClampedToZero
        {
            get
            {
                return RawDynamicCapacity < 0f;
            }
        }


        public LearningCapacityBreakdown(
            float baseCapacity,
            float dynamicModifier,
            float rawDynamicCapacity,
            float dynamicCapacity,
            float consciousnessFactor,
            float finalCapacity,
            List<LearningCapacityContribution> contributions = null)
        {
            BaseCapacity =
                baseCapacity;

            DynamicModifier =
                dynamicModifier;

            RawDynamicCapacity =
                rawDynamicCapacity;

            DynamicCapacity =
                dynamicCapacity;

            ConsciousnessFactor =
                consciousnessFactor;

            FinalCapacity =
                finalCapacity;

            Contributions =
                contributions ??
                new List<LearningCapacityContribution>();
        }
    }
}