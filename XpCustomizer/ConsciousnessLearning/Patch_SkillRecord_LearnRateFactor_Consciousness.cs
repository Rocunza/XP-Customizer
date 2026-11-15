using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace XpCustomizer.ConsciousnessLearning
{
    /// <summary>
    /// Applies the Consciousness Learning multiplier to normal skill learning.
    ///
    /// This patch intentionally does not:
    /// - modify Daily Learning Capacity
    /// - replace Further Learning
    /// - affect skill decay
    /// - affect direct learning such as skilltrainer XP
    ///
    /// Further Learning has its own independent LearnRateFactor patch.
    /// Both systems multiply the final learning rate independently.
    /// </summary>
    [HarmonyPatch(
        typeof(SkillRecord),
        nameof(SkillRecord.LearnRateFactor))]
    public static class Patch_SkillRecord_LearnRateFactor_Consciousness
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


            // Direct XP sources intentionally bypass Consciousness Learning.
            //
            // This keeps systems such as skilltrainers independent from
            // normal activity-based learning speed.
            if (direct)
            {
                return;
            }


            // Preserve RimWorld's developer/debug fast-learning behavior.
            if (DebugSettings.fastLearning)
            {
                return;
            }


            float consciousnessFactor =
                ConsciousnessLearningService.GetFactor(
                    __instance.Pawn);


            if (Mathf.Approximately(
                    consciousnessFactor,
                    ConsciousnessLearningService.NeutralFactor))
            {
                return;
            }


            // Preserve all vanilla and modded upstream factors:
            //
            // Passion
            // Global Learning Factor
            // Animals Learning Factor
            // Further Learning
            // etc.
            //
            // Consciousness is simply an independent multiplier.
            __result *=
                consciousnessFactor;
        }
    }
}