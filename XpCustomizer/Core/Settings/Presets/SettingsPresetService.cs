namespace XpCustomizer
{
    /// <summary>
    /// Applies groups of preset values to XP Customizer settings.
    ///
    /// Responsibilities:
    /// - Vanilla-behavior presets
    /// - Independent Cons/GWS section-level default resets
    /// - Retains saved legacy PP metadata for compatibility
    ///
    /// This class does not draw UI and does not calculate gameplay effects.
    /// </summary>
    internal static partial class SettingsPresetService
    {
        // ================================================================
        // Global Vanilla preset
        // ================================================================


        public static void ResetAllToVanilla()
        {
            ResetProgressionToVanilla();
            ResetLearningToVanilla();
            ResetBiotechToVanilla();
            ResetDecayToVanilla();
            ResetGwsToVanilla();
        }

        // R4: unlike the original Vanilla preset, a full MOD-default reset
        // must reproduce the install-time Mental Threshold strength (60%).
        // Other public tuning and activation fields already use the same
        // defaults in the existing section reset routes. The per-tab Further
        // Learning button uses 60% while retaining its old Vanilla caption;
        // the separate internal Vanilla route still uses the 100% reference.
        public static void ResetAllToModDefaults()
        {
            ResetProgressionToVanilla();
            ResetLearningCapacityToVanilla();
            ResetFurtherLearningToModDefaults();
            ResetConsciousnessLearningToVanilla();
            ResetBiotechToVanilla();
            ResetDecayToVanilla();
            ResetGwsToVanilla();
        }
    }
}
