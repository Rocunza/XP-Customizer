using Verse;

namespace XpCustomizer.UI
{
    /// <summary>
    /// Draws the Progression settings tab and delegates each major section
    /// to its dedicated UI class.
    ///
    /// This class owns layout order only.
    /// </summary>
    internal static class ProgressionSettingsUI
    {
        public static void Draw(
            Listing_Standard listing)
        {
            CareerProgressionSettingsUI.Draw(
                listing);

            DrawSeparator(
                listing);

            Level20ReserveSettingsUI.Draw(
                listing);

            DrawSeparator(
                listing);

            SkilltrainerSettingsUI.Draw(
                listing);

            DrawSeparator(listing);
            BookXpSettingsUI.Draw(listing);

            listing.Gap(20f);
        }


        public static void NotifySettingsExternallyChanged()
        {
            CareerProgressionSettingsUI
                .NotifySettingsExternallyChanged();
            SkillSpecificXpSettingsUI.NotifySettingsExternallyChanged();

            Level20ReserveSettingsUI
                .NotifySettingsExternallyChanged();

            SkilltrainerSettingsUI
                .NotifySettingsExternallyChanged();
            BookXpSettingsUI.NotifySettingsExternallyChanged();
        }


        private static void DrawSeparator(
            Listing_Standard listing)
        {
            listing.Gap(8f);
            listing.GapLine();
            listing.Gap(8f);
        }
    }
}
