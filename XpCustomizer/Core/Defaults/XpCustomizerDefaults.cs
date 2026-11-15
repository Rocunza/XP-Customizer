namespace XpCustomizer
{
    /// <summary>
    /// Central location for XP Customizer install/default values
    /// and shared setting limits.
    ///
    /// The install/default state is intentionally vanilla-neutral.
    /// Optional Hard/Very Hard balance profiles will ship as independent JSON templates.
    /// </summary>
    public static class XpCustomizerDefaults
    {
        // ============================================================
        // Progression
        // ============================================================

        public const float CareerXpMultiplier = 1.00f;
        public const float Level20ReserveMultiplier = 1.00f;
        public const float SkilltrainerXpMultiplier = 1.00f;

        public const bool CareerXpUseCustomDistribution = false;
        public const int CareerXpRoundingStep = 0;
        public const float CareerXpStageWeight = 1.00f;

        public const float ProgressionMultiplierMin = 0.01f;
        public const float ProgressionMultiplierMax = 100.00f;


        // ============================================================
        // Dynamic Learning Capacity
        // ============================================================

        public const float LearningCapacityBaseDailyCap = 4000f;
        public const float LearningCapacityBaseDailyCapMin = 0f;
        public const float LearningCapacityBaseDailyCapMax = 9999999f;

        public const bool LearningCapacitySourceModifiersEnabled = false;

        // V3 global strength applies to the effective modifier only;
        // individual saved source values are never overwritten.
        public const float LearningCapacitySourceStrength = 1f;
        public const float LearningCapacitySourceStrengthMin = 0f;
        public const float LearningCapacitySourceStrengthMax = 2f;

        public const float LearningCapacityCustomMin = -100f;
        public const float LearningCapacityCustomMax = 100f;

        public const float SlowLearnerCapacity = 0f;
        public const float FastLearnerCapacity = 0f;
        public const float TooSmartCapacity = 0f;

        public const float BlissLobotomyCapacity = 0f;

        public const float SlowStudyCapacity = 0f;
        public const float QuickStudyCapacity = 0f;

        public const float NeuralSuperchargeCapacity = 0f;
        public const float LearningAssistantCapacity = 0f;
        public const float RanchingCentralCapacity = 0f;


        // ============================================================
        // Further Learning
        // ============================================================

        public const float FurtherLearningBase = 0.20f;

        // The Mental Threshold effect starts OFF (vanilla-neutral).
        // When enabled or restored to Mod Defaults, its configured strength is 60%.
        // Only the internal Vanilla reset uses the 100% reference position;
        // the Vanilla-labelled Further Learning button uses Mod Defaults (60%).
        public const bool FurtherLearningMentalThresholdEnabled = false;
        public const float FurtherLearningMentalStrength = 0.6f;
        public const float FurtherLearningMentalVanillaStrength = 1f;
        public const float FurtherLearningMentalStrengthMin = 0f;
        public const float FurtherLearningMentalStrengthMax = 2f;


        // OFF on fresh installs. Trait values remain stored while disabled.
        public const bool FurtherLearningTraitModifiersEnabled = false;

        public const float FurtherLearningBaseMin = 0f;
        public const float FurtherLearningBaseMax = 1.00f;

        public const float FurtherLearningTraitMin = -100f;
        public const float FurtherLearningTraitMax = 100f;

        public const float VolatileFurtherLearning = 0f;
        public const float NervousFurtherLearning = 0f;
        public const float SteadfastFurtherLearning = 0f;
        public const float IronWilledFurtherLearning = 0f;

        public const float SlothfulFurtherLearning = 0f;
        public const float LazyFurtherLearning = 0f;
        public const float HardWorkerFurtherLearning = 0f;
        public const float IndustriousFurtherLearning = 0f;

        public const float NeuroticFurtherLearning = 0f;
        public const float VeryNeuroticFurtherLearning = 0f;


        // ============================================================
        // Consciousness Learning
        // ============================================================

        public const bool ConsciousnessAffectsLearning = false;

        public const bool ConsciousnessAffectsLearningSpeed = true;
        public const bool ConsciousnessAffectsDailyCapacity = true;
        public const bool ConsciousnessAffectsFurtherLearning = true;


        // ================================================================
        // Consciousness - Further Learning
        // ================================================================

        public const float ConsciousnessFurtherPenaltyAt80 =
            -5f;

        public const float ConsciousnessFurtherMaximumPenalty =
            -20f;

        public const float ConsciousnessFurtherBonusAt170 =
            30f;

        public const float ConsciousnessFurtherMaximumBonus =
            50f;


        // ------------------------------------------------------------
        // Strength settings
        // ------------------------------------------------------------




        // ------------------------------------------------------------
        // High-consciousness curve
        // ------------------------------------------------------------



        // ============================================================
        // Biotech skill genes
        // ============================================================

        // Vanilla-neutral install state: do not take ownership of GeneDefs.
        public const bool BiotechCustomizationEnabled = false;

        public const int BiotechAwfulAptitude = -8;
        public const int BiotechPoorAptitude = -4;
        public const int BiotechStrongAptitude = 4;
        public const int BiotechGreatAptitude = 8;

        public const int BiotechAwfulComplexity = 1;
        public const int BiotechPoorComplexity = 1;
        public const int BiotechStrongComplexity = 2;
        public const int BiotechGreatComplexity = 2;

        public const int BiotechAwfulMetabolism = 2;
        public const int BiotechPoorMetabolism = 1;
        public const int BiotechStrongMetabolism = -1;
        public const int BiotechGreatMetabolism = -3;
    }
}
