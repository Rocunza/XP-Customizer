using RimWorld;
using UnityEngine;
using Verse;

namespace XpCustomizer.ConsciousnessLearning
{
    /// <summary>
    /// Post-cap Consciousness multiplier. The independent Further profile
    /// shares only the pure Anchor V2 curve calculator with Speed/Capacity.
    /// It never writes settings or adds percentage points to the base rate.
    /// </summary>
    public static class ConsciousnessFurtherLearningService
    {
        public const float NeutralMultiplier = 1f;
        public const string Revision = "CONS-FURTHER-MULTIPLIER-V1-20261104";

        public static float GetMultiplier(Pawn pawn)
        {
            if (pawn == null || pawn.health?.capacities == null)
                return NeutralMultiplier;
            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            if (settings == null || !settings.consciousnessAffectsLearning ||
                !settings.consciousnessAffectsFurtherLearning)
                return NeutralMultiplier;
            return CalculateMultiplier(
                pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness), settings);
        }

        // A pure settings-aware entry point for UI, detached regressions and
        // gameplay. Independent of the two other Consciousness profiles.
        internal static float CalculateMultiplier(float consciousness, XpCustomizerSettings settings)
        {
            if (settings == null) return NeutralMultiplier;
            return ConsciousnessAnchorCurveService.Calculate(consciousness,
                ConsciousnessFurtherV2SettingsStore.GetProfile(settings));
        }

        internal static bool HasConfiguredEffect(XpCustomizerSettings settings)
        {
            if (settings == null || !settings.consciousnessAffectsLearning ||
                !settings.consciousnessAffectsFurtherLearning)
                return false;
            ConsciousnessFurtherV2Settings p = settings.consciousnessFurtherV2;
            if (p == null) return true; // default profile is non-neutral
            ConsAnchorCurveProfile normalized = p.ToProfile();
            return normalized.LowFactor < 1f || normalized.HighFactor > 1f;
        }
    }
}
