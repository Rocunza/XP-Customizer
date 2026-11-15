namespace XpCustomizer.ConsciousnessLearning
{
    /// <summary>
    /// Phase 4B-R2: single Anchor V2 gameplay route. Existing Harmony
    /// entrypoints are unchanged and are still gated by the Cons master and
    /// independent Speed/Capacity switches. GWS and Further Learning are separate.
    /// </summary>
    internal static class ConsciousnessCurveDispatcher
    {
        internal const string Revision = "CONS-V2-ONLY-DISPATCH-P4BR2-R1-20261103";

        internal static float CalculateSpeed(float consciousness, XpCustomizerSettings settings)
        {
            if (settings == null || !settings.consciousnessAffectsLearning ||
                !settings.consciousnessAffectsLearningSpeed)
                return 1f;
            return ConsciousnessAnchorCurveService.Calculate(
                consciousness, ConsciousnessAnchorV2SettingsStore.GetSpeedProfile(settings));
        }

        internal static float CalculateCapacity(float consciousness, XpCustomizerSettings settings)
        {
            if (settings == null || !settings.consciousnessAffectsLearning ||
                !settings.consciousnessAffectsDailyCapacity)
                return 1f;
            return ConsciousnessAnchorCurveService.Calculate(
                consciousness, ConsciousnessAnchorV2SettingsStore.GetCapacityProfile(settings));
        }

        internal static bool HasConfiguredSpeedEffect(XpCustomizerSettings settings)
        {
            return settings != null && settings.consciousnessAffectsLearning &&
                settings.consciousnessAffectsLearningSpeed &&
                HasNonNeutralAnchorEffect(settings.consciousnessAnchorSpeed);
        }

        /// <summary>
        /// Allocation-free activation guard on the LearningSaturatedToday path;
        /// the fact that a profile is configured does not change a vanilla
        /// daily-cap unless the Consciousness effect is actually enabled.
        /// </summary>
        internal static bool HasConfiguredCapacityEffect(XpCustomizerSettings settings)
        {
            return settings != null && settings.consciousnessAffectsLearning &&
                settings.consciousnessAffectsDailyCapacity &&
                HasNonNeutralAnchorEffect(settings.consciousnessAnchorCapacity);
        }

        private static bool HasNonNeutralAnchorEffect(ConsAnchorV2ProfileSettings profile)
        {
            // Missing/corrupt profile normalizes to V2 defaults. The
            // low-side endpoint remains x0 for any enabled nonneutral low.
            if (profile == null) return true;
            if (profile.LowEnabled)
            {
                float low = profile.LowFactor;
                if (float.IsNaN(low) || float.IsInfinity(low))
                    low = ConsciousnessAnchorCurveService.DefaultLowFactor;
                if (low < 1f) return true;
            }
            if (profile.HighEnabled)
            {
                float high = profile.HighFactor;
                if (float.IsNaN(high) || float.IsInfinity(high))
                    high = ConsciousnessAnchorCurveService.DefaultHighFactor;
                if (high > 1f) return true;
            }
            return false;
        }
    }
}
