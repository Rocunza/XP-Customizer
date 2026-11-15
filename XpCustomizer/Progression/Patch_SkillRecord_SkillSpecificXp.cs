using HarmonyLib;
using RimWorld;

namespace XpCustomizer.Progression
{
    /// <summary>
    /// Scale the instance requirement AFTER the shared static curve has been
    /// evaluated. This preserves other mods' shared-curve result and keeps a
    /// neutral / disabled setting entirely inert.
    /// </summary>
    [HarmonyPatch(typeof(SkillRecord), "XpRequiredForLevelUp", MethodType.Getter)]
    internal static class Patch_SkillRecord_XpRequiredForLevelUp
    {
        public static void Postfix(SkillRecord __instance, ref float __result)
        {
            __result = SkillSpecificXpService.Apply(
                __result, __instance, XpCustomizerMod.Settings);
        }
    }

    // XpTotalEarned calls the shared static curve directly, not the instance
    // property above. Keep this reporting value consistent with per-skill XP.
    [HarmonyPatch(typeof(SkillRecord), "XpTotalEarned", MethodType.Getter)]
    internal static class Patch_SkillRecord_XpTotalEarned
    {
        public static void Postfix(SkillRecord __instance, ref float __result)
        {
            __result = SkillSpecificXpService.Apply(
                __result, __instance, XpCustomizerMod.Settings);
        }
    }
}
