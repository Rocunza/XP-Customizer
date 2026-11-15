using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.FurtherLearning;
using XpCustomizer.Gws;
using XpCustomizer.LearningCapacity;

namespace XpCustomizer.SkillTooltip
{
    // TT2: same CLR type; original method bodies remain byte-identical.
    internal static partial class SkillTooltipService
    {


        internal static string BuildConsPreviewLine(float consciousness, float factor)
        {
            if (IsNeutralAppliedMultiplier(factor)) return string.Empty;
            return "  - Cons " + TooltipSourceRatioFormatter.Format(consciousness) +
                ": " + FormatMultiplierPercent(factor);
        }

        // ================================================================
        // Consciousness learning line
        // ================================================================

        public static string BuildConsciousnessLearningLine(
            SkillRecord skill)
        {
            if (skill == null ||
                skill.Pawn == null)
            {
                return string.Empty;
            }


            if (!IsConsciousnessLearningSpeedPresentationActive())
            {
                return string.Empty;
            }


            Pawn pawn =
                skill.Pawn;


            if (pawn.health?.capacities == null)
            {
                return string.Empty;
            }


            float factor =
                ConsciousnessLearningService.GetFactor(
                    pawn);

            if (IsNeutralAppliedMultiplier(factor))
            {
                return string.Empty;
            }

            float consciousness =
                pawn.health.capacities.GetLevel(
                    PawnCapacityDefOf.Consciousness);


            return SkillTooltipLocalizedPresentation.BuildConsLine(consciousness, factor);
        }
    }
}
