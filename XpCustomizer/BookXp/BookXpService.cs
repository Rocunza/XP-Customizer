using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace XpCustomizer.BookXp
{
    /// <summary>
    /// Changes XP *input* from reading only. SkillRecord.Learn still processes
    /// passion, learning stat, daily saturation and other-mod patches normally.
    /// Does not change generated/saved book outcomes, book caps or research.
    /// </summary>
    public static class BookXpService
    {
        public const float VanillaIntellectualXpPerHour = 250f;
        public const float VanillaIntellectualXpPerTick = 0.1f;
        public const float MinIntellectualXpPerHour = 0f;
        public const float MaxIntellectualXpPerHour = 5000f;
        public const float MinMultiplier = 0f;
        public const float MaxMultiplier = 20f;

        // Fixed vanilla SkillDef set; avoid modifying other mods' SkillDefs.
        public static readonly string[] VanillaSkillNames = {
            "Shooting", "Melee", "Construction", "Mining", "Cooking", "Plants",
            "Animals", "Crafting", "Artistic", "Medicine", "Social", "Intellectual"
        };
        // The old Book XP skill table used the translated label "Medical" instead
        // of SkillDef.defName "Medicine". Convert only that exact legacy key;
        // unrelated mod-owned skill names and other settings must be untouched.
        // The canonical Medicine value wins conflicts. Do not read or write disk.
        internal static bool MigrateLegacyMedicalKey(Dictionary<string, float> skills,
            out bool canonicalWon)
        {
            canonicalWon = false;
            if (skills == null) return false;
            bool exactLegacyKey = false;
            foreach (string key in skills.Keys)
                if (string.Equals(key, "Medical", StringComparison.Ordinal))
                {
                    exactLegacyKey = true;
                    break;
                }
            if (!exactLegacyKey) return false;

            float legacyValue = skills["Medical"];
            canonicalWon = skills.ContainsKey("Medicine");
            if (!canonicalWon) skills["Medicine"] = legacyValue;
            skills.Remove("Medical");
            return true;
        }

#if DEBUG
        private static bool legacySettingsMigrationLogged;
#endif
        private static readonly float[] VanillaQualityXpPerTick = {
            0.05f, 0.075f, 0.10f, 0.125f, 0.15f, 0.175f, 0.20f
        };
#if DEBUG
        private static bool intellectualSampleLogged;
        private static bool textbookSampleLogged;
#endif
        public static float NormalizeIntellectual(float xpPerHour)
        {
            if (float.IsNaN(xpPerHour) || float.IsInfinity(xpPerHour))
                return VanillaIntellectualXpPerHour;
            return Mathf.Clamp(xpPerHour, MinIntellectualXpPerHour, MaxIntellectualXpPerHour);
        }
        public static float NormalizeMultiplier(float multiplier)
        {
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier)) return 1f;
            return Mathf.Clamp(multiplier, MinMultiplier, MaxMultiplier);
        }
        public static string QualityKey(QualityCategory quality)
        {
            return quality.ToString();
        }
        public static float VanillaQualityXpPerHour(QualityCategory quality)
        {
            int i = (int)quality;
            return i >= 0 && i < VanillaQualityXpPerTick.Length
                ? VanillaQualityXpPerTick[i] * 2500f : 0f;
        }
        private static float GetConfigured(Dictionary<string, float> values, string key)
        {
            if (values == null || string.IsNullOrEmpty(key)) return 1f;
            float configured;
            return values.TryGetValue(key, out configured)
                ? NormalizeMultiplier(configured) : 1f;
        }
        public static float GetQualityMultiplier(XpCustomizerSettings settings, QualityCategory quality)
        {
            return settings == null ? 1f : GetConfigured(settings.bookQualityMultipliers, QualityKey(quality));
        }
        public static float GetSkillMultiplier(XpCustomizerSettings settings, string skillName)
        {
            return settings == null ? 1f : GetConfigured(settings.bookSkillMultipliers, skillName);
        }
        public static float GetEffectiveTextbookMultiplier(XpCustomizerSettings settings,
            QualityCategory quality, string skillName)
        {
            if (settings == null || !settings.bookXpEnabled) return 1f;
            float qualityRatio = settings.bookQualityEnabled
                ? GetQualityMultiplier(settings, quality) : 1f;
            float skillRatio = settings.bookSkillEnabled && IsVanillaSkill(skillName)
                ? GetSkillMultiplier(settings, skillName) : 1f;
            return qualityRatio * skillRatio;
        }
        public static bool IsVanillaSkill(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return false;
            foreach (string name in VanillaSkillNames)
                if (name == defName) return true;
            return false;
        }
        public static void SetQualityMultiplier(XpCustomizerSettings settings, QualityCategory quality, float multiplier)
        {
            if (settings == null) return;
            if (settings.bookQualityMultipliers == null)
                settings.bookQualityMultipliers = new Dictionary<string, float>();
            SetMultiplier(settings.bookQualityMultipliers, QualityKey(quality), multiplier);
        }
        public static void SetSkillMultiplier(XpCustomizerSettings settings, string skillName, float multiplier)
        {
            if (settings == null || !IsVanillaSkill(skillName)) return;
            if (settings.bookSkillMultipliers == null)
                settings.bookSkillMultipliers = new Dictionary<string, float>();
            SetMultiplier(settings.bookSkillMultipliers, skillName, multiplier);
        }
        private static void SetMultiplier(Dictionary<string, float> dict, string key, float value)
        {
            value = NormalizeMultiplier(value);
            if (Mathf.Approximately(value, 1f)) dict.Remove(key);
            else dict[key] = value;
        }
        public static void NormalizeSettings(XpCustomizerSettings settings)
        {
            if (settings == null) return;
            settings.bookIntellectualXpPerHour = NormalizeIntellectual(settings.bookIntellectualXpPerHour);
            settings.bookQualityMultipliers = Sanitize(settings.bookQualityMultipliers);
            // Scribe PostLoadInit: migrate before sanitizing the float map.
            // Medical=0 means zero XP, never an absent/neutral multiplier.
            bool canonicalWon;
            bool migrated = MigrateLegacyMedicalKey(settings.bookSkillMultipliers,
                out canonicalWon);
#if DEBUG
            if (migrated && !legacySettingsMigrationLogged)
            {
                legacySettingsMigrationLogged = true;
                Log.Message("[XPC-BOOKXP-MEDFIX] phase=SETTINGS_NORMALIZE; legacyPresent=true" +
                    "; canonicalWon=" + canonicalWon +
                    "; source=POSTLOAD_OR_DETACHED_TEST; fileWritesByMigration=0; build=" +
                    XpCustomizerMod.BuildId);
            }
#endif
            settings.bookSkillMultipliers = Sanitize(settings.bookSkillMultipliers);
        }
        private static Dictionary<string, float> Sanitize(Dictionary<string, float> values)
        {
            var result = new Dictionary<string, float>();
            if (values == null) return result;
            foreach (var pair in values)
            {
                if (string.IsNullOrEmpty(pair.Key)) continue;
                float value = NormalizeMultiplier(pair.Value);
                if (!Mathf.Approximately(value, 1f)) result[pair.Key] = value;
            }
            return result;
        }

        // Called at the exact, verified 1.6 reading-tick IL constant site.
        public static float IntellectualInputPerTick()
        {
            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            if (settings == null || !settings.bookXpEnabled || !settings.bookIntellectualEnabled)
                return VanillaIntellectualXpPerTick;
            float adjusted = NormalizeIntellectual(settings.bookIntellectualXpPerHour) / 2500f;
#if DEBUG
            if (!intellectualSampleLogged)
            {
                intellectualSampleLogged = true;
                Log.Message("[XP Customizer][BOOKXP-V1] live intellectual INPUT per tick: vanilla=0.1, custom=" +
                    adjusted.ToString("0.####") + "; SkillRecord LearnRateFactor unchanged; first sample only");
            }
#endif
            return adjusted;
        }

        // The transpiler substitutes only the targeted textbook Learn call,
        // preserving the source's CanProgressSkill gate and vanilla arguments.
        public static void LearnTextbookAdjusted(SkillRecord record, float xp,
            bool direct, bool ignoreLearnRate, BookOutcomeDoerGainSkillExp doer)
        {
            if (record == null) return;
            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            if (settings == null || !settings.bookXpEnabled ||
                (!settings.bookQualityEnabled && !settings.bookSkillEnabled)
                || doer == null || record.def == null)
            {
                record.Learn(xp, direct, ignoreLearnRate);
                return;
            }
            float multiplier = GetEffectiveTextbookMultiplier(settings, doer.Quality, record.def.defName);
            float adjusted = Mathf.Approximately(multiplier, 1f) ? xp : xp * multiplier;
#if DEBUG
            if (!textbookSampleLogged)
            {
                textbookSampleLogged = true;
                Log.Message("[XP Customizer][BOOKXP-V1] live textbook INPUT sample: quality=" + doer.Quality +
                    "; skill=" + record.def.defName + "; original=" + xp.ToString("0.####") +
                    "; multiplier=x" + multiplier.ToString("0.##") +
                    "; adjusted=" + adjusted.ToString("0.####") +
                    "; levelCap=vanilla; SkillRecord.Learn=preserved");
            }
#endif
            record.Learn(adjusted, direct, ignoreLearnRate);
        }
    }
}
