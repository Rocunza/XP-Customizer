#if DEBUG
using System;
using XpCustomizer.ConsciousnessLearning;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// Anchor V2 pure mathematical tests. Independent of Unity UI and Scribe.
    /// </summary>
    internal static class ConsciousnessAnchorCurveSelfTests
    {
        private const float Tolerance = 0.00002f;

        private static void Check(bool condition, string label)
        {
            if (!condition)
                throw new InvalidOperationException("[XP Customizer][CONS-ANCHOR-V2] " + label);
        }

        private static void Equal(float actual, float expected, string label)
        {
            Check(!float.IsNaN(actual) && !float.IsInfinity(actual) &&
                Math.Abs(actual - expected) <= Tolerance,
                label + " expected=" + expected + "; actual=" + actual);
        }

        private static ConsAnchorCurveProfile Profile(
            float lowFactor, ConsAnchorCurveMode lowMode, float lowCurvature,
            float highFactor, ConsAnchorCurveMode highMode, float highCurvature,
            float diminishingLimit, float safetyLimit)
        {
            return new ConsAnchorCurveProfile(
                0.5f, lowFactor, lowMode, lowCurvature,
                1.7f, highFactor, highMode, highCurvature,
                diminishingLimit, safetyLimit);
        }

        internal static void Run()
        {
            var speed = ConsciousnessAnchorCurveService.CreateSpeedDefaults();
            var capacity = ConsciousnessAnchorCurveService.CreateCapacityDefaults();
            Check(!object.ReferenceEquals(speed, capacity),
                "speed/capacity profiles must not share one instance");

            // Default Low stays xC. Default High anchors 120% -> x1.08,
            // Linear high slope with x2 safety cap (old Diminishing is still selectable).
            // The old 170% -> x1.28 curve is still user-configurable.
            float[] baselineLevels = { 0f, 0.1f, 0.3f, 0.5f, 0.8f, 0.9999f,
                1f, 1.0001f, 1.2f, 1.35f, 1.7f, 3f, 4f, 10f };
            for (int i = 0; i < baselineLevels.Length; i++)
            {
                float c = baselineLevels[i];
                float expected = c <= 1f ? c : Math.Min(2f, 1f + (c - 1f) * (0.08f / 0.20f));
                Equal(ConsciousnessAnchorCurveService.Calculate(c, speed), expected,
                    "default V2 speed baseline at c=" + c);
                Equal(ConsciousnessAnchorCurveService.Calculate(c, capacity), expected,
                    "default V2 capacity baseline at c=" + c);
            }
            Equal(ConsciousnessAnchorCurveService.Calculate(0.5f, speed), 0.5f,
                "low anchor exact");
            Equal(ConsciousnessAnchorCurveService.Calculate(1f, speed), 1f,
                "neutral anchor exact");
            Check(speed.HighMode == ConsAnchorCurveMode.Linear &&
                capacity.HighMode == ConsAnchorCurveMode.Linear,
                "speed/capacity high default Linear");
            Equal(speed.HighAnchor, 1.20f, "speed high anchor default");
            Equal(capacity.HighAnchor, 1.20f, "capacity high anchor default");
            Equal(speed.HighFactor, 1.08f, "speed high factor default");
            Equal(capacity.HighFactor, 1.08f, "capacity high factor default");
            Equal(speed.HighDiminishingLimit, 2f, "speed default limit");
            Equal(capacity.HighDiminishingLimit, 2f, "capacity default limit");
            Equal(ConsciousnessAnchorCurveService.Calculate(1.20f, speed), 1.08f,
                "speed high anchor exact");
            Equal(ConsciousnessAnchorCurveService.Calculate(1.20f, capacity), 1.08f,
                "capacity high anchor exact");
            // Previously saved / explicitly edited 170% x1.28 must remain usable.
            var oldExplicit = Profile(0.5f, ConsAnchorCurveMode.Linear, 1f,
                1.28f, ConsAnchorCurveMode.Diminishing, 1f, 2f, 2f);
            Equal(ConsciousnessAnchorCurveService.Calculate(1.7f, oldExplicit), 1.28f,
                "explicit old anchor remains supported");
            Equal(ConsciousnessAnchorCurveService.Calculate(0f, speed), 0f,
                "consciousness zero is zero, not a low-anchor clamp");

            int scenarios = 0;
            // Mode-specific anchors, 0..100% low domain continuity,
            // monotonicity and non-negative factors for ALL three modes.
            for (int lm = 0; lm < 3; lm++)
            for (int hm = 0; hm < 3; hm++)
            for (int curvature = 0; curvature <= 200; curvature += 50)
            {
                float k = curvature / 100f;
                var p = Profile(0.63f, (ConsAnchorCurveMode)lm, k,
                    1.46f, (ConsAnchorCurveMode)hm, k, 1.9f, 2.1f);
                Equal(ConsciousnessAnchorCurveService.Calculate(0f, p), 0f,
                    "0% boundary " + scenarios);
                Equal(ConsciousnessAnchorCurveService.Calculate(0.5f, p), 0.63f,
                    "50% anchor " + scenarios);
                Equal(ConsciousnessAnchorCurveService.Calculate(1f, p), 1f,
                    "100% anchor " + scenarios);
                Equal(ConsciousnessAnchorCurveService.Calculate(1.7f, p), 1.46f,
                    "170% anchor " + scenarios);

                float last = 0f;
                for (int index = 0; index <= 200; index++)
                {
                    float c = index / 200f;
                    float current = ConsciousnessAnchorCurveService.Calculate(c, p);
                    Check(current >= last - 0.00001f && current >= 0f && current <= 1f,
                        "low monotonic/nonnegative " + scenarios + ":" + index);
                    last = current;
                }
                float lastHigh = 1f;
                for (int index = 0; index <= 400; index++)
                {
                    float c = 1f + index * 0.01f;
                    float current = ConsciousnessAnchorCurveService.Calculate(c, p);
                    Check(current >= lastHigh - 0.00001f &&
                        current <= 2.1f + Tolerance,
                        "high monotonic/capped " + scenarios + ":" + index);
                    lastHigh = current;
                }
                scenarios++;
            }

            // At zero curvature, Accelerating is exactly linear. Linear is
            // independent of its curvature slider at every sampled point.
            var linear = Profile(0.5f, ConsAnchorCurveMode.Linear, 2f,
                1.28f, ConsAnchorCurveMode.Linear, 2f, 2f, 3f);
            var accelZero = Profile(0.5f, ConsAnchorCurveMode.Accelerating, 0f,
                1.28f, ConsAnchorCurveMode.Accelerating, 0f, 2f, 3f);
            foreach (float c in new[] { 0.2f, 0.4f, 0.7f, 0.9f, 1.2f, 1.7f, 2.5f, 5f })
                Equal(ConsciousnessAnchorCurveService.Calculate(c, accelZero),
                    ConsciousnessAnchorCurveService.Calculate(c, linear),
                    "zero accelerating equals linear at c=" + c);

            // Higher high Diminishing curvature makes the early bonus larger
            // and later bonus smaller while preserving both anchors/limit.
            var dimLow = Profile(0.5f, ConsAnchorCurveMode.Linear, 1f,
                1.28f, ConsAnchorCurveMode.Diminishing, 0f, 2f, 2f);
            var dimHigh = Profile(0.5f, ConsAnchorCurveMode.Linear, 1f,
                1.28f, ConsAnchorCurveMode.Diminishing, 2f, 2f, 2f);
            Check(ConsciousnessAnchorCurveService.Calculate(1.2f, dimHigh) >
                  ConsciousnessAnchorCurveService.Calculate(1.2f, dimLow),
                "higher high curvature must front-load diminishing bonus");
            Check(ConsciousnessAnchorCurveService.Calculate(3f, dimHigh) <
                  ConsciousnessAnchorCurveService.Calculate(3f, dimLow),
                "higher high curvature must slow post-anchor bonus");
            Check(ConsciousnessAnchorCurveService.Calculate(1000000f, dimHigh) <= 2f,
                "diminishing never exceeds the limit");
            Check(ConsciousnessAnchorCurveService.Calculate(1000000f, dimHigh) > 1.99f,
                "diminishing must approach the limit");

            // Speed and capacity profiles may have different parameters.
            var customCapacity = Profile(0.7f, ConsAnchorCurveMode.Accelerating, 1.5f,
                1.1f, ConsAnchorCurveMode.Linear, 0f, 1.3f, 1.5f);
            Equal(ConsciousnessAnchorCurveService.Calculate(0.5f, customCapacity),
                0.7f, "independent capacity low anchor");
            Equal(ConsciousnessAnchorCurveService.Calculate(1.7f, customCapacity),
                1.1f, "independent capacity high anchor");
            Equal(ConsciousnessAnchorCurveService.Calculate(0.5f, speed),
                0.5f, "custom capacity does not alter speed");
            Equal(ConsciousnessAnchorCurveService.Calculate(1.35f, speed),
                1.14f, "custom capacity does not alter speed high");

            // Configured-neutral sides must not impose a hidden penalty.
            var neutralLow = Profile(1f, ConsAnchorCurveMode.Accelerating, 2f,
                1.28f, ConsAnchorCurveMode.Diminishing, 1f, 2f, 2f);
            foreach (float c in new[] { 0f, 0.1f, 0.5f, 0.8f, 0.999f })
                Equal(ConsciousnessAnchorCurveService.Calculate(c, neutralLow), 1f,
                    "neutral low factor must not penalize at c=" + c);
            var neutralHigh = Profile(0.5f, ConsAnchorCurveMode.Linear, 1f,
                1f, ConsAnchorCurveMode.Accelerating, 2f, 2f, 2f);
            foreach (float c in new[] { 1.01f, 1.7f, 3f, 100f })
                Equal(ConsciousnessAnchorCurveService.Calculate(c, neutralHigh), 1f,
                    "neutral high factor must not boost at c=" + c);

            // Check both non-saturating families against explicit safety x2.
            var boundedLinear = Profile(0.5f, ConsAnchorCurveMode.Linear, 1f,
                1.28f, ConsAnchorCurveMode.Linear, 1f, 2f, 2f);
            var boundedAccelerating = Profile(0.5f, ConsAnchorCurveMode.Linear, 1f,
                1.28f, ConsAnchorCurveMode.Accelerating, 2f, 2f, 2f);
            Equal(ConsciousnessAnchorCurveService.Calculate(100f, boundedLinear),
                2f, "linear safety cap x2");
            Equal(ConsciousnessAnchorCurveService.Calculate(100f, boundedAccelerating),
                2f, "accelerating safety cap x2");

            // Out-of-range, nonfinite, and contradictory future settings
            // must never produce NaN/Infinity or unsafe exponential XP.
            Equal(ConsciousnessAnchorCurveService.Calculate(0.5f, null), 1f,
                "null profile neutral");
            Equal(ConsciousnessAnchorCurveService.Calculate(float.NaN, speed), 1f,
                "NaN consciousness neutral");
            Equal(ConsciousnessAnchorCurveService.Calculate(float.PositiveInfinity, speed), 1f,
                "+Infinity consciousness neutral");
            Equal(ConsciousnessAnchorCurveService.Calculate(float.NegativeInfinity, speed), 1f,
                "-Infinity consciousness neutral");
            var corrupt = new ConsAnchorCurveProfile(float.NaN, float.NegativeInfinity,
                (ConsAnchorCurveMode)999, float.NaN,
                float.PositiveInfinity, float.NaN,
                (ConsAnchorCurveMode)(-12), float.NegativeInfinity,
                -100f, float.PositiveInfinity);
            foreach (float c in new[] { -1f, 0f, 0.3f, 0.5f,
                0.8f, 1f, 1.2f, 1.7f, 3f, float.MaxValue })
            {
                float value = ConsciousnessAnchorCurveService.Calculate(c, corrupt);
                Check(!float.IsNaN(value) && !float.IsInfinity(value) &&
                    value >= 0f && value <=
                    ConsciousnessAnchorCurveService.TechnicalMaximumFactor,
                    "corrupted profile safety " + c);
            }

            Log.Message("[XP Customizer][CONS-ANCHOR-V2] pure tests PASS; " +
                "default speed/capacity reference=" + baselineLevels.Length +
                " levels each; independent profiles; 3x3x5 curves=" + scenarios +
                "; low 0% endpoint; monotonic/anchor checks; diminishing " +
                "asymptote; linear/accelerating caps; invalid inputs; " +
                "gameplay=ANCHOR_V2_ONLY; savedFields=V2_SCHEMA3; UI=PHASE4B-R2");
        }
    }
}
#endif
