using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace XpCustomizer.Skilltrainer
{
    /// <summary>
    /// S2: independent per-SkillDef payout ratios for vanilla skill neurotrainers.
    /// No shared ThingDef/CompProperties mutation. No coupling to progression XP.
    /// </summary>
    internal static class SkilltrainerSpecificXpService
    {
        public const float MinimumMultiplier = 0.01f;
        public const float MaximumMultiplier = 100f;
        public const float NeutralMultiplier = 1f;

        // New game versions may use a differently named field/property. Accept
        // exactly one statically declared SkillDef member, otherwise fail closed.
        private static readonly MemberInfo SkillMember = FindSkillMember();

        private static MemberInfo FindSkillMember()
        {
            Type type = typeof(CompProperties_UseEffect_LearnSkill);
            const BindingFlags flags = BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo field = type.GetField("skill", flags);
            if (field != null && field.FieldType == typeof(SkillDef)) return field;
            PropertyInfo property = type.GetProperty("skill", flags);
            if (property != null && property.PropertyType == typeof(SkillDef) &&
                property.GetIndexParameters().Length == 0) return property;
            MemberInfo found = null;
            foreach (FieldInfo candidate in type.GetFields(flags))
                if (candidate.FieldType == typeof(SkillDef))
                {
                    if (found != null) return null;
                    found = candidate;
                }
            if (found != null) return found;
            foreach (PropertyInfo candidate in type.GetProperties(flags))
                if (candidate.PropertyType == typeof(SkillDef) &&
                    candidate.GetIndexParameters().Length == 0)
                {
                    if (found != null) return null;
                    found = candidate;
                }
            return found;
        }

        public static SkillDef GetSkillFromProperties(
            CompProperties_UseEffect_LearnSkill properties)
        {
            if (properties == null || SkillMember == null) return null;
            try
            {
                FieldInfo field = SkillMember as FieldInfo;
                return field != null
                    ? field.GetValue(properties) as SkillDef
                    : ((PropertyInfo)SkillMember).GetValue(properties, null) as SkillDef;
            }
            catch (Exception)
            {
                // Unexpected modded getter: do not try to infer the skill by name.
                return null;
            }
        }

        public static bool HasSkillResolver { get { return SkillMember != null; } }

        public static bool IsVanillaNeurotrainer(string defName)
        {
            return !string.IsNullOrEmpty(defName) &&
                defName.StartsWith("Neurotrainer_", StringComparison.Ordinal);
        }

        // Startup diagnostic in both Debug and Release. No game state mutation.
        public static void LogResolutionAudit()
        {
            int supported = 0;
            int resolved = 0;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def == null || !IsVanillaNeurotrainer(def.defName) ||
                    def.comps == null) continue;
                foreach (CompProperties comp in def.comps)
                {
                    var trainer = comp as CompProperties_UseEffect_LearnSkill;
                    if (trainer == null) continue;
                    supported++;
                    if (GetSkillFromProperties(trainer) != null) resolved++;
                }
            }
            string summary = "[XP Customizer] SKILLTRAINER-S2 target-skill audit: " +
                "resolved=" + resolved + "/" + supported +
                ", SkillDefMemberFound=" + HasSkillResolver +
                ", gamePayoutUntested=True.";
            if (supported == 0 || resolved != supported)
                Log.Warning(summary + " Per-skill payout is skipped for " +
                    "unresolved items; the original/global trainer payout is preserved.");
            else
                Log.Message(summary);
        }

        public static float Normalize(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return NeutralMultiplier;
            return Mathf.Clamp(value, MinimumMultiplier, MaximumMultiplier);
        }

        public static float GetConfiguredMultiplier(XpCustomizerSettings settings,
            string skillDefName)
        {
            if (settings == null || string.IsNullOrEmpty(skillDefName) ||
                settings.skilltrainerSpecificMultipliers == null)
                return NeutralMultiplier;
            float value;
            return settings.skilltrainerSpecificMultipliers.TryGetValue(skillDefName, out value)
                ? Normalize(value) : NeutralMultiplier;
        }

        public static float GetEffectiveMultiplier(XpCustomizerSettings settings,
            string skillDefName)
        {
            return settings != null && settings.skilltrainerSpecificEnabled
                ? GetConfiguredMultiplier(settings, skillDefName)
                : NeutralMultiplier;
        }

        public static float Apply(float incomingXp, string itemDefName,
            string skillDefName, XpCustomizerSettings settings)
        {
            if (!IsVanillaNeurotrainer(itemDefName) ||
                string.IsNullOrEmpty(skillDefName) || settings == null)
                return incomingXp;
            float factor = GetEffectiveMultiplier(settings, skillDefName);
            if (Mathf.Approximately(factor, NeutralMultiplier)) return incomingXp;
            return incomingXp * factor;
        }

        public static void SetConfiguredMultiplier(XpCustomizerSettings settings,
            string skillDefName, float value)
        {
            if (settings == null || string.IsNullOrEmpty(skillDefName)) return;
            if (settings.skilltrainerSpecificMultipliers == null)
                settings.skilltrainerSpecificMultipliers = new Dictionary<string, float>();
            value = Normalize(value);
            if (Mathf.Approximately(value, NeutralMultiplier))
                settings.skilltrainerSpecificMultipliers.Remove(skillDefName);
            else
                settings.skilltrainerSpecificMultipliers[skillDefName] = value;
        }

        public static void NormalizeSettings(XpCustomizerSettings settings)
        {
            if (settings == null) return;
            var clean = new Dictionary<string, float>();
            if (settings.skilltrainerSpecificMultipliers != null)
                foreach (var pair in settings.skilltrainerSpecificMultipliers)
                {
                    if (string.IsNullOrEmpty(pair.Key)) continue;
                    float value = Normalize(pair.Value);
                    if (!Mathf.Approximately(value, NeutralMultiplier))
                        clean[pair.Key] = value;
                }
            // Retain unknown defs for temporary mod uninstalls.
            settings.skilltrainerSpecificMultipliers = clean;
        }

        public static int GetModifiedCount(XpCustomizerSettings settings)
        {
            if (settings == null || settings.skilltrainerSpecificMultipliers == null)
                return 0;
            int result = 0;
            foreach (var pair in settings.skilltrainerSpecificMultipliers)
                if (!Mathf.Approximately(Normalize(pair.Value), NeutralMultiplier))
                    result++;
            return result;
        }
    }
}
