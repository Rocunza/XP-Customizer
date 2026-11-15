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
        // Further Learning
        // ================================================================

        private static void AppendFurtherLearning(
            StringBuilder builder,
            SkillRecord skill)
        {
            FurtherLearningBreakdown breakdown =
                FurtherLearningService.GetBreakdown(
                    skill);


            if (breakdown == null)
            {
                return;
            }


            AppendSectionSpacing(
                builder);


            builder.Append(
                (
                    SkillTooltipLocalizedPresentation.FurtherTitle
                        .AsTipTitle() +
                    FormatPercent(
                        breakdown.FinalFactor)
                )
                .ToString());


            builder.Append(SkillTooltipLocalizedPresentation.BuildFurtherDetailLines(breakdown));
        }

        // Pure formatter: additive percentage points first, then the full
        // Consciousness multiplier. The displayed xNN% is rounded ONLY for UI;
        // production calculations retain the unrounded multiplier.
        internal static string BuildFurtherLearningDetailLines(FurtherLearningBreakdown breakdown)
        {
            if (breakdown == null || breakdown.Contributions == null ||
                breakdown.Contributions.Count == 0)
                return string.Empty;

            var details = new StringBuilder();
            details.Append("\n  - Base value: ").Append(FormatPercent(breakdown.BaseFactor));

            foreach (FurtherLearningContribution contribution in breakdown.Contributions)
            {
                if (contribution == null || contribution.IsMultiplier) continue;
                // Preserve live MentalBreakThreshold stat semantics while
                // presenting only the derived Willpower adjustment.
                string label = contribution.Label != null &&
                    contribution.Label.StartsWith(
                        "Mental threshold ", System.StringComparison.Ordinal)
                    ? "Willpower" : contribution.Label;
                details.Append("\n  - ").Append(label).Append(": ")
                    .Append(FormatPercentagePoints(contribution.PercentagePoints));
            }

            foreach (FurtherLearningContribution contribution in breakdown.Contributions)
            {
                if (contribution == null || !contribution.IsMultiplier ||
                    IsNeutralAppliedMultiplier(contribution.Multiplier)) continue;
                details.Append("\n  - ").Append(contribution.Label).Append(": ")
                    .Append(FormatMultiplierPercent(contribution.Multiplier));
            }

            if (breakdown.WasClampedToZero)
                details.Append("\n  - Minimum floor applied: 0%");
            return details.ToString();
        }
    }
}
