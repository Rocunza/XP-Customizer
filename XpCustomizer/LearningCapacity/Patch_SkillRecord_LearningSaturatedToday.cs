using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace XpCustomizer.LearningCapacity
{
    [HarmonyPatch(
        typeof(SkillRecord),
        nameof(SkillRecord.LearningSaturatedToday),
        MethodType.Getter)]
    public static class Patch_SkillRecord_LearningSaturatedToday
    {
        public static void Postfix(
            SkillRecord __instance,
            ref bool __result)
        {
            if (__instance == null)
            {
                return;
            }


            // Vanilla-neutral fast path.
            // Do not allocate breakdown data or inspect pawn sources when
            // current settings cannot change the 4,000 XP threshold.
            if (!LearningCapacityService.IsConfiguredToChangeDailyCap())
            {
                return;
            }


            float dailyCap =
                LearningCapacityService.GetDailyCap(
                    __instance);


            // A zero capacity means there is no full-rate learning window.
            // Treat the skill as saturated immediately, even before the pawn
            // has gained any XP today.
            if (dailyCap <= 0f)
            {
                __result = true;
                return;
            }


            // True vanilla pass-through for this pawn/skill.
            // If XP Customizer resolves to the vanilla 4,000 XP
            // threshold, preserve RimWorld's result and any upstream
            // changes made by another mod.
            if (Mathf.Approximately(
                    dailyCap,
                    LearningCapacityService.VanillaDailyCap))
            {
                return;
            }


            __result =
                __instance.xpSinceMidnight >
                dailyCap;
        }
    }
}