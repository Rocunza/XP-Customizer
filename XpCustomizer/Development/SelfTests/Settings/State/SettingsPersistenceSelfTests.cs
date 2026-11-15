#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.Progression;

namespace XpCustomizer
{
    /// <summary>
    /// R2-A: settings migration regression tests on temporary settings objects.
    /// No global ModSettings, Scribe state, game save, Def or Pawn is mutated.
    /// This is NOT an on-disk Scribe serialization / deserialization test.
    /// </summary>
    internal static class SettingsPersistenceSelfTests
    {
        public static void Run()
        {
            VerifyFreshVanillaNeutralDefaults();
            VerifyConsGwsSectionPresetIsolation();
            VerifyCareerXpVersionMigration();
            VerifyLearningCapacityVersionMigration();
            VerifyFurtherLearningTraitVersionMigration();
            VerifyFurtherLearningMentalVersionMigration();
            VerifyConsciousnessV2Storage();
        }

        private static void VerifyFreshVanillaNeutralDefaults()
        {
            var s = new XpCustomizerSettings();
            Near(s.careerXpMultiplier, 1f, "fresh career multiplier");
            Near(s.level20ReserveMultiplier, 1f, "fresh reserve multiplier");
            Near(s.skilltrainerXpMultiplier, 1f, "fresh Skilltrainer multiplier");
            Check(!s.skilltrainerSpecificEnabled &&
                s.skilltrainerSpecificMultipliers != null &&
                s.skilltrainerSpecificMultipliers.Count == 0,
                "fresh S2 Skilltrainer multiplier neutral");
            Near(s.learningCapacityBaseDailyCap, 4000f, "fresh daily capacity");
            Near(s.furtherLearningBaseFactor, 0.20f, "fresh Further Learning rate");
            Check(!s.careerXpUseCustomDistribution, "fresh career distribution must be OFF");
            Check(!s.learningCapacitySourceModifiersEnabled, "fresh capacity sources must be OFF");
            Near(s.learningCapacitySourceStrength, 1f, "fresh V4 global strength neutral");
            Check(!s.learningCapacityOverrideFastLearner, "fresh V4 source Auto by default");
            Check(!s.furtherLearningTraitModifiersEnabled, "fresh legacy Further Learning traits OFF");
            Check(!s.furtherLearningMentalThresholdEnabled, "fresh mental threshold OFF");
            Near(s.furtherLearningMentalThresholdStrength, 0.6f, "fresh mental slider mod default 60%");
            Near(XpCustomizerDefaults.FurtherLearningMentalVanillaStrength, 1f,
                "Vanilla reset retains 100% reference position");
            Near(s.consciousnessFurtherV2.LowAnchor, 0.80f, "fresh Further V2 low anchor");
            Near(s.consciousnessFurtherV2.LowFactor, 0.80f, "fresh Further V2 low factor");
            Near(s.consciousnessFurtherV2.HighAnchor, 1.20f, "fresh Further V2 high anchor");
            Near(s.consciousnessFurtherV2.HighFactor, 1.20f, "fresh Further V2 high factor");
            Check(s.consciousnessFurtherV2Version ==
                ConsciousnessFurtherV2SettingsStore.SchemaVersion,
                "fresh Further V2 schema version");
            Check(!s.consciousnessAffectsLearning, "fresh Consciousness master must be OFF");
            Check(!s.gwsEnabled, "fresh GWS master must be OFF");
            Check(s.gwsLowCurve == (int)XpCustomizer.Gws.GwsCurveMode.Linear &&
                s.gwsHighCurve == (int)XpCustomizer.Gws.GwsCurveMode.Diminishing,
                "fresh GWS low Linear / high Diminishing");
            Near(s.gwsLowAnchor, 0.30f, "fresh low anchor position 30%");
            Near(s.gwsHighAnchor, 5.00f, "fresh high anchor position 500%");
            Near(s.gwsHighFactor, 1.30f, "fresh high anchor factor x1.30");
            Near(s.gwsHighDiminishingLimit, 1.50f, "fresh diminishing limit x1.50");
            Near(s.gwsHighTechnicalLimit, 2f, "fresh high safety cap x2");
            Near(s.gwsHighCurvature, 0.40f, "fresh high curvature 40%");
            Check(!s.biotechCustomizationEnabled, "fresh Biotech customization must be OFF");
        }

        // Detached configuration objects only; never changes live mod settings.
        // Covers OFF-on-install, switch preservation, cross-tab isolation and
        // explicit Vanilla resets for both new section-level preset buttons.
        private static void VerifyConsGwsSectionPresetIsolation()
        {
            var g = new XpCustomizerSettings();
            g.gwsEnabled = false;
            g.gwsLowEnabled = false;
            g.gwsHighEnabled = true;
            g.gwsApplyToReading = true;
            g.gwsLowCurve = 2;
            g.gwsHighCurve = 0;
            g.gwsLowAnchor = 0.55f;
            g.gwsHighAnchor = 2.30f;
            g.gwsLowFactor = 0.3f;
            g.gwsHighFactor = 2.5f;
            g.gwsLowCurvature = 1.8f;
            g.gwsHighCurvature = 0.5f;
            g.gwsHighDiminishingLimit = 5f;
            g.gwsHighTechnicalLimit = 17f;
            g.gwsSkillEnabled["Shooting"] = true;
            g.consciousnessFurtherBonusAt170 = 37f;

            SettingsPresetService.ApplyGwsModDefaults(g);
            Check(!g.gwsEnabled && !g.gwsLowEnabled && g.gwsHighEnabled &&
                g.gwsApplyToReading, "GWS Mod Defaults must preserve switches");
            Near(g.gwsLowAnchor, 0.30f, "GWS Mod Defaults low position");
            Near(g.gwsHighAnchor, 5.00f, "GWS Mod Defaults high position");
            Near(g.gwsLowFactor, 0.70f, "GWS Mod Defaults low factor");
            Near(g.gwsHighFactor, 1.30f, "GWS Mod Defaults high anchor");
            Check(g.gwsLowCurve == 1 && g.gwsHighCurve == 0,
                "GWS Mod Defaults Linear low / Diminishing high");
            Near(g.gwsLowCurvature, 1f, "GWS Mod Defaults low curvature");
            Near(g.gwsHighCurvature, 0.40f, "GWS Mod Defaults high curvature");
            Near(g.gwsHighDiminishingLimit, 1.50f, "GWS Mod Defaults Diminishing limit");
            Near(g.gwsHighTechnicalLimit, 2f, "GWS Mod Defaults technical limit");
            Check(g.gwsSkillEnabled != null && g.gwsSkillEnabled.Count == 0,
                "GWS Mod Defaults restores default skill choices");
            Check(!XpCustomizer.Gws.GwsLearningService.IsSkillEnabled(g, "Social"),
                "GWS Mod Defaults leaves Social OFF by default");
            Near(g.consciousnessFurtherBonusAt170, 37f,
                "GWS Mod Defaults must not change Consciousness");

            g.gwsEnabled = true;
            SettingsPresetService.ApplyGwsModDefaults(g);
            Check(g.gwsEnabled, "GWS Mod Defaults must preserve an ON master too");
            SettingsPresetService.ApplyGwsVanillaPreset(g);
            Near(g.gwsLowAnchor, 0.30f, "GWS Vanilla restores low anchor");
            Near(g.gwsHighAnchor, 5.00f, "GWS Vanilla restores high anchor");
            Check(g.gwsHighCurve == 0 && Math.Abs(g.gwsHighTechnicalLimit - 2f) < 0.001f,
                "GWS Vanilla keeps master OFF and restores Diminishing editing preset with x2 stored safety");
            Check(!g.gwsEnabled && g.gwsLowEnabled && g.gwsHighEnabled &&
                !g.gwsApplyToReading, "GWS Vanilla must disable its master");
            Near(g.consciousnessFurtherBonusAt170, 37f,
                "GWS Vanilla must not change Consciousness");

            var c = new XpCustomizerSettings();
            c.consciousnessAffectsLearning = false;
            c.consciousnessAffectsLearningSpeed = false;
            c.consciousnessAffectsDailyCapacity = true;
            c.consciousnessAffectsFurtherLearning = false;
            c.consciousnessFurtherBonusAt170 = 123f;
            c.consciousnessFurtherV2.HighFactor = 1.75f;
            c.consciousnessFurtherV2.LowEnabled = false;
            c.consciousnessAnchorSpeed.HighFactor = 1.85f;
            c.consciousnessAnchorCapacity.LowFactor = 0.33f;
            c.gwsLowFactor = 0.42f;
            SettingsPresetService.ApplyConsciousnessModDefaults(c);
            Check(!c.consciousnessAffectsLearning &&
                !c.consciousnessAffectsLearningSpeed &&
                c.consciousnessAffectsDailyCapacity &&
                !c.consciousnessAffectsFurtherLearning,
                "Cons Mod Defaults must preserve master and all sub-switches");
            Near(c.consciousnessFurtherBonusAt170,
                XpCustomizerDefaults.ConsciousnessFurtherBonusAt170,
                "Cons Mod Defaults Further anchor");
            Near(c.consciousnessFurtherV2.HighFactor, 1.20f,
                "Cons Mod Defaults Further V2 high factor");
            Near(c.consciousnessFurtherV2.LowFactor, 0.80f,
                "Cons Mod Defaults Further V2 low factor");
            Near(c.consciousnessFurtherV2.HighDiminishingLimit, 2f,
                "Cons Mod Defaults Further V2 high diminishing limit");
            Check(c.consciousnessFurtherV2.LowCurve == (int)ConsAnchorCurveMode.Linear &&
                  c.consciousnessFurtherV2.HighCurve == (int)ConsAnchorCurveMode.Linear,
                "Cons Mod Defaults Further V2 curve modes");
            Check(!c.consciousnessFurtherV2.LowEnabled,
                "Cons Mod Defaults preserve Further low switch");
            Near(c.consciousnessAnchorSpeed.HighFactor, 1.08f,
                "Cons Mod Defaults speed V2 high factor");
            Near(c.consciousnessAnchorCapacity.LowFactor, 0.50f,
                "Cons Mod Defaults capacity V2 low factor");
            Near(c.consciousnessAnchorSpeed.HighAnchor, 1.20f,
                "Cons Mod Defaults speed high anchor");
            Near(c.consciousnessAnchorCapacity.HighAnchor, 1.20f,
                "Cons Mod Defaults capacity high anchor");
            Near(c.consciousnessAnchorCapacity.HighFactor, 1.08f,
                "Cons Mod Defaults capacity high factor");
            Near(c.consciousnessAnchorSpeed.HighDiminishingLimit, 2f,
                "Cons Mod Defaults speed diminishing limit");
            Near(c.consciousnessAnchorCapacity.HighDiminishingLimit, 2f,
                "Cons Mod Defaults capacity diminishing limit");
            Near(c.gwsLowFactor, 0.42f,
                "Cons Mod Defaults must not change GWS");

            c.consciousnessAffectsLearning = true;
            SettingsPresetService.ApplyConsciousnessModDefaults(c);
            Check(c.consciousnessAffectsLearning,
                "Cons Mod Defaults must preserve an ON master too");
            SettingsPresetService.ApplyConsciousnessVanillaPreset(c);
            Check(!c.consciousnessAffectsLearning,
                "Cons Vanilla must disable Cons master");
            Near(c.consciousnessFurtherV2.HighFactor, 1.20f,
                "Cons Vanilla resets Further V2 high factor");
            Near(c.gwsLowFactor, 0.42f,
                "Cons Vanilla must not change GWS");
        }

        private static void VerifyCareerXpVersionMigration()
        {
            var old = new XpCustomizerSettings();
            old.careerXpMultiplier = 3.25f;
            old.careerXpUseCustomDistribution = true;
            old.careerXpSplitLevels = new List<int> { 2, 19 };
            old.careerXpStageWeights = new List<float> { 7f };
            SetPrivate(old, "careerXpDistributionDataVersion", 0);
            InvokePrivate(old, "NormalizeCareerXpDistributionSettings");
            Check(!old.careerXpUseCustomDistribution, "legacy career migration must default custom OFF");
            Near(old.careerXpMultiplier, 3.25f, "legacy career must preserve XP multiplier");
            var expected = CareerXpDistributionService.GetRecommendedSplitLevels();
            Check(old.careerXpSplitLevels.Count == expected.Length,
                "legacy career must recreate recommended stage boundaries");
            for (int i = 0; i < expected.Length; i++)
                Check(old.careerXpSplitLevels[i] == expected[i], "legacy stage boundary " + i);
            Check(old.careerXpStageWeights.Count == expected.Length + 1,
                "legacy career stage weight count");
            foreach (float value in old.careerXpStageWeights)
                Near(value, 1f, "legacy stage default weight");
            InvokePrivate(old, "NormalizeCareerXpDistributionSettings");
            Near(old.careerXpMultiplier, 3.25f, "repeat legacy normalization must be idempotent");

            // A deliberately empty list is one 0->20 stage, not a missing key.
            var current = new XpCustomizerSettings();
            current.careerXpUseCustomDistribution = true;
            current.careerXpSplitLevels = new List<int>();
            current.careerXpStageWeights = new List<float> { 1.25f };
            SetPrivate(current, "careerXpDistributionDataVersion", 2);
            InvokePrivate(current, "NormalizeCareerXpDistributionSettings");
            Check(current.careerXpUseCustomDistribution, "current career custom toggle preserved");
            Check(current.careerXpSplitLevels.Count == 0, "current empty split list preserved");
            Check(current.careerXpStageWeights.Count == 1,
                "current one-stage configuration preserved");
            Near(current.careerXpStageWeights[0], 1.25f, "current precise stage weight preserved");
            InvokePrivate(current, "NormalizeCareerXpDistributionSettings");
            Check(current.careerXpSplitLevels.Count == 0,
                "repeat current normalization must preserve empty split list");
        }

        private static void VerifyLearningCapacityVersionMigration()
        {
            var oldCustom = new XpCustomizerSettings();
            oldCustom.learningCapacityBaseDailyCap = 9900f;
            oldCustom.learningCapacityCustomFastLearner = 12.5f;
            SetPrivate(oldCustom, "learningCapacityDataVersion", 1);
            SetPrivate(oldCustom, "legacyLearningCapacityUseCustomValues", true);
            InvokePrivate(oldCustom, "NormalizeLearningCapacitySettings");
            Check(oldCustom.learningCapacitySourceModifiersEnabled,
                "legacy custom capacity switch must migrate ON");
            Near(oldCustom.learningCapacityBaseDailyCap, 4000f,
                "legacy capacity base must migrate to vanilla 4000");
            Near(oldCustom.learningCapacityCustomFastLearner, 12.5f,
                "legacy custom source value preserved");
            InvokePrivate(oldCustom, "NormalizeLearningCapacitySettings");
            Near(oldCustom.learningCapacityCustomFastLearner, 12.5f,
                "repeat legacy custom migration must be idempotent");

            var oldScaled = new XpCustomizerSettings();
            SetPrivate(oldScaled, "learningCapacityDataVersion", 1);
            SetPrivate(oldScaled, "legacyLearningCapacityBonusConversion", 0.5f);
            InvokePrivate(oldScaled, "NormalizeLearningCapacitySettings");
            Check(oldScaled.learningCapacitySourceModifiersEnabled,
                "legacy positive scale must enable capacity sources");
            Near(oldScaled.learningCapacityCustomFastLearner, 37.5f,
                "legacy +75% conversion preserved");
            Near(oldScaled.learningCapacityCustomSlowLearner, -37.5f,
                "legacy -75% conversion preserved");

            var oldNeutral = new XpCustomizerSettings();
            SetPrivate(oldNeutral, "learningCapacityDataVersion", 0);
            InvokePrivate(oldNeutral, "NormalizeLearningCapacitySettings");
            Check(!oldNeutral.learningCapacitySourceModifiersEnabled,
                "legacy zero-scale must remain OFF");

            var current = new XpCustomizerSettings();
            current.learningCapacityBaseDailyCap = 1234f;
            current.learningCapacitySourceModifiersEnabled = false;
            current.learningCapacityCustomFastLearner = 25f;
            SetPrivate(current, "learningCapacityDataVersion", 2);
            SetPrivate(current, "legacyLearningCapacityUseCustomValues", true);
            SetPrivate(current, "legacyLearningCapacityBonusConversion", 1f);
            InvokePrivate(current, "NormalizeLearningCapacitySettings");
            Near(current.learningCapacityBaseDailyCap, 1234f,
                "V2 explicit base capacity preserved");
            Check(!current.learningCapacitySourceModifiersEnabled,
                "V2 explicit capacity source OFF preserved despite legacy keys");
            Near(current.learningCapacityCustomFastLearner, 25f,
                "V2 stored source value preserved while OFF");
            Near(current.learningCapacitySourceStrength, 1f,
                "V2 migrated global strength defaults to 100%");

            // V3 normalization of strength must never touch saved Custom values.
            current.learningCapacitySourceModifiersEnabled = true;
            current.learningCapacitySourceStrength = 2.5f;
            InvokePrivate(current, "NormalizeLearningCapacitySettings");
            Near(current.learningCapacitySourceStrength, 2f,
                "V3 global strength upper clamp");
            Near(current.learningCapacityCustomFastLearner, 25f,
                "V3 normalization preserves custom input");
            current.learningCapacitySourceStrength = 0f;
            InvokePrivate(current, "NormalizeLearningCapacitySettings");
            Near(current.learningCapacitySourceStrength, 0f,
                "V3 explicit zero preserved");
            Near(current.learningCapacityCustomFastLearner, 25f,
                "V3 zero strength keeps custom for later reactivation");

            // V3 persisted custom values must not silently switch to the new
            // automatic formula, including when the old slider was not 100%.
            var v3 = new XpCustomizerSettings();
            v3.learningCapacitySourceModifiersEnabled = true;
            v3.learningCapacitySourceStrength = 1.3f;
            v3.learningCapacityCustomFastLearner = 15f;
            v3.learningCapacityCustomTooSmart = 15f;
            SetPrivate(v3, "learningCapacityDataVersion", 3);
            InvokePrivate(v3, "NormalizeLearningCapacitySettings");
            Check(v3.learningCapacityOverrideFastLearner &&
                v3.learningCapacityOverrideTooSmart,
                "V3 sources migrate into independent Custom final values");
            Near(v3.learningCapacityCustomFastLearner, 19.5f,
                "V3 +15 at 130% becomes effective +19.5");
            Near(v3.learningCapacityCustomTooSmart, 19.5f,
                "V3 second source effective preserved");
            InvokePrivate(v3, "NormalizeLearningCapacitySettings");
            Near(v3.learningCapacityCustomFastLearner, 19.5f,
                "V4 repeat normalize must not double convert");
            v3.learningCapacitySourceStrength = 0f;
            InvokePrivate(v3, "NormalizeLearningCapacitySettings");
            Near(v3.learningCapacityCustomFastLearner, 19.5f,
                "V4 manual final remains fixed at slider zero");

            var v3Neutral = new XpCustomizerSettings();
            v3Neutral.learningCapacitySourceModifiersEnabled = true;
            SetPrivate(v3Neutral, "learningCapacityDataVersion", 3);
            InvokePrivate(v3Neutral, "NormalizeLearningCapacitySettings");
            Check(v3Neutral.learningCapacityOverrideFastLearner &&
                v3Neutral.learningCapacityCustomFastLearner == 0f,
                "V3 zero configured values stay zero until explicit Auto opt-in");
        }

        private static void VerifyFurtherLearningTraitVersionMigration()
        {
            var oldEnabled = new XpCustomizerSettings();
            oldEnabled.furtherLearningTraitModifiersEnabled = false;
            oldEnabled.furtherLearningIronWilledModifier = 7f;
            SetPrivate(oldEnabled, "furtherLearningTraitSettingsVersion", 0);
            oldEnabled.NormalizeFurtherLearningTraitSettings();
            Check(oldEnabled.furtherLearningTraitModifiersEnabled,
                "legacy nonzero Further Learning traits migrate ON");
            Near(oldEnabled.furtherLearningIronWilledModifier, 7f,
                "legacy Further Learning value preserved");
            oldEnabled.furtherLearningTraitModifiersEnabled = false;
            oldEnabled.NormalizeFurtherLearningTraitSettings();
            Check(!oldEnabled.furtherLearningTraitModifiersEnabled,
                "post-migration explicit trait OFF must stay OFF");

            var oldNeutral = new XpCustomizerSettings();
            SetPrivate(oldNeutral, "furtherLearningTraitSettingsVersion", 0);
            oldNeutral.NormalizeFurtherLearningTraitSettings();
            Check(!oldNeutral.furtherLearningTraitModifiersEnabled,
                "legacy zero trait values migrate OFF");
        }

        private static void VerifyFurtherLearningMentalVersionMigration()
        {
            var old = new XpCustomizerSettings();
            old.furtherLearningTraitModifiersEnabled = false; // avoid expected user-migration warning during self-tests
            old.furtherLearningVolatileModifier = -10f;
            old.furtherLearningMentalThresholdEnabled = true;
            SetPrivate(old, "furtherLearningMentalSettingsVersion", 0);
            old.NormalizeFurtherLearningMentalSettings();
            Check(!old.furtherLearningMentalThresholdEnabled,
                "old config cannot silently enable new mental threshold model");
            Near(old.furtherLearningVolatileModifier, -10f,
                "old volatile value saved for rollback");
            old.furtherLearningMentalThresholdEnabled = true;
            old.furtherLearningMentalThresholdStrength = 1.6f;
            old.NormalizeFurtherLearningMentalSettings();
            Check(old.furtherLearningMentalThresholdEnabled,
                "current mental threshold ON must survive normalization");
            Near(old.furtherLearningMentalThresholdStrength, 1.6f,
                "current slider position must survive normalization");
            old.furtherLearningMentalThresholdStrength = 9f;
            old.NormalizeFurtherLearningMentalSettings();
            Near(old.furtherLearningMentalThresholdStrength, 2f,
                "out-of-range slider settings clamped");
        }

        private static void VerifyConsciousnessV2Storage()
        {
            var s = new XpCustomizerSettings();
            s.consciousnessAnchorSpeed.LowFactor = 0.36f;
            s.consciousnessAnchorCapacity.LowFactor = 0.74f;
            s.consciousnessAnchorCapacity.HighFactor = 1.9f;
            s.consciousnessAnchorSettingsVersion = 2; // previous V2 schema
            ConsciousnessAnchorV2SettingsStore.NormalizeAfterLoad(s);
            Near(s.consciousnessAnchorSpeed.LowFactor, 0.36f,
                "V2 Speed low stored independently");
            Near(s.consciousnessAnchorCapacity.LowFactor, 0.74f,
                "V2 Capacity low stored independently");
            Near(s.consciousnessAnchorCapacity.HighFactor, 1.9f,
                "V2 Capacity high stored independently");
            Check(s.consciousnessAnchorSettingsVersion ==
                ConsciousnessAnchorV2SettingsStore.CurrentSchemaVersion,
                "V2 only schema version upgraded");
        }

        private static void SetPrivate(object instance, string fieldName, object value)
        {
            FieldInfo field = typeof(XpCustomizerSettings).GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Check(field != null, "missing version / legacy field: " + fieldName);
            field.SetValue(instance, value);
        }

        private static void InvokePrivate(object instance, string methodName)
        {
            MethodInfo method = typeof(XpCustomizerSettings).GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Check(method != null, "missing migration method: " + methodName);
            method.Invoke(instance, null);
        }

        private static void Near(float actual, float expected, string label)
        {
            if (float.IsNaN(actual) || float.IsInfinity(actual) ||
                Math.Abs(actual - expected) > 0.0001f)
                throw new InvalidOperationException(
                    "[XP Customizer] R2-A settings migration FAILED: " + label +
                    " expected=" + expected + ", actual=" + actual);
        }

        private static void Check(bool condition, string label)
        {
            if (!condition)
                throw new InvalidOperationException(
                    "[XP Customizer] R2-A settings migration FAILED: " + label);
        }
    }
}
#endif
