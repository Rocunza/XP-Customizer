using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace XpCustomizer.LearningCapacity
{
    /// <summary>
    /// Allocating presentation detail only for explicit breakdown requests.
    /// LC1 preserves existing numerical and gameplay semantics.
    /// </summary>
    internal static class LearningCapacityBreakdownBuilder
    {
        internal static LearningCapacityBreakdown GetBreakdown(
            SkillRecord skill)
        {
            if (skill == null ||
                skill.Pawn == null)
            {
                return CreateVanillaBreakdown();
            }


            XpCustomizerSettings settings =
                XpCustomizerMod.Settings;


            if (settings == null)
            {
                return CreateVanillaBreakdown();
            }


            List<LearningCapacityContribution> contributions =
                new List<LearningCapacityContribution>();


            float dynamicModifier;
            float rawDynamicCapacity;
            float dynamicCapacity;
            float consciousnessFactor;


            float finalCapacity =
                LearningCapacityCalculator.CalculateDailyCap(
                    skill,
                    settings,
                    contributions,
                    out dynamicModifier,
                    out rawDynamicCapacity,
                    out dynamicCapacity,
                    out consciousnessFactor);


            return new LearningCapacityBreakdown(
                LearningCapacityNormalization.NormalizeBaseDailyCap(
                    settings.learningCapacityBaseDailyCap),
                dynamicModifier,
                rawDynamicCapacity,
                dynamicCapacity,
                consciousnessFactor,
                finalCapacity,
                contributions);
        }


        internal static LearningCapacityBreakdown CreateVanillaBreakdown()
        {
            return new LearningCapacityBreakdown(
                LearningCapacityService.VanillaDailyCap,
                0f,
                LearningCapacityService.VanillaDailyCap,
                LearningCapacityService.VanillaDailyCap,
                1f,
                LearningCapacityService.VanillaDailyCap);
        }


        internal static void AddCapacityContribution(
            List<LearningCapacityContribution> contributions,
            string label,
            float modifier)
        {
            if (contributions == null ||
                string.IsNullOrEmpty(label) ||
                Mathf.Approximately(
                    modifier,
                    0f))
            {
                return;
            }


            for (int i = 0;
                 i < contributions.Count;
                 i++)
            {
                LearningCapacityContribution existing =
                    contributions[i];


                if (existing == null ||
                    existing.Label != label)
                {
                    continue;
                }


                contributions[i] =
                    new LearningCapacityContribution(
                        label,
                        existing.Modifier +
                        modifier);


                return;
            }


            contributions.Add(
                new LearningCapacityContribution(
                    label,
                    modifier));
        }


        internal static string GetDefLabel<T>(
            string defName)
            where T : Def
        {
            T def =
                DefDatabase<T>
                    .GetNamedSilentFail(
                        defName);


            if (def == null)
            {
                return defName;
            }


            string label =
                def.LabelCap.ToString();


            return string.IsNullOrEmpty(
                    label)
                ? defName
                : label;
        }
    }
}
