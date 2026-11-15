using RimWorld;
using Verse;

namespace XpCustomizer.ConsciousnessLearning
{
    /// <summary>Daily Learning Capacity Consciousness factor. Anchor V2 only.</summary>
    public static class ConsciousnessCapacityService
    {
        public const float NeutralFactor = 1f;

        public static float GetFactor(Pawn pawn)
        {
            if (pawn == null || pawn.health?.capacities == null)
                return NeutralFactor;
            return CalculateConfiguredFactor(
                pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness),
                XpCustomizerMod.Settings);
        }

        internal static float CalculateConfiguredFactor(
            float consciousness, XpCustomizerSettings settings)
        {
            return ConsciousnessCurveDispatcher.CalculateCapacity(consciousness, settings);
        }
    }
}
