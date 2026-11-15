#if DEBUG
using System;
using System.Collections.Generic;
using RimWorld;
using XpCustomizer.Progression;
using Verse;

namespace XpCustomizer
{
    internal static class SkillSpecificXpSelfTests
    {
        public static void Run()
        {
            var settings = new XpCustomizerSettings();
            var shooting = new SkillRecord { def = new SkillDef { defName = "Shooting" } };
            var research = new SkillRecord { def = new SkillDef { defName = "Intellectual" } };
            const float original = 1000f;

            XpCustomizerSelfTests.AssertTrueForSkillXp(
                SkillSpecificXpService.Apply(original, shooting, settings) == original,
                "default pass-through");
            SkillSpecificXpService.SetConfiguredMultiplier(settings, "Shooting", .5f);
            SkillSpecificXpService.SetConfiguredMultiplier(settings, "Intellectual", 2f);
            XpCustomizerSelfTests.AssertTrueForSkillXp(
                SkillSpecificXpService.Apply(original, shooting, settings) == original,
                "disabled settings must pass through");

            settings.skillSpecificXpEnabled = true;
            Check(SkillSpecificXpService.Apply(original, shooting, settings), 500f,
                "Shooting 0.5x");
            Check(SkillSpecificXpService.Apply(original, research, settings), 2000f,
                "Research 2x");
            Check(SkillSpecificXpService.Apply(original,
                new SkillRecord { def = new SkillDef { defName = "Crafting" } },
                settings), original, "unknown/neutral skill");
            Check(SkillSpecificXpService.Apply(original, null, settings), original,
                "null record pass-through");
            SkillSpecificXpService.SetConfiguredMultiplier(settings, "Shooting", 1f);
            Check(SkillSpecificXpService.Apply(original, shooting, settings), original,
                "reset to neutral");

            settings.skillSpecificXpMultipliers = new Dictionary<string, float>
            {
                { "Shooting", float.NaN }, { "Intellectual", 1000f },
                { "FutureModSkill", 2f }, { "", 3f }
            };
            SkillSpecificXpService.NormalizeSettings(settings);
            Check(SkillSpecificXpService.GetConfiguredMultiplier(settings, "Shooting"),
                1f, "NaN must reset");
            Check(SkillSpecificXpService.GetConfiguredMultiplier(settings, "Intellectual"),
                100f, "max clamp");
            Check(SkillSpecificXpService.GetConfiguredMultiplier(settings, "FutureModSkill"),
                2f, "retain unknown SkillDef");
            XpCustomizerSelfTests.AssertTrueForSkillXp(
                !settings.skillSpecificXpMultipliers.ContainsKey(""),
                "remove invalid key");

            Log.Message("[XP Customizer] SKILLXP-S1 pure tests PASS: default/disabled " +
                "pass-through, independent SkillDefs, neutral reset, invalid-value " +
                "normalization, unknown-def retention. Live patched SkillRecord " +
                "Learn()/level-down remains unverified.");
        }

        private static void Check(float value, float expected, string label)
        {
            if (Math.Abs(value - expected) > .001f)
                throw new InvalidOperationException("SKILLXP-S1 " + label +
                    ": expected " + expected + ", got " + value);
        }
    }
}
#endif
