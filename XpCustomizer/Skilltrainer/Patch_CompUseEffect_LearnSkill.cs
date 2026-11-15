using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace XpCustomizer.Skilltrainer
{
    /// <summary>
    /// S6: Scale only the XP value read by DoEffect. Never mutate a shared
    /// CompProperties/ThingDef object, even temporarily.
    /// </summary>
    [HarmonyPatch(
        typeof(CompUseEffect_LearnSkill),
        nameof(CompUseEffect_LearnSkill.DoEffect))]
    public static class Patch_CompUseEffect_LearnSkill
    {
        // Set during Harmony's IL transformation. Validated after PatchAll
        // by the DEBUG patch-registration audit.
        public static int MatchedXpReadCount { get; private set; }

        public static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> original =
                new List<CodeInstruction>(instructions);

            FieldInfo gainField = AccessTools.Field(
                typeof(CompProperties_UseEffect_LearnSkill),
                nameof(CompProperties_UseEffect_LearnSkill.xpGainAmount));
            MethodInfo adjustMethod = AccessTools.Method(
                typeof(Patch_CompUseEffect_LearnSkill),
                nameof(ApplyXpMultiplier));

            MatchedXpReadCount = 0;
            if (gainField == null || adjustMethod == null)
            {
                Log.Error("[XP Customizer] S6 Skilltrainer IL audit FAILED: " +
                    "missing XP field or adjustment method. Vanilla XP left intact.");
                return original;
            }

            foreach (CodeInstruction instruction in original)
            {
                if (instruction.opcode == OpCodes.Ldfld &&
                    Equals(instruction.operand, gainField))
                {
                    MatchedXpReadCount++;
                }
            }

            // Only rewrite a version of DoEffect with ONE exact XP field
            // read. An unexpected game/mod IL shape must not be patched
            // partially or scaled more than once.
            if (MatchedXpReadCount != 1)
            {
                Log.Error("[XP Customizer] S6 Skilltrainer IL audit FAILED: " +
                    "expected one xpGainAmount read; found " +
                    MatchedXpReadCount + ". Vanilla XP left intact.");
                return original;
            }

            List<CodeInstruction> rewritten =
                new List<CodeInstruction>(original.Count + 2);
            foreach (CodeInstruction instruction in original)
            {
                rewritten.Add(instruction);
                if (instruction.opcode == OpCodes.Ldfld &&
                    Equals(instruction.operand, gainField))
                {
                    // Stack after ldfld: ... , originalXp
                    // Push the DoEffect instance, then replace only the
                    // float on the stack. The Props field is never written.
                    rewritten.Add(new CodeInstruction(OpCodes.Ldarg_0));
                    rewritten.Add(new CodeInstruction(OpCodes.Call, adjustMethod));
                }
            }

            return rewritten;
        }

        // Public because Harmony's generated method may live in a distinct
        // dynamic assembly. Only called when DoEffect reads the XP field.
        public static float ApplyXpMultiplier(
            float originalXp,
            CompUseEffect_LearnSkill instance)
        {
            if (instance == null || instance.parent == null ||
                instance.parent.def == null)
            {
                return originalXp;
            }

            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            if (settings == null)
            {
                return originalXp;
            }

            string itemName = instance.parent.def.defName;
            float globallyAdjusted = CalculateAdjustedXp(
                originalXp, itemName, settings.skilltrainerXpMultiplier);

            // A genuine skill must be available from the comp itself.
            // Never guess a modded trainer's skill from its item name.
            if (!SkilltrainerSpecificXpService.IsVanillaNeurotrainer(itemName) ||
                !settings.skilltrainerSpecificEnabled)
                return globallyAdjusted;

            CompProperties_UseEffect_LearnSkill properties =
                instance.props as CompProperties_UseEffect_LearnSkill;
            SkillDef skill = SkilltrainerSpecificXpService.GetSkillFromProperties(properties);
            return SkilltrainerSpecificXpService.Apply(
                globallyAdjusted, itemName,
                skill != null ? skill.defName : null, settings);
        }

        // Pure calculation: separately regression-tested in DEBUG without
        // altering the active settings, Defs, items, Pawn skills, or save.
        public static float CalculateAdjustedXp(
            float originalXp,
            string itemDefName,
            float configuredMultiplier)
        {
            if (string.IsNullOrEmpty(itemDefName) ||
                !itemDefName.StartsWith("Neurotrainer_", StringComparison.Ordinal))
            {
                return originalXp;
            }

            float multiplier = Mathf.Clamp(
                configuredMultiplier,
                XpCustomizerDefaults.ProgressionMultiplierMin,
                XpCustomizerDefaults.ProgressionMultiplierMax);

            // Vanilla-neutral default: preserve the exact incoming value.
            if (Mathf.Approximately(multiplier,
                XpCustomizerDefaults.SkilltrainerXpMultiplier))
            {
                return originalXp;
            }

            return originalXp * multiplier;
        }
    }
}
