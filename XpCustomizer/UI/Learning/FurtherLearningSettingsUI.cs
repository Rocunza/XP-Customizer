using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.FurtherLearning;

namespace XpCustomizer.UI
{
    /// <summary>
    /// Further Learning UI. Mental Break Threshold is the only source model
    /// on this tab; Consciousness is managed separately from the Cons tab.
    /// </summary>
    internal static class FurtherLearningSettingsUI
    {
        private static readonly NumericInputState baseFactorInput = new NumericInputState();
        private static bool uiRevisionLogged;

        // Vanilla single-trait MentalBreakThreshold examples. These are ONLY
        // preview stat values, not custom XP bonuses per trait. Game calculations
        // always read the pawn's actual stat (including modded contributors).
        private static readonly string[] previewTraits = {
            "XPC_Learning_TraitIronWilled", "XPC_Learning_TraitSteadfast", "XPC_Learning_TraitNone", "XPC_Learning_TraitNervous", "XPC_Learning_TraitTooSmart", "XPC_Learning_TraitVolatile"
        };
        private static readonly float[] previewThresholds = {
            0.17f, 0.26f, 0.35f, 0.43f, 0.47f, 0.50f
        };

        public static void Draw(Listing_Standard listing)
        {
            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            if (settings == null) return;

            if (!uiRevisionLogged)
            {
                uiRevisionLogged = true;
                Log.Message("[XP Customizer][Further Learning UI] UI_REVISION=FLR1-R1-20261115; resetLabel=VANILLA_UNCHANGED; resetPolicy=MOD_DEFAULTS_60; gameplay=unchanged; build=" + XpCustomizerMod.BuildId);
            }

            SettingsUIHelpers.DrawSectionTitle(listing, "XPC_Learning_FurtherTitle".Translate().ToString());
            SettingsUIHelpers.DrawDescription(listing,
                "XPC_Learning_FurtherDescription".Translate().ToString());
            listing.Gap(4f);

            // The base post-cap efficiency is the primary Further Learning control.
            DrawBaseFactor(listing);
            listing.Gap(12f);
            listing.GapLine();
            listing.Gap(12f);

            // Secondary adjustment: visually separate from the base post-cap
            // efficiency above. This remains UI-only; gameplay is unchanged.
            SettingsUIHelpers.DrawSectionTitle(listing, "XPC_Learning_ThresholdTitle".Translate().ToString());
            SettingsUIHelpers.DrawDescription(listing,
                "XPC_Learning_ThresholdDescription".Translate().ToString());
            listing.Gap(8f);

            bool enabled = settings.furtherLearningMentalThresholdEnabled;
            SettingsUIHelpers.DrawRightAdjacentToggle(listing, "XPC_Learning_ThresholdEnabled".Translate().ToString(), ref enabled);
            settings.furtherLearningMentalThresholdEnabled = enabled;
            listing.Gap(8f);

            DrawThresholdStrength(listing, settings);
            listing.Gap(14f);

            if (listing.ButtonText("XPC_Learning_FurtherReset".Translate().ToString()))
            {
                // Keep the familiar Vanilla button label, but restore the
                // install-default 60% strength for this one UI action only.
                SettingsPresetService.ResetFurtherLearningToModDefaults();
                NotifySettingsExternallyChanged();
                Log.Message("[XPC-FLR1][SECTION_RESET] source=LEARNING_TAB; label=VANILLA_UNCHANGED" +
                    "; policy=MOD_DEFAULTS; strength=" +
                    settings.furtherLearningMentalThresholdStrength.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) +
                    "; enabled=" + settings.furtherLearningMentalThresholdEnabled +
                    "; build=" + XpCustomizerMod.BuildId);
            }
            listing.Gap(20f);
        }

        private static void DrawThresholdStrength(Listing_Standard listing,
            XpCustomizerSettings settings)
        {
            float strength = FurtherLearningService.NormalizeThresholdStrength(
                settings.furtherLearningMentalThresholdStrength);
            listing.Label(string.Format("XPC_Learning_ThresholdStrength".Translate().ToString(),
                Mathf.RoundToInt(strength * 100f)));
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && settings.furtherLearningMentalThresholdEnabled;
            strength = listing.Slider(strength,
                XpCustomizerDefaults.FurtherLearningMentalStrengthMin,
                XpCustomizerDefaults.FurtherLearningMentalStrengthMax);
            GUI.enabled = previousEnabled;

            // Symmetric strength slider: 100% is the mathematical reference; mod default is 60%.
            strength = Mathf.Round(strength * 10f) / 10f;
            settings.furtherLearningMentalThresholdStrength = strength;
            VanillaReferenceAccentUI.Draw(listing,
                "XPC_L3F3_ThresholdOtherPart", "XPC_L3F3_ThresholdNeutralPart",
                true, "XPC_Learning_ThresholdRange");

            float effectiveStrength = settings.furtherLearningMentalThresholdEnabled
                ? strength : 0f;
            float basePercent = FurtherLearningService.NormalizeBaseFactor(
                settings.furtherLearningBaseFactor) * 100f;
            // The six single-trait examples below replace the redundant
            // 17/35/50 anchor strip. They still use the gameplay formula.
            DrawLiveTraitExamples(listing, basePercent, effectiveStrength);
        }

        /// <summary>
        /// UI-only comparison. Uses the SAME formula as gameplay; it does not
        /// inspect a pawn and it does not grant any trait-specific XP bonuses.
        /// Reacts during the current OnGUI pass to switch, strength and base.
        /// </summary>
        private static void DrawLiveTraitExamples(Listing_Standard listing,
            float basePercent, float effectiveStrength)
        {
            listing.Gap(9f);
            listing.Label("XPC_Learning_TraitExamples".Translate().ToString());
            SettingsUIHelpers.DrawReferenceText(listing,
                "XPC_Learning_TraitExamplesDescription".Translate().ToString());

            GameFont previousFont = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;
            Color previousColor = GUI.color;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = new Color(0.80f, 0.80f, 0.80f, 1f);

                // Reserve only the rows actually displayed and adapt to the
                // settings-window width; avoid clipping on smaller resolutions.
                Rect row = listing.GetRect(27f);
                int columns = row.width >= 1000f ? 3 : (row.width >= 530f ? 2 : 1);
                float cellWidth = row.width / columns;

                for (int i = 0; i < previewTraits.Length; i++)
                {
                    if (i > 0 && i % columns == 0)
                        row = listing.GetRect(27f);

                    float percent = Mathf.Max(0f, basePercent +
                        FurtherLearningService.CalculateThresholdAdjustment(
                            previewThresholds[i], effectiveStrength));
                    float x = row.x + cellWidth * (i % columns);
                    Rect cell = new Rect(x, row.y,
                        Mathf.Max(0f, cellWidth - 8f), row.height);
                    Widgets.Label(cell,
                        string.Format("XPC_Learning_TraitPreview".Translate().ToString(),
                            previewTraits[i].Translate().ToString(),
                            Mathf.RoundToInt(previewThresholds[i] * 100f),
                            percent.ToString("0.#")));
                }
            }
            finally
            {
                Text.Font = previousFont;
                Text.Anchor = previousAnchor;
                GUI.color = previousColor;
            }
        }

        // ================================================================
        // Base factor
        // ================================================================

        private static void DrawBaseFactor(
            Listing_Standard listing)
        {
            XpCustomizerSettings settings =
                XpCustomizerMod.Settings;

            // Settings and gameplay store a 0..1 factor, while the player
            // edits a 0..100 percentage. Keep the conversion in UI only.
            float percent =
                Mathf.Clamp(
                    settings.furtherLearningBaseFactor,
                    XpCustomizerDefaults.FurtherLearningBaseMin,
                    XpCustomizerDefaults.FurtherLearningBaseMax) * 100f;

            // Presets / reset / other UI actions must not be overwritten by
            // a stale text buffer left from a prior editing session.
            if (!float.IsNaN(baseFactorInput.lastValue) &&
                !Mathf.Approximately(percent, baseFactorInput.lastValue))
            {
                baseFactorInput.buffer = null;
            }

            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Text.Font = GameFont.Medium;
            string label = "XPC_Learning_BaseFactorTitle".Translate().ToString();
            NumericRowLayout.Placement geometry = NumericRowLayout.Allocate(
                listing, label, 100f, 24f, 40f, 30f);
            Rect fieldRect = geometry.Field;
            Rect suffixRect = geometry.Suffix;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(geometry.Label, label);

            Text.Font = oldFont;

            ProfileNumericFocus.Draw(
                fieldRect,
                ref percent,
                baseFactorInput,
                XpCustomizerDefaults.FurtherLearningBaseMin * 100f,
                XpCustomizerDefaults.FurtherLearningBaseMax * 100f);

            Widgets.Label(suffixRect, "%");
            Text.Anchor = oldAnchor;

            percent = Mathf.Clamp(
                percent,
                XpCustomizerDefaults.FurtherLearningBaseMin * 100f,
                XpCustomizerDefaults.FurtherLearningBaseMax * 100f);

            settings.furtherLearningBaseFactor =
                percent / 100f;

            baseFactorInput.lastValue = percent;

            VanillaReferenceAccentUI.Draw(listing,
                "XPC_L3F3_BaseRangePart", "XPC_L3F3_BaseVanillaPart",
                false, "XPC_Learning_BaseFactorRange");

            SettingsUIHelpers.DrawReferenceText(
                listing,
                "XPC_Learning_BaseFactorHelp".Translate().ToString());
        }


        public static void NotifySettingsExternallyChanged()
        {
            baseFactorInput.Reset();
        }
    }
}
