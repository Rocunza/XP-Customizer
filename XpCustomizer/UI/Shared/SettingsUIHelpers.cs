using UnityEngine;
using Verse;

namespace XpCustomizer.UI
{
    /// <summary>
    /// Reusable drawing helpers for XP Customizer settings UI.
    ///
    /// This class contains presentation logic only.
    /// It must not modify gameplay rules or calculate gameplay values.
    /// </summary>
    internal static partial class SettingsUIHelpers
    {
        public static void DrawSectionTitle(
            Listing_Standard listing,
            string title)
        {
            GameFont oldFont =
                Text.Font;

            try
            {
                Text.Font =
                    GameFont.Medium;

                listing.Label(
                    title);
            }
            finally
            {
                Text.Font =
                    oldFont;
            }
        }


        public static void DrawDescription(
            Listing_Standard listing,
            string text)
        {
            Color oldColor =
                GUI.color;

            try
            {
                GUI.color =
                    Color.gray;

                listing.Label(
                    text);
            }
            finally
            {
                GUI.color =
                    oldColor;
            }
        }


        public static void DrawReferenceText(
            Listing_Standard listing,
            string text)
        {
            Color oldColor =
                GUI.color;

            GameFont oldFont =
                Text.Font;

            // Use RimWorld's built-in Small font. Never mutate fontSize or
            // shared GUIStyle instances; they can corrupt unrelated UI text.
            try
            {
                GUI.color =
                    new Color(0.76f, 0.76f, 0.76f, 1f);

                Text.Font =
                    GameFont.Small;

                listing.Label(
                    text);
            }
            finally
            {
                Text.Font =
                    oldFont;

                GUI.color =
                    oldColor;
            }
        }


    }
}
