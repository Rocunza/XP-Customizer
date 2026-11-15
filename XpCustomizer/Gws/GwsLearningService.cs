using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace XpCustomizer.Gws
{
    public enum GwsCurveMode { Diminishing = 0, Linear = 1, Accelerating = 2 }

    /// <summary>
    /// GWS changes skill learning speed only. It never alters WorkSpeedGlobal,
    /// source XP, daily cap, saturation, skilltrainers or reading speed.
    /// </summary>
    public static class GwsLearningService
    {
        public const float DefaultLowAnchorGws = 0.30f;
        public const float DefaultHighAnchorGws = 5.00f;
        public const float LowAnchorMinimum = 0.30f;
        public const float LowAnchorMaximum = 0.95f;
        public const float HighAnchorMinimum = 1.10f;
        public const float HighAnchorMaximum = 10.00f;
        // Historical fixed-point aliases for old source consumers (NOT new Mod Defaults).
        public const float MinimumGws = DefaultLowAnchorGws;
        public const float NeutralGws = 1.00f;
        public const float HighAnchorGws = LegacyCurveXmlDefaults.GwsHighAnchor;
        public const float DefaultLowFactor = 0.70f;
        public const float DefaultHighFactor = 1.30f;
        public const int DefaultHighCurve = (int)GwsCurveMode.Diminishing;
        public const float DefaultHighCurvature = 0.40f;
        public const float DefaultDiminishingLimit = 1.50f;
        public const float DefaultAcceleratingLimit = 2f;
        public const float TechnicalMaxFactor = 20f;

        private static readonly string[] VanillaSkillDefs = {
            "Shooting", "Melee", "Construction", "Mining", "Cooking", "Plants",
            "Animals", "Crafting", "Artistic", "Medicine", "Social", "Intellectual"
        };
        public static IEnumerable<string> Skills { get { return VanillaSkillDefs; } }

        public static bool DefaultSkillEnabled(string defName)
        {
            if (defName == "Shooting" || defName == "Melee" || defName == "Social") return false;
            foreach (string item in VanillaSkillDefs)
                if (item == defName) return true;
            return false;
        }
        public static bool IsKnownSkill(string defName)
        {
            foreach (string item in VanillaSkillDefs)
                if (item == defName) return true;
            return false;
        }
        public static bool IsSkillEnabled(XpCustomizerSettings settings, string defName)
        {
            if (!IsKnownSkill(defName)) return false; // no foreign SkillDef hijack
            bool enabled;
            return settings != null && settings.gwsSkillEnabled != null &&
                   settings.gwsSkillEnabled.TryGetValue(defName, out enabled)
                ? enabled : DefaultSkillEnabled(defName);
        }
        public static void SetSkillEnabled(XpCustomizerSettings settings, string defName, bool enabled)
        {
            if (settings == null || !IsKnownSkill(defName)) return;
            if (settings.gwsSkillEnabled == null)
                settings.gwsSkillEnabled = new Dictionary<string, bool>();
            if (enabled == DefaultSkillEnabled(defName)) settings.gwsSkillEnabled.Remove(defName);
            else settings.gwsSkillEnabled[defName] = enabled;
        }
        /// <summary>
        /// Convert only the old GWS settings key to the real RimWorld SkillDef ID.
        /// This operates on a settings-owned or detached candidate dictionary; it
        /// never touches a source profile document, any SkillDef, or other mods.
        /// The canonical key wins whenever both names exist. Preserves comparer.
        /// </summary>
        internal static bool MigrateLegacyMedicalKey(Dictionary<string, bool> skills,
            out bool canonicalWon)
        {
            canonicalWon = false;
            if (skills == null) return false;
            // Do not treat foreign skills with different casing as legacy data,
            // even when a caller constructed a case-insensitive dictionary.
            bool exactLegacyKey = false;
            foreach (string key in skills.Keys)
                if (string.Equals(key, "Medical", StringComparison.Ordinal))
                {
                    exactLegacyKey = true;
                    break;
                }
            if (!exactLegacyKey) return false;

            bool legacyValue = skills["Medical"];
            canonicalWon = skills.ContainsKey("Medicine");
            if (!canonicalWon) skills["Medicine"] = legacyValue;
            skills.Remove("Medical");
            return true;
        }

#if DEBUG
        private static bool legacySettingsMigrationLogged;
#endif
        public static GwsCurveMode NormalizeMode(int id)
        {
            return id == 1 ? GwsCurveMode.Linear :
                id == 2 ? GwsCurveMode.Accelerating : GwsCurveMode.Diminishing;
        }
        public static float NormalizeNumber(float value, float fallback, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return Mathf.Clamp(fallback, min, max);
            return Mathf.Clamp(value, min, max);
        }
        public static void NormalizeSettings(XpCustomizerSettings s)
        {
            if (s == null) return;
            s.gwsLowAnchor = NormalizeNumber(s.gwsLowAnchor,
                DefaultLowAnchorGws, LowAnchorMinimum, LowAnchorMaximum);
            s.gwsHighAnchor = NormalizeNumber(s.gwsHighAnchor,
                DefaultHighAnchorGws, HighAnchorMinimum, HighAnchorMaximum);
            s.gwsLowFactor = NormalizeNumber(s.gwsLowFactor, DefaultLowFactor, 0f, 1f);
            s.gwsHighFactor = NormalizeNumber(s.gwsHighFactor, DefaultHighFactor, 1f, 5f);
            s.gwsLowCurvature = NormalizeNumber(s.gwsLowCurvature, 1f, 0f, 2f);
            s.gwsHighCurvature = NormalizeNumber(s.gwsHighCurvature,
                DefaultHighCurvature, 0f, 2f);
            s.gwsLowCurve = (int)NormalizeMode(s.gwsLowCurve);
            s.gwsHighCurve = (int)NormalizeMode(s.gwsHighCurve);
            s.gwsHighDiminishingLimit = NormalizeNumber(s.gwsHighDiminishingLimit,
                DefaultDiminishingLimit, s.gwsHighFactor + 0.01f, TechnicalMaxFactor);
            // Linear and Accelerating share an editable safety cap (default x2, allowable up to x20).
            s.gwsHighTechnicalLimit = NormalizeNumber(s.gwsHighTechnicalLimit,
                DefaultAcceleratingLimit, Math.Max(1f, s.gwsHighFactor), TechnicalMaxFactor);
            if (s.gwsSkillEnabled == null) s.gwsSkillEnabled = new Dictionary<string, bool>();
            // Runs before unknown-key deletion during Scribe PostLoadInit. An
            // old Medical=false must not silently revert to Medicine's ON default.
            bool canonicalWon;
            bool migrated = MigrateLegacyMedicalKey(s.gwsSkillEnabled, out canonicalWon);
#if DEBUG
            if (migrated && !legacySettingsMigrationLogged)
            {
                legacySettingsMigrationLogged = true;
                Log.Message("[XPC-GWS-MEDFIX] phase=SETTINGS_NORMALIZE; legacyPresent=true" +
                    "; canonicalWon=" + canonicalWon +
                    "; source=POSTLOAD_OR_DETACHED_TEST; fileWritesByMigration=0; build=" +
                    XpCustomizerMod.BuildId);
            }
#endif
            var sanitized = new Dictionary<string, bool>();
            foreach (var pair in s.gwsSkillEnabled)
                if (IsKnownSkill(pair.Key) && pair.Value != DefaultSkillEnabled(pair.Key))
                    sanitized[pair.Key] = pair.Value;
            s.gwsSkillEnabled = sanitized;
        }

        /// <summary>
        /// Pure, side-effect-free curve. Explicit 30%/100%/180% anchors preserve V1 exactly.
        /// Editable anchors only change each side's normalized coordinate.
        /// High diminishing tends to limit, never overshoots it. For high
        /// accelerating/linear, highTechnicalLimit caps extreme modded GWS.
        /// </summary>
        public static float Calculate(float gws, XpCustomizerSettings s)
        {
            if (s == null || !s.gwsEnabled) return 1f;
            if (float.IsNaN(gws) || float.IsInfinity(gws)) return 1f;
            if (gws < 1f)
            {
                if (!s.gwsLowEnabled) return 1f;
                float lowAnchor = NormalizeNumber(s.gwsLowAnchor,
                    DefaultLowAnchorGws, LowAnchorMinimum, LowAnchorMaximum);
                float t = Mathf.Clamp01((1f - Mathf.Max(gws, lowAnchor)) / (1f - lowAnchor));
                float curve = NormalizeNumber(s.gwsLowCurvature, 1f, 0f, 2f);
                GwsCurveMode mode = NormalizeMode(s.gwsLowCurve);
                float share = mode == GwsCurveMode.Diminishing
                    ? 1f - Mathf.Pow(1f - t, 1f + curve)
                    : mode == GwsCurveMode.Accelerating ? Mathf.Pow(t, 1f + curve) : t;
                float anchor = NormalizeNumber(s.gwsLowFactor, DefaultLowFactor, 0f, 1f);
                return Mathf.Clamp(1f - (1f - anchor) * share, 0f, 1f);
            }
            if (gws <= 1f || !s.gwsHighEnabled) return 1f;

            float high = NormalizeNumber(s.gwsHighFactor, DefaultHighFactor, 1f, 5f);
            float highAnchor = NormalizeNumber(s.gwsHighAnchor,
                DefaultHighAnchorGws, HighAnchorMinimum, HighAnchorMaximum);
            float tHigh = Mathf.Max(0f, (gws - 1f) / (highAnchor - 1f));
            float strength = NormalizeNumber(s.gwsHighCurvature,
                DefaultHighCurvature, 0f, 2f);
            GwsCurveMode highMode = NormalizeMode(s.gwsHighCurve);
            if (highMode == GwsCurveMode.Diminishing)
            {
                float limit = NormalizeNumber(s.gwsHighDiminishingLimit,
                    DefaultDiminishingLimit, high + 0.01f, TechnicalMaxFactor);
                float proportion = Mathf.Clamp((high - 1f) / (limit - 1f), 0f, 0.9999f);
                float k = -(float)Math.Log(1f - proportion);
                float exponent = Mathf.Clamp(1f - 0.30f * strength, 0.40f, 1f);
                float saturation = 1f - Mathf.Exp(-k * Mathf.Pow(tHigh, exponent));
                return Mathf.Clamp(1f + (limit - 1f) * saturation, 1f, limit);
            }
            float max = NormalizeNumber(s.gwsHighTechnicalLimit,
                DefaultAcceleratingLimit, high, TechnicalMaxFactor);
            float power = highMode == GwsCurveMode.Accelerating ? 1f + strength : 1f;
            float scaled = 1f + (high - 1f) * Mathf.Pow(tHigh, power);
            return float.IsNaN(scaled) || float.IsInfinity(scaled) ? max : Mathf.Clamp(scaled, 1f, max);
        }

#if DEBUG
        private static bool sampleLogged;
        private static bool readingBypassLogged;
#endif
        public static float GetFactor(SkillRecord skill)
        {
            XpCustomizerSettings s = XpCustomizerMod.Settings;
            if (s == null || !s.gwsEnabled || skill == null || skill.Pawn == null ||
                skill.def == null || !IsSkillEnabled(s, skill.def.defName)) return 1f;
            if (!s.gwsApplyToReading && GwsReadingScope.IsReadingTick)
            {
#if DEBUG
                if (!readingBypassLogged)
                {
                    readingBypassLogged = true;
                    Log.Message("[XP Customizer][GWS-V2] Reading bypass confirmed: skill=" +
                        skill.def.defName + "; source=JobDriver_Reading; multiplier=x1.00");
                }
#endif
                return 1f;
            }
            Pawn pawn = skill.Pawn;
            if (pawn.RaceProps == null || !pawn.RaceProps.Humanlike) return 1f;
            float globalWorkSpeed = pawn.GetStatValue(StatDefOf.WorkSpeedGlobal);
            float factor = Calculate(globalWorkSpeed, s);
#if DEBUG
            if (!sampleLogged)
            {
                sampleLogged = true;
                Log.Message("[XP Customizer][GWS-V2] Live sample: skill=" + skill.def.defName +
                    "; GWS=" + (globalWorkSpeed * 100f).ToString("0.#") +
                    "%; factor=x" + factor.ToString("0.###") +
                    "; reading=" + GwsReadingScope.IsReadingTick +
                    "; lowAnchor=" + (s.gwsLowAnchor * 100f).ToString("0.#") +
                    "%; highAnchor=" + (s.gwsHighAnchor * 100f).ToString("0.#") +
                    "%; appliedToLearningOnly=True");
            }
#endif
            return factor;
        }
    }
}
