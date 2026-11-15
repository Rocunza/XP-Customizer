#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.FurtherLearning;
using Verse;

namespace XpCustomizer
{
    internal static partial class XpCustomizerSelfTests
    {
        // ================================================================
        // S6-A: pure Skilltrainer XP adjustment (no Def or Pawn mutation)
        // ================================================================

        private static void TestSkilltrainerXpNoSharedMutation()
        {
            const string skillTrainer = "Neurotrainer_Construction";
            const string moddedTrainer = "Neurotrainer_ModAddedSkill";
            const float originalXp = 12345.5f;

            AssertNear(
                XpCustomizer.Skilltrainer.Patch_CompUseEffect_LearnSkill
                    .CalculateAdjustedXp(originalXp, skillTrainer, 1f),
                originalXp, 0.0001f, "S6-A vanilla-neutral trainer");
            AssertNear(
                XpCustomizer.Skilltrainer.Patch_CompUseEffect_LearnSkill
                    .CalculateAdjustedXp(originalXp, "UnrelatedLearningItem", 3f),
                originalXp, 0.0001f, "S6-A unrelated item untouched");
            AssertNear(
                XpCustomizer.Skilltrainer.Patch_CompUseEffect_LearnSkill
                    .CalculateAdjustedXp(originalXp, null, 3f),
                originalXp, 0.0001f, "S6-A missing def name untouched");
            AssertNear(
                XpCustomizer.Skilltrainer.Patch_CompUseEffect_LearnSkill
                    .CalculateAdjustedXp(originalXp, skillTrainer, 2f),
                originalXp * 2f, 0.01f, "S6-A trainer x2");
            AssertNear(
                XpCustomizer.Skilltrainer.Patch_CompUseEffect_LearnSkill
                    .CalculateAdjustedXp(originalXp, moddedTrainer, 1.23f),
                originalXp * 1.23f, 0.01f, "S6-A modded trainer 123 percent");
            AssertNear(
                XpCustomizer.Skilltrainer.Patch_CompUseEffect_LearnSkill
                    .CalculateAdjustedXp(originalXp, skillTrainer, 0f),
                originalXp * XpCustomizerDefaults.ProgressionMultiplierMin,
                0.01f, "S6-A lower clamp");
            AssertNear(
                XpCustomizer.Skilltrainer.Patch_CompUseEffect_LearnSkill
                    .CalculateAdjustedXp(originalXp, skillTrainer, 999f),
                originalXp * XpCustomizerDefaults.ProgressionMultiplierMax,
                0.5f, "S6-A upper clamp");
        }

        // S2: per-SkillDef payout scaling without shared Def writes or changes
        // to the original global-only S6 calculation.
        private static void TestSkilltrainerSpecificXpS2()
        {
            var settings = new XpCustomizerSettings();
            const string item = "Neurotrainer_Shooting";
            const float original = 50000f;
            AssertNear(
                XpCustomizer.Skilltrainer.SkilltrainerSpecificXpService.Apply(
                    original, item, "Shooting", settings),
                original, 0.001f, "S2 disabled-neutral");
            settings.skilltrainerSpecificEnabled = true;
            XpCustomizer.Skilltrainer.SkilltrainerSpecificXpService.SetConfiguredMultiplier(
                settings, "Shooting", 0.5f);
            XpCustomizer.Skilltrainer.SkilltrainerSpecificXpService.SetConfiguredMultiplier(
                settings, "Intellectual", 2f);
            AssertNear(
                XpCustomizer.Skilltrainer.SkilltrainerSpecificXpService.Apply(
                    original, item, "Shooting", settings),
                25000f, 0.001f, "S2 shooting x0.5");
            AssertNear(
                XpCustomizer.Skilltrainer.SkilltrainerSpecificXpService.Apply(
                    original * 1.5f, "Neurotrainer_Intellectual", "Intellectual", settings),
                150000f, 0.01f, "S2 global x1.5 and intellectual x2");
            AssertNear(
                XpCustomizer.Skilltrainer.SkilltrainerSpecificXpService.Apply(
                    original, "Neurotrainer_Melee", "Melee", settings),
                original, 0.001f, "S2 unconfigured skill neutral");
            AssertNear(
                XpCustomizer.Skilltrainer.SkilltrainerSpecificXpService.Apply(
                    original, "UnrelatedThing", "Shooting", settings),
                original, 0.001f, "S2 unrelated item protected");
            AssertNear(
                XpCustomizer.Skilltrainer.SkilltrainerSpecificXpService.Apply(
                    original, item, null, settings),
                original, 0.001f, "S2 unresolved skill protected");
            settings.skilltrainerSpecificEnabled = false;
            AssertNear(
                XpCustomizer.Skilltrainer.SkilltrainerSpecificXpService.Apply(
                    original, item, "Shooting", settings),
                original, 0.001f, "S2 OFF restores neutral");
            settings.skilltrainerSpecificEnabled = true;
            AssertNear(
                XpCustomizer.Skilltrainer.SkilltrainerSpecificXpService.GetConfiguredMultiplier(
                    settings, "Shooting"),
                0.5f, 0.001f, "S2 values retained through OFF");
            XpCustomizer.Skilltrainer.SkilltrainerSpecificXpService.SetConfiguredMultiplier(
                settings, "Shooting", 1f);
            AssertTrueForSkillXp(
                !settings.skilltrainerSpecificMultipliers.ContainsKey("Shooting"),
                "S2 neutral removes stored override");
            settings.skilltrainerSpecificMultipliers["UnavailableModSkill"] = 3f;
            settings.skilltrainerSpecificMultipliers["Melee"] = float.PositiveInfinity;
            XpCustomizer.Skilltrainer.SkilltrainerSpecificXpService.NormalizeSettings(settings);
            AssertTrueForSkillXp(
                settings.skilltrainerSpecificMultipliers.ContainsKey("UnavailableModSkill"),
                "S2 unknown skill preserved");
            AssertNear(
                XpCustomizer.Skilltrainer.SkilltrainerSpecificXpService.GetConfiguredMultiplier(
                    settings, "Melee"),
                1f, 0.001f, "S2 invalid value resets neutral");
        }

    }
}
#endif
