#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.FurtherLearning;
using Verse;

namespace XpCustomizer
{
    internal static partial class XpCustomizerSelfTests
    {
        // ================================================================
        // Curve-5D-C: audit live Harmony patch registrations
        // ================================================================
        // Executed ONLY after harmony.PatchAll() in XpCustomizerMod.
        // No Pawn, SkillRecord, map, save, or shared settings are mutated.
        // A registered patch is not proof that an in-world XP event executed;
        // the isolated Pawn/SkillRecord integration test follows in 5D-D.

        public static void RunHarmonyPatchAudit()
        {
            const string learnRate = nameof(SkillRecord.LearnRateFactor);
            const string saturated = nameof(SkillRecord.LearningSaturatedToday);
            const string xpRequired = nameof(SkillRecord.XpRequiredToLevelUpFrom);

            MethodBase learn = AccessTools.Method(typeof(SkillRecord), learnRate);
            MethodBase cap = AccessTools.PropertyGetter(typeof(SkillRecord), saturated);
            MethodBase tooltip = AccessTools.Method(typeof(SkillUI), "GetSkillDescription");
            MethodBase curve = AccessTools.Method(typeof(SkillRecord), xpRequired);
            MethodBase decay = AccessTools.Method(typeof(SkillRecord), "Interval");
            MethodBase skillRequirement = AccessTools.PropertyGetter(
                typeof(SkillRecord), "XpRequiredForLevelUp");
            MethodBase skillTotal = AccessTools.PropertyGetter(
                typeof(SkillRecord), "XpTotalEarned");
            MethodBase trainer = AccessTools.Method(typeof(CompUseEffect_LearnSkill),
                nameof(CompUseEffect_LearnSkill.DoEffect));

            AssertOwnedHarmonyHook(learn,
                typeof(XpCustomizer.ConsciousnessLearning.Patch_SkillRecord_LearnRateFactor_Consciousness),
                "Postfix", "Learning Speed");
            AssertOwnedHarmonyHook(learn,
                typeof(XpCustomizer.FurtherLearning.Patch_SkillRecord_LearnRateFactor),
                "Postfix", "Further Learning");
            AssertOwnedHarmonyHook(learn,
                typeof(XpCustomizer.Gws.Patch_SkillRecord_GwsLearning),
                "Postfix", "GWS learning multiplier");
            AssertOwnedHarmonyHook(cap,
                typeof(XpCustomizer.LearningCapacity.Patch_SkillRecord_LearningSaturatedToday),
                "Postfix", "Daily Capacity");
            AssertOwnedHarmonyHook(tooltip,
                typeof(XpCustomizer.LearningCapacity.Patch_SkillUI_GetSkillDescription),
                "Postfix", "Skill tooltip");
            AssertOwnedHarmonyHook(curve,
                typeof(XpCustomizer.Progression.Patch_SkillRecord_XpRequiredToLevelUpFrom),
                "Postfix", "Career XP curve");
            AssertOwnedHarmonyHook(trainer,
                typeof(XpCustomizer.Skilltrainer.Patch_CompUseEffect_LearnSkill),
                "Transpiler", "Skilltrainer Transpiler");
            AssertOwnedHarmonyHook(decay,
                typeof(XpCustomizer.Decay.Patch_SkillRecord_Interval),
                "Prefix", "Skill Decay Interval");
            AssertOwnedHarmonyHook(decay,
                typeof(XpCustomizer.Decay.Patch_SkillRecord_Interval_DecayDiagnostics),
                "Prefix", "Decay live diagnostic snapshot");
            AssertOwnedHarmonyHook(decay,
                typeof(XpCustomizer.Decay.Patch_SkillRecord_Interval_DecayDiagnostics),
                "Postfix", "Decay live diagnostic delta");
            AssertOwnedHarmonyHook(skillRequirement,
                typeof(XpCustomizer.Progression.Patch_SkillRecord_XpRequiredForLevelUp),
                "Postfix", "Skill-specific level requirement");
            AssertOwnedHarmonyHook(skillTotal,
                typeof(XpCustomizer.Progression.Patch_SkillRecord_XpTotalEarned),
                "Postfix", "Skill-specific total-earned getter");
            MethodBase readingTick = AccessTools.Method(typeof(JobDriver_Reading),
                "<ReadBook>b__17_1", new[] { typeof(int) });
            MethodBase textbookTick = AccessTools.Method(typeof(BookOutcomeDoerGainSkillExp),
                nameof(BookOutcomeDoerGainSkillExp.OnReadingTick));
            AssertOwnedHarmonyHook(readingTick,
                typeof(XpCustomizer.BookXp.Patch_ReadingIntellectualXp),
                "Transpiler", "Reading Intellectual XP");
            AssertOwnedHarmonyHook(readingTick,
                typeof(XpCustomizer.Gws.Patch_GwsReadingTickScope),
                "Prefix", "GWS reading scope begin");
            AssertOwnedHarmonyHook(readingTick,
                typeof(XpCustomizer.Gws.Patch_GwsReadingTickScope),
                "Finalizer", "GWS reading scope end");
            AssertOwnedHarmonyHook(textbookTick,
                typeof(XpCustomizer.BookXp.Patch_TextbookXp),
                "Transpiler", "Textbook Skill XP");
            string bookAudit = XpCustomizer.BookXp.Patch_ReadingIntellectualXp.MatchedSites == 1 &&
                XpCustomizer.BookXp.Patch_TextbookXp.MatchedSites == 1 ? "PASS" : "INERT";
            if (bookAudit == "INERT")
                Log.Warning("[XP Customizer][BOOKXP-V1] Harmony IL patterns unmatched; " +
                    "unmatched reading path remains vanilla. intellectual=" +
                    XpCustomizer.BookXp.Patch_ReadingIntellectualXp.MatchedSites +
                    ", textbook=" + XpCustomizer.BookXp.Patch_TextbookXp.MatchedSites);
            Log.Message("[XP Customizer][BOOKXP-V1] IL-VERIFY=" + bookAudit +
                "; intellectualSites=" + XpCustomizer.BookXp.Patch_ReadingIntellectualXp.MatchedSites +
                "; textbookSites=" + XpCustomizer.BookXp.Patch_TextbookXp.MatchedSites);
            AssertTrue(
                XpCustomizer.Skilltrainer.Patch_CompUseEffect_LearnSkill.MatchedXpReadCount == 1,
                "S6-A Skilltrainer DoEffect did not contain exactly one supported XP field read.");

            Log.Message(
                "[XP Customizer] DEBUG Harmony patch audit passed: " +
                "16 owned hooks on 10 target methods (Curve-5D-C + S6-A + DECAY-D4-DIAG + SKILLXP-S1 + BOOKXP-V1 + GWS-V1). " +
                "Skilltrainer Transpiler XP-field match=1; no shared Props mutation. " +
                "Registration/IL only; one-shot Decay probe requires live pawn sampling.");
        }

        private static void AssertOwnedHarmonyHook(
            MethodBase target,
            Type declaringType,
            string hookKind,
            string label)
        {
            AssertTrue(target != null,
                "Curve-5D-C missing target method: " + label);

            Patches info = Harmony.GetPatchInfo(target);
            AssertTrue(info != null,
                "Curve-5D-C target has no Harmony patches: " + label);

            IEnumerable<Patch> hooks;
            switch (hookKind)
            {
                case "Prefix":
                    hooks = info.Prefixes;
                    break;
                case "Postfix":
                    hooks = info.Postfixes;
                    break;
                case "Finalizer":
                    hooks = info.Finalizers;
                    break;
                case "Transpiler":
                    hooks = info.Transpilers;
                    break;
                default:
                    throw new InvalidOperationException(
                        "Curve-5D-C unsupported hook kind: " + hookKind);
            }

            int matches = 0;
            foreach (Patch hook in hooks)
            {
                if (hook.owner == "zcn.xpcustomizer" &&
                    hook.PatchMethod != null &&
                    hook.PatchMethod.DeclaringType == declaringType &&
                    hook.PatchMethod.Name == hookKind)
                {
                    matches++;
                }
            }

            AssertTrue(matches == 1,
                "Curve-5D-C expected exactly one registered " +
                hookKind + " for " + label + ", found " + matches);
        }

    }
}
#endif
