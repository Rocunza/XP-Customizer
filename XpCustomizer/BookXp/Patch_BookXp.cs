using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace XpCustomizer.BookXp
{
    /// <summary>
    /// Patch exactly the reading Intellectual 0.1 XP/tick literal. We do not
    /// patch SkillRecord.Learn globally, JobDriver jobs or reading queues.
    /// If the verified 1.6 IL pattern changes, return the original unmodified.
    /// </summary>
    [HarmonyPatch(typeof(JobDriver_Reading), "<ReadBook>b__17_1")]
    internal static class Patch_ReadingIntellectualXp
    {
        internal static int MatchedSites { get; private set; }
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            MethodInfo learn = AccessTools.Method(typeof(Pawn_SkillTracker), "Learn",
                new[] { typeof(SkillDef), typeof(float), typeof(bool), typeof(bool) });
            MethodInfo helper = AccessTools.Method(typeof(BookXpService),
                nameof(BookXpService.IntellectualInputPerTick));
            FieldInfo intellectual = AccessTools.Field(typeof(SkillDefOf), "Intellectual");
            MatchedSites = 0;
            if (learn == null || helper == null || intellectual == null) return code;
            // Verified 1.6 IL: ldsfld Intellectual; ldc.r4 0.1; ldarg.1;
            // conv.r4; mul; ldc.i4.0; ldc.i4.0; call Pawn_SkillTracker.Learn.
            for (int i = 1; i + 6 < code.Count; i++)
            {
                if (code[i - 1].opcode != OpCodes.Ldsfld ||
                    !Equals(code[i - 1].operand, intellectual) ||
                    code[i].opcode != OpCodes.Ldc_R4 ||
                    !(code[i].operand is float) ||
                    Math.Abs((float)code[i].operand - 0.1f) > 0.00001f ||
                    code[i + 1].opcode != OpCodes.Ldarg_1 ||
                    code[i + 2].opcode != OpCodes.Conv_R4 ||
                    code[i + 3].opcode != OpCodes.Mul ||
                    code[i + 6].operand == null ||
                    !Equals(code[i + 6].operand, learn)) continue;
                MatchedSites++;
            }
            if (MatchedSites != 1)
            {
                Log.Warning("[XP Customizer][BOOKXP-V1] Intellectual IL mismatch (" +
                    MatchedSites + "); vanilla code preserved.");
                return code;
            }
            for (int i = 1; i + 6 < code.Count; i++)
            {
                if (code[i - 1].opcode == OpCodes.Ldsfld &&
                    Equals(code[i - 1].operand, intellectual) &&
                    code[i].opcode == OpCodes.Ldc_R4 &&
                    code[i].operand is float &&
                    Math.Abs((float)code[i].operand - 0.1f) <= 0.00001f &&
                    code[i + 6].operand != null && Equals(code[i + 6].operand, learn))
                {
                    CodeInstruction original = code[i];
                    var substitute = new CodeInstruction(OpCodes.Call, helper);
                    substitute.labels.AddRange(original.labels);
                    substitute.blocks.AddRange(original.blocks);
                    code[i] = substitute;
                    break;
                }
            }
            return code;
        }
    }

    /// <summary>
    /// Replace just the vanilla SkillRecord.Learn invocation in a textbook
    /// outcome doer. Original CanProgressSkill, 1-or-2 skills, room/reading
    /// speed and book value calculation run without modification.
    /// </summary>
    [HarmonyPatch(typeof(BookOutcomeDoerGainSkillExp), nameof(BookOutcomeDoerGainSkillExp.OnReadingTick))]
    internal static class Patch_TextbookXp
    {
        internal static int MatchedSites { get; private set; }
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            MethodInfo learn = AccessTools.Method(typeof(SkillRecord), "Learn",
                new[] { typeof(float), typeof(bool), typeof(bool) });
            MethodInfo helper = AccessTools.Method(typeof(BookXpService),
                nameof(BookXpService.LearnTextbookAdjusted));
            MatchedSites = 0;
            if (learn == null || helper == null) return code;
            int site = -1;
            for (int i = 0; i < code.Count; i++)
            {
                if ((code[i].opcode == OpCodes.Call || code[i].opcode == OpCodes.Callvirt)
                    && Equals(code[i].operand, learn))
                {
                    MatchedSites++;
                    site = i;
                }
            }
            if (MatchedSites != 1)
            {
                Log.Warning("[XP Customizer][BOOKXP-V1] Textbook IL mismatch (" +
                    MatchedSites + "); vanilla code preserved.");
                return code;
            }
            // Before call: [SkillRecord, xp, direct, ignoreLearnRate]
            // After ldarg.0: [..., this-doer] -> wrapper with same original call.
            CodeInstruction originalCall = code[site];
            var argument = new CodeInstruction(OpCodes.Ldarg_0);
            // If a branch targets the old Learn site, it must first push
            // this doer argument before invoking the replacement helper.
            argument.labels.AddRange(originalCall.labels);
            argument.blocks.AddRange(originalCall.blocks);
            code.Insert(site, argument);
            code[site + 1] = new CodeInstruction(OpCodes.Call, helper);
            return code;
        }
    }
}
