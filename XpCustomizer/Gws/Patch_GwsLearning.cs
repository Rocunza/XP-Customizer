using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace XpCustomizer.Gws
{
    // The existing Book XP transpiler targets this SAME method. This scoped
    // Prefix/Finalizer is independent of its IL instructions and preserves the
    // reading job, book reward, other patches and the current skill Learn call.
    internal static class GwsReadingScope
    {
        [ThreadStatic] private static int depth;
        public static bool IsReadingTick { get { return depth > 0; } }
        public static void Enter() { depth++; }
        public static void Exit() { if (depth > 0) depth--; }
    }

    [HarmonyPatch(typeof(JobDriver_Reading), "<ReadBook>b__17_1")]
    internal static class Patch_GwsReadingTickScope
    {
        public static void Prefix() { GwsReadingScope.Enter(); }
        public static Exception Finalizer(Exception __exception)
        {
            GwsReadingScope.Exit();
            return __exception; // preserve original failures; never swallow them
        }
    }

    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.LearnRateFactor))]
    internal static class Patch_SkillRecord_GwsLearning
    {
        public static void Postfix(SkillRecord __instance, bool direct, ref float __result)
        {
            if (direct || DebugSettings.fastLearning || __instance == null) return;
            float factor = GwsLearningService.GetFactor(__instance);
            if (!Mathf.Approximately(factor, 1f)) __result *= factor;
        }
    }
}
