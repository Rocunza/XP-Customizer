using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.FurtherLearning;
using XpCustomizer.LearningCapacity;

namespace XpCustomizer.SkillTooltip
{
    /// <summary>
    /// TT1: prepare original vanilla-localized display values. Read only.
    /// Gameplay-learning and further-cap mechanics remain in their services.
    /// </summary>
    internal static class SkillTooltipPresentationValues
    {
        internal static string GetLearningSpeedLabel()
        {
            return "LearningSpeed".Translate().ToString();
        }

        internal static string GetVanillaSaturatedLine(SkillRecord skill)
        {
            return "LearnedMaxToday".Translate(
                    skill.xpSinceMidnight
                        .ToString("F0"),
                    4000,
                    0.20f
                        .ToStringPercent("F0"))
                .ToString();
        }

        internal static string GetAdjustedSaturatedLine(SkillRecord skill)
        {
            int finalCapacity =
                Mathf.Max(
                    0,
                    Mathf.RoundToInt(
                        LearningCapacityService
                            .GetDailyCap(
                                skill)));

            float furtherLearningFactor =
                FurtherLearningService
                    .GetFactor(
                        skill);

            return "LearnedMaxToday".Translate(
                    skill.xpSinceMidnight
                        .ToString("F0"),
                    finalCapacity,
                    furtherLearningFactor
                        .ToStringPercent("F0"))
                .ToString();
        }

        internal static string BuildLearningSpeedHeadline(SkillRecord skill)
        {
            float effectiveLearningSpeed =
                GetEffectiveFullRateLearningSpeed(skill);

            return (
                    ("LearningSpeed".Translate() + ": ")
                        .AsTipTitle() +
                    effectiveLearningSpeed
                        .ToStringPercent()
                )
                .ToString();
        }

        internal static float GetEffectiveFullRateLearningSpeed(
            SkillRecord skill)
        {
            if (skill == null ||
                skill.Pawn == null)
            {
                return 1f;
            }


            // This mirrors the value RimWorld presents as
            // "Learning speed":
            //
            // Passion
            // x Global Learning Factor
            // x Animals Learning Factor when applicable
            //
            // GWS and Consciousness add independent full-rate multipliers.
            // Work Factor describes ordinary learning. Book-reading XP
            // can opt out without changing this generic SkillUI headline.
            //
            // Further Learning is intentionally excluded because
            // it is the separate post-cap efficiency.

            float factor =
                skill.passion
                    .GetLearningFactor();


            factor *=
                skill.Pawn.GetStatValue(
                    StatDefOf.GlobalLearningFactor);


            if (skill.def ==
                SkillDefOf.Animals)
            {
                factor *=
                    skill.Pawn.GetStatValue(
                        StatDefOf.AnimalsLearningFactor);
            }


            factor *=
                SkillTooltipService.GetWorkFactorLearningMultiplier(skill);

            factor *=
                ConsciousnessLearningService.GetFactor(skill.Pawn);


            return factor;
        }
    }
}
