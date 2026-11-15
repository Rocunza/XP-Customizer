namespace XpCustomizer.LearningCapacity
{
    /// <summary>
    /// Describes one source contribution to Dynamic Learning Capacity.
    ///
    /// Modifier is stored as a factor-style change from neutral:
    ///
    /// +0.15 = +15% capacity
    /// -0.15 = -15% capacity
    ///
    /// Consciousness is deliberately NOT represented here.
    /// It is a separate multiplier applied after Dynamic Capacity.
    /// </summary>
    public sealed class LearningCapacityContribution
    {
        public string Label
        {
            get;
        }


        public float Modifier
        {
            get;
        }


        public LearningCapacityContribution(
            string label,
            float modifier)
        {
            Label =
                label;

            Modifier =
                modifier;
        }
    }
}