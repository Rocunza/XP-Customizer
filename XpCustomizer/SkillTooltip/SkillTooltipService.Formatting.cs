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


        // Presentation ONLY: hide exactly neutral EFFECTS, not neutral source
        // stats. Inspect the unrounded effective multiplier so small actual
        // modifiers are never erased by integer-percent formatting.
        internal static bool IsNeutralAppliedMultiplier(float factor)
        {
            return factor == 1f;
        }


        // ================================================================
        // Formatting
        // ================================================================

        private static string FormatMultiplierPercent(
            float factor)
        {
            return
                "x" +
                factor.ToStringPercent(
                    "F0");
        }


        private static string FormatPercent(
            float factor)
        {
            return
                Mathf.RoundToInt(
                    factor *
                    100f)
                .ToString() +
                "%";
        }


        private static string FormatSignedPercent(
            float modifier)
        {
            int percentage =
                Mathf.RoundToInt(
                    modifier *
                    100f);


            string sign =
                percentage > 0
                    ? "+"
                    : string.Empty;


            return
                sign +
                percentage +
                "%";
        }


        private static string FormatPercentagePoints(
            float percentagePoints)
        {
            int roundedPercentagePoints =
                Mathf.RoundToInt(
                    percentagePoints);


            string sign =
                roundedPercentagePoints > 0
                    ? "+"
                    : string.Empty;


            return
                sign +
                roundedPercentagePoints +
                "%";
        }
    }
}
