using System.Collections.Generic;
using XpCustomizer.Decay;

namespace XpCustomizer
{
    internal static partial class SettingsPresetService
    {
        public static void ResetDecayToVanilla()
        {
            if (XpCustomizerMod.Settings == null) return;
            XpCustomizerMod.Settings.decayByLevelEnabled = false;
            XpCustomizerMod.Settings.decayDailyByLevel = SkillDecayService.DefaultValues();
            XpCustomizerMod.Settings.decaySkillMultipliersEnabled = false;
            XpCustomizerMod.Settings.decaySkillMultipliers = new Dictionary<string, float>();
            XpCustomizerMod.Settings.greatMemoryDecayReduction = 50f;
            XpCustomizerMod.Settings.perfectMemoryDecayReduction = 100f;
        }
    }
}
