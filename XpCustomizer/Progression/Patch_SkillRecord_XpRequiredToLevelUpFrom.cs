using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace XpCustomizer.Progression
{
    [HarmonyPatch(
        typeof(SkillRecord),
        nameof(SkillRecord.XpRequiredToLevelUpFrom))]
    public static class Patch_SkillRecord_XpRequiredToLevelUpFrom
    {
        public static void Postfix(
            int startingLevel,
            ref float __result)
        {
            XpCustomizerSettings settings =
                XpCustomizerMod.Settings;

            if (settings == null)
            {
                return;
            }


            // ------------------------------------------------------------
            // Normal level-up requirements: 0 -> 1 through 19 -> 20
            // ------------------------------------------------------------
            //
            // XpCurve is now a cached facade over the same dynamic-stage
            // calculator used by the settings UI.
            if (startingLevel >= 0 &&
                startingLevel <= 19)
            {
                // True vanilla pass-through: when Career XP is untouched,
                // leave RimWorld's original result (and other mods' changes)
                // exactly as they are.
                bool vanillaCareerConfiguration =
                    Mathf.Approximately(
                        settings.careerXpMultiplier,
                        XpCustomizerDefaults.CareerXpMultiplier) &&
                    !settings.careerXpUseCustomDistribution &&
                    settings.careerXpRoundingStep ==
                        XpCustomizerDefaults.CareerXpRoundingStep;

                if (vanillaCareerConfiguration)
                {
                    return;
                }


                __result =
                    XpCurve.GetRequirement(
                        startingLevel,
                        settings);

                return;
            }


            // ------------------------------------------------------------
            // Level-20 reserve
            // ------------------------------------------------------------
            //
            // Level 20 has no 20 -> 21 level-up. RimWorld instead uses
            // this value as the XP reserve that protects a level-20 skill
            // against decay.
            if (startingLevel == 20)
            {
                float reserveMultiplier =
                    Mathf.Clamp(
                        settings.level20ReserveMultiplier,
                        XpCustomizerDefaults.ProgressionMultiplierMin,
                        XpCustomizerDefaults.ProgressionMultiplierMax);

                if (Mathf.Approximately(
                        reserveMultiplier,
                        XpCustomizerDefaults.Level20ReserveMultiplier))
                {
                    return;
                }


                __result =
                    30000f *
                    reserveMultiplier;
            }
        }
    }
}
