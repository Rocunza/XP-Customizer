using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace XpCustomizer.Compatibility
{
    internal static class CompatibilityFeatureCatalog
    {
        // These are EXPECTED targets, not a replacement for runtime discovery.
        // New Harmony targets are automatically discovered and flagged UNMAPPED.
        private sealed class ExpectedTarget
        {
            internal string Label;
            internal MethodBase Method;
            internal string Features;
        }

        internal static string MethodKey(MethodBase method)
        {
            if (method == null) return "<null>";
            string declaring = method.DeclaringType == null ? "<global>" :
                method.DeclaringType.FullName;
            var parameterNames = new List<string>();
            foreach (ParameterInfo parameter in method.GetParameters())
                parameterNames.Add(parameter.ParameterType.FullName ?? parameter.ParameterType.Name);
            return declaring + "." + method.Name + "(" +
                string.Join(",", parameterNames.ToArray()) + ")";
        }

        private static List<ExpectedTarget> KnownTargets()
        {
            // Method reflection here is read-only and happens only during explicit audits.
            return new List<ExpectedTarget>
            {
                New("LearnRateFactor", AccessTools.Method(typeof(SkillRecord),
                    nameof(SkillRecord.LearnRateFactor)),
                    "Consciousness|GWS|FurtherLearning"),
                New("LearningSaturatedToday", AccessTools.PropertyGetter(typeof(SkillRecord),
                    nameof(SkillRecord.LearningSaturatedToday)), "DailyLearningCapacity"),
                New("GetSkillDescription", AccessTools.Method(typeof(SkillUI),
                    "GetSkillDescription"), "SkillTooltip"),
                New("XpRequiredToLevelUpFrom", AccessTools.Method(typeof(SkillRecord),
                    nameof(SkillRecord.XpRequiredToLevelUpFrom)), "CareerXP"),
                New("DoEffect", AccessTools.Method(typeof(CompUseEffect_LearnSkill),
                    nameof(CompUseEffect_LearnSkill.DoEffect)), "SkilltrainerXP"),
                New("Interval", AccessTools.Method(typeof(SkillRecord),
                    "Interval"), "SkillDecay"),
                New("XpRequiredForLevelUp", AccessTools.PropertyGetter(typeof(SkillRecord),
                    "XpRequiredForLevelUp"), "SkillSpecificXP"),
                New("XpTotalEarned", AccessTools.PropertyGetter(typeof(SkillRecord),
                    "XpTotalEarned"), "SkillSpecificXP"),
                New("JobDriver_Reading.ReadBook.Tick", AccessTools.Method(
                    typeof(JobDriver_Reading), "<ReadBook>b__17_1",
                    new[] { typeof(int) }), "BookXP|GWSReadingScope"),
                New("BookOutcomeDoerGainSkillExp.OnReadingTick", AccessTools.Method(
                    typeof(BookOutcomeDoerGainSkillExp),
                    nameof(BookOutcomeDoerGainSkillExp.OnReadingTick)), "BookXP")
            };
        }

        private static ExpectedTarget New(string label, MethodBase method, string features)
        {
            return new ExpectedTarget { Label = label, Method = method, Features = features };
        }

        internal static Dictionary<string, string> BuildMap(List<string> missing)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ExpectedTarget expected in KnownTargets())
            {
                if (expected.Method == null)
                {
                    missing.Add("TARGET_NOT_FOUND:" + expected.Label);
                    continue;
                }
                string key = MethodKey(expected.Method);
                if (result.ContainsKey(key))
                    missing.Add("DUPLICATE_EXPECTED_TARGET:" + expected.Label + ":" + key);
                else result.Add(key, expected.Features);
            }
            return result;
        }

        // Explicit scope reporting. These are not Harmony-registration based features.
        internal const string OutOfScope = "BiotechGeneDef=NOT_COVERED;" +
            "ProfilePersistence=NOT_COVERED;XmlDefMutations=NOT_COVERED;" +
            "ActualXpPayout=NOT_VERIFIED";
    }
}
