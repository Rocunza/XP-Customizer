using System;
using Verse;

namespace XpCustomizer.ConsciousnessLearning
{
    /// <summary>
    /// Persisted, independent Anchor V2 values for exactly one Cons effect.
    /// Only prefixed V2 keys are written; obsolete pre-V2 settings are ignored.
    /// </summary>
    internal sealed class ConsAnchorV2ProfileSettings
    {
        // Historical Scribe fallback: old XML may omit a Diminishing(0) curve.
        // Keep this on READ/WRITE; fresh objects and section presets use Linear(1).
        internal const int LegacyXmlHighCurveFallback = (int)ConsAnchorCurveMode.Diminishing;
        internal bool LowEnabled = true;
        internal bool HighEnabled = true;
        internal float LowAnchor = ConsciousnessAnchorCurveService.DefaultLowAnchor;
        internal float LowFactor = ConsciousnessAnchorCurveService.DefaultLowFactor;
        internal int LowCurve = (int)ConsAnchorCurveMode.Linear;
        internal float LowCurvature = ConsciousnessAnchorCurveService.DefaultCurvature;
        internal float HighAnchor = ConsciousnessAnchorCurveService.DefaultHighAnchor;
        internal float HighFactor = ConsciousnessAnchorCurveService.DefaultHighFactor;
        internal int HighCurve = (int)ConsAnchorCurveMode.Linear;
        internal float HighCurvature = ConsciousnessAnchorCurveService.DefaultCurvature;
        internal float HighDiminishingLimit = ConsciousnessAnchorCurveService.DefaultHighDiminishingLimit;
        internal float HighSafetyLimit = ConsciousnessAnchorCurveService.DefaultHighSafetyLimit;

        /// <summary>
        /// Flat, uniquely prefixed Scribe keys avoid Deep-save ambiguities.
        /// Explicit zero values survive. Speed and Capacity are independent.
        /// </summary>
        internal void ExposeValues(string prefix)
        {
            Scribe_Values.Look(ref LowEnabled, prefix + "LowEnabled", true);
            Scribe_Values.Look(ref HighEnabled, prefix + "HighEnabled", true);
            Scribe_Values.Look(ref LowAnchor, prefix + "LowAnchor",
                ConsciousnessAnchorCurveService.DefaultLowAnchor);
            Scribe_Values.Look(ref LowFactor, prefix + "LowFactor",
                ConsciousnessAnchorCurveService.DefaultLowFactor);
            Scribe_Values.Look(ref LowCurve, prefix + "LowCurve",
                (int)ConsAnchorCurveMode.Linear);
            Scribe_Values.Look(ref LowCurvature, prefix + "LowCurvature",
                ConsciousnessAnchorCurveService.DefaultCurvature);
            Scribe_Values.Look(ref HighAnchor, prefix + "HighAnchor",
                LegacyCurveXmlDefaults.ConsSpeedCapacityHighAnchor);
            Scribe_Values.Look(ref HighFactor, prefix + "HighFactor",
                LegacyCurveXmlDefaults.ConsSpeedCapacityHighFactor);
            Scribe_Values.Look(ref HighCurve, prefix + "HighCurve",
                LegacyXmlHighCurveFallback);
            Scribe_Values.Look(ref HighCurvature, prefix + "HighCurvature",
                ConsciousnessAnchorCurveService.DefaultCurvature);
            Scribe_Values.Look(ref HighDiminishingLimit, prefix + "HighDiminishingLimit",
                ConsciousnessAnchorCurveService.DefaultHighDiminishingLimit);
            Scribe_Values.Look(ref HighSafetyLimit, prefix + "HighSafetyLimit",
                ConsciousnessAnchorCurveService.DefaultHighSafetyLimit);
        }

        internal void Normalize()
        {
            LowAnchor = NormalizeFloat(LowAnchor,
                ConsciousnessAnchorCurveService.DefaultLowAnchor, 0.05f, 0.95f);
            LowFactor = NormalizeFloat(LowFactor,
                ConsciousnessAnchorCurveService.DefaultLowFactor, 0f, 1f);
            LowCurve = NormalizeMode(LowCurve, ConsAnchorCurveMode.Linear);
            LowCurvature = NormalizeFloat(LowCurvature,
                ConsciousnessAnchorCurveService.DefaultCurvature, 0f, 2f);
            HighAnchor = NormalizeFloat(HighAnchor,
                ConsciousnessAnchorCurveService.DefaultHighAnchor, 1.01f, 10f);
            HighFactor = NormalizeFloat(HighFactor,
                ConsciousnessAnchorCurveService.DefaultHighFactor, 1f,
                ConsciousnessAnchorCurveService.TechnicalMaximumFactor - 0.01f);
            HighCurve = NormalizeMode(HighCurve, ConsAnchorCurveMode.Linear);
            HighCurvature = NormalizeFloat(HighCurvature,
                ConsciousnessAnchorCurveService.DefaultCurvature, 0f, 2f);
            HighDiminishingLimit = NormalizeFloat(HighDiminishingLimit,
                ConsciousnessAnchorCurveService.DefaultHighDiminishingLimit,
                HighFactor + 0.01f,
                ConsciousnessAnchorCurveService.TechnicalMaximumFactor);
            HighSafetyLimit = NormalizeFloat(HighSafetyLimit,
                ConsciousnessAnchorCurveService.DefaultHighSafetyLimit,
                HighFactor,
                ConsciousnessAnchorCurveService.TechnicalMaximumFactor);
        }

        // Read and normalize into local primitives. The original settings
        // object is never mutated by Pawn XP, tooltip or preview reads, and
        // this avoids allocating a temporary mutable settings clone on every
        // learn-rate / daily-cap factor request.
        internal ConsAnchorCurveProfile ToProfile()
        {
            float lowAnchor = NormalizeFloat(LowAnchor,
                ConsciousnessAnchorCurveService.DefaultLowAnchor, 0.05f, 0.95f);
            float lowFactor = NormalizeFloat(LowFactor,
                ConsciousnessAnchorCurveService.DefaultLowFactor, 0f, 1f);
            int lowMode = NormalizeMode(LowCurve, ConsAnchorCurveMode.Linear);
            float lowCurvature = NormalizeFloat(LowCurvature,
                ConsciousnessAnchorCurveService.DefaultCurvature, 0f, 2f);
            float highAnchor = NormalizeFloat(HighAnchor,
                ConsciousnessAnchorCurveService.DefaultHighAnchor, 1.01f, 10f);
            float highFactor = NormalizeFloat(HighFactor,
                ConsciousnessAnchorCurveService.DefaultHighFactor, 1f,
                ConsciousnessAnchorCurveService.TechnicalMaximumFactor - 0.01f);
            int highMode = NormalizeMode(HighCurve, ConsAnchorCurveMode.Linear);
            float highCurvature = NormalizeFloat(HighCurvature,
                ConsciousnessAnchorCurveService.DefaultCurvature, 0f, 2f);
            float diminishingLimit = NormalizeFloat(HighDiminishingLimit,
                ConsciousnessAnchorCurveService.DefaultHighDiminishingLimit,
                highFactor + 0.01f,
                ConsciousnessAnchorCurveService.TechnicalMaximumFactor);
            float safetyLimit = NormalizeFloat(HighSafetyLimit,
                ConsciousnessAnchorCurveService.DefaultHighSafetyLimit,
                highFactor,
                ConsciousnessAnchorCurveService.TechnicalMaximumFactor);
            return new ConsAnchorCurveProfile(
                lowAnchor, LowEnabled ? lowFactor : 1f,
                (ConsAnchorCurveMode)lowMode, lowCurvature,
                highAnchor, HighEnabled ? highFactor : 1f,
                (ConsAnchorCurveMode)highMode, highCurvature,
                diminishingLimit, safetyLimit);
        }

        private static int NormalizeMode(int value, ConsAnchorCurveMode fallback)
        {
            return value >= (int)ConsAnchorCurveMode.Diminishing &&
                   value <= (int)ConsAnchorCurveMode.Accelerating
                ? value : (int)fallback;
        }

        private static float NormalizeFloat(float value, float fallback,
            float minimum, float maximum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = fallback;
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }

    /// <summary>
    /// Single-source Anchor V2 storage: two independent profiles and schema.
    /// Old selector keys and old curve-strength keys are no longer read/written.
    /// </summary>
    internal static class ConsciousnessAnchorV2SettingsStore
    {
        internal const string Revision = "CONS-ANCHOR-V2-SETTINGS-P4BR2-R1-20261103";
        internal const int CurrentSchemaVersion = 3;

        internal static void ExposeData(XpCustomizerSettings settings)
        {
            if (settings == null) return;
            Scribe_Values.Look(ref settings.consciousnessAnchorSettingsVersion,
                "consciousnessAnchorSettingsVersion", CurrentSchemaVersion);

            if (settings.consciousnessAnchorSpeed == null)
                settings.consciousnessAnchorSpeed = new ConsAnchorV2ProfileSettings();
            if (settings.consciousnessAnchorCapacity == null)
                settings.consciousnessAnchorCapacity = new ConsAnchorV2ProfileSettings();

            settings.consciousnessAnchorSpeed.ExposeValues("consAnchorV2Speed");
            settings.consciousnessAnchorCapacity.ExposeValues("consAnchorV2Capacity");
        }

        internal static void NormalizeAfterLoad(XpCustomizerSettings settings)
        {
            if (settings == null) return;
            if (settings.consciousnessAnchorSpeed == null)
                settings.consciousnessAnchorSpeed = new ConsAnchorV2ProfileSettings();
            if (settings.consciousnessAnchorCapacity == null)
                settings.consciousnessAnchorCapacity = new ConsAnchorV2ProfileSettings();

            settings.consciousnessAnchorSpeed.Normalize();
            settings.consciousnessAnchorCapacity.Normalize();
            if (settings.consciousnessAnchorSettingsVersion < CurrentSchemaVersion)
                settings.consciousnessAnchorSettingsVersion = CurrentSchemaVersion;
        }

        internal static ConsAnchorCurveProfile GetSpeedProfile(XpCustomizerSettings settings)
        {
            return settings == null || settings.consciousnessAnchorSpeed == null
                ? ConsciousnessAnchorCurveService.CreateSpeedDefaults()
                : settings.consciousnessAnchorSpeed.ToProfile();
        }

        internal static ConsAnchorCurveProfile GetCapacityProfile(XpCustomizerSettings settings)
        {
            return settings == null || settings.consciousnessAnchorCapacity == null
                ? ConsciousnessAnchorCurveService.CreateCapacityDefaults()
                : settings.consciousnessAnchorCapacity.ToProfile();
        }
    }
}
