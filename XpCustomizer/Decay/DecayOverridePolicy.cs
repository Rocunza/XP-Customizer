using System;
using UnityEngine;

namespace XpCustomizer.Decay
{
    // DP1: one source of truth for per-record customization intent.
    // Pure, allocation-free during SkillRecord.Interval; no Harmony or game writes.
    [Flags]
    internal enum DecayOverrideReason
    {
        None = 0,
        Level = 1,
        Skill = 2,
        Memory = 4
    }

    internal static class DecayOverridePolicy
    {
        internal const string Revision = "DECAY-POLICY-DP1-R1";

        internal static DecayOverrideReason GetReasons(XpCustomizerSettings settings,
            int rawLevel, string defName, bool great, bool perfect, bool anomalyActive)
        {
            // Level 0 is protected by the vanilla skill system; do not suppress
            // its Interval or the original for invalid/missing records.
            if (settings == null || rawLevel <= 0 ||
                rawLevel >= SkillDecayService.VanillaDaily.Length ||
                string.IsNullOrEmpty(defName))
                return DecayOverrideReason.None;

            DecayOverrideReason reasons = DecayOverrideReason.None;

            // Merely enabling the editor is not a change. Evaluate only the
            // CURRENT level. An invalid/malformed value cannot claim ownership.
            if (settings.decayByLevelEnabled && settings.decayDailyByLevel != null &&
                settings.decayDailyByLevel.Count == SkillDecayService.VanillaDaily.Length)
            {
                float configured = settings.decayDailyByLevel[rawLevel];
                if (!float.IsNaN(configured) && !float.IsInfinity(configured) &&
                    !Mathf.Approximately(Mathf.Clamp(configured, 0f, SkillDecayService.MaxDaily),
                        SkillDecayService.VanillaDaily[rawLevel]))
                    reasons |= DecayOverrideReason.Level;
            }

            // 0x is an explicit override, not a neutral/pass-through setting.
            if (!Mathf.Approximately(DecayModifiersService.ActiveSkillMultiplier(settings, defName),
                DecayModifiersService.NeutralSkillMultiplier))
                reasons |= DecayOverrideReason.Skill;

            // Perfect Memory takes precedence only with Anomaly active.
            if (anomalyActive && perfect)
            {
                if (!Mathf.Approximately(DecayModifiersService.NormalizeReduction(
                    settings.perfectMemoryDecayReduction,
                    DecayModifiersService.VanillaPerfectReduction),
                    DecayModifiersService.VanillaPerfectReduction))
                    reasons |= DecayOverrideReason.Memory;
            }
            else if (great && !Mathf.Approximately(DecayModifiersService.NormalizeReduction(
                    settings.greatMemoryDecayReduction,
                    DecayModifiersService.VanillaGreatReduction),
                    DecayModifiersService.VanillaGreatReduction))
            {
                reasons |= DecayOverrideReason.Memory;
            }
            return reasons;
        }
    }
}
