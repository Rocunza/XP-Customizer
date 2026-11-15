using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace XpCustomizer.FurtherLearning
{
    /// <summary>
    /// Replaces RimWorld's vanilla 20% post-cap learning efficiency
    /// with the factor calculated by FurtherLearningService.
    ///
    /// Vanilla LearnRateFactor already applies:
    /// - Passion
    /// - Global Learning Factor
    /// - Animals Learning Factor
    /// - x0.20 when saturated
    ///
    /// Therefore this patch only replaces the final vanilla x0.20
    /// portion instead of recalculating the entire learning rate.
    /// </summary>
    [HarmonyPatch(
        typeof(SkillRecord),
        nameof(SkillRecord.LearnRateFactor))]
    public static class Patch_SkillRecord_LearnRateFactor
    {
        public static void Postfix(
            SkillRecord __instance,
            bool direct,
            ref float __result)
        {
            if (__instance == null)
            {
                return;
            }


            // Direct learning intentionally bypasses daily saturation
            // in vanilla RimWorld.
            if (direct)
            {
                return;
            }


            // Preserve RimWorld's developer/debug fast-learning behavior.
            if (DebugSettings.fastLearning)
            {
                return;
            }


            // Vanilla-neutral fast path.
            // Avoid a second saturation query and all pawn-specific Further
            // Learning work when current settings cannot replace x0.20.
            if (!FurtherLearningService.IsConfiguredToChangeGameplay())
            {
                return;
            }


            // Nothing to replace until this skill has actually exceeded
            // its dynamic daily learning capacity.
            if (!__instance.LearningSaturatedToday)
            {
                return;
            }


            float desiredFactor =
                FurtherLearningService.GetFactor(
                    __instance);


            // Vanilla has already multiplied the final result by 0.20.
            //
            // Example:
            //
            // Vanilla result:
            //     base learning rate × 0.20
            //
            // Desired factor = 0.25:
            //
            //     result × (0.25 / 0.20)
            //
            // produces:
            //
            //     base learning rate × 0.25
            //
            // This preserves passion, GlobalLearningFactor,
            // AnimalsLearningFactor, and other upstream modifiers.
            if (Mathf.Approximately(
                    desiredFactor,
                    FurtherLearningService.VanillaFactor))
            {
                return;
            }


            __result *=
                desiredFactor /
                FurtherLearningService.VanillaFactor;
        }
    }
}