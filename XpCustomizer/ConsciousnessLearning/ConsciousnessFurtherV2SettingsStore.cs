using System;
using Verse;

namespace XpCustomizer.ConsciousnessLearning
{
    /// <summary>
    /// Separate post-cap Consciousness multiplier profile. Never shares the
    /// Speed/Capacity profile objects or their serialized keys.
    /// The previous pp keys remain inert for compatibility and rollback.
    /// </summary>
    internal sealed class ConsciousnessFurtherV2Settings
    {
        internal const float DefaultLowAnchor = 0.80f;
        internal const float DefaultLowFactor = 0.80f;
        internal const float DefaultHighAnchor = 1.20f;
        internal const float DefaultHighFactor = 1.20f;
        internal const float DefaultCurvature = 1f;
        internal const float DefaultHighLimit = 2f;
        internal const float DefaultSafetyCap = 2f;

        // Existing XML written under the previous Diminishing(0) default may omit this key.
        // Preserve that missing-key interpretation, not the old fresh-install preset.
        internal const int LegacyXmlHighCurveFallback = (int)ConsAnchorCurveMode.Diminishing;

        internal bool LowEnabled = true;
        internal bool HighEnabled = true;
        internal float LowAnchor = DefaultLowAnchor;
        internal float LowFactor = DefaultLowFactor;
        internal int LowCurve = (int)ConsAnchorCurveMode.Linear;
        internal float LowCurvature = DefaultCurvature;
        internal float HighAnchor = DefaultHighAnchor;
        internal float HighFactor = DefaultHighFactor;
        internal int HighCurve = (int)ConsAnchorCurveMode.Linear;
        internal float HighCurvature = DefaultCurvature;
        internal float HighDiminishingLimit = DefaultHighLimit;
        internal float HighSafetyLimit = DefaultSafetyCap;

        internal void ExposeValues(string prefix)
        {
            Scribe_Values.Look(ref LowEnabled, prefix + "LowEnabled", true);
            Scribe_Values.Look(ref HighEnabled, prefix + "HighEnabled", true);
            Scribe_Values.Look(ref LowAnchor, prefix + "LowAnchor", DefaultLowAnchor);
            Scribe_Values.Look(ref LowFactor, prefix + "LowFactor", DefaultLowFactor);
            Scribe_Values.Look(ref LowCurve, prefix + "LowCurve", (int)ConsAnchorCurveMode.Linear);
            Scribe_Values.Look(ref LowCurvature, prefix + "LowCurvature", DefaultCurvature);
            Scribe_Values.Look(ref HighAnchor, prefix + "HighAnchor",
                LegacyCurveXmlDefaults.ConsFurtherHighAnchor);
            Scribe_Values.Look(ref HighFactor, prefix + "HighFactor",
                LegacyCurveXmlDefaults.ConsFurtherHighFactor);
            Scribe_Values.Look(ref HighCurve, prefix + "HighCurve", LegacyXmlHighCurveFallback);
            Scribe_Values.Look(ref HighCurvature, prefix + "HighCurvature", DefaultCurvature);
            Scribe_Values.Look(ref HighDiminishingLimit, prefix + "HighDiminishingLimit", DefaultHighLimit);
            Scribe_Values.Look(ref HighSafetyLimit, prefix + "HighSafetyLimit", DefaultSafetyCap);
        }

        internal void Normalize()
        {
            LowAnchor = Bound(LowAnchor, DefaultLowAnchor, 0.05f, 0.95f);
            LowFactor = Bound(LowFactor, DefaultLowFactor, 0f, 1f);
            LowCurve = ValidMode(LowCurve, ConsAnchorCurveMode.Linear);
            LowCurvature = Bound(LowCurvature, DefaultCurvature, 0f, 2f);
            HighAnchor = Bound(HighAnchor, DefaultHighAnchor, 1.01f, 10f);
            HighFactor = Bound(HighFactor, DefaultHighFactor, 1f, 19.99f);
            HighCurve = ValidMode(HighCurve, ConsAnchorCurveMode.Linear);
            HighCurvature = Bound(HighCurvature, DefaultCurvature, 0f, 2f);
            HighDiminishingLimit = Bound(HighDiminishingLimit, DefaultHighLimit,
                HighFactor + 0.01f, ConsciousnessAnchorCurveService.TechnicalMaximumFactor);
            HighSafetyLimit = Bound(HighSafetyLimit, DefaultSafetyCap,
                HighFactor, ConsciousnessAnchorCurveService.TechnicalMaximumFactor);
        }

        internal ConsAnchorCurveProfile ToProfile()
        {
            // Normalize a local copy only: gameplay reads never edit ModSettings.
            float lowAnchor = Bound(LowAnchor, DefaultLowAnchor, 0.05f, 0.95f);
            float lowFactor = Bound(LowFactor, DefaultLowFactor, 0f, 1f);
            float highAnchor = Bound(HighAnchor, DefaultHighAnchor, 1.01f, 10f);
            float highFactor = Bound(HighFactor, DefaultHighFactor, 1f, 19.99f);
            return new ConsAnchorCurveProfile(
                lowAnchor, LowEnabled ? lowFactor : 1f,
                (ConsAnchorCurveMode)ValidMode(LowCurve, ConsAnchorCurveMode.Linear),
                Bound(LowCurvature, DefaultCurvature, 0f, 2f),
                highAnchor, HighEnabled ? highFactor : 1f,
                (ConsAnchorCurveMode)ValidMode(HighCurve, ConsAnchorCurveMode.Linear),
                Bound(HighCurvature, DefaultCurvature, 0f, 2f),
                Bound(HighDiminishingLimit, DefaultHighLimit, highFactor + 0.01f, 20f),
                Bound(HighSafetyLimit, DefaultSafetyCap, highFactor, 20f));
        }

        private static int ValidMode(int value, ConsAnchorCurveMode fallback)
        {
            return value >= 0 && value <= 2 ? value : (int)fallback;
        }

        private static float Bound(float value, float fallback, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) value = fallback;
            return Math.Max(min, Math.Min(max, value));
        }
    }

    internal static class ConsciousnessFurtherV2SettingsStore
    {
        internal const int SchemaVersion = 1;
        internal const string Revision = "CONS-FURTHER-V2-DEFAULTS-R3-20261104";
        private const string Prefix = "consFurtherV2";

        internal static void ExposeData(XpCustomizerSettings settings)
        {
            if (settings == null) return;
            Scribe_Values.Look(ref settings.consciousnessFurtherV2Version,
                "consciousnessFurtherV2Version", SchemaVersion);
            if (settings.consciousnessFurtherV2 == null)
                settings.consciousnessFurtherV2 = new ConsciousnessFurtherV2Settings();
            settings.consciousnessFurtherV2.ExposeValues(Prefix);
        }

        internal static void NormalizeAfterLoad(XpCustomizerSettings settings)
        {
            if (settings == null) return;
            if (settings.consciousnessFurtherV2 == null)
                settings.consciousnessFurtherV2 = new ConsciousnessFurtherV2Settings();
            settings.consciousnessFurtherV2.Normalize();
            settings.consciousnessFurtherV2Version = SchemaVersion;
        }

        internal static ConsAnchorCurveProfile GetProfile(XpCustomizerSettings settings)
        {
            return settings == null || settings.consciousnessFurtherV2 == null
                ? new ConsciousnessFurtherV2Settings().ToProfile()
                : settings.consciousnessFurtherV2.ToProfile();
        }
    }
}
