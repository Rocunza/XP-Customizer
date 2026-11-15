#if DEBUG
using System;
using System.Reflection;
using Verse;
using XpCustomizer.ConsciousnessLearning;

namespace XpCustomizer
{
    /// <summary>
    /// H1: preserve the live Cons section Mod Defaults contract while
    /// retiring unused preset source. Detached settings only: NO real UI,
    /// Scribe XML, profile disk, GeneDef, or game effect writes.
    /// </summary>
    internal static class LegacyPresetRetirementH1SelfTests
    {
        private static int checks;
        private static void Assert(bool condition, string label)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(
                "[XPC-H1] " + label);
        }

        private static void Approx(float actual, float expected, string label)
        {
            Assert(!float.IsNaN(actual) && !float.IsInfinity(actual) &&
                Math.Abs(actual - expected) < 0.0001f, label);
        }

        internal static void Run()
        {
            checks = 0;
            // An old entry point must not accidentally reappear on the
            // service via another partial implementation or stale compile.
            string[] retiredMethods = {
                "ResetAllToProgressiveDefaults",
                "ResetProgressionToProgressiveDefaults",
                "ResetLearningToProgressiveDefaults",
                "ResetLearningCapacityToProgressiveDefaults",
                "ResetFurtherLearningToProgressiveDefaults",
                "ResetConsciousnessLearningToProgressiveDefaults",
                "ApplyConsciousnessProgressiveDefaults",
                "ResetBiotechToProgressiveDefaults"
            };
            for (int i = 0; i < retiredMethods.Length; i++)
                Assert(typeof(SettingsPresetService).GetMethod(retiredMethods[i],
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) == null,
                    "retired method must remain absent: " + retiredMethods[i]);

            // Stable old Cons Mod Defaults behavior across master ON/OFF
            // and Further V2 switch combinations; the two sibling V2
            // profiles historically have their own Low/High reset to ON.
            for (int combination = 0; combination < 4; combination++)
            {
                var s = new XpCustomizerSettings();
                bool master = (combination & 1) != 0;
                bool low = (combination & 2) != 0;
                s.consciousnessAffectsLearning = master;
                s.consciousnessAffectsLearningSpeed = false;
                s.consciousnessAffectsDailyCapacity = true;
                s.consciousnessAffectsFurtherLearning = false;
                s.consciousnessFurtherV2.LowEnabled = low;
                s.consciousnessFurtherV2.HighEnabled = !low;
                s.consciousnessAnchorSpeed.LowEnabled = false;
                s.consciousnessAnchorSpeed.HighEnabled = false;
                s.consciousnessAnchorCapacity.LowEnabled = false;
                s.consciousnessAnchorCapacity.HighEnabled = false;
                s.consciousnessAnchorSpeed.HighFactor = 3.0f;
                s.consciousnessAnchorCapacity.LowFactor = 0.1f;
                s.consciousnessFurtherV2.HighFactor = 1.8f;
                s.consciousnessFurtherPenaltyAt80 = -90f;
                s.consciousnessFurtherMaximumPenalty = -99f;
                s.consciousnessFurtherBonusAt170 = 90f;
                s.consciousnessFurtherMaximumBonus = 99f;
                s.gwsHighFactor = 1.78f;
                s.careerXpMultiplier = 9f;
                s.biotechGreatAptitude = 19;
                s.learningCapacityBaseDailyCap = 4567f;
                int schema = s.consciousnessAnchorSettingsVersion;

                SettingsPresetService.ApplyConsciousnessModDefaults(s);
                Assert(s.consciousnessAffectsLearning == master, "master preserved");
                Assert(!s.consciousnessAffectsLearningSpeed, "speed switch preserved");
                Assert(s.consciousnessAffectsDailyCapacity, "capacity switch preserved");
                Assert(!s.consciousnessAffectsFurtherLearning, "further switch preserved");
                Assert(s.consciousnessFurtherV2.LowEnabled == low, "Further Low preserved");
                Assert(s.consciousnessFurtherV2.HighEnabled != low, "Further High preserved");
                Assert(s.consciousnessAnchorSpeed.LowEnabled &&
                       s.consciousnessAnchorSpeed.HighEnabled &&
                       s.consciousnessAnchorCapacity.LowEnabled &&
                       s.consciousnessAnchorCapacity.HighEnabled,
                    "existing Speed/Capacity section reset behavior preserved");
                Assert(!Object.ReferenceEquals(s.consciousnessAnchorSpeed,
                    s.consciousnessAnchorCapacity), "independent Speed/Capacity objects");
                Approx(s.consciousnessAnchorSpeed.HighAnchor, 1.20f, "speed high anchor");
                Approx(s.consciousnessAnchorSpeed.HighFactor, 1.08f, "speed high factor");
                Approx(s.consciousnessAnchorCapacity.LowFactor, 0.5f, "capacity low factor");
                Approx(s.consciousnessFurtherV2.HighFactor, 1.20f, "Further high factor");
                Approx(s.consciousnessFurtherV2.LowFactor, 0.8f, "Further low factor");
                Approx(s.consciousnessFurtherPenaltyAt80,
                    XpCustomizerDefaults.ConsciousnessFurtherPenaltyAt80, "legacy PP low");
                Approx(s.consciousnessFurtherMaximumPenalty,
                    XpCustomizerDefaults.ConsciousnessFurtherMaximumPenalty, "legacy PP low limit");
                Approx(s.consciousnessFurtherBonusAt170,
                    XpCustomizerDefaults.ConsciousnessFurtherBonusAt170, "legacy PP high");
                Approx(s.consciousnessFurtherMaximumBonus,
                    XpCustomizerDefaults.ConsciousnessFurtherMaximumBonus, "legacy PP high limit");
                Approx(s.gwsHighFactor, 1.78f, "GWS untouched");
                Approx(s.careerXpMultiplier, 9f, "Career untouched");
                Assert(s.biotechGreatAptitude == 19, "Biotech untouched");
                Approx(s.learningCapacityBaseDailyCap, 4567f, "LC untouched");
                Assert(s.consciousnessAnchorSettingsVersion == schema, "schema untouched");

                // A second click must be value-idempotent, not toggle
                // either master or Further V2 switch state.
                SettingsPresetService.ApplyConsciousnessModDefaults(s);
                Assert(s.consciousnessAffectsLearning == master &&
                    s.consciousnessFurtherV2.LowEnabled == low &&
                    s.consciousnessFurtherV2.HighEnabled != low,
                    "second section reset is value-idempotent");
                Approx(s.consciousnessAnchorSpeed.HighFactor, 1.08f,
                    "second section reset numerical idempotence");
            }
            SettingsPresetService.ApplyConsciousnessModDefaults(null);
            Assert(true, "null safe");
            Log.Message("[XPC-H1][LEGACY_PRESET_RETIREMENT_SELFTEST] status=PASS; checks=" +
                checks + "; retiredEntryPoints=8; numericDefaultsSource=XpCustomizerDefaults; " +
                "consSectionBehavior=PRESERVED; legacyXmlFields=PRESERVED; " +
                "tests=DETACHED_NO_IMGUI; realSettingsWrites=0; profileJsonWrites=0; " +
                "geneDefWrites=0; configXmlWrites=0; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
