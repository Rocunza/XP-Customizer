using HarmonyLib;
using RimWorld;
using Verse;

namespace XpCustomizer.Decay
{
    // DP1: neutral records delegate; active user settings apply. Foreign
    // Harmony owners are observed by CA-1 and never gate this gameplay patch.
    // This retains the existing custom Learn(-loss) / return-false behavior.
    [HarmonyPatch(typeof(SkillRecord), "Interval")]
    internal static class Patch_SkillRecord_Interval
    {
        public static bool Prefix(SkillRecord __instance)
        {
            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            if (settings == null || __instance == null) return true;
            // D3: Memory values have no ON/OFF flags. Neutral values preserve
            // original Interval() and any other mod's behavior.
            if (!settings.decayByLevelEnabled && !settings.decaySkillMultipliersEnabled &&
                settings.greatMemoryDecayReduction == DecayModifiersService.VanillaGreatReduction &&
                settings.perfectMemoryDecayReduction == DecayModifiersService.VanillaPerfectReduction)
            {
#if DEBUG
                DecayLiveDiagnostics.NotePath(__instance, "VANILLA-NEUTRAL");
#endif
                return true;
            }
            Pawn pawn = __instance.Pawn;
            if (pawn == null || pawn.story == null || pawn.story.traits == null ||
                __instance.def == null)
            {
#if DEBUG
                DecayLiveDiagnostics.NotePath(__instance, "VANILLA-NO-PAWN-OR-DEF");
#endif
                return true;
            }

            bool great = pawn.story.traits.HasTrait(TraitDefOf.GreatMemory);
            bool perfect = pawn.story.traits.HasTrait(TraitDefOf.PerfectMemory);
            bool anomaly = ModsConfig.AnomalyActive;
            if (!DecayModifiersService.NeedsOverride(settings, __instance.levelInt,
                    __instance.def.defName, great, perfect, anomaly))
            {
#if DEBUG
                DecayLiveDiagnostics.NotePath(__instance, "VANILLA-NO-OVERRIDE");
#endif
                return true;
            }

            // Foreign Harmony patch registration is a diagnostic concern.
            // It must not silently discard the user's Decay configuration.
            float loss = DecayModifiersService.LossPerInterval(settings,
                __instance.levelInt, __instance.def.defName, great, perfect, anomaly);
#if DEBUG
            DecayLiveDiagnostics.NotePath(__instance, "CUSTOM-INTERVAL", loss);
#endif
            if (loss > 0f) __instance.Learn(-loss);
            return false;
        }
    }
}
