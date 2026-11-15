#if DEBUG
using System;
using System.Collections.Generic;
using XpCustomizer.Decay;

namespace XpCustomizer
{
    // Detached DP1 contract tests: no pawn, save, settings file or game tick writes.
    internal static class DecayPolicySelfTests
    {
        internal static void Run()
        {
            var s = new XpCustomizerSettings();
            int checks = 0;
            for (int level = 0; level <= 20; level++)
            {
                AssertReasons(s, level, "Shooting", false, false, true,
                    DecayOverrideReason.None); checks++;
                s.decayByLevelEnabled = true;
                AssertReasons(s, level, "Shooting", false, false, true,
                    DecayOverrideReason.None); checks++;
                s.decayByLevelEnabled = false;
            }
            s.decayByLevelEnabled = true;
            s.decayDailyByLevel[20] = 1800f;
            AssertReasons(s, 20, "Shooting", false, false, true,
                DecayOverrideReason.Level); checks++;
            AssertReasons(s, 19, "Shooting", false, false, true,
                DecayOverrideReason.None); checks++;
            AssertReasons(s, 0, "Shooting", false, false, true,
                DecayOverrideReason.None); checks++;
            s.decayDailyByLevel[20] = 0f;
            AssertReasons(s, 20, "Shooting", false, false, true,
                DecayOverrideReason.Level); checks++;
            AssertNear(DecayModifiersService.LossPerInterval(s, 20, "Shooting", false, false, true),
                0f, "explicit level zero is not neutral"); checks++;
            s.decayDailyByLevel[20] = SkillDecayService.VanillaDaily[20];
            s.decayDailyByLevel[5] = 12f;
            AssertReasons(s, 5, "Shooting", false, false, true,
                DecayOverrideReason.Level); checks++;
            AssertReasons(s, 6, "Shooting", false, false, true,
                DecayOverrideReason.None); checks++;
            s.decayByLevelEnabled = false;
            AssertReasons(s, 5, "Shooting", false, false, true,
                DecayOverrideReason.None); checks++;

            s.decaySkillMultipliersEnabled = true;
            DecayModifiersService.SetSkillMultiplier(s, "Shooting", 0f);
            AssertReasons(s, 20, "Shooting", false, false, true,
                DecayOverrideReason.Skill); checks++;
            AssertReasons(s, 20, "Melee", false, false, true,
                DecayOverrideReason.None); checks++;
            AssertReasons(s, 0, "Shooting", false, false, true,
                DecayOverrideReason.None); checks++;
            s.decaySkillMultipliersEnabled = false;
            AssertReasons(s, 20, "Shooting", false, false, true,
                DecayOverrideReason.None); checks++;
            s.decaySkillMultipliersEnabled = true;
            DecayModifiersService.SetSkillMultiplier(s, "Shooting", 0.5f);
            s.decayByLevelEnabled = true;
            s.decayDailyByLevel[20] = 1800f;
            AssertReasons(s, 20, "Shooting", false, false, true,
                DecayOverrideReason.Level | DecayOverrideReason.Skill); checks++;
            AssertReasons(s, 19, "Shooting", false, false, true,
                DecayOverrideReason.Skill); checks++;
            AssertReasons(s, 20, null, false, false, true,
                DecayOverrideReason.None); checks++;
            s.greatMemoryDecayReduction = 90f;
            AssertReasons(s, 20, "Shooting", true, false, true,
                DecayOverrideReason.Level | DecayOverrideReason.Skill | DecayOverrideReason.Memory); checks++;
            s.perfectMemoryDecayReduction = 90f;
            AssertReasons(s, 20, "Shooting", true, true, true,
                DecayOverrideReason.Level | DecayOverrideReason.Skill | DecayOverrideReason.Memory); checks++;
            s.decaySkillMultipliersEnabled = false;
            s.decayByLevelEnabled = false;
            AssertReasons(s, 20, "Shooting", true, true, false,
                DecayOverrideReason.Memory); checks++; // Great applies without Anomaly.
            AssertReasons(s, 20, "Shooting", false, true, false,
                DecayOverrideReason.None); checks++;
            AssertReasons(s, 20, "Shooting", false, true, true,
                DecayOverrideReason.Memory); checks++;
            s.perfectMemoryDecayReduction = 100f;
            AssertReasons(s, 20, "Shooting", true, true, true,
                DecayOverrideReason.None); checks++; // custom Great suppressed by Perfect.
            s.greatMemoryDecayReduction = 50f;
            s.decayByLevelEnabled = true;
            s.decayDailyByLevel[20] = float.PositiveInfinity;
            AssertReasons(s, 20, "Shooting", false, false, true,
                DecayOverrideReason.None); checks++;
            s.decayDailyByLevel[20] = float.NaN;
            AssertReasons(s, 20, "Shooting", false, false, true,
                DecayOverrideReason.None); checks++;
            s.decayDailyByLevel = new List<float> { 100f };
            AssertReasons(s, 20, "Shooting", false, false, true,
                DecayOverrideReason.None); checks++;

            Verse.Log.Message("[XPC-DP1][POLICY_SELFTEST] status=PASS; revision=" +
                DecayOverridePolicy.Revision + "; checks=" + checks +
                "; scope=DETACHED_PURE; livePawnXp=NOT_RUN; foreignHarmony=NOT_SIMULATED; " +
                "settingsWrites=0; profileIO=0; build=" + XpCustomizerMod.BuildId);
        }

        private static void AssertReasons(XpCustomizerSettings s, int level, string skill,
            bool great, bool perfect, bool anomaly, DecayOverrideReason expected)
        {
            DecayOverrideReason actual = DecayOverridePolicy.GetReasons(
                s, level, skill, great, perfect, anomaly);
            if (actual != expected ||
                DecayModifiersService.NeedsOverride(s, level, skill, great, perfect, anomaly) !=
                    (expected != DecayOverrideReason.None))
                throw new InvalidOperationException("DP1 policy mismatch: level=" + level +
                    "; skill=" + skill + "; expected=" + expected + "; actual=" + actual);
        }

        private static void AssertNear(float actual, float expected, string label)
        {
            if (float.IsNaN(actual) || Math.Abs(actual - expected) > 0.005f)
                throw new InvalidOperationException("DP1 " + label + ": " + actual + " != " + expected);
        }
    }
}
#endif
