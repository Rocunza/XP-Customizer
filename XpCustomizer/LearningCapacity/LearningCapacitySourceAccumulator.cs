using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace XpCustomizer.LearningCapacity
{
    /// <summary>
    /// Accumulate per-source percentage points in original evaluation order.
    /// LC1 preserves existing numerical and gameplay semantics.
    /// </summary>
    internal static class LearningCapacitySourceAccumulator
    {
        internal static void AddTraitModifier(
            Pawn pawn,
            string defName,
            float percentagePoints,
            List<LearningCapacityContribution> contributions,
            ref float totalPercentagePoints)
        {
            percentagePoints =
                LearningCapacityNormalization.NormalizeSourcePercentagePoints(
                    percentagePoints);


            if (Mathf.Approximately(
                    percentagePoints,
                    0f) ||
                !LearningCapacitySourceDetector.HasActiveTrait(
                    pawn,
                    defName))
            {
                return;
            }


            totalPercentagePoints +=
                percentagePoints;


            if (contributions != null)
            {
                LearningCapacityBreakdownBuilder.AddCapacityContribution(
                    contributions,
                    LearningCapacitySourceDetector.GetActiveTraitLabel(
                        pawn,
                        defName),
                    percentagePoints /
                    100f);
            }
        }


        internal static void AddGeneModifier(
            Pawn pawn,
            string defName,
            float percentagePoints,
            List<LearningCapacityContribution> contributions,
            ref float totalPercentagePoints)
        {
            percentagePoints =
                LearningCapacityNormalization.NormalizeSourcePercentagePoints(
                    percentagePoints);


            if (Mathf.Approximately(
                    percentagePoints,
                    0f) ||
                !LearningCapacitySourceDetector.HasActiveGene(
                    pawn,
                    defName))
            {
                return;
            }


            totalPercentagePoints +=
                percentagePoints;


            if (contributions != null)
            {
                LearningCapacityBreakdownBuilder.AddCapacityContribution(
                    contributions,
                    LearningCapacityBreakdownBuilder.GetDefLabel<GeneDef>(
                        defName),
                    percentagePoints /
                    100f);
            }
        }


        internal static void AddHediffModifier(
            Pawn pawn,
            string defName,
            float percentagePoints,
            List<LearningCapacityContribution> contributions,
            ref float totalPercentagePoints)
        {
            percentagePoints =
                LearningCapacityNormalization.NormalizeSourcePercentagePoints(
                    percentagePoints);


            if (Mathf.Approximately(
                    percentagePoints,
                    0f) ||
                !LearningCapacitySourceDetector.HasActiveHediff(
                    pawn,
                    defName))
            {
                return;
            }


            totalPercentagePoints +=
                percentagePoints;


            if (contributions != null)
            {
                LearningCapacityBreakdownBuilder.AddCapacityContribution(
                    contributions,
                    LearningCapacityBreakdownBuilder.GetDefLabel<HediffDef>(
                        defName),
                    percentagePoints /
                    100f);
            }
        }


        internal static void AddPreceptModifier(
            Pawn pawn,
            string defName,
            float percentagePoints,
            List<LearningCapacityContribution> contributions,
            ref float totalPercentagePoints)
        {
            percentagePoints =
                LearningCapacityNormalization.NormalizeSourcePercentagePoints(
                    percentagePoints);


            if (Mathf.Approximately(
                    percentagePoints,
                    0f) ||
                !LearningCapacitySourceDetector.HasActivePrecept(
                    pawn,
                    defName))
            {
                return;
            }


            totalPercentagePoints +=
                percentagePoints;


            if (contributions != null)
            {
                LearningCapacityBreakdownBuilder.AddCapacityContribution(
                    contributions,
                    LearningCapacityBreakdownBuilder.GetDefLabel<PreceptDef>(
                        defName),
                    percentagePoints /
                    100f);
            }
        }
    }
}
