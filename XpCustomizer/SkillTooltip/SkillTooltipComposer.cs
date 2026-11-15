using System;
using RimWorld;
using Verse;

namespace XpCustomizer.SkillTooltip
{
    /// <summary>
    /// TT1: preserves the original SkillUI Postfix edit ordering and
    /// non-atomic ref-result behavior; it does not own Harmony patches.
    /// </summary>
    internal static class SkillTooltipComposer
    {
        internal const string HeadingFixVersion =
            "XPC-L3F2-HF1-LINE-ANCHORED-HEADING-20261112-R1";

#if DEBUG
        private static bool headingMatcherLogged;
#endif

        internal static void Compose(SkillRecord skill, ref string __result)
        {
            // Own tooltip display version; no writes or changes to vanilla.
            SkillTooltipLocalizedPresentation.LogFirstValidTooltip();

            // ------------------------------------------------------------
            // Remove vanilla saturated-learning summary
            // ------------------------------------------------------------

            if (SkillTooltipService
                    .ShouldReplaceVanillaSaturatedLine())
            {
                ReplaceVanillaSaturatedLine(
                    skill,
                    ref __result);
            }


            // ------------------------------------------------------------
            // Work Factor + Consciousness -> Learning speed
            // ------------------------------------------------------------

            string learningSpeedLines =
                SkillTooltipService.BuildLearningSpeedModifierLines(skill);

            bool learningSpeedInserted = string.IsNullOrEmpty(learningSpeedLines);

            if (!learningSpeedInserted)
            {
                // Further Learning is a distinct post-cap mechanic and is
                // deliberately excluded from this full-rate headline.
                TryReplaceLearningSpeedHeadline(skill, ref __result);

                // Insert Work Factor directly above Consc, as one block,
                // preserving vanilla and other mods' existing bullets.
                learningSpeedInserted = TryInsertIntoLearningSpeed(
                    ref __result, learningSpeedLines);
            }


            // ------------------------------------------------------------
            // Daily Capacity / Further Learning block
            // ------------------------------------------------------------

            string progressiveBlock =
                SkillTooltipService
                    .BuildXpCustomizerBlock(
                        skill,
                        !learningSpeedInserted);


            if (string.IsNullOrEmpty(
                    progressiveBlock))
            {
                return;
            }


            __result =
                __result.TrimEnd();


            __result +=
                "\n\n" +
                progressiveBlock;
        }

        private static void ReplaceVanillaSaturatedLine(
            SkillRecord skill,
            ref string result)
        {
            if (!skill.LearningSaturatedToday)
            {
                return;
            }


            // Exact vanilla line produced by SkillUI.
            //
            // We only replace it when the exact localized string is present.
            // If another mod has already changed the line, leave it alone.
            string vanillaLine =
                SkillTooltipPresentationValues.GetVanillaSaturatedLine(skill);


            if (!result.Contains(
                    vanillaLine))
            {
                return;
            }


            string replacementLine =
                SkillTooltipPresentationValues.GetAdjustedSaturatedLine(skill);


            result =
                SkillTooltipTextEditor.ReplaceAllExact(
                    result,
                    vanillaLine,
                    replacementLine);
        }

        private static bool TryReplaceLearningSpeedHeadline(
            SkillRecord skill,
            ref string result)
        {
            int headingStart;
            int headingEnd;
            int sectionEnd;


            if (!TryFindLearningSpeedSection(
                    result,
                    out headingStart,
                    out headingEnd,
                    out sectionEnd))
            {
                return false;
            }


            string replacementLine =
                SkillTooltipPresentationValues.BuildLearningSpeedHeadline(skill);


            SkillTooltipTextEditor.ReplaceHeadingRange(
                    ref result,
                    headingStart,
                    headingEnd,
                    replacementLine);


            return true;
        }

        private static bool TryInsertIntoLearningSpeed(
            ref string result,
            string learningSpeedLines)
        {
            if (string.IsNullOrEmpty(result) ||
                string.IsNullOrEmpty(learningSpeedLines))
            {
                return false;
            }


            int headingStart;
            int headingEnd;
            int sectionEnd;


            if (!TryFindLearningSpeedSection(
                    result,
                    out headingStart,
                    out headingEnd,
                    out sectionEnd))
            {
                return false;
            }


            result =
                SkillTooltipTextEditor.InsertModifierLines(
                    result,
                    sectionEnd,
                    learningSpeedLines);


            return true;
        }

        // Keep the empty-input guard before localized label evaluation.
        // This preserves the original search/evaluation boundary.
        private static bool TryFindLearningSpeedSection(
            string result,
            out int headingStart,
            out int headingEnd,
            out int sectionEnd)
        {
            if (string.IsNullOrEmpty(result))
            {
                headingStart = -1;
                headingEnd = -1;
                sectionEnd = -1;
                return false;
            }

            string localizedLabel =
                SkillTooltipPresentationValues.GetLearningSpeedLabel();

            bool found = SkillTooltipTextEditor.TryFindLearningSpeedSection(
                result,
                localizedLabel,
                out headingStart,
                out headingEnd,
                out sectionEnd);

#if DEBUG
            if (!headingMatcherLogged)
            {
                headingMatcherLogged = true;
                Log.Message("[XPC-TT1-HF1][HEADING_MATCHER] version=" +
                    HeadingFixVersion +
                    "; policy=LINE_ANCHORED_UNIQUE_RICHTEXT; firstMatch=" +
                    (found ? "FOUND" : "NOT_FOUND") +
                    "; source=SKILLUI_POSTFIX; writes=0; build=" +
                    XpCustomizerMod.BuildId);
            }
#endif
            return found;
        }
    }
}
