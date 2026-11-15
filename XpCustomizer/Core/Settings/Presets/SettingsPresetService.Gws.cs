using System.Collections.Generic;
using Verse;
using XpCustomizer.Gws;

namespace XpCustomizer
{
    internal static partial class SettingsPresetService
    {
        // Section presets must not enable effects when a user restores numeric
        // defaults. In particular, a fresh install keeps BOTH masters OFF.
        public static void ResetGwsToModDefaults()
        {
            XpCustomizerSettings s = XpCustomizerMod.Settings;
            if (s == null) return;
            ApplyGwsModDefaults(s);
            Log.Message("[XP Customizer][PRESETS-R1] action=GWS-ModDefaults; master=" +
                s.gwsEnabled + "; low=" + s.gwsLowEnabled + "; high=" +
                s.gwsHighEnabled + "; reading=" + s.gwsApplyToReading +
                "; activations=preserved; skills=Shooting/Melee/Social-OFF; anchorPositions=30%/500%; lowCurve=Linear; highCurve=Diminishing; highCurvature=40%; highFactor=x1.30; diminishingLimit=x1.50; highSafety=x2(stored)");
        }

        // Reset curve values and the twelve-skill preset, but preserve all
        // runtime activation switches (master, low, high and reading XP).
        // Detached-settings overload supports Debug regression tests.
        internal static void ApplyGwsModDefaults(XpCustomizerSettings s)
        {
            if (s == null) return;
            s.gwsLowAnchor = GwsLearningService.DefaultLowAnchorGws;
            s.gwsHighAnchor = GwsLearningService.DefaultHighAnchorGws;
            s.gwsLowFactor = GwsLearningService.DefaultLowFactor;
            s.gwsHighFactor = GwsLearningService.DefaultHighFactor;
            s.gwsLowCurve = (int)GwsCurveMode.Linear;
            s.gwsHighCurve = GwsLearningService.DefaultHighCurve;
            s.gwsLowCurvature = 1f;
            s.gwsHighCurvature = GwsLearningService.DefaultHighCurvature;
            s.gwsHighDiminishingLimit = GwsLearningService.DefaultDiminishingLimit;
            s.gwsHighTechnicalLimit = GwsLearningService.DefaultAcceleratingLimit;
            s.gwsSkillEnabled = new Dictionary<string, bool>();
        }

        public static void ResetGwsToVanilla()
        {
            XpCustomizerSettings s = XpCustomizerMod.Settings;
            if (s == null) return;
            ApplyGwsVanillaPreset(s);
            Log.Message("[XP Customizer][PRESETS-R1] action=GWS-Vanilla; master=OFF; scope=GWS-only");
        }

        internal static void ApplyGwsVanillaPreset(XpCustomizerSettings s)
        {
            if (s == null) return;
            s.gwsEnabled = false;
            s.gwsLowEnabled = true;
            s.gwsHighEnabled = true;
            s.gwsApplyToReading = false;
            ApplyGwsModDefaults(s);
        }
    }
}
