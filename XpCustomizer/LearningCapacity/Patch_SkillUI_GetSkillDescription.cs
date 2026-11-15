using HarmonyLib;
using RimWorld;
using XpCustomizer.SkillTooltip;

namespace XpCustomizer.LearningCapacity
{
    /// <summary>
    /// Connects XP Customizer tooltip presentation to
    /// RimWorld's skill description.
    ///
    /// Responsibilities:
    /// - Remove vanilla's saturated-learning summary when safe.
    /// - Update the displayed Learning speed with GWS and Consciousness.
    /// - Insert Work Factor immediately above Consciousness in the breakdown.
    /// - Append Daily Learning Capacity and Further Learning.
    ///
    /// Gameplay calculations do not belong here.
    /// </summary>
    [HarmonyPatch(
        typeof(SkillUI),
        "GetSkillDescription")]
    public static class Patch_SkillUI_GetSkillDescription
    {
        public static void Postfix(
            SkillRecord __0,
            ref string __result)
        {
            SkillRecord skill =
                __0;


            if (skill == null ||
                skill.Pawn == null ||
                string.IsNullOrEmpty(__result))
            {
                return;
            }

            // ------------------------------------------------------------
            // Disabled skills
            // ------------------------------------------------------------
            //
            // RimWorld deliberately omits learning-speed information for
            // totally disabled skills.
            //
            // Keep those tooltips completely vanilla:
            // no Consciousness, Capacity, or Further Learning information.

            if (skill.TotallyDisabled)
            {
                return;
            }


            // ------------------------------------------------------------
            // Vanilla-neutral pass-through
            // ------------------------------------------------------------
            //
            // When every tooltip-facing system is neutral, do not touch the
            // vanilla string at all. This also avoids needlessly overriding
            // presentation changes made by other mods.

            if (!SkillTooltipService
                    .HasAnyPresentationCustomization(skill))
            {
                return;
            }


            // TT1: compose the presentation without changing the Harmony entry point.
            SkillTooltipComposer.Compose(skill, ref __result);
        }
    }
}
