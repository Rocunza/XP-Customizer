#if DEBUG
using System;
using System.Collections.Generic;
using XpCustomizer.Decay;

namespace XpCustomizer
{
    /// <summary>Pure Decay regression checks; does not alter game state or saved settings.</summary>
    internal static class DecaySelfTests
    {
        internal static void Run()
        {
            var defaults = SkillDecayService.DefaultValues();
            AssertTrue(defaults.Count == 21, "21 decay levels");
            AssertTrue(!new XpCustomizerSettings().decayByLevelEnabled, "fresh decay OFF");
            for (int level = 0; level <= 20; level++)
            {
                AssertNear(defaults[level], SkillDecayService.VanillaDaily[level], "vanilla daily " + level);
            }
            var settings = new XpCustomizerSettings();
            AssertNear(SkillDecayService.LossPerInterval(settings, 10) * 300f, 30f, "level 10");
            AssertNear(SkillDecayService.LossPerInterval(settings, 20) * 300f, 3600f, "level 20");
            AssertNear(SkillDecayService.LossPerInterval(settings, 9), 0f, "vanilla low level");
            settings.decayDailyByLevel[4] = 150f;
            AssertNear(SkillDecayService.LossPerInterval(settings, 4), 0.5f, "custom low level");
            settings.decayDailyByLevel[20] = 9000f;
            AssertNear(SkillDecayService.LossPerInterval(settings, 20), 30f, "custom high level");
            AssertNear(SkillDecayService.LossPerInterval(settings, 20) * 0.5f, 15f, "great-memory multiplier arithmetic");
            AssertNear(SkillDecayService.LossPerInterval(settings, 0), 0f, "zero level protected");
            AssertNear(SkillDecayService.LossPerInterval(settings, 21), 0f, "above cap safe");
            settings.decayDailyByLevel = new List<float> { 999f, float.NaN, -5f, float.PositiveInfinity, 10000000f };
            SkillDecayService.Sanitize(settings);
            AssertTrue(settings.decayDailyByLevel.Count == 21, "malformed list repaired");
            AssertNear(settings.decayDailyByLevel[0], 0f, "zero forcibly protected");
            AssertNear(settings.decayDailyByLevel[1], 0f, "NaN sanitized");
            AssertNear(settings.decayDailyByLevel[2], 0f, "negative sanitized");
            AssertNear(settings.decayDailyByLevel[3], 0f, "Infinity sanitized");
            AssertNear(settings.decayDailyByLevel[4], SkillDecayService.MaxDaily, "upper clamp");
            AssertNear(settings.decayDailyByLevel[20], 3600f, "missing tail default");
            SkillDecayService.Sanitize(settings);
            AssertNear(settings.decayDailyByLevel[4], SkillDecayService.MaxDaily, "sanitize idempotent");
            VerifyD3NumericMemoryOptions();
            VerifyD3SkillNormalizationAndResets();
            DecayPolicySelfTests.Run();
            LogPass();
        }


        private static void VerifyD3NumericMemoryOptions()
        {
            var s = new XpCustomizerSettings();
            AssertTrue(!s.decaySkillMultipliersEnabled, "skill multipliers default OFF");
            AssertNear(s.greatMemoryDecayReduction, 50f, "Great default is vanilla");
            AssertNear(s.perfectMemoryDecayReduction, 100f, "Perfect default is vanilla");
            AssertNear(DecayModifiersService.DailyLoss(s, 20, "Shooting", false, false, true),
                3600f, "default level 20 unchanged");
            AssertNear(DecayModifiersService.DailyLoss(s, 20, "Shooting", true, false, true),
                1800f, "vanilla Great Memory 50% reduction");
            AssertNear(DecayModifiersService.DailyLoss(s, 20, "Shooting", true, true, true),
                0f, "vanilla Perfect Memory priority");
            AssertNear(DecayModifiersService.DailyLoss(s, 20, "Shooting", true, true, false),
                1800f, "Anomaly gate matches vanilla");
            AssertTrue(!DecayModifiersService.NeedsOverride(s, 20, "Shooting", false, false, true),
                "all neutral permits original Interval");

            AssertTrue(!DecayModifiersService.NeedsOverride(s, 20, "Shooting", true, false, true),
                "neutral Great Memory still vanilla passthrough");
            s.greatMemoryDecayReduction = 0f;
            AssertNear(DecayModifiersService.DailyLoss(s, 20, "Shooting", true, false, true),
                3600f, "Great reduction 0%");
            s.greatMemoryDecayReduction = 90f;
            AssertNear(DecayModifiersService.DailyLoss(s, 20, "Shooting", true, false, true),
                360f, "Great reduction 90%");
            s.greatMemoryDecayReduction = 100f;
            AssertNear(DecayModifiersService.DailyLoss(s, 20, "Shooting", true, false, true),
                0f, "Great reduction 100%");
            AssertTrue(DecayModifiersService.NeedsOverride(s, 20, "Shooting", true, false, true),
                "Great value change triggers override without checkbox");

            s.perfectMemoryDecayReduction = 90f;
            AssertNear(DecayModifiersService.DailyLoss(s, 20, "Shooting", true, true, true),
                360f, "Perfect 90% overrides Great 100%");
            s.perfectMemoryDecayReduction = 0f;
            AssertNear(DecayModifiersService.DailyLoss(s, 20, "Shooting", true, true, true),
                3600f, "Perfect reduction 0%");
            s.perfectMemoryDecayReduction = 100f;
            AssertNear(DecayModifiersService.DailyLoss(s, 20, "Shooting", false, true, true),
                0f, "Perfect reduction 100%");
            AssertTrue(!DecayModifiersService.NeedsOverride(s, 20, "Shooting", true, true, true),
                "Perfect vanilla priority ignores custom Great");
            s.perfectMemoryDecayReduction = 90f;
            AssertTrue(DecayModifiersService.NeedsOverride(s, 20, "Shooting", false, true, true),
                "Perfect 90 value triggers override without checkbox");
            AssertTrue(!DecayModifiersService.NeedsOverride(s, 20, "Shooting", false, true, false),
                "Perfect 90 ignored without Anomaly");
            s.perfectMemoryDecayReduction = 100f;
            s.greatMemoryDecayReduction = 50f;
            AssertTrue(!DecayModifiersService.NeedsOverride(s, 20, "Shooting", true, false, true),
                "both neutral memory values permit vanilla original");
            AssertNear(DecayModifiersService.DailyLoss(s, 0, "Shooting", false, false, true),
                0f, "level 0 protected regardless of options");

            s.decaySkillMultipliersEnabled = true;
            DecayModifiersService.SetSkillMultiplier(s, "Shooting", 0.5f);
            AssertNear(DecayModifiersService.DailyLoss(s, 20, "Shooting", false, false, true),
                1800f, "Shooting 0.5x independent");
            AssertNear(DecayModifiersService.DailyLoss(s, 20, "Melee", false, false, true),
                3600f, "other skill unchanged");
            AssertTrue(DecayModifiersService.NeedsOverride(s, 20, "Shooting", false, false, true),
                "Shooting affected");
            AssertTrue(!DecayModifiersService.NeedsOverride(s, 20, "Melee", false, false, true),
                "Melee passthrough");
            s.decaySkillMultipliersEnabled = false;
            AssertNear(DecayModifiersService.DailyLoss(s, 20, "Shooting", false, false, true),
                3600f, "skill OFF preserves vanilla despite stored value");
            s.decaySkillMultipliersEnabled = true;
            DecayModifiersService.SetSkillMultiplier(s, "Shooting", 0f);
            AssertNear(DecayModifiersService.DailyLoss(s, 20, "Shooting", false, false, true),
                0f, "skill zero grants exemption");
            DecayModifiersService.SetSkillMultiplier(s, "Shooting", 0.5f);
            s.greatMemoryDecayReduction = 90f;
            AssertNear(DecayModifiersService.DailyLoss(s, 20, "Shooting", true, false, true),
                180f, "skill x memory multiplied once");
            s.decayByLevelEnabled = true;
            s.decayDailyByLevel[12] = 200f;
            AssertNear(DecayModifiersService.DailyLoss(s, 12, "Shooting", true, false, true),
                10f, "level custom x skill x memory independent");
            AssertNear(DecayModifiersService.LossPerInterval(s, 12, "Shooting", true, false, true)
                * SkillDecayService.IntervalsPerDay, 10f, "interval conversion D3");
            s.decayByLevelEnabled = false;
            AssertNear(DecayModifiersService.DailyLoss(s, 12, "Shooting", true, false, true),
                6f, "level OFF falls back to vanilla before factors");
        }

        private static void VerifyD3SkillNormalizationAndResets()
        {
            var s = new XpCustomizerSettings();
            s.decaySkillMultipliersEnabled = true;
            s.decaySkillMultipliers["DisabledModSkill"] = 2.5f;
            s.decaySkillMultipliers["Shooting"] = float.PositiveInfinity;
            s.decaySkillMultipliers["Melee"] = -20f;
            s.decaySkillMultipliers["Medical"] = 1f;
            s.greatMemoryDecayReduction = 33.33333f;
            s.perfectMemoryDecayReduction = float.NaN;
            DecayModifiersService.Sanitize(s);
            AssertNear(s.greatMemoryDecayReduction, 33.33f, "memory precision two decimals");
            AssertNear(s.perfectMemoryDecayReduction, 100f, "invalid perfect fallback");
            AssertNear(DecayModifiersService.ConfiguredSkillMultiplier(s, "DisabledModSkill"),
                2.5f, "disabled mod SkillDef retained");
            AssertNear(DecayModifiersService.ConfiguredSkillMultiplier(s, "Shooting"),
                1f, "NaN/Infinity skill neutral");
            AssertNear(DecayModifiersService.ConfiguredSkillMultiplier(s, "Melee"),
                0f, "negative clamped zero");
            AssertTrue(!s.decaySkillMultipliers.ContainsKey("Medical"), "neutral key removed");
            int count = DecayModifiersService.ModifiedCount(s);
            DecayModifiersService.Sanitize(s);
            AssertTrue(count == DecayModifiersService.ModifiedCount(s),
                "D3 sanitize idempotent");
            DecayModifiersService.SetSkillMultiplier(s, "DisabledModSkill", 1f);
            AssertNear(DecayModifiersService.ConfiguredSkillMultiplier(s, "DisabledModSkill"),
                1f, "1x reset removes custom value");
            AssertNear(DecayModifiersService.NormalizeReduction(-50f, 50f), 0f,
                "memory lower clamp");
            AssertNear(DecayModifiersService.NormalizeReduction(155f, 50f), 100f,
                "memory upper clamp");
        }

        private static void AssertTrue(bool value, string name)
        {
            if (!value) throw new InvalidOperationException("[XP Customizer] DECAY-D3 failed: " + name);
        }
        private static void AssertNear(float value, float expected, string name)
        {
            AssertTrue(!float.IsNaN(value) && Math.Abs(value - expected) < 0.005f,
                name + ": " + value + " != " + expected);
        }
        private static void LogPass()
        {
            Verse.Log.Message("[XP Customizer] DECAY-D3 pure tests PASS: vanilla passthrough, level/skill/memory independence, memory 0/90/100%, Perfect priority/Anomaly gate, 0-level, skill zero, 2-decimal precision, unknown SkillDefs and normalization. Live Pawn/foreign Harmony interaction NOT simulated.");
        }
    }
}
#endif
