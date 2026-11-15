using System;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.FurtherLearning;

namespace XpCustomizer.SkillTooltip
{
    /// <summary>
    /// L3-F2: labels and display-only formatting for lines owned by this mod.
    /// Never changes Def.LabelCap, vanilla/foreign tooltip text, gameplay data,
    /// or the original English formatter fixtures in SkillTooltipService.
    /// </summary>
    internal static class SkillTooltipLocalizedPresentation
    {
        internal const string Version =
            "XPC-L10N-L3F2-SKILL-TOOLTIPS-20261112-R1";
        internal const int KeyCount = 9;

#if DEBUG
        private static bool logged;
#endif

        private static string Label(string key)
        {
            return key.Translate().ToString();
        }

        internal static string DailyCapHeading
        {
            get { return Label("XPC_L3F2_Tooltip_DailyCap") + ":"; }
        }

        internal static string FurtherHeading
        {
            get { return Label("XPC_L3F2_Tooltip_Further") + ":"; }
        }

        internal static string BaseCapacityLinePrefix
        {
            get { return "- " + Label("XPC_L3F2_Tooltip_BaseCapacity") + ":"; }
        }

        internal static string WorkFactorLinePrefix
        {
            get { return "- " + Label("XPC_L3F2_Tooltip_GwsSpeed") + ": "; }
        }

        internal static string WillpowerLinePrefix
        {
            get { return "\n  - " + Label("XPC_L3F2_Tooltip_Willpower") + ":"; }
        }

        internal static string DailyCapTitle
        {
            get { return DailyCapHeading + " "; }
        }

        internal static string FurtherTitle
        {
            get { return FurtherHeading + " "; }
        }

        internal static string BaseCapacityLabel
        {
            get { return Label("XPC_L3F2_Tooltip_BaseCapacity"); }
        }

        internal static string CapacityFloorLabel
        {
            get { return Label("XPC_L3F2_Tooltip_CapacityFloor"); }
        }

        internal static string BaseValueLabel
        {
            get { return Label("XPC_L3F2_Tooltip_BaseValue"); }
        }

        internal static string FurtherFloorLabel
        {
            get { return Label("XPC_L3F2_Tooltip_FurtherFloor"); }
        }

        internal static string GwsSpeedLabel
        {
            get { return Label("XPC_L3F2_Tooltip_GwsSpeed"); }
        }

        internal static string ConsLabel
        {
            get { return Label("XPC_L3F2_Tooltip_Cons"); }
        }

        internal static string BuildGwsLine(float sourceRatio, float factor)
        {
            if (SkillTooltipService.IsNeutralAppliedMultiplier(factor))
                return string.Empty;
            return "  - " + GwsSpeedLabel + ": x" + factor.ToStringPercent("F0");
        }

        internal static string BuildConsLine(float sourceRatio, float factor)
        {
            if (SkillTooltipService.IsNeutralAppliedMultiplier(factor))
                return string.Empty;
            return "  - " + ConsLabel + " " +
                TooltipSourceRatioFormatter.Format(sourceRatio) +
                ": x" + factor.ToStringPercent("F0");
        }

        // The service owns the label "Cons <ratio>". Only this exact owned
        // prefix is localized; arbitrary source labels remain untouched.
        // This transformation never decides whether a contribution is active.
        internal static string FurtherSourceLabel(string sourceLabel)
        {
            if (sourceLabel != null && sourceLabel.StartsWith(
                    "Mental threshold ", StringComparison.Ordinal))
                return Label("XPC_L3F2_Tooltip_Willpower");
            if (sourceLabel != null && sourceLabel.StartsWith(
                    "Cons ", StringComparison.Ordinal))
                return ConsLabel + sourceLabel.Substring("Cons".Length);
            return sourceLabel;
        }

        // R5F1 original additive-first calculation and ordering are untouched.
        // All arguments come from the already-computed typed breakdown.
        internal static string BuildFurtherDetailLines(FurtherLearningBreakdown breakdown)
        {
            if (breakdown == null || breakdown.Contributions == null ||
                breakdown.Contributions.Count == 0)
                return string.Empty;

            var details = new StringBuilder();
            details.Append("\n  - ").Append(BaseValueLabel).Append(": ")
                .Append(Mathf.RoundToInt(breakdown.BaseFactor * 100f))
                .Append("%");

            foreach (FurtherLearningContribution contribution in breakdown.Contributions)
            {
                if (contribution == null || contribution.IsMultiplier) continue;
                int points = Mathf.RoundToInt(contribution.PercentagePoints);
                details.Append("\n  - ").Append(FurtherSourceLabel(contribution.Label))
                    .Append(": ").Append(points > 0 ? "+" : string.Empty)
                    .Append(points).Append("%");
            }

            foreach (FurtherLearningContribution contribution in breakdown.Contributions)
            {
                if (contribution == null || !contribution.IsMultiplier ||
                    SkillTooltipService.IsNeutralAppliedMultiplier(contribution.Multiplier))
                    continue;
                details.Append("\n  - ").Append(FurtherSourceLabel(contribution.Label))
                    .Append(": x")
                    .Append(contribution.Multiplier.ToStringPercent("F0"));
            }

            if (breakdown.WasClampedToZero)
                details.Append("\n  - ").Append(FurtherFloorLabel).Append(": 0%");
            return details.ToString();
        }

        internal static void LogFirstValidTooltip()
        {
#if DEBUG
            if (logged) return;
            logged = true;
            Log.Message("[XPC-L10N-L3F2][KEYED_SKILL_TOOLTIP] version=" + Version +
                "; keys=" + KeyCount +
                "; sampleKey=XPC_L3F2_Tooltip_DailyCap; sample=" +
                Label("XPC_L3F2_Tooltip_DailyCap") +
                "; source=OWN_SKILL_TOOLTIP; build=" + XpCustomizerMod.BuildId);
#endif
        }
    }
}
