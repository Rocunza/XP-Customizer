using System;
using System.Collections.Generic;
using UnityEngine;

namespace XpCustomizer.Decay
{
    /// <summary>
    /// D3: independent level, per-SkillDef, and numeric memory-trait adjustments.
    /// Pure arithmetic only. No Pawn or Def mutation; unknown defs are kept.
    /// </summary>
    internal static class DecayModifiersService
    {
        internal const float NeutralSkillMultiplier = 1f;
        internal const float MinimumSkillMultiplier = 0f;
        internal const float MaximumSkillMultiplier = 100f;
        internal const float VanillaGreatReduction = 50f;
        internal const float VanillaPerfectReduction = 100f;

        internal static float NormalizeMultiplier(float v)
        {
            return float.IsNaN(v) || float.IsInfinity(v)
                ? NeutralSkillMultiplier : Mathf.Clamp(v, MinimumSkillMultiplier, MaximumSkillMultiplier);
        }

        internal static float NormalizeReduction(float v, float fallback)
        {
            return float.IsNaN(v) || float.IsInfinity(v)
                ? fallback : Mathf.Round(Mathf.Clamp(v, 0f, 100f) * 100f) / 100f;
        }

        internal static void Sanitize(XpCustomizerSettings s)
        {
            if (s == null) return;
            s.greatMemoryDecayReduction = NormalizeReduction(
                s.greatMemoryDecayReduction, VanillaGreatReduction);
            s.perfectMemoryDecayReduction = NormalizeReduction(
                s.perfectMemoryDecayReduction, VanillaPerfectReduction);
            var clean = new Dictionary<string, float>();
            if (s.decaySkillMultipliers != null)
                foreach (var pair in s.decaySkillMultipliers)
                {
                    if (string.IsNullOrEmpty(pair.Key)) continue;
                    float value = NormalizeMultiplier(pair.Value);
                    if (!Mathf.Approximately(value, NeutralSkillMultiplier))
                        clean[pair.Key] = value;
                }
            s.decaySkillMultipliers = clean;
        }

        internal static float ConfiguredSkillMultiplier(XpCustomizerSettings s, string defName)
        {
            if (s == null || string.IsNullOrEmpty(defName) || s.decaySkillMultipliers == null)
                return NeutralSkillMultiplier;
            float value;
            return s.decaySkillMultipliers.TryGetValue(defName, out value)
                ? NormalizeMultiplier(value) : NeutralSkillMultiplier;
        }

        internal static float ActiveSkillMultiplier(XpCustomizerSettings s, string defName)
        {
            return s != null && s.decaySkillMultipliersEnabled
                ? ConfiguredSkillMultiplier(s, defName) : NeutralSkillMultiplier;
        }

        internal static void SetSkillMultiplier(XpCustomizerSettings s, string defName, float value)
        {
            if (s == null || string.IsNullOrEmpty(defName)) return;
            if (s.decaySkillMultipliers == null)
                s.decaySkillMultipliers = new Dictionary<string, float>();
            value = NormalizeMultiplier(value);
            if (Mathf.Approximately(value, NeutralSkillMultiplier))
                s.decaySkillMultipliers.Remove(defName);
            else
                s.decaySkillMultipliers[defName] = value;
        }

        internal static int ModifiedCount(XpCustomizerSettings s)
        {
            if (s == null || s.decaySkillMultipliers == null) return 0;
            int count = 0;
            foreach (var pair in s.decaySkillMultipliers)
                if (!string.IsNullOrEmpty(pair.Key) &&
                    !Mathf.Approximately(NormalizeMultiplier(pair.Value), NeutralSkillMultiplier))
                    count++;
            return count;
        }

        // Perfect Memory has precedence only when Anomaly enables its vanilla effect.
        // Great Memory's vanilla 50% reduction applies regardless of Anomaly.
        internal static float MemoryRemainingFactor(XpCustomizerSettings s,
            bool great, bool perfect, bool anomalyActive)
        {
            if (anomalyActive && perfect)
            {
                float reduction = s != null
                    ? NormalizeReduction(s.perfectMemoryDecayReduction, VanillaPerfectReduction)
                    : VanillaPerfectReduction;
                return (100f - reduction) / 100f;
            }
            if (great)
            {
                float reduction = s != null
                    ? NormalizeReduction(s.greatMemoryDecayReduction, VanillaGreatReduction)
                    : VanillaGreatReduction;
                return (100f - reduction) / 100f;
            }
            return 1f;
        }

        // Keep the external call contract minimal. Per-record policy and debug
        // diagnostics both use the same level/skill/trait decision function.
        internal static bool NeedsOverride(XpCustomizerSettings s, int rawLevel, string defName,
            bool great, bool perfect, bool anomalyActive)
        {
            return DecayOverridePolicy.GetReasons(s, rawLevel, defName,
                great, perfect, anomalyActive) != DecayOverrideReason.None;
        }

        internal static float DailyLoss(XpCustomizerSettings s, int level,
            string defName, bool great, bool perfect, bool anomalyActive)
        {
            if (level <= 0 || level > 20) return 0f;
            float baseDaily = SkillDecayService.VanillaDaily[level];
            if (s != null && s.decayByLevelEnabled &&
                s.decayDailyByLevel != null && s.decayDailyByLevel.Count == 21)
            {
                float custom = s.decayDailyByLevel[level];
                if (!float.IsNaN(custom) && !float.IsInfinity(custom))
                    baseDaily = Mathf.Clamp(custom, 0f, SkillDecayService.MaxDaily);
            }
            double final = (double)baseDaily * ActiveSkillMultiplier(s, defName) *
                MemoryRemainingFactor(s, great, perfect, anomalyActive);
            // Keep the final loss bounded for extreme user configurations.
            return (float)Math.Max(0d, Math.Min(SkillDecayService.MaxDaily, final));
        }

        internal static float LossPerInterval(XpCustomizerSettings s, int level,
            string defName, bool great, bool perfect, bool anomalyActive)
        {
            return DailyLoss(s, level, defName, great, perfect, anomalyActive) /
                SkillDecayService.IntervalsPerDay;
        }
    }
}
