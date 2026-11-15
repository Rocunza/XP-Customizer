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



        // ================================================================
        // Daily Learning Capacity
        // ================================================================
        private static void AppendDailyCapacity(
            StringBuilder builder,
            SkillRecord skill)
        {
            LearningCapacityBreakdown breakdown =
                LearningCapacityService.GetBreakdown(
                    skill);


            if (breakdown == null)
            {
                return;
            }

            int finalCapacity =
                Mathf.Max(
                    0,
                    Mathf.RoundToInt(
                        breakdown.FinalCapacity));

            float finalCapacityFactor =
                breakdown.FinalCapacity /
                LearningCapacityService.VanillaDailyCap;

            int baseCapacity =
                Mathf.Max(
                    0,
                    Mathf.RoundToInt(
                        breakdown.BaseCapacity));

            float baseCapacityFactor =
                breakdown.BaseCapacity /
                LearningCapacityService.VanillaDailyCap;

            AppendSectionSpacing(
                builder);


            // Show actual computed full-rate daily capacity in parentheses.
            // Keep the numeric value, but omit the redundant "XP" unit label.
            builder.Append(
                (
                    SkillTooltipLocalizedPresentation.DailyCapTitle
                        .AsTipTitle() +
                    FormatPercent(
                        finalCapacityFactor) +
                    " (" +
                    finalCapacity +
                    ")"
                )
                .ToString());


            // ------------------------------------------------------------
            // Base
            // ------------------------------------------------------------

            builder.Append(
                "\n  - " + SkillTooltipLocalizedPresentation.BaseCapacityLabel + ": " +
                FormatPercent(
                    baseCapacityFactor) +
                " (" +
                baseCapacity +
                ")");


            // ------------------------------------------------------------
            // Dynamic source contributions
            // ------------------------------------------------------------

            if (breakdown.Contributions != null)
            {
                foreach (LearningCapacityContribution contribution in
                         breakdown.Contributions)
                {
                    if (contribution == null ||
                        Mathf.Approximately(
                            contribution.Modifier,
                            0f))
                    {
                        continue;
                    }


                    builder.Append(
                        "\n  - " +
                        contribution.Label +
                        ": " +
                        FormatSignedPercent(
                            contribution.Modifier));
                }
            }


            // ------------------------------------------------------------
            // Consciousness
            // ------------------------------------------------------------

            XpCustomizerSettings settings =
                XpCustomizerMod.Settings;


            bool consciousnessCapacityEnabled =
                settings != null &&
                settings.consciousnessAffectsLearning &&
                settings.consciousnessAffectsDailyCapacity;


            if (consciousnessCapacityEnabled &&
                skill.Pawn.health?.capacities != null)
            {
                float consciousness =
                    skill.Pawn.health.capacities.GetLevel(
                        PawnCapacityDefOf.Consciousness);


                string capacityConsLine = SkillTooltipLocalizedPresentation.BuildConsLine(
                    consciousness, breakdown.ConsciousnessFactor);
                if (!string.IsNullOrEmpty(capacityConsLine))
                {
                    builder.Append("\n").Append(capacityConsLine);
                }
            }


            // ------------------------------------------------------------
            // Extreme custom-setting floor
            // ------------------------------------------------------------

            if (breakdown.WasDynamicCapacityClampedToZero)
            {
                builder.Append(
                    "\n  - " + SkillTooltipLocalizedPresentation.CapacityFloorLabel + ": 0 XP");
            }
        }
    }
}
