using Verse;
using UnityEngine;

namespace XpCustomizer.UI
{
    /// <summary>
    /// Coordinates the Learning settings tab.
    ///
    /// Individual learning systems own their own UI sections.
    /// Gameplay calculations do not belong here.
    /// </summary>
    internal static class LearningSettingsUI
    {
#if DEBUG
        private const string LocalizationVersion = "XPC-L10N-L3C1-LEARNING-20261112-R1";
        private static bool translationLogged;
#endif

        // ================================================================
        // Main draw
        // ================================================================

        public static void Draw(
            Listing_Standard listing)
        {
#if DEBUG
            if (!translationLogged && Event.current != null &&
                Event.current.type == EventType.Repaint)
            {
                translationLogged = true;
                Log.Message("[XPC-L10N-L3C1][KEYED_LEARNING] version=" +
                    LocalizationVersion + "; phase=C1; keys=59; sampleKey=XPC_Learning_DailyCapTitle; sample=" +
                    "XPC_Learning_DailyCapTitle".Translate().ToString() +
                    "; build=" + XpCustomizerMod.BuildId);
            }
#endif
            // ------------------------------------------------------------
            // Dynamic Learning Capacity
            // ------------------------------------------------------------

            LearningCapacitySettingsUI.Draw(
                listing);


            DrawSeparator(
                listing);


            // ------------------------------------------------------------
            // Further Learning
            // ------------------------------------------------------------

            FurtherLearningSettingsUI.Draw(
                listing);
        }


        // ================================================================
        // External setting changes
        // ================================================================

        /// <summary>
        /// Notifies child UI sections that settings were changed outside
        /// their own controls, such as by the global reset button.
        ///
        /// This prevents stale TextFieldNumeric buffers from restoring
        /// values that were replaced by a preset or reset.
        /// </summary>
        public static void NotifySettingsExternallyChanged()
        {
            LearningCapacitySettingsUI
                .NotifySettingsExternallyChanged();

            FurtherLearningSettingsUI
                .NotifySettingsExternallyChanged();
        }


        // ================================================================
        // Layout
        // ================================================================

        private static void DrawSeparator(
            Listing_Standard listing)
        {
            listing.Gap(8f);
            listing.GapLine();
            listing.Gap(12f);
        }
    }
}
