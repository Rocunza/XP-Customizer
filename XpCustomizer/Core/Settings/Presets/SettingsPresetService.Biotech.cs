using Verse;
using XpCustomizer.Biotech;

namespace XpCustomizer
{
    internal static partial class SettingsPresetService
    {
        // ================================================================
        // Biotech skill genes
        // ================================================================


        public static void ResetBiotechToVanilla()
        {
            XpCustomizerSettings settings =
                XpCustomizerMod.Settings;

            if (settings == null)
            {
                return;
            }


            // Vanilla-neutral means XP Customizer releases ownership
            // of the GeneDefs. The service restores the values that existed
            // immediately before customization was enabled.
            settings.biotechCustomizationEnabled =
                XpCustomizerDefaults.BiotechCustomizationEnabled;


            // Keep the stored editor values at vanilla as the next starting
            // point if customization is enabled again later.
            settings.biotechAwfulAptitude =
                XpCustomizerDefaults.BiotechAwfulAptitude;
            settings.biotechPoorAptitude =
                XpCustomizerDefaults.BiotechPoorAptitude;
            settings.biotechStrongAptitude =
                XpCustomizerDefaults.BiotechStrongAptitude;
            settings.biotechGreatAptitude =
                XpCustomizerDefaults.BiotechGreatAptitude;

            settings.biotechAwfulComplexity =
                XpCustomizerDefaults.BiotechAwfulComplexity;
            settings.biotechPoorComplexity =
                XpCustomizerDefaults.BiotechPoorComplexity;
            settings.biotechStrongComplexity =
                XpCustomizerDefaults.BiotechStrongComplexity;
            settings.biotechGreatComplexity =
                XpCustomizerDefaults.BiotechGreatComplexity;

            settings.biotechAwfulMetabolism =
                XpCustomizerDefaults.BiotechAwfulMetabolism;
            settings.biotechPoorMetabolism =
                XpCustomizerDefaults.BiotechPoorMetabolism;
            settings.biotechStrongMetabolism =
                XpCustomizerDefaults.BiotechStrongMetabolism;
            settings.biotechGreatMetabolism =
                XpCustomizerDefaults.BiotechGreatMetabolism;


            ApplyBiotechSettingsIfActive();
        }


        // ================================================================
        // Internal helpers
        // ================================================================

        private static void ApplyBiotechSettingsIfActive()
        {
            if (ModsConfig.BiotechActive)
            {
                BiotechGeneService.ApplySettings();
            }
        }
    }
}
