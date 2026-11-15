using RimWorld;
using Verse;

namespace XpCustomizer.ConsciousnessLearning
{
    /// <summary>Consciousness learning-speed factor. Anchor V2 only.</summary>
    public static class ConsciousnessLearningService
    {
        public const float NeutralFactor = 1f;

        public static float GetFactor(Pawn pawn)
        {
            if (pawn == null || pawn.health?.capacities == null)
                return NeutralFactor;
            return GetFactor(pawn.health.capacities.GetLevel(
                PawnCapacityDefOf.Consciousness));
        }

        public static float GetFactor(float consciousness)
        {
            return CalculateConfiguredFactor(consciousness, XpCustomizerMod.Settings);
        }

        internal static float CalculateConfiguredFactor(
            float consciousness, XpCustomizerSettings settings)
        {
            return ConsciousnessCurveDispatcher.CalculateSpeed(consciousness, settings);
        }
    }
}
