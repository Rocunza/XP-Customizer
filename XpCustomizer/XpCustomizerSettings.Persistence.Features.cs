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
    public partial class XpCustomizerSettings
    {
        private void ExposeGwsData()
        {
            // ============================================================
            // Book XP V1/R3 master switch (persisted with migration)
            // ============================================================
            // GWS V2 adds position-only anchors. Old XML without the fields
            // defaults to 30% / 180%, preserving historical missing-key behavior.
            Scribe_Values.Look(ref gwsEnabled, "gwsEnabled", false);
            Scribe_Values.Look(ref gwsLowEnabled, "gwsLowEnabled", true);
            Scribe_Values.Look(ref gwsHighEnabled, "gwsHighEnabled", true);
            Scribe_Values.Look(ref gwsLowAnchor, "gwsLowAnchor", GwsLearningService.DefaultLowAnchorGws);
            Scribe_Values.Look(ref gwsHighAnchor, "gwsHighAnchor", LegacyCurveXmlDefaults.GwsHighAnchor);
            Scribe_Values.Look(ref gwsLowFactor, "gwsLowFactor", 0.70f);
            // LEGACY-COMPAT: Do NOT update these two Scribe fallbacks to the new preset.
            // Old settings could omit x1.20 / Linear, 180%, 100% curvature and
            // x1.30 diminishing-limit values. Fresh instances use the NEW
            // Diminishing preset; historical Scribe fallbacks stay frozen.
            Scribe_Values.Look(ref gwsHighFactor, "gwsHighFactor", LegacyCurveXmlDefaults.GwsHighFactor);
            Scribe_Values.Look(ref gwsLowCurve, "gwsLowCurve", (int)GwsCurveMode.Linear);
            Scribe_Values.Look(ref gwsHighCurve, "gwsHighCurve", LegacyCurveXmlDefaults.GwsHighCurve);
            Scribe_Values.Look(ref gwsLowCurvature, "gwsLowCurvature", 1f);
            Scribe_Values.Look(ref gwsHighCurvature, "gwsHighCurvature", LegacyCurveXmlDefaults.GwsHighCurvature);
            Scribe_Values.Look(ref gwsHighDiminishingLimit, "gwsHighDiminishingLimit", LegacyCurveXmlDefaults.GwsHighDiminishingLimit);
            Scribe_Values.Look(ref gwsHighTechnicalLimit, "gwsHighTechnicalLimit", GwsLearningService.DefaultAcceleratingLimit);
            Scribe_Values.Look(ref gwsApplyToReading, "gwsApplyToReading", false);
            Scribe_Collections.Look(ref gwsSkillEnabled, "gwsSkillEnabled",
                LookMode.Value, LookMode.Value);

        }

        private void ExposeBookXpData()
        {
            Scribe_Values.Look(ref bookXpSettingsVersion, "bookXpSettingsVersion", 0);
            Scribe_Values.Look(ref bookXpEnabled, "bookXpEnabled", false);
            Scribe_Values.Look(ref bookIntellectualEnabled, "bookIntellectualEnabled", false);
            Scribe_Values.Look(ref bookIntellectualXpPerHour,
                "bookIntellectualXpPerHour", 250f);
            Scribe_Values.Look(ref bookQualityEnabled, "bookQualityEnabled", false);
            Scribe_Collections.Look(ref bookQualityMultipliers,
                "bookQualityMultipliers", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref bookSkillEnabled, "bookSkillEnabled", false);
            Scribe_Collections.Look(ref bookSkillMultipliers,
                "bookSkillMultipliers", LookMode.Value, LookMode.Value);

        }

        private void ExposeConsciousnessData()
        {
            // ============================================================
            // Consciousness Learning
            // ============================================================

            Scribe_Values.Look(
                ref consciousnessAffectsLearning,
                "consciousnessAffectsLearning",
                XpCustomizerDefaults.ConsciousnessAffectsLearning);

            Scribe_Values.Look(
                ref consciousnessAffectsLearningSpeed,
                "consciousnessAffectsLearningSpeed",
                XpCustomizerDefaults.ConsciousnessAffectsLearningSpeed);

            Scribe_Values.Look(
                ref consciousnessAffectsDailyCapacity,
                "consciousnessAffectsDailyCapacity",
                XpCustomizerDefaults.ConsciousnessAffectsDailyCapacity);

            Scribe_Values.Look(
                ref consciousnessAffectsFurtherLearning,
                "consciousnessAffectsFurtherLearning",
                XpCustomizerDefaults.ConsciousnessAffectsFurtherLearning);


            // Further Learning Consciousness curve.

            Scribe_Values.Look(
                ref consciousnessFurtherPenaltyAt80,
                "consciousnessFurtherPenaltyAt80",
                XpCustomizerDefaults.ConsciousnessFurtherPenaltyAt80);

            Scribe_Values.Look(
                ref consciousnessFurtherMaximumPenalty,
                "consciousnessFurtherMaximumPenalty",
                XpCustomizerDefaults.ConsciousnessFurtherMaximumPenalty);

            Scribe_Values.Look(
                ref consciousnessFurtherBonusAt170,
                "consciousnessFurtherBonusAt170",
                XpCustomizerDefaults.ConsciousnessFurtherBonusAt170);

            Scribe_Values.Look(
                ref consciousnessFurtherMaximumBonus,
                "consciousnessFurtherMaximumBonus",
                XpCustomizerDefaults.ConsciousnessFurtherMaximumBonus);


            // New Further V2 keys coexist with inert legacy pp keys. An old
            // Historical XML with no V2 keys uses Low80/x0.80 and High135/x1.35.
            // Fresh settings use High120/x1.20. Explicit old x0.75/x1.10 survives.
            ConsciousnessFurtherV2SettingsStore.ExposeData(this);

            // The old strengths and source selectors are intentionally not
            // serialized anymore. Existing XML may still contain those tags;
            // RimWorld Scribe simply ignores them on loading.
            ConsciousnessAnchorV2SettingsStore.ExposeData(this);

        }

        private void ExposeBiotechData()
        {
            // ============================================================
            // Biotech skill genes
            // ============================================================

            Scribe_Values.Look(
                ref biotechCustomizationEnabled,
                "biotechCustomizationEnabled",
                XpCustomizerDefaults.BiotechCustomizationEnabled);

            Scribe_Values.Look(
                ref biotechAwfulAptitude,
                "biotechAwfulAptitude",
                XpCustomizerDefaults.BiotechAwfulAptitude);

            Scribe_Values.Look(
                ref biotechPoorAptitude,
                "biotechPoorAptitude",
                XpCustomizerDefaults.BiotechPoorAptitude);

            Scribe_Values.Look(
                ref biotechStrongAptitude,
                "biotechStrongAptitude",
                XpCustomizerDefaults.BiotechStrongAptitude);

            Scribe_Values.Look(
                ref biotechGreatAptitude,
                "biotechGreatAptitude",
                XpCustomizerDefaults.BiotechGreatAptitude);


            Scribe_Values.Look(
                ref biotechAwfulComplexity,
                "biotechAwfulComplexity",
                XpCustomizerDefaults.BiotechAwfulComplexity);

            Scribe_Values.Look(
                ref biotechPoorComplexity,
                "biotechPoorComplexity",
                XpCustomizerDefaults.BiotechPoorComplexity);

            Scribe_Values.Look(
                ref biotechStrongComplexity,
                "biotechStrongComplexity",
                XpCustomizerDefaults.BiotechStrongComplexity);

            Scribe_Values.Look(
                ref biotechGreatComplexity,
                "biotechGreatComplexity",
                XpCustomizerDefaults.BiotechGreatComplexity);


            Scribe_Values.Look(
                ref biotechAwfulMetabolism,
                "biotechAwfulMetabolism",
                XpCustomizerDefaults.BiotechAwfulMetabolism);

            Scribe_Values.Look(
                ref biotechPoorMetabolism,
                "biotechPoorMetabolism",
                XpCustomizerDefaults.BiotechPoorMetabolism);

            Scribe_Values.Look(
                ref biotechStrongMetabolism,
                "biotechStrongMetabolism",
                XpCustomizerDefaults.BiotechStrongMetabolism);

            Scribe_Values.Look(
                ref biotechGreatMetabolism,
                "biotechGreatMetabolism",
                XpCustomizerDefaults.BiotechGreatMetabolism);

        }

    }
}
