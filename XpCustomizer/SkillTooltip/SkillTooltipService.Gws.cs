using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.FurtherLearning;
using XpCustomizer.Gws;
using XpCustomizer.LearningCapacity;

namespace XpCustomizer.SkillTooltip
{
    // TT2: same CLR type; original method bodies remain byte-identical.
    internal static partial class SkillTooltipService
    {



        // ================================================================
        // Work Factor: read-only GWS preview used by SkillUI
        // ================================================================
        // Use the same stat, curve, skill allowlist and switches as the
        // gameplay Postfix, but do NOT call GwsLearningService.GetFactor():
        // that method logs a first-use *gameplay* sample in Debug.
        // SkillUI describes ordinary (non-direct, non-reading) learning.
        // A book-reading exception can therefore make actual book XP differ.
        // Return the original WorkSpeedGlobal AND the derived XP multiplier
        // from one stat read; the UI must not confuse the two values.
        private static bool TryGetWorkFactorLearningPreview(
            SkillRecord skill, out float globalWorkSpeed, out float factor)
        {
            globalWorkSpeed = 1f;
            factor = 1f;
            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            if (settings == null || !settings.gwsEnabled ||
                DebugSettings.fastLearning || skill == null ||
                skill.TotallyDisabled || skill.def == null ||
                skill.Pawn == null || skill.Pawn.RaceProps == null ||
                !skill.Pawn.RaceProps.Humanlike ||
                !GwsLearningService.IsSkillEnabled(settings, skill.def.defName) ||
                (!settings.gwsApplyToReading && GwsReadingScope.IsReadingTick))
            {
                return false;
            }

            globalWorkSpeed = skill.Pawn.GetStatValue(StatDefOf.WorkSpeedGlobal);
            if (float.IsNaN(globalWorkSpeed) || float.IsInfinity(globalWorkSpeed) ||
                (globalWorkSpeed < 1f && !settings.gwsLowEnabled) ||
                (globalWorkSpeed > 1f && !settings.gwsHighEnabled))
            {
                return false;
            }

            factor = GwsLearningService.Calculate(globalWorkSpeed, settings);
            return true;
        }

        public static bool TryGetWorkFactorLearningMultiplier(
            SkillRecord skill, out float factor)
        {
            float globalWorkSpeed;
            return TryGetWorkFactorLearningPreview(skill, out globalWorkSpeed, out factor);
        }

        public static float GetWorkFactorLearningMultiplier(SkillRecord skill)
        {
            float factor;
            return TryGetWorkFactorLearningMultiplier(skill, out factor)
                ? factor : 1f;
        }

        private static bool IsWorkFactorLearningSpeedPresentationActive(SkillRecord skill)
        {
            float factor;
            return TryGetWorkFactorLearningMultiplier(skill, out factor) &&
                !IsNeutralAppliedMultiplier(factor);
        }

        public static string BuildWorkFactorLearningLine(SkillRecord skill)
        {
            float globalWorkSpeed;
            float factor;
            if (!TryGetWorkFactorLearningPreview(skill, out globalWorkSpeed, out factor) ||
                IsNeutralAppliedMultiplier(factor))
            {
                return string.Empty;
            }

            return SkillTooltipLocalizedPresentation.BuildGwsLine(globalWorkSpeed, factor);
        }

        // Pure, side-effect-free formatting fixtures for the DEBUG regressions.
        internal static string BuildGwsPreviewLine(float globalWorkSpeed, float factor)
        {
            if (IsNeutralAppliedMultiplier(factor)) return string.Empty;
            return "  - GW speed: " + FormatMultiplierPercent(factor);
        }
    }
}
