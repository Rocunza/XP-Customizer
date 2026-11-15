#if DEBUG
using System;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.LearningCapacity;

namespace XpCustomizer
{
    /// <summary>
    /// DEBUG-only regressions for Learning Capacity V3 arithmetic.
    /// No Pawn, Def, saved settings, or Harmony patch execution is required.
    /// The tested arithmetic is also used by the gameplay service.
    /// </summary>
    internal static class LearningCapacitySelfTests
    {
        private const float Tolerance = 0.01f;

        public static void Run()
        {
            TestBaseNormalization();
            TestSourceNormalization();
            TestV3SourceStrengthAndPreview();
            TestV4AutoConversionAndManualOverride();
            TestVanillaNeutralAndMasterOff();
            TestAdditiveSourceModifiers();
            TestRoundStages();
            TestZeroAndNegativeLimits();
            TestConsciousnessCombination();
            TestIndependentCapacityActivationGate();
            TestLargeButFiniteCapacities();
            Verse.Log.Message("[XP Customizer][LC-CONS-R7-HF1] tests PASS; " +
                "defaults=C120/x1.08; C135=x1.14; C170=x1.28; customC170/x1.28=preserved; " +
                "dailyCap4000=4560/5120/5120; modeDefault=Linear_L1");
        }


        private static void TestBaseNormalization()
        {
            AssertNear(LearningCapacityService.NormalizeBaseDailyCap(-1f), 0f,
                "base lower clamp");
            AssertNear(LearningCapacityService.NormalizeBaseDailyCap(0f), 0f,
                "base zero");
            AssertNear(LearningCapacityService.NormalizeBaseDailyCap(4000f), 4000f,
                "vanilla base");
            AssertNear(LearningCapacityService.NormalizeBaseDailyCap(8000f), 8000f,
                "custom base");
            AssertNear(LearningCapacityService.NormalizeBaseDailyCap(9999999f), 9999999f,
                "maximum base");
            AssertNear(LearningCapacityService.NormalizeBaseDailyCap(10000000f), 9999999f,
                "base upper clamp");
        }


        private static void TestSourceNormalization()
        {
            AssertNear(LearningCapacityService.NormalizeSourcePercentagePoints(-200f), -100f,
                "source lower clamp");
            AssertNear(LearningCapacityService.NormalizeSourcePercentagePoints(200f), 100f,
                "source upper clamp");
            AssertNear(LearningCapacityService.NormalizeSourcePercentagePoints(15f), 15f,
                "source ordinary positive");
        }


        private static void TestV3SourceStrengthAndPreview()
        {
            // Same pure method in UI and live source detection.
            AssertNear(LearningCapacityService.NormalizeSourceStrength(-1f), 0f,
                "strength lower bound");
            AssertNear(LearningCapacityService.NormalizeSourceStrength(3f), 2f,
                "strength upper bound");
            AssertNear(LearningCapacityService.NormalizeSourceStrength(float.NaN), 1f,
                "strength nonfinite normalization");

            float original = 15f;
            foreach (float strength in new float[] { 0f, 0.5f, 1f, 1.3f, 2f })
            {
                float pp = LearningCapacityService.GetEffectiveSourcePercentagePoints(original, strength);
                AssertNear(pp, original * strength, "strength independent scale " + strength);
                float raw, intermediate;
                float livePreview = LearningCapacityService.CalculateFromComponents(
                    4000f, true, pp / 100f, 1f, out raw, out intermediate);
                AssertNear(livePreview, (float)Math.Round(4000f * (1f + pp / 100f)),
                    "V3 single-source preview/live arithmetic");
            }
            AssertNear(original, 15f, "stored Custom value never mutated");
            AssertNear(LearningCapacityService.GetEffectiveSourcePercentagePoints(-75f, 2f), -100f,
                "negative effective clamp");
            AssertNear(LearningCapacityService.GetEffectiveSourcePercentagePoints(75f, 2f), 100f,
                "positive effective clamp");
            AssertNear(LearningCapacityService.GetEffectiveSourcePercentagePoints(-15f, 0f), 0f,
                "zero strength pass-through");
            AssertNear(LearningCapacityService.GetEffectiveSourcePercentagePoints(15f, 1f), 15f,
                "V2 settings preserve original results at 100% strength");

            var settings = new XpCustomizerSettings();
            settings.learningCapacityBaseDailyCap = 4000f;
            settings.learningCapacitySourceModifiersEnabled = true;
            settings.learningCapacityCustomFastLearner = 15f;
            settings.learningCapacitySourceStrength = 0f;
            AssertTrue(!LearningCapacityService.IsConfiguredToChangeDailyCap(settings),
                "0% strength + neutral base must keep vanilla unpatched");
            settings.learningCapacitySourceStrength = 1f;
            AssertTrue(LearningCapacityService.IsConfiguredToChangeDailyCap(settings),
                "100% strength with source must activate");
            settings.learningCapacitySourceStrength = 2f;
            AssertTrue(LearningCapacityService.IsConfiguredToChangeDailyCap(settings),
                "200% strength with source must activate");
            settings.learningCapacitySourceModifiersEnabled = false;
            AssertTrue(!LearningCapacityService.IsConfiguredToChangeDailyCap(settings),
                "OFF ignores strength even at 200%");
        }


        private static void TestV4AutoConversionAndManualOverride()
        {
            // Vanilla LF source bonus × conversion 20% × slider (100%=x1).
            AssertNear(LearningCapacityService.GetV4SourcePercentagePoints(
                false, 0f, 75f, 1f), 15f, "Fast Learner Auto +15");
            AssertNear(LearningCapacityService.GetV4SourcePercentagePoints(
                false, 0f, -75f, 1f), -15f, "Slow Learner Auto -15");
            AssertNear(LearningCapacityService.GetV4SourcePercentagePoints(
                false, 0f, 75f, 1.30f), 19.5f, "Fast Learner Auto 130% +19.5");
            AssertNear(LearningCapacityService.GetV4SourcePercentagePoints(
                false, 0f, 75f, 2f), 30f, "Fast Learner Auto 200% +30");
            // Slow Study's x0.5 GLF factor reduces the other Auto offsets.
            AssertNear(LearningCapacityService.GetV4SourcePercentagePoints(
                false, 0f, 75f, 1f, 0.5f), 7.5f,
                "Fast Learner under Slow Study gives +7.5 auto DLC");
            AssertNear(LearningCapacityService.GetV4SourcePercentagePoints(
                false, 0f, -50f, 1f), -10f,
                "Slow Study itself contributes -10 DLC");
            AssertNear(LearningCapacityService.GetV4SourcePercentagePoints(
                true, 0f, 75f, 1f, 0.5f), 0f,
                "manual zero bypasses Slow Study scaling on that source");
            AssertNear(LearningCapacityService.GetV4SourcePercentagePoints(
                false, 0f, 75f, 0f), 0f, "Auto strength zero");
            AssertNear(LearningCapacityService.GetV4SourcePercentagePoints(
                true, 0f, 75f, 1.3f), 0f, "manual zero bypass Auto source");
            AssertNear(LearningCapacityService.GetV4SourcePercentagePoints(
                true, 17f, 75f, 2f), 17f, "manual 17 unaffected by slider");
            AssertNear(LearningCapacityService.GetV4SourcePercentagePoints(
                true, -150f, 75f, 2f), -100f, "manual negative clamp");
            AssertNear(LearningCapacityService.GetV4SourcePercentagePoints(
                false, 0f, 400f, 2f), 100f, "auto positive clamp");
            float raw, intermediate;
            float example = LearningCapacityService.CalculateFromComponents(
                4000f, true, (19.5f + 19.5f) / 100f, 1f, out raw, out intermediate);
            AssertNear(example, 5560f, "Fast Learner + Too Smart Auto = 5560 XP");
            example = LearningCapacityService.CalculateFromComponents(
                4000f, true, (19.5f + 0f) / 100f, 1f, out raw, out intermediate);
            AssertNear(example, 4780f, "one source manual zero = 4780 XP");

            var s = new XpCustomizerSettings();
            s.learningCapacitySourceModifiersEnabled = true;
            s.learningCapacitySourceStrength = 0f;
            AssertTrue(!LearningCapacityService.IsConfiguredToChangeDailyCap(s),
                "V4 0% Auto with no overrides is vanilla-neutral");
            s.learningCapacityOverrideFastLearner = true;
            s.learningCapacityCustomFastLearner = 10f;
            AssertTrue(LearningCapacityService.IsConfiguredToChangeDailyCap(s),
                "V4 manual source still activates at 0% Auto strength");
            s.learningCapacityCustomFastLearner = 0f;
            AssertTrue(!LearningCapacityService.IsConfiguredToChangeDailyCap(s),
                "V4 zero manual + 0% Auto returns vanilla");
            s.learningCapacitySourceModifiersEnabled = false;
            AssertTrue(!LearningCapacityService.IsConfiguredToChangeDailyCap(s),
                "V4 master OFF ignores manual overrides");
        }

        private static void TestVanillaNeutralAndMasterOff()
        {
            AssertCapacity(4000f, false, 0f, 1f, 4000f,
                "neutral daily cap");
            AssertCapacity(4000f, false, 0.75f, 1f, 4000f,
                "source OFF ignores stored positive modifier");
            AssertCapacity(4000f, false, -0.75f, 1f, 4000f,
                "source OFF ignores stored negative modifier");
            AssertCapacity(8000f, false, 1f, 1f, 8000f,
                "custom base still applies when sources OFF");
        }


        private static void TestAdditiveSourceModifiers()
        {
            AssertCapacity(4000f, true, 0.15f, 1f, 4600f,
                "one positive source +15pp");
            AssertCapacity(4000f, true, -0.10f, 1f, 3600f,
                "one negative source -10pp");
            AssertCapacity(4000f, true, (15f - 10f) / 100f, 1f, 4200f,
                "two sources add percentage points");
            AssertCapacity(4000f, true, 0.25f, 1f, 5000f,
                "two positive sources add rather than multiply");
        }


        private static void TestRoundStages()
        {
            // First round after sources, then round again after consciousness.
            // Rounding only the final raw product would incorrectly return 11.
            float raw;
            float intermediate;
            float finalCapacity = LearningCapacityService.CalculateFromComponents(
                11f, true, 0.20f, 0.80f, out raw, out intermediate);

            AssertNear(raw, 13.2f, "rounding raw after sources");
            AssertNear(intermediate, 13f, "first rounding before consciousness");
            AssertNear(finalCapacity, 10f, "second rounding after consciousness");
        }


        private static void TestZeroAndNegativeLimits()
        {
            AssertCapacity(0f, false, 0f, 1f, 0f,
                "zero base remains immediate saturation threshold");
            AssertCapacity(0f, true, 9f, 2f, 0f,
                "zero base with positive sources and consciousness");
            AssertCapacity(4000f, true, -1f, 1f, 0f,
                "-100 percent capacity");
            AssertCapacity(4000f, true, -2f, 1f, 0f,
                "combined negative sources never produce a negative cap");
            AssertCapacity(4000f, true, 0.15f, 0f, 0f,
                "zero consciousness multiplier");
        }


        private static void TestConsciousnessCombination()
        {
            var s = new XpCustomizerSettings();
            s.consciousnessAffectsLearning = true;
            s.consciousnessAffectsDailyCapacity = true;
            float lowFactor = ConsciousnessCapacityService.CalculateConfiguredFactor(0.80f, s);
            float baseline = ConsciousnessCapacityService.CalculateConfiguredFactor(1f, s);
            float highAnchorFactor = ConsciousnessCapacityService.CalculateConfiguredFactor(1.35f, s);
            float c170Factor = ConsciousnessCapacityService.CalculateConfiguredFactor(1.70f, s);
            AssertNear(lowFactor, 0.80f, "V2 C80 capacity multiplier");
            AssertNear(baseline, 1f, "V2 C100 capacity multiplier");
            // D01 default: 120% -> x1.08, Linear high mode, safety cap x2.
            // Independently check the resulting 170% point; it is no longer an anchor.
            AssertNear(highAnchorFactor, 1.14f, "V2 default C135 capacity above anchor");
            AssertNear(c170Factor, 1.28f, "V2 default C170 capacity Linear curve");
            s.consciousnessAnchorCapacity.LowEnabled = false;
            AssertNear(ConsciousnessCapacityService.CalculateConfiguredFactor(0.80f, s),
                1f, "V2 low OFF returns vanilla neutral");
            AssertCapacity(4000f, true, 0.15f, 1f, 4600f,
                "Consciousness OFF preserves source cap");
            AssertCapacity(4000f, false, 0f, lowFactor, 3200f,
                "Consciousness ON low anchor");
            AssertCapacity(4000f, false, 0f, highAnchorFactor, 4560f,
                "Consciousness ON C135 above new high anchor");
            AssertCapacity(4000f, false, 0f, c170Factor, 5120f,
                "Consciousness ON default C170 above-anchor curve");
            AssertCapacity(4000f, true, 0.15f, lowFactor, 3680f,
                "Source plus Consciousness combined");

            // User-selected old 170% -> x1.28 remains valid. Never silently
            // change a saved custom profile merely to satisfy default tests.
            s.consciousnessAnchorCapacity.HighAnchor = 1.70f;
            s.consciousnessAnchorCapacity.HighFactor = 1.28f;
            float oldCustomFactor = ConsciousnessCapacityService.CalculateConfiguredFactor(1.70f, s);
            AssertNear(oldCustomFactor, 1.28f, "V2 user-selected C170 capacity anchor");
            AssertCapacity(4000f, false, 0f, oldCustomFactor, 5120f,
                "Consciousness ON user-selected old high anchor");
        }

        /// <summary>
        /// Phase 4B-R2: V2-only activation predicate used in the live
        /// SkillRecord.LearningSaturatedToday Harmony postfix.
        /// This does not modify global settings or require a save / Pawn.
        /// </summary>
        private static void TestIndependentCapacityActivationGate()
        {
            AssertTrue(!LearningCapacityService.IsConfiguredToChangeDailyCap(null),
                "Null settings do not activate DLC Harmony");
            var settings = new XpCustomizerSettings();
            settings.learningCapacityBaseDailyCap = 4000f;
            settings.learningCapacitySourceModifiersEnabled = false;
            settings.consciousnessAffectsLearning = true;
            settings.consciousnessAffectsDailyCapacity = true;
            AssertTrue(LearningCapacityService.IsConfiguredToChangeDailyCap(settings),
                "V2 default profile activates DLC");

            settings.consciousnessAnchorCapacity.LowEnabled = false;
            settings.consciousnessAnchorCapacity.HighEnabled = false;
            AssertTrue(!LearningCapacityService.IsConfiguredToChangeDailyCap(settings),
                "Both V2 sides OFF preserve vanilla DLC");
            settings.consciousnessAnchorCapacity.HighEnabled = true;
            AssertTrue(LearningCapacityService.IsConfiguredToChangeDailyCap(settings),
                "V2 High enables DLC independent of Low OFF");
            settings.consciousnessAnchorCapacity.HighFactor = 1f;
            AssertTrue(!LearningCapacityService.IsConfiguredToChangeDailyCap(settings),
                "Neutral V2 High must not enable DLC");
            settings.consciousnessAnchorCapacity.LowEnabled = true;
            AssertTrue(LearningCapacityService.IsConfiguredToChangeDailyCap(settings),
                "V2 Low activates DLC independently");

            settings.consciousnessAffectsDailyCapacity = false;
            AssertTrue(!LearningCapacityService.IsConfiguredToChangeDailyCap(settings),
                "Capacity OFF preserves vanilla DLC");
            settings.consciousnessAffectsDailyCapacity = true;
            settings.consciousnessAffectsLearning = false;
            AssertTrue(!LearningCapacityService.IsConfiguredToChangeDailyCap(settings),
                "Consciousness master OFF preserves vanilla DLC");
            settings.consciousnessAffectsLearning = true;
            settings.consciousnessAffectsLearningSpeed = false;
            AssertTrue(LearningCapacityService.IsConfiguredToChangeDailyCap(settings),
                "Speed OFF does not disable Capacity");

            settings.consciousnessAffectsLearning = false;
            settings.learningCapacityBaseDailyCap = 5000f;
            AssertTrue(LearningCapacityService.IsConfiguredToChangeDailyCap(settings),
                "Custom daily-cap base is independent from Cons OFF");
            settings.learningCapacityBaseDailyCap = 4000f;
            settings.learningCapacityCustomSlowLearner = 15f;
            settings.learningCapacitySourceModifiersEnabled = true;
            AssertTrue(LearningCapacityService.IsConfiguredToChangeDailyCap(settings),
                "Source modifiers are independent from Cons OFF");
            settings.learningCapacitySourceModifiersEnabled = false;
            AssertTrue(!LearningCapacityService.IsConfiguredToChangeDailyCap(settings),
                "Disabled sources and Cons should preserve vanilla DLC");
        }

        private static void TestLargeButFiniteCapacities()
        {
            float maxBase = LearningCapacityService.NormalizeBaseDailyCap(9999999f);
            AssertCapacity(maxBase, false, 0f, 1f, maxBase,
                "maximum base without modifiers");

            // Nine sources can each contribute up to +100 percentage points.
            // The max here intentionally exceeds the base INPUT limit:
            // that limit is not a clamp on the calculated final daily cap.
            float rawCapacity;
            float intermediateCapacity;
            float finalCapacity = LearningCapacityService.CalculateFromComponents(
                maxBase, true, 9f, 5f,
                out rawCapacity, out intermediateCapacity);

            AssertFiniteNonNegative(rawCapacity, "extreme raw cap");
            AssertFiniteNonNegative(intermediateCapacity, "extreme intermediate cap");
            AssertFiniteNonNegative(finalCapacity, "extreme final cap");
            AssertTrue(finalCapacity >= intermediateCapacity,
                "positive consciousness should not reduce a huge cap");
        }


        private static void AssertCapacity(
            float baseCapacity,
            bool sourceEnabled,
            float sourceModifier,
            float consciousnessFactor,
            float expected,
            string label)
        {
            float raw;
            float intermediate;
            float actual = LearningCapacityService.CalculateFromComponents(
                LearningCapacityService.NormalizeBaseDailyCap(baseCapacity),
                sourceEnabled, sourceModifier, consciousnessFactor,
                out raw, out intermediate);

            AssertFiniteNonNegative(actual, label);
            AssertNear(actual, expected, label);
        }


        private static void AssertFiniteNonNegative(float value, string label)
        {
            AssertTrue(!float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f,
                label + " must be finite and nonnegative; got " + value);
        }


        private static void AssertNear(float actual, float expected, string label)
        {
            AssertTrue(!float.IsNaN(actual) && Math.Abs(actual - expected) <= Tolerance,
                label + " expected " + expected.ToString("0.###") +
                ", got " + actual.ToString("0.###"));
        }


        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(
                    "[XP Customizer] DEBUG self-test failed (Learning Capacity V3): " +
                    message);
            }
        }
    }
}
#endif
