using Verse;
using XpCustomizer.ConsciousnessLearning;

namespace XpCustomizer
{
    internal static partial class SettingsPresetService
    {
        // ================================================================
        // Consciousness Learning
        // ================================================================


        // The Cons section Mod Defaults button restores its established
        // numerical/curve defaults; it is NOT the R3-A global numeric reset.
        // Preserve the master and all three independent enable switches.
        public static void ResetConsciousnessToModDefaults()
        {
            XpCustomizerSettings s = XpCustomizerMod.Settings;
            if (s == null) return;
            ApplyConsciousnessModDefaults(s);
            Log.Message("[XP Customizer][PRESETS-R1] action=Cons-ModDefaults; master=" +
                s.consciousnessAffectsLearning + "; speed=" +
                s.consciousnessAffectsLearningSpeed + "; capacity=" +
                s.consciousnessAffectsDailyCapacity + "; further=" +
                s.consciousnessAffectsFurtherLearning + "; activations=preserved; " +
                "V2Profiles=High120/Linear/x1.08/safety2; FurtherV2=Low80/Linear/x0.80+High120/Linear/x1.20/safety2; " +
                "source=ANCHOR_V2_ONLY; otherTabs=unchanged");
        }

        internal static void ApplyConsciousnessModDefaults(XpCustomizerSettings settings)
        {
            if (settings == null) return;
            bool master = settings.consciousnessAffectsLearning;
            bool speed = settings.consciousnessAffectsLearningSpeed;
            bool capacity = settings.consciousnessAffectsDailyCapacity;
            bool further = settings.consciousnessAffectsFurtherLearning;
            bool furtherLow = settings.consciousnessFurtherV2 == null ||
                settings.consciousnessFurtherV2.LowEnabled;
            bool furtherHigh = settings.consciousnessFurtherV2 == null ||
                settings.consciousnessFurtherV2.HighEnabled;
            // H1: Retired Hard/Progressive presets are not a dependency of
            // the Cons section button. These four legacy PP fields are
            // persisted rollback metadata; their old Mod Defaults numeric
            // values happen to equal the supported install defaults.
            settings.consciousnessFurtherPenaltyAt80 =
                XpCustomizerDefaults.ConsciousnessFurtherPenaltyAt80;
            settings.consciousnessFurtherMaximumPenalty =
                XpCustomizerDefaults.ConsciousnessFurtherMaximumPenalty;
            settings.consciousnessFurtherBonusAt170 =
                XpCustomizerDefaults.ConsciousnessFurtherBonusAt170;
            settings.consciousnessFurtherMaximumBonus =
                XpCustomizerDefaults.ConsciousnessFurtherMaximumBonus;

            // Retain the exact historical section-button behavior: Speed
            // and Capacity profiles are recreated (their own Low/High
            // switches go to defaults), while Further V2 Low/High toggles
            // and the four main Cons toggles are restored below.
            ResetConsciousnessV2ProfileValues(settings);
            settings.consciousnessFurtherV2 = new ConsciousnessFurtherV2Settings();

            settings.consciousnessAffectsLearning = master;
            settings.consciousnessAffectsLearningSpeed = speed;
            settings.consciousnessAffectsDailyCapacity = capacity;
            settings.consciousnessAffectsFurtherLearning = further;
            settings.consciousnessFurtherV2.LowEnabled = furtherLow;
            settings.consciousnessFurtherV2.HighEnabled = furtherHigh;
            // V2 numeric curves reset. Master and all switches are preserved.
        }


        // Replace both V2 profile objects with independent defaults.
        private static void ResetConsciousnessV2ProfileValues(XpCustomizerSettings settings)
        {
            settings.consciousnessAnchorSpeed = new ConsAnchorV2ProfileSettings();
            settings.consciousnessAnchorCapacity = new ConsAnchorV2ProfileSettings();
        }


        public static void ResetConsciousnessLearningToVanilla()
        {
            XpCustomizerSettings s = XpCustomizerMod.Settings;
            if (s == null) return;
            ApplyConsciousnessVanillaPreset(s);
            Log.Message("[XP Customizer][PRESETS-R1] action=Cons-Vanilla; master=OFF; scope=Cons-only");
        }

        internal static void ApplyConsciousnessVanillaPreset(XpCustomizerSettings settings)
        {
            if (settings == null)
            {
                return;
            }


            // Vanilla RimWorld does not use the XP Customizer
            // consciousness-learning system.
            settings.consciousnessAffectsLearning = false;

            settings.consciousnessAffectsLearningSpeed =
                XpCustomizerDefaults
                    .ConsciousnessAffectsLearningSpeed;

            settings.consciousnessAffectsDailyCapacity =
                XpCustomizerDefaults
                    .ConsciousnessAffectsDailyCapacity;

            settings.consciousnessAffectsFurtherLearning =
                XpCustomizerDefaults
                    .ConsciousnessAffectsFurtherLearning;


            // Keep the Further Learning curve at its normal baseline.
            // It has no gameplay effect while the master switch is disabled.
            settings.consciousnessFurtherPenaltyAt80 =
                XpCustomizerDefaults
                    .ConsciousnessFurtherPenaltyAt80;

            settings.consciousnessFurtherMaximumPenalty =
                XpCustomizerDefaults
                    .ConsciousnessFurtherMaximumPenalty;

            settings.consciousnessFurtherBonusAt170 =
                XpCustomizerDefaults
                    .ConsciousnessFurtherBonusAt170;

            settings.consciousnessFurtherMaximumBonus =
                XpCustomizerDefaults
                    .ConsciousnessFurtherMaximumBonus;


            // Vanilla preset disables the Cons master; all three stored
            // profiles reset to their mod's default editing values. Since the
            // master is OFF, vanilla gameplay remains neutral.
            ResetConsciousnessV2ProfileValues(settings);
            settings.consciousnessFurtherV2 = new ConsciousnessFurtherV2Settings();
        }
    }
}
