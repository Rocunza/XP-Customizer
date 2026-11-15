namespace XpCustomizer.LearningCapacity
{
    /// <summary>
    /// Vanilla Def names recognized by Dynamic Learning Capacity
    /// when Custom mode is enabled.
    ///
    /// Keep source identification in one place so gameplay calculations
    /// do not scatter Def-name strings throughout the service.
    /// </summary>
    internal static class LearningCapacitySourceDefs
    {
        // ================================================================
        // Core traits
        // ================================================================

        public const string SlowLearnerTrait =
            "SlowLearner";

        public const string FastLearnerTrait =
            "FastLearner";

        public const string TooSmartTrait =
            "TooSmart";


        // ================================================================
        // Anomaly
        // ================================================================

        public const string BlissLobotomyHediff =
            "BlissLobotomy";


        // ================================================================
        // Biotech
        // ================================================================

        public const string SlowStudyGene =
            "Learning_Slow";

        public const string QuickStudyGene =
            "Learning_Fast";


        // ================================================================
        // Ideology
        // ================================================================

        public const string NeuralSuperchargeHediff =
            "NeuralSupercharge";

        public const string RanchingCentralPrecept =
            "Ranching_Central";


        // ================================================================
        // Royalty
        // ================================================================

        public const string LearningAssistantHediff =
            "LearningAssistant";
    }
}