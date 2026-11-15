using System;

namespace XpCustomizer.ConsciousnessLearning
{
    /// <summary>
    /// Anchor V2 curve families with independent low and high modes.
    /// </summary>
    internal enum ConsAnchorCurveMode
    {
        Diminishing = 0,
        Linear = 1,
        Accelerating = 2
    }

    /// <summary>
    /// Immutable, ephemeral inputs for one Cons effect (speed OR capacity).
    /// This is NOT a ModSettings object and does not persist anything.
    /// Independent profiles are supplied from versioned V2 settings.
    /// </summary>
    internal sealed class ConsAnchorCurveProfile
    {
        internal readonly float LowAnchor;
        internal readonly float LowFactor;
        internal readonly ConsAnchorCurveMode LowMode;
        internal readonly float LowCurvature;
        internal readonly float HighAnchor;
        internal readonly float HighFactor;
        internal readonly ConsAnchorCurveMode HighMode;
        internal readonly float HighCurvature;
        internal readonly float HighDiminishingLimit;
        internal readonly float HighSafetyLimit;

        internal ConsAnchorCurveProfile(
            float lowAnchor, float lowFactor,
            ConsAnchorCurveMode lowMode, float lowCurvature,
            float highAnchor, float highFactor,
            ConsAnchorCurveMode highMode, float highCurvature,
            float highDiminishingLimit, float highSafetyLimit)
        {
            LowAnchor = lowAnchor;
            LowFactor = lowFactor;
            LowMode = lowMode;
            LowCurvature = lowCurvature;
            HighAnchor = highAnchor;
            HighFactor = highFactor;
            HighMode = highMode;
            HighCurvature = highCurvature;
            HighDiminishingLimit = highDiminishingLimit;
            HighSafetyLimit = highSafetyLimit;
        }
    }

    /// <summary>
    /// Independent, pure Cons Anchor V2 calculator (Phase 2 design).
    ///
    /// No direct Harmony, UI, Scribe, or settings writes. Gameplay uses
    /// this calculator for Speed, Capacity and the independent Further
    /// Learning post-cap multiplier profile.
    ///
    /// Low: 0% -> x0 if a penalty is configured, configurable anchor ->
    /// configured factor, 100% -> x1. A neutral x1 low anchor is no-op.
    /// Three modes interpolate both low segments independently while keeping
    /// those three points exact; no GWS-style 30% floor is imposed on Cons.
    /// High: 100% -> x1, configurable anchor -> configured factor.
    /// Diminishing asymptotes to its own limit, Linear/Accelerating use an
    /// independent technical safety cap. Default anchors reproduce the
    /// specified reference values at 50%, 100%, and 120%.
    /// </summary>
    internal static class ConsciousnessAnchorCurveService
    {
        internal const string Revision = "CONS-ANCHOR-V2-PURE-R1-20261103";
        internal const float NeutralFactor = 1f;
        internal const float DefaultLowAnchor = 0.50f;
        internal const float DefaultLowFactor = 0.50f;
        internal const float DefaultHighAnchor = 1.20f;
        internal const float DefaultHighFactor = 1.08f;
        internal const float DefaultHighDiminishingLimit = 2f;
        internal const float DefaultHighSafetyLimit = 2f;
        internal const float DefaultCurvature = 1f;
        internal const float TechnicalMaximumFactor = 20f;

        // Return two different instances to avoid accidental coupling of
        // Learning speed and Daily learning capacity when wiring Phase 3.
        internal static ConsAnchorCurveProfile CreateSpeedDefaults()
        {
            return CreateDefaultProfile();
        }

        internal static ConsAnchorCurveProfile CreateCapacityDefaults()
        {
            return CreateDefaultProfile();
        }

        private static ConsAnchorCurveProfile CreateDefaultProfile()
        {
            return new ConsAnchorCurveProfile(
                DefaultLowAnchor, DefaultLowFactor,
                ConsAnchorCurveMode.Linear, DefaultCurvature,
                DefaultHighAnchor, DefaultHighFactor,
                ConsAnchorCurveMode.Linear, DefaultCurvature,
                DefaultHighDiminishingLimit, DefaultHighSafetyLimit);
        }

        /// <summary>
        /// Side-effect-free multiplication factor. A null profile or a
        /// non-finite consciousness produces neutral x1, never NaN/Infinity.
        /// Bad user inputs are normalized for computation, not written back.
        /// </summary>
        internal static float Calculate(float consciousness, ConsAnchorCurveProfile profile)
        {
            if (profile == null || float.IsNaN(consciousness) ||
                float.IsInfinity(consciousness))
            {
                return NeutralFactor;
            }

            if (consciousness <= 0f)
            {
                // An x1 low anchor is an intentionally disabled penalty,
                // including at C0. Otherwise low curves reach x0 at C0.
                return Normalize(profile.LowFactor, DefaultLowFactor, 0f, 1f)
                    >= 1d ? NeutralFactor : 0f;
            }

            if (consciousness == 1f)
            {
                return NeutralFactor;
            }

            if (consciousness < 1f)
            {
                double anchor = Normalize(profile.LowAnchor, DefaultLowAnchor, 0.05f, 0.95f);
                double factor = Normalize(profile.LowFactor, DefaultLowFactor, 0f, 1f);
                double curvature = Normalize(profile.LowCurvature, DefaultCurvature, 0f, 2f);
                ConsAnchorCurveMode mode = NormalizeMode(profile.LowMode,
                    ConsAnchorCurveMode.Linear);

                // x1 explicitly means low-side neutral regardless of C.
                if (factor >= 1d) return NeutralFactor;

                if (consciousness == (float)anchor)
                {
                    return (float)factor;
                }

                double result;
                if (consciousness > anchor)
                {
                    // From 100% down to low anchor: x1 -> configured factor.
                    double progress = (1d - consciousness) / (1d - anchor);
                    result = 1d - (1d - factor) * Shape(progress, mode, curvature);
                }
                else
                {
                    // From low anchor down to zero: configured factor -> x0.
                    // This explicit second segment avoids clamping the factor
                    // at 50%, unlike GWS's genuine 30% stat minimum.
                    double progress = (anchor - consciousness) / anchor;
                    result = factor * (1d - Shape(progress, mode, curvature));
                }

                return (float)Clamp(result, 0d, 1d);
            }

            double highAnchor = Normalize(profile.HighAnchor,
                DefaultHighAnchor, 1.01f, 10f);
            double highFactor = Normalize(profile.HighFactor,
                DefaultHighFactor, 1f, TechnicalMaximumFactor - 0.01f);
            ConsAnchorCurveMode highMode = NormalizeMode(profile.HighMode,
                ConsAnchorCurveMode.Linear);
            double highCurvature = Normalize(profile.HighCurvature,
                DefaultCurvature, 0f, 2f);
            double delta = (double)consciousness - 1d;
            double anchorDelta = highAnchor - 1d;

            if (consciousness == (float)highAnchor)
            {
                return (float)highFactor;
            }
            if (highFactor <= 1d)
            {
                return NeutralFactor;
            }

            if (highMode == ConsAnchorCurveMode.Diminishing)
            {
                double limit = Normalize(profile.HighDiminishingLimit,
                    DefaultHighDiminishingLimit, highFactor + 0.01d,
                    TechnicalMaximumFactor);
                double fraction = (highFactor - 1d) / (limit - 1d);

                // Asymptote form: 1 + (limit - 1) *
                // (1 - (1 + d / scale)^(-power)).
                // Calibrate 'scale' from the high anchor so curve strength
                // changes never move the configured high anchor.
                // The scale is calibrated from the active anchor and factor.
                // All allowed powers are positive, so the curve stays
                // increasing and concave (genuine diminishing returns).
                double power = Math.Pow(2d, 1d - highCurvature); // 0%:2, 100%:1, 200%:0.5
                double inverse = Math.Pow(1d - fraction, -1d / power);
                double scale = anchorDelta / (inverse - 1d);
                double saturation = 1d - Math.Pow(1d + delta / scale, -power);
                double result = 1d + (limit - 1d) * saturation;
                return (float)Clamp(result, 1d, limit);
            }

            double safety = Normalize(profile.HighSafetyLimit,
                DefaultHighSafetyLimit, highFactor, TechnicalMaximumFactor);
            double normalized = delta / anchorDelta;
            // 0% accelerating curvature equals linear; 200% is cubic.
            double exponent = highMode == ConsAnchorCurveMode.Accelerating
                ? 1d + highCurvature : 1d;
            double bonus = Math.Pow(normalized, exponent);
            double scaled = 1d + (highFactor - 1d) * bonus;
            if (double.IsNaN(scaled)) return NeutralFactor;
            return (float)Clamp(scaled, 1d, safety);
        }

        private static double Shape(double fraction, ConsAnchorCurveMode mode,
            double curvature)
        {
            double t = Clamp(fraction, 0d, 1d);
            if (mode == ConsAnchorCurveMode.Diminishing)
                return 1d - Math.Pow(1d - t, 1d + curvature);
            if (mode == ConsAnchorCurveMode.Accelerating)
                return Math.Pow(t, 1d + curvature);
            return t;
        }

        private static ConsAnchorCurveMode NormalizeMode(ConsAnchorCurveMode mode,
            ConsAnchorCurveMode fallback)
        {
            if (mode == ConsAnchorCurveMode.Diminishing ||
                mode == ConsAnchorCurveMode.Linear ||
                mode == ConsAnchorCurveMode.Accelerating)
                return mode;
            return fallback;
        }

        private static double Normalize(float value, float fallback,
            double min, double max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                value = fallback;
            return Clamp(value, min, max);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (double.IsNaN(value)) return min;
            if (double.IsPositiveInfinity(value)) return max;
            if (double.IsNegativeInfinity(value)) return min;
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
