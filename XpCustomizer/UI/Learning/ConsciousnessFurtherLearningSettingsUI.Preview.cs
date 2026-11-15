using System;
using System.Globalization;
using UnityEngine;
using Verse;
using XpCustomizer.FurtherLearning;
using XpCustomizer.ConsciousnessLearning;

namespace XpCustomizer.UI
{
    internal static partial class ConsciousnessFurtherLearningSettingsUI
    {
        // Preview-only state. Never serialized and never assigned to
        // settings.furtherLearningBaseFactor. The one selector is shared
        // between both Low and High sample groups.
        private const string CustomPreviewControl = "ZCN.XpCustomizer.FurtherV2.PreviewBase";
        private static bool useCustomPreview;
        private static float customPreviewPercent = 20f;
        private static string customPreviewBuffer;
        private static bool customPreviewWasFocused;
        private static bool customPreviewInvalid;
        private static readonly Color lowColor = new Color(0.87f, 0.64f, 0.44f);
        private static readonly Color highColor = new Color(0.62f, 0.80f, 1f);
        // This is a read-only comparison against the configured Learning tab
        // base or a transient example. The actual base setting is never written.
        internal static float GetCurrentPreviewBasePercent(XpCustomizerSettings settings)
        {
            float factor = settings == null ? FurtherLearningService.VanillaFactor :
                settings.furtherLearningBaseFactor;
            if (float.IsNaN(factor) || float.IsInfinity(factor))
                factor = FurtherLearningService.VanillaFactor;
            return FurtherLearningService.NormalizeBaseFactor(factor) * 100f;
        }

        internal static bool TryParsePreviewPercent(string raw, out float percent)
        {
            return ConsciousnessAnchorV2SettingsUI.TryParseNumericValue(raw, out percent) &&
                percent >= 0f && percent <= 100f;
        }

        internal static float ResolvePreviewPercentCommit(string raw, float previous)
        {
            return ConsciousnessAnchorV2SettingsUI.ResolveNumericCommit(
                raw, previous, 0f, 100f);
        }

        // Reuses the existing gameplay arithmetic with Mental adjustment 0pp.
        // Return value is in percentage points for DISPLAY, not a game factor.
        internal static float CalculatePreviewEfficiencyPercent(float basePercent, float multiplier)
        {
            return FurtherLearningService.CalculateRawFactor(
                Mathf.Clamp(basePercent, 0f, 100f) / 100f, multiplier, 0f) * 100f;
        }

        private static void DrawPreviewBaseSelector(Listing_Standard listing, XpCustomizerSettings settings)
        {
            float current = GetCurrentPreviewBasePercent(settings);
            Rect buttons = listing.GetRect(32f);
            const float gap = 8f;
            float width = (buttons.width - gap) / 2f;
            if (Widgets.ButtonText(new Rect(buttons.x, buttons.y, width, buttons.height),
                (useCustomPreview ? "" : "✓ ") + string.Format(
                    "XPC_Cons_FurtherCurrentButton".Translate().ToString(),
                    current.ToString("0.##", CultureInfo.InvariantCulture))))
            {
                useCustomPreview = false;
                GUI.FocusControl(null);
            }
            if (Widgets.ButtonText(new Rect(buttons.x + width + gap, buttons.y, width, buttons.height),
                (useCustomPreview ? "✓ " : "") + "XPC_Cons_FurtherCustomButton".Translate().ToString()))
            {
                if (!useCustomPreview)
                {
                    // Seed from the actual setting on entry; thereafter edits
                    // only affect this temporary preview buffer.
                    customPreviewPercent = current;
                    customPreviewBuffer = current.ToString("0.####", CultureInfo.InvariantCulture);
                    customPreviewWasFocused = false;
                    customPreviewInvalid = false;
                }
                useCustomPreview = true;
            }

            Rect description = listing.GetRect(30f);
            if (!useCustomPreview)
            {
                Widgets.Label(description, string.Format("XPC_Cons_FurtherCurrentDetail".Translate().ToString(),
                    current.ToString("0.##", CultureInfo.InvariantCulture)));
            }
            else
            {
                // Allocate this independent preview row adjacent to its label.
                // Reuse the already-allocated description Rect; a wrap needs
                // one extra Listing row so no subsequent input overlaps.
                string previewLabel = "XPC_Cons_FurtherExampleLabel".Translate().ToString();
                float labelWidth = Text.CalcSize(previewLabel).x + 4f;
                float wrappedLabelHeight = Text.CalcHeight(previewLabel,
                    Mathf.Max(1f, description.width));
                NumericRowLayout.Placement geometry = NumericRowLayout.Compute(
                    description, labelWidth, 0f, 100f, 20f, 30f, 27f,
                    12f, wrappedLabelHeight);
                if (geometry.Wrapped) listing.Gap(geometry.ExtraHeight);
                Widgets.Label(geometry.Label, previewLabel);
                DrawPreviewBaseInput(geometry.Field);
                Widgets.Label(geometry.Suffix, "%");
            }
            SettingsUIHelpers.DrawReferenceText(listing,
                "XPC_Cons_FurtherPreviewHelp".Translate().ToString());
        }

        private static void DrawPreviewBaseInput(Rect rect)
        {
            if (!customPreviewWasFocused && customPreviewBuffer == null)
                customPreviewBuffer = customPreviewPercent.ToString("0.####", CultureInfo.InvariantCulture);
            Event evt = Event.current;
            if (customPreviewWasFocused && evt != null && evt.type == EventType.KeyDown &&
                (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter))
            {
                GUI.FocusControl(null);
                evt.Use();
            }
            GUI.SetNextControlName(CustomPreviewControl);
            customPreviewBuffer = Widgets.TextField(rect, customPreviewBuffer ?? string.Empty);
            bool focused = string.Equals(GUI.GetNameOfFocusedControl(),
                CustomPreviewControl, StringComparison.Ordinal);
            float parsed;
            if (focused)
            {
                customPreviewInvalid = !TryParsePreviewPercent(customPreviewBuffer, out parsed);
                if (!customPreviewInvalid)
                    customPreviewPercent = parsed;
            }
            else if (customPreviewWasFocused)
            {
                customPreviewPercent = ResolvePreviewPercentCommit(
                    customPreviewBuffer, customPreviewPercent);
                customPreviewBuffer = customPreviewPercent.ToString("0.####", CultureInfo.InvariantCulture);
                customPreviewInvalid = false;
            }
            customPreviewWasFocused = focused;
            ProfileNumericFocus.ObserveExisting(
                "ConsciousnessFurtherLearningSettingsUI.customPreviewBuffer",
                customPreviewBuffer, focused);
        }

        private static float PreviewBasePercent(XpCustomizerSettings settings)
        {
            if (!useCustomPreview) return GetCurrentPreviewBasePercent(settings);
            return customPreviewInvalid ? float.NaN : customPreviewPercent;
        }

        // Calling this after a modal settings save returns the next opening to
        // Current setting, even if the prior window was closed on the Cons tab.
        internal static void ResetPreviewToCurrent()
        {
            useCustomPreview = false;
            customPreviewPercent = 20f;
            customPreviewBuffer = null;
            customPreviewWasFocused = false;
            customPreviewInvalid = false;
        }

        private static void DrawPreview(Listing_Standard listing, XpCustomizerSettings settings,
            ConsciousnessFurtherV2Settings p, bool high)
        {
            SettingsUIHelpers.DrawReferenceText(listing,
                high ? "XPC_Cons_FurtherHighPreview".Translate().ToString() :
                       "XPC_Cons_FurtherLowPreview".Translate().ToString());
            // High uses the SAME fixed samples as Speed and Capacity. Low still
            // includes the current user-selected anchor, exactly as before CP1.
            float[] lowSamples = high ? null : new[] { 0.30f, 0.50f, p.LowAnchor, 1.00f };
            int count = high ? ConsciousnessPreviewGridLayout.HighSampleCount : lowSamples.Length;
            int columns = ConsciousnessPreviewGridLayout.ResolveColumns(listing.ColumnWidth, high);
            float basePercent = PreviewBasePercent(settings);
            Rect grid = listing.GetRect(ConsciousnessPreviewGridLayout.GridHeight(
                count, columns, ConsciousnessPreviewGridLayout.DetailedRowHeight));
            Color original = GUI.color;
            GameFont font = Text.Font;
            TextAnchor anchor = Text.Anchor;
            bool wordWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                for (int i = 0; i < count; i++)
                {
                    float level = high
                        ? ConsciousnessPreviewGridLayout.HighSampleAt(i) : lowSamples[i];
                    float factor = ConsciousnessFurtherLearningService.CalculateMultiplier(level, settings);
                    Rect cell = ConsciousnessPreviewGridLayout.CellRect(grid, i, columns,
                        ConsciousnessPreviewGridLayout.DetailedRowHeight);
                    float labelWidth = Mathf.Min(64f, cell.width * 0.42f);
                    GUI.color = original;
                    Widgets.Label(new Rect(cell.x, cell.y, labelWidth, 25f),
                        Mathf.RoundToInt(level * 100f) + "%");
                    GUI.color = high ? highColor : lowColor;
                    Widgets.Label(new Rect(cell.x + labelWidth, cell.y,
                        Mathf.Max(0f, cell.width - labelWidth), 25f),
                        "x" + factor.ToString("0.###", CultureInfo.InvariantCulture));
                    GUI.color = original;
                    string result = float.IsNaN(basePercent)
                        ? "XPC_Cons_FurtherPostCapUnavailable".Translate().ToString()
                        : string.Format("XPC_Cons_FurtherPostCap".Translate().ToString(),
                            CalculatePreviewEfficiencyPercent(basePercent, factor)
                            .ToString("0.##", CultureInfo.InvariantCulture));
                    Widgets.Label(new Rect(cell.x, cell.y + 26f, cell.width, 26f), result);
                }
            }
            finally
            {
                GUI.color = original;
                Text.Font = font;
                Text.Anchor = anchor;
                Text.WordWrap = wordWrap;
            }

            if (high)
            {
                // Before CP1, the selected High anchor was included among four
                // samples. Preserve that information as an explicit readout.
                float selectedFactor = ConsciousnessFurtherLearningService.CalculateMultiplier(
                    p.HighAnchor, settings);
                string selectedPostCap = float.IsNaN(basePercent)
                    ? "XPC_Cons_FurtherPostCapUnavailable".Translate().ToString()
                    : string.Format("XPC_Cons_FurtherPostCap".Translate().ToString(),
                        CalculatePreviewEfficiencyPercent(basePercent, selectedFactor)
                        .ToString("0.##", CultureInfo.InvariantCulture));
                SettingsUIHelpers.DrawReferenceText(listing,
                    string.Format("XPC_Cons_FurtherSelectedAnchor".Translate().ToString(),
                        (p.HighAnchor * 100f).ToString("0.#", CultureInfo.InvariantCulture),
                        selectedFactor.ToString("0.###", CultureInfo.InvariantCulture),
                        selectedPostCap));
            }
        }
    }
}
