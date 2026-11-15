using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;

namespace XpCustomizer.Progression
{
    /// <summary>
    /// Per-SkillDef relative requirement scaling. The shared career curve remains
    /// unchanged; only SkillRecord instance-specific requirements are adjusted.
    /// A neutral value never changes another mod's computed result.
    /// </summary>
    internal static class SkillSpecificXpService
    {
        public const float MinimumMultiplier = 0.01f;
        public const float MaximumMultiplier = 100f;
        public const float NeutralMultiplier = 1f;

        public static float Normalize(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return NeutralMultiplier;
            return Mathf.Clamp(value, MinimumMultiplier, MaximumMultiplier);
        }

        public static float GetConfiguredMultiplier(XpCustomizerSettings settings, string defName)
        {
            if (settings == null || string.IsNullOrEmpty(defName) ||
                settings.skillSpecificXpMultipliers == null)
                return NeutralMultiplier;

            float value;
            return settings.skillSpecificXpMultipliers.TryGetValue(defName, out value)
                ? Normalize(value) : NeutralMultiplier;
        }

        public static float GetEffectiveMultiplier(SkillRecord record, XpCustomizerSettings settings)
        {
            if (settings == null || !settings.skillSpecificXpEnabled ||
                record == null || record.def == null)
                return NeutralMultiplier;
            return GetConfiguredMultiplier(settings, record.def.defName);
        }

        public static float Apply(float originalResult, SkillRecord record,
            XpCustomizerSettings settings)
        {
            float multiplier = GetEffectiveMultiplier(record, settings);
            if (Mathf.Approximately(multiplier, NeutralMultiplier) ||
                float.IsNaN(originalResult) || float.IsInfinity(originalResult))
                return originalResult;
            return originalResult * multiplier;
        }

        public static void SetConfiguredMultiplier(XpCustomizerSettings settings,
            string defName, float value)
        {
            if (settings == null || string.IsNullOrEmpty(defName))
                return;
            if (settings.skillSpecificXpMultipliers == null)
                settings.skillSpecificXpMultipliers = new Dictionary<string, float>();
            value = Normalize(value);
            if (Mathf.Approximately(value, NeutralMultiplier))
                settings.skillSpecificXpMultipliers.Remove(defName);
            else
                settings.skillSpecificXpMultipliers[defName] = value;
        }

        public static void NormalizeSettings(XpCustomizerSettings settings)
        {
            if (settings == null) return;
            if (settings.skillSpecificXpMultipliers == null)
            {
                settings.skillSpecificXpMultipliers = new Dictionary<string, float>();
                return;
            }
            // Never remove unknown SkillDefs: the user may temporarily disable a
            // mod that defines one, then re-enable it later.
            var cleaned = new Dictionary<string, float>();
            foreach (KeyValuePair<string, float> item in settings.skillSpecificXpMultipliers)
            {
                if (string.IsNullOrEmpty(item.Key)) continue;
                float value = Normalize(item.Value);
                if (!Mathf.Approximately(value, NeutralMultiplier))
                    cleaned[item.Key] = value;
            }
            settings.skillSpecificXpMultipliers = cleaned;
        }

        public static int GetModifiedCount(XpCustomizerSettings settings)
        {
            if (settings == null || settings.skillSpecificXpMultipliers == null) return 0;
            int count = 0;
            foreach (KeyValuePair<string, float> item in settings.skillSpecificXpMultipliers)
            {
                if (!Mathf.Approximately(Normalize(item.Value), NeutralMultiplier))
                    count++;
            }
            return count;
        }
    }
}
