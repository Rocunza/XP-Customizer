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
    /// <summary>
    /// Persistent user settings for XP Customizer.
    ///
    /// This class stores settings only.
    /// Gameplay calculations belong in their respective service classes.
    /// UI drawing belongs in the UI classes.
    /// </summary>
    public partial class XpCustomizerSettings : ModSettings
    {
        // ================================================================
        // Career progression
        // ================================================================

        public float careerXpMultiplier =
            XpCustomizerDefaults.CareerXpMultiplier;
        public float level20ReserveMultiplier =
            XpCustomizerDefaults.Level20ReserveMultiplier;

        // Per SkillDef career XP scaling. Missing keys mean vanilla-neutral.
        // Data persists while the feature is disabled.
        public bool skillSpecificXpEnabled = false;
        public Dictionary<string, float> skillSpecificXpMultipliers =
            new Dictionary<string, float>();


        // Career-XP distribution settings.
        //
        // The same values drive both the settings UI and gameplay through
        // CareerXpDistributionService / XpCurve.
        private const int CurrentCareerXpDistributionDataVersion = 2;

        private int careerXpDistributionDataVersion =
            CurrentCareerXpDistributionDataVersion;

        public bool careerXpUseCustomDistribution =
            XpCustomizerDefaults.CareerXpUseCustomDistribution;

        // 0 = Exact, otherwise a supported XP rounding step.
        // The backend may temporarily use a smaller effective step when the
        // requested step is too coarse for the selected total XP.
        public int careerXpRoundingStep =
            XpCustomizerDefaults.CareerXpRoundingStep;

        public List<int> careerXpSplitLevels =
            new List<int>(
                CareerXpDistributionService
                    .GetRecommendedSplitLevels());

        public List<float> careerXpStageWeights =
            CreateDefaultCareerXpStageWeights(
                CareerXpDistributionService
                    .RecommendedStageCount);

        // -1 = unlocked; otherwise target share in 0..1.
        // One unlocked stage is always retained as the residual allocator.
        public List<float> careerXpStageLockedShares =
            CreateDefaultCareerXpStageLocks(
                CareerXpDistributionService.RecommendedStageCount);


        // ================================================================
        // Skilltrainer
        // ================================================================

        public float skilltrainerXpMultiplier =
            XpCustomizerDefaults.SkilltrainerXpMultiplier;

        // Skilltrainer-only payout multipliers, independent of skill XP requirements.
        public bool skilltrainerSpecificEnabled = false;
        public Dictionary<string, float> skilltrainerSpecificMultipliers =
            new Dictionary<string, float>();


        // ================================================================
        // Book XP V1/R3 — master switch + independent reading rewards.
        // Version 0 = pre-master Book XP V1. Keep enabled legacy sections ON
        // when introducing the master switch, but default new installs to OFF.
        // ================================================================
        private const int CurrentBookXpSettingsVersion = 1;
        private int bookXpSettingsVersion = 0;
        public bool bookXpEnabled = false;
        public bool bookIntellectualEnabled = false;
        public float bookIntellectualXpPerHour = 250f;
        public bool bookQualityEnabled = false;
        public Dictionary<string, float> bookQualityMultipliers =
            new Dictionary<string, float>();
        public bool bookSkillEnabled = false;
        public Dictionary<string, float> bookSkillMultipliers =
            new Dictionary<string, float>();

        // ================================================================
        // GWS V2: editable anchors (old XML missing both keys keeps 30% / 180%).
        // Skill dictionary stores only deviations from per-skill defaults.
        // ================================================================
        public bool gwsEnabled = false;
        public bool gwsLowEnabled = true;
        public bool gwsHighEnabled = true;
        public float gwsLowAnchor = GwsLearningService.DefaultLowAnchorGws;
        public float gwsHighAnchor = GwsLearningService.DefaultHighAnchorGws;
        public float gwsLowFactor = 0.70f;
        public float gwsHighFactor = GwsLearningService.DefaultHighFactor;
        public int gwsLowCurve = (int)GwsCurveMode.Linear;
        public int gwsHighCurve = GwsLearningService.DefaultHighCurve;
        public float gwsLowCurvature = 1f;
        public float gwsHighCurvature = GwsLearningService.DefaultHighCurvature;
        public float gwsHighDiminishingLimit = GwsLearningService.DefaultDiminishingLimit;
        public float gwsHighTechnicalLimit = 2f;
        public bool gwsApplyToReading = false;
        public Dictionary<string, bool> gwsSkillEnabled = new Dictionary<string, bool>();

        // ================================================================
        // Daily Learning Capacity
        // ================================================================

        // Version 0/1 = legacy Scaled/Custom model.
        // Version 2   = direct base capacity + optional source modifiers.
        // Version 3   = separate global strength; V2 source fields stay intact.
        // Version 4   = automatic vanilla-LF conversion + per-source final overrides.
        private const int CurrentLearningCapacityDataVersion = 4;

        private int learningCapacityDataVersion = 0;


        // Full-rate XP allowance before Further Learning applies.
        // Vanilla RimWorld = 4,000 XP.
        public float learningCapacityBaseDailyCap =
            XpCustomizerDefaults.LearningCapacityBaseDailyCap;


        // Master switch for XP Customizer' own trait/gene/hediff/precept
        // capacity modifiers. OFF means these sources do not alter the daily
        // capacity at all. Profiles can enable this switch and populate the
        // values below.
        public bool learningCapacitySourceModifiersEnabled =
            XpCustomizerDefaults.LearningCapacitySourceModifiersEnabled;

        public float learningCapacitySourceStrength =
            XpCustomizerDefaults.LearningCapacitySourceStrength;


        // ------------------------------------------------
        // Per-source capacity modifiers
        // ------------------------------------------------
        //
        // Values are percentage points applied to Base Daily Capacity.
        // Example: +15 means +15% capacity; -10 means -10% capacity.
        // These fields are inert while the master switch above is OFF.

        // Core traits
        public float learningCapacityCustomSlowLearner =
            XpCustomizerDefaults.SlowLearnerCapacity;

        public float learningCapacityCustomFastLearner =
            XpCustomizerDefaults.FastLearnerCapacity;

        public float learningCapacityCustomTooSmart =
            XpCustomizerDefaults.TooSmartCapacity;


        // Anomaly
        public float learningCapacityCustomBlissLobotomy =
            XpCustomizerDefaults.BlissLobotomyCapacity;


        // Biotech
        public float learningCapacityCustomSlowStudy =
            XpCustomizerDefaults.SlowStudyCapacity;

        public float learningCapacityCustomQuickStudy =
            XpCustomizerDefaults.QuickStudyCapacity;


        // Ideology
        public float learningCapacityCustomNeuralSupercharge =
            XpCustomizerDefaults.NeuralSuperchargeCapacity;

        public float learningCapacityCustomRanchingCentral =
            XpCustomizerDefaults.RanchingCentralCapacity;


        // Royalty
        public float learningCapacityCustomLearningAssistant =
            XpCustomizerDefaults.LearningAssistantCapacity;


        // V4: set true only for a manually overridden FINAL DLC percentage.
        // False = derive automatically from the nominal vanilla source's
        // Global Learning Factor contribution. V3 migration preserves edits.
        public bool learningCapacityOverrideSlowLearner = false;
        public bool learningCapacityOverrideFastLearner = false;
        public bool learningCapacityOverrideTooSmart = false;
        public bool learningCapacityOverrideBlissLobotomy = false;
        public bool learningCapacityOverrideSlowStudy = false;
        public bool learningCapacityOverrideQuickStudy = false;
        public bool learningCapacityOverrideNeuralSupercharge = false;
        public bool learningCapacityOverrideRanchingCentral = false;
        public bool learningCapacityOverrideLearningAssistant = false;


        // ------------------------------------------------
        // Legacy V1 fields
        // ------------------------------------------------
        //
        // These are read only so older settings can be migrated once.
        // They are not used by gameplay or the current settings UI.
        private bool legacyLearningCapacityUseCustomValues = false;
        private float legacyLearningCapacityBonusConversion = 0f;


        // ================================================================
        // Further Learning
        // ================================================================

        // Base post-cap learning efficiency.
        //
        // Vanilla RimWorld = 0.20, meaning that after the daily
        // learning threshold is exceeded, XP is gained at 20%
        // of the normal rate.
        public float furtherLearningBaseFactor =
            XpCustomizerDefaults.FurtherLearningBase;


        // Version 1: threshold-stat model replaces the old trait editor.
        // Old fields are still persisted for rollback/diagnostics; they are
        // inert in gameplay. No lossy auto-conversion is possible.
        private const int CurrentFurtherLearningMentalSettingsVersion = 1;
        private int furtherLearningMentalSettingsVersion =
            CurrentFurtherLearningMentalSettingsVersion;
        public bool furtherLearningMentalThresholdEnabled =
            XpCustomizerDefaults.FurtherLearningMentalThresholdEnabled;
        public float furtherLearningMentalThresholdStrength =
            XpCustomizerDefaults.FurtherLearningMentalStrength;

        // Version 0: settings predating the trait master switch.
        // Version 1: explicit ON/OFF stored independently of trait values.
        // Missing legacy switch keys are migrated once on load.
        private const int CurrentFurtherLearningTraitSettingsVersion = 1;
        private int furtherLearningTraitSettingsVersion =
            CurrentFurtherLearningTraitSettingsVersion;

        public bool furtherLearningTraitModifiersEnabled =
            XpCustomizerDefaults.FurtherLearningTraitModifiersEnabled;


        // Legacy trait modifiers (inactive since Mental Threshold V1)
        // were stored as percentage points.
        //
        // Example:
        // Base 20% + Iron-willed +10 = 30%.
        //
        // They are NOT relative multipliers such as 20% * 1.05.


        // ------------------------------------------------
        // Nerves
        // ------------------------------------------------

        public float furtherLearningVolatileModifier =
            XpCustomizerDefaults.VolatileFurtherLearning;

        public float furtherLearningNervousModifier =
            XpCustomizerDefaults.NervousFurtherLearning;

        public float furtherLearningSteadfastModifier =
            XpCustomizerDefaults.SteadfastFurtherLearning;

        public float furtherLearningIronWilledModifier =
            XpCustomizerDefaults.IronWilledFurtherLearning;


        // ------------------------------------------------
        // Industriousness
        // ------------------------------------------------

        public float furtherLearningSlothfulModifier =
            XpCustomizerDefaults.SlothfulFurtherLearning;

        public float furtherLearningLazyModifier =
            XpCustomizerDefaults.LazyFurtherLearning;

        public float furtherLearningHardWorkerModifier =
            XpCustomizerDefaults.HardWorkerFurtherLearning;

        public float furtherLearningIndustriousModifier =
            XpCustomizerDefaults.IndustriousFurtherLearning;


        // ------------------------------------------------
        // Neurotic drive
        // ------------------------------------------------

        public float furtherLearningNeuroticModifier =
            XpCustomizerDefaults.NeuroticFurtherLearning;

        public float furtherLearningVeryNeuroticModifier =
            XpCustomizerDefaults.VeryNeuroticFurtherLearning;


        // ================================================================
        // Consciousness Learning
        // ================================================================

        // Master switch for all XP Customizer
        // Consciousness-related skill-learning effects.
        //
        // Individual systems can still be enabled or disabled separately.
        public bool consciousnessAffectsLearning =
            XpCustomizerDefaults.ConsciousnessAffectsLearning;

        // Individual effect switches.
        public bool consciousnessAffectsLearningSpeed =
            XpCustomizerDefaults.ConsciousnessAffectsLearningSpeed;

        public bool consciousnessAffectsDailyCapacity =
            XpCustomizerDefaults.ConsciousnessAffectsDailyCapacity;

        public bool consciousnessAffectsFurtherLearning =
            XpCustomizerDefaults.ConsciousnessAffectsFurtherLearning;

        // Legacy pp curve: retained ONLY as rollback metadata. These four
        // fields are no longer used by gameplay, tooltips or the V2 UI.
        public float consciousnessFurtherPenaltyAt80 =
            XpCustomizerDefaults.ConsciousnessFurtherPenaltyAt80;

        public float consciousnessFurtherMaximumPenalty =
            XpCustomizerDefaults.ConsciousnessFurtherMaximumPenalty;

        public float consciousnessFurtherBonusAt170 =
            XpCustomizerDefaults.ConsciousnessFurtherBonusAt170;

        public float consciousnessFurtherMaximumBonus =
            XpCustomizerDefaults.ConsciousnessFurtherMaximumBonus;

        // Further Learning V2: separate post-cap multiplier profile (not pp).
        // Never share profile object or keys with Learning Speed / DLC.
        internal int consciousnessFurtherV2Version =
            ConsciousnessFurtherV2SettingsStore.SchemaVersion;
        internal ConsciousnessFurtherV2Settings consciousnessFurtherV2 =
            new ConsciousnessFurtherV2Settings();

        // Anchor V2 is the sole Consciousness Speed / Capacity curve model.
        // These profiles are serialized independently under consAnchorV2* keys.
        internal int consciousnessAnchorSettingsVersion =
            ConsciousnessAnchorV2SettingsStore.CurrentSchemaVersion;
        internal ConsAnchorV2ProfileSettings consciousnessAnchorSpeed =
            new ConsAnchorV2ProfileSettings();
        internal ConsAnchorV2ProfileSettings consciousnessAnchorCapacity =
            new ConsAnchorV2ProfileSettings();

        // ================================================================
        // Biotech skill genes
        // ================================================================

        public bool biotechCustomizationEnabled =
            XpCustomizerDefaults.BiotechCustomizationEnabled;

        public int biotechAwfulAptitude =
            XpCustomizerDefaults.BiotechAwfulAptitude;
        public int biotechPoorAptitude =
            XpCustomizerDefaults.BiotechPoorAptitude;
        public int biotechStrongAptitude =
            XpCustomizerDefaults.BiotechStrongAptitude;
        public int biotechGreatAptitude =
            XpCustomizerDefaults.BiotechGreatAptitude;

        public int biotechAwfulComplexity =
            XpCustomizerDefaults.BiotechAwfulComplexity;
        public int biotechPoorComplexity =
            XpCustomizerDefaults.BiotechPoorComplexity;
        public int biotechStrongComplexity =
            XpCustomizerDefaults.BiotechStrongComplexity;
        public int biotechGreatComplexity =
            XpCustomizerDefaults.BiotechGreatComplexity;

        public int biotechAwfulMetabolism =
            XpCustomizerDefaults.BiotechAwfulMetabolism;
        public int biotechPoorMetabolism =
            XpCustomizerDefaults.BiotechPoorMetabolism;
        public int biotechStrongMetabolism =
            XpCustomizerDefaults.BiotechStrongMetabolism;
        public int biotechGreatMetabolism =
            XpCustomizerDefaults.BiotechGreatMetabolism;


        // ================================================================
        // Decay (new settings; absent in old saves means vanilla behavior)
        // ================================================================
        public bool decayByLevelEnabled = false;
        public List<float> decayDailyByLevel = SkillDecayService.DefaultValues();
        // D3: Memory reduction percentages are always applied when the trait exists.
        // Neutral defaults exactly reproduce vanilla. Former D2 checkbox flags
        // are retired; persisted numeric percentages are retained on migration.
        public bool decaySkillMultipliersEnabled = false;
        public Dictionary<string, float> decaySkillMultipliers = new Dictionary<string, float>();
        public float greatMemoryDecayReduction = 50f;
        public float perfectMemoryDecayReduction = 100f;

        // ================================================================
        // Save / load
        // ================================================================

        public override void ExposeData()
        {
            base.ExposeData();


            ExposeDecayData();
            ExposeCareerData();
            ExposeSkilltrainerData();
            ExposeGwsData();
            ExposeBookXpData();
            ExposeLearningCapacityData();
            ExposeFurtherLearningData();
            ExposeConsciousnessData();
            ExposeBiotechData();


            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                SkillDecayService.Sanitize(this);
                DecayModifiersService.Sanitize(this);
                SkillSpecificXpService.NormalizeSettings(this);
                SkilltrainerSpecificXpService.NormalizeSettings(this);
                BookXpService.NormalizeSettings(this);
                GwsLearningService.NormalizeSettings(this);
                NormalizeBookXpMasterSettings();
                NormalizeCareerXpDistributionSettings();
                NormalizeLearningCapacitySettings();
                NormalizeFurtherLearningTraitSettings();
                NormalizeFurtherLearningMentalSettings();
                ConsciousnessAnchorV2SettingsStore.NormalizeAfterLoad(this);
                ConsciousnessFurtherV2SettingsStore.NormalizeAfterLoad(this);
            }
#if DEBUG
            // B2-C1 read-only phase observation, active only during an explicit
            // detached load; never changes settings, Scribe or normal saves.
            if (Scribe.mode == LoadSaveMode.ResolvingCrossRefs ||
                Scribe.mode == LoadSaveMode.PostLoadInit)
                DetachedVerseLoadAudit.ObservePhaseCompleted(this, Scribe.mode);
#endif
        }
    }
}
