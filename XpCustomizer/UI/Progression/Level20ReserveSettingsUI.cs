using UnityEngine;
using Verse;

namespace XpCustomizer.UI
{
    /// <summary>
    /// Draws the Level 20 reserve settings section.
    /// </summary>
    internal static class Level20ReserveSettingsUI
    {
        private const float VanillaReserveXp = 30000f;

        private static readonly NumericInputState multiplierInput =
            new NumericInputState();

#if DEBUG
        private static bool reserveLayoutLogged;
#endif


        public static void Draw(
            Listing_Standard listing)
        {
            XpCustomizerSettings settings =
                XpCustomizerMod.Settings;


            SettingsUIHelpers.DrawSectionTitle(
                listing,
                "XPC_L3_ReserveTitle".Translate().ToString());

            SettingsUIHelpers.DrawDescription(
                listing,
                "XPC_L3_ReserveDesc".Translate().ToString());

            listing.Gap(4f);


            float value =
                settings.level20ReserveMultiplier;


            if (!float.IsNaN(
                    multiplierInput.lastValue) &&
                !Mathf.Approximately(
                    value,
                    multiplierInput.lastValue))
            {
                multiplierInput.buffer = null;
            }


            // UL1-HF1: same single-item adjacent layout as Skilltrainer.
            // Keep the original NumericInputState and ProfileNumericFocus pipeline;
            // only the three drawing rectangles change.
            string label = "XPC_L3_ReserveMultiplier".Translate().ToString();
            NumericRowLayout.Placement geometry = NumericRowLayout.Allocate(
                listing, label, 90f, 24f, 30f, 30f);
            Widgets.Label(geometry.Label, label);
            ProfileNumericFocus.Draw(geometry.Field, ref value, multiplierInput,
                XpCustomizerDefaults.ProgressionMultiplierMin,
                XpCustomizerDefaults.ProgressionMultiplierMax);
            TextAnchor previousAnchor = Text.Anchor;
            try
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(geometry.Suffix, "x");
            }
            finally { Text.Anchor = previousAnchor; }

#if DEBUG
            if (!reserveLayoutLogged && Event.current != null &&
                Event.current.type == EventType.Repaint)
            {
                reserveLayoutLogged = true;
                Log.Message("[XPC-UL1-HF1][RESERVE_LAYOUT] status=INSTALLED" +
                    "; mode=SINGLE_ADJACENT; geometry=NumericRowLayout" +
                    "; numericInput=UNCHANGED; build=" + XpCustomizerMod.BuildId);
            }
#endif


            settings.level20ReserveMultiplier =
                Mathf.Clamp(
                    value,
                    XpCustomizerDefaults.ProgressionMultiplierMin,
                    XpCustomizerDefaults.ProgressionMultiplierMax);

            multiplierInput.lastValue =
                settings.level20ReserveMultiplier;


            VanillaReferenceAccentUI.DrawProgressionRange(listing,
                "XPC_L3F3_ReserveRangeMuted", "XPC_L3F3_ReserveRangeVanilla",
                "XPC_L3_ReserveRange");

            listing.Gap(6f);


            VanillaReferenceAccentUI.DrawVanillaLabel(listing,
                "XPC_L3_ReserveVanillaPrefix".Translate().ToString() + " " +
                VanillaReserveXp.ToString("N0"));

            listing.Label(
                "XPC_L3_ReserveCustomPrefix".Translate().ToString() + " " +
                (VanillaReserveXp *
                 settings.level20ReserveMultiplier)
                .ToString("N0"));
        }


        public static void NotifySettingsExternallyChanged()
        {
            multiplierInput.Reset();
        }
    }
}
