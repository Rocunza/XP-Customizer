using XpCustomizer.ConsciousnessLearning;
using UnityEngine;
using Verse;

namespace XpCustomizer.UI
{
    /// <summary>
    /// Consciousness settings coordinator. Switches affect gameplay;
    /// the effect preview toggle is presentation-only.
    /// </summary>
    internal static class ConsciousnessSettingsUI
    {
        internal const string SectionUiRevision = "CONS-SECTION-HIERARCHY-R7-20261103";

#if DEBUG
        private static bool translationLogged;
#endif

        public static void Draw(Listing_Standard listing)
        {
#if DEBUG
            if (!translationLogged)
            {
                translationLogged = true;
                Log.Message("[XPC-L10N-L3C2][KEYED_CONS_GWS] version=XPC-L10N-L3C2-CONS-GWS-20261112-R1; screen=CONSCIOUSNESS; keys=78; sampleKey=XPC_Cons_Title; sample=" +
                    "XPC_Cons_Title".Translate().ToString());
            }
#endif
            SettingsUIHelpers.DrawSectionTitle(listing, "XPC_Cons_Title".Translate().ToString());
            SettingsUIHelpers.DrawDescription(
                listing, "XPC_Cons_Description".Translate().ToString());

            listing.Gap(4f);

            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            bool masterEnabled = settings.consciousnessAffectsLearning;
            SettingsUIHelpers.DrawRightAdjacentToggle(
                listing, "XPC_Cons_Enable".Translate().ToString(), ref masterEnabled);
            settings.consciousnessAffectsLearning = masterEnabled;

            if (!masterEnabled)
            {
                SettingsUIHelpers.DrawReferenceText(
                    listing, "XPC_Cons_DisabledHelp".Translate().ToString());
                DrawPresetButtons(listing);
                return;
            }

            listing.Gap(10f);
            listing.GapLine();
            listing.Gap(8f);

            bool speedEnabled = settings.consciousnessAffectsLearningSpeed;
            DrawSubsectionToggle(listing, "XPC_Cons_SpeedTitle".Translate().ToString(), ref speedEnabled);
            settings.consciousnessAffectsLearningSpeed = speedEnabled;
            if (speedEnabled)
                ConsciousnessAnchorV2SettingsUI.DrawProfile(listing, settings, true);

            listing.Gap(12f);
            listing.GapLine();
            listing.Gap(8f);

            bool capacityEnabled = settings.consciousnessAffectsDailyCapacity;
            DrawSubsectionToggle(listing, "XPC_Cons_CapacityTitle".Translate().ToString(), ref capacityEnabled);
            settings.consciousnessAffectsDailyCapacity = capacityEnabled;
            if (capacityEnabled)
                ConsciousnessAnchorV2SettingsUI.DrawProfile(listing, settings, false);

            listing.Gap(12f);
            listing.GapLine();
            listing.Gap(8f);

            bool furtherEnabled = settings.consciousnessAffectsFurtherLearning;
            DrawSubsectionToggle(listing, "XPC_Cons_FurtherTitle".Translate().ToString(), ref furtherEnabled);
            settings.consciousnessAffectsFurtherLearning = furtherEnabled;
            if (furtherEnabled)
            {
                listing.Gap(4f);
                ConsciousnessFurtherLearningSettingsUI.Draw(listing);
            }

            // All three Cons systems now show Low/High previews inline.

            DrawPresetButtons(listing);
        }

        // Keep section-level resets accessible when the Cons master is OFF.
        private static void DrawPresetButtons(Listing_Standard listing)
        {
            listing.Gap(12f);
            Rect row = listing.GetRect(36f);
            const float gap = 8f;
            float width = (row.width - gap) / 2f;
            Rect defaults = new Rect(row.x, row.y, width, row.height);
            Rect vanilla = new Rect(row.x + width + gap, row.y, width, row.height);
            if (Widgets.ButtonText(defaults, "XPC_Cons_ResetDefaults".Translate().ToString()))
            {
                SettingsPresetService.ResetConsciousnessToModDefaults();
                NotifySettingsExternallyChanged();
            }
            if (Widgets.ButtonText(vanilla, "XPC_Cons_ResetVanilla".Translate().ToString()))
            {
                SettingsPresetService.ResetConsciousnessLearningToVanilla();
                NotifySettingsExternallyChanged();
            }
            TooltipHandler.TipRegion(defaults,
                "XPC_Cons_ResetDefaultsTip".Translate().ToString());
            TooltipHandler.TipRegion(vanilla,
                "XPC_Cons_ResetVanillaTip".Translate().ToString());
            listing.Gap(12f);
        }

        // A clear hierarchy: these three sections are larger than the
        // Low/High subsection titles. Only this Cons page gets the quiet
        // neutral background; no shared Unity/Verse GUIStyle is mutated.
        private static void DrawSubsectionToggle(
            Listing_Standard listing,
            string title,
            ref bool enabled)
        {
            Rect row = listing.GetRect(44f);
            Widgets.DrawBoxSolid(row, new Color(0.235f, 0.235f, 0.245f, 0.95f));

            GameFont previousFont = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;
            try
            {
                Text.Font = GameFont.Medium;
                GUIStyle labelStyle = new GUIStyle(Text.CurFontStyle);
                labelStyle.fontSize = 23;
                labelStyle.alignment = TextAnchor.MiddleLeft;

                const float inset = 12f;
                const float checkboxSize = 24f;
                const float gap = 8f;
                Rect inner = new Rect(row.x + inset, row.y + 3f,
                    Mathf.Max(0f, row.width - inset * 2f), row.height - 6f);
                float labelWidth = Mathf.Min(
                    Mathf.Max(0f, inner.width - checkboxSize - gap),
                    Mathf.Ceil(labelStyle.CalcSize(new GUIContent(title)).x) + 2f);
                Rect labelRect = new Rect(inner.x, inner.y, labelWidth, inner.height);
                if (Widgets.ButtonInvisible(labelRect))
                    enabled = !enabled;
                GUI.Label(labelRect, title, labelStyle);
                Widgets.Checkbox(labelRect.xMax + gap,
                    inner.y + (inner.height - checkboxSize) * 0.5f,
                    ref enabled, checkboxSize);
            }
            finally
            {
                Text.Anchor = previousAnchor;
                Text.Font = previousFont;
            }
        }

        public static void NotifySettingsExternallyChanged()
        {
            ConsciousnessAnchorV2SettingsUI.NotifySettingsExternallyChanged();
            ConsciousnessFurtherLearningSettingsUI.NotifySettingsExternallyChanged();
        }
    }
}
