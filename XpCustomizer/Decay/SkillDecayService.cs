using System;
using System.Collections.Generic;
using UnityEngine;

namespace XpCustomizer.Decay
{
    internal static class SkillDecayService
    {
        // Actual vanilla SkillRecord.Interval() losses, 300 intervals per day.
        // Level 0 is never allowed to lose XP through SkillRecord.Learn.
        internal static readonly float[] VanillaDaily =
        {
            0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
            30f, 60f, 120f, 180f, 300f, 540f,
            840f, 1200f, 1800f, 2400f, 3600f
        };
        internal const float MaxDaily = 9999999f;
        internal const float IntervalsPerDay = 300f;

        internal static List<float> DefaultValues()
        {
            return new List<float>(VanillaDaily);
        }

        internal static void Sanitize(XpCustomizerSettings settings)
        {
            if (settings == null) return;
            if (settings.decayDailyByLevel == null || settings.decayDailyByLevel.Count != 21)
            {
                List<float> fixedValues = DefaultValues();
                if (settings.decayDailyByLevel != null)
                    for (int i = 1; i < Math.Min(21, settings.decayDailyByLevel.Count); i++)
                        fixedValues[i] = settings.decayDailyByLevel[i];
                settings.decayDailyByLevel = fixedValues;
            }
            settings.decayDailyByLevel[0] = 0f;
            for (int i = 1; i < 21; i++)
            {
                float v = settings.decayDailyByLevel[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) v = VanillaDaily[i];
                settings.decayDailyByLevel[i] = Mathf.Clamp(v, 0f, MaxDaily);
            }
        }

        internal static float LossPerInterval(XpCustomizerSettings settings, int level)
        {
            if (settings == null || settings.decayDailyByLevel == null ||
                settings.decayDailyByLevel.Count != 21 || level <= 0 || level > 20)
                return 0f;
            float daily = settings.decayDailyByLevel[level];
            if (float.IsNaN(daily) || float.IsInfinity(daily) || daily < 0f) return 0f;
            return Mathf.Min(daily, MaxDaily) / IntervalsPerDay;
        }
    }
}
