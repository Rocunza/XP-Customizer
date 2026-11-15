namespace XpCustomizer.UI
{
    /// <summary>
    /// Top-level categories shown in the XP Customizer settings window.
    ///
    /// This is UI state only and is not saved as gameplay settings.
    /// </summary>
    internal enum SettingsTab
    {
        Progression,
        Learning,
        Cons,
        GWS,
#if DEBUG
        Insp,
        Combat,
#endif
        Decay,
        Biotech
    }
}