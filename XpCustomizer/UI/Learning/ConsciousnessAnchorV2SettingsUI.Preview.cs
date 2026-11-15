using System;
using System.Globalization;
using UnityEngine;
using Verse;
using XpCustomizer.ConsciousnessLearning;

namespace XpCustomizer.UI
{
    /// <summary>
    /// Single-source Anchor V2 editor for independent Speed and Capacity.
    /// GWS-style mode choices are placed directly below each Low/High toggle.
    /// No additional Harmony patches are introduced.
    /// </summary>
    internal static partial class ConsciousnessAnchorV2SettingsUI
    {
        private static readonly float[] LowSampleLevels = { 0.3f, 0.5f, 0.65f, 0.8f };
        // Match the GWS Low/High preview palette exactly (UI-only).
        private static readonly Color LowExampleColor = new Color(0.87f, 0.64f, 0.44f);
        private static readonly Color HighExampleColor = new Color(0.62f, 0.80f, 1.00f);

        internal static float GetSelectedPreviewFactor(float consciousness,
            XpCustomizerSettings settings, bool speed)
        {
            return speed
                ? ConsciousnessCurveDispatcher.CalculateSpeed(consciousness, settings)
                : ConsciousnessCurveDispatcher.CalculateCapacity(consciousness, settings);
        }

        private static void DrawLowPreview(Listing_Standard listing,
            XpCustomizerSettings settings, bool speed)
        {
            SettingsUIHelpers.DrawReferenceText(listing,
                speed ? "XPC_Cons_SpeedLowPreview".Translate().ToString() :
                    "XPC_Cons_CapacityLowPreview".Translate().ToString());
            DrawSamples(listing, settings, speed, false, LowExampleColor);
        }

        private static void DrawHighPreview(Listing_Standard listing,
            XpCustomizerSettings settings, bool speed)
        {
            SettingsUIHelpers.DrawReferenceText(listing,
                speed ? "XPC_Cons_SpeedHighPreview".Translate().ToString() :
                    "XPC_Cons_CapacityHighPreview".Translate().ToString());
            DrawSamples(listing, settings, speed, true, HighExampleColor);
        }

        private static void DrawSamples(Listing_Standard listing,
            XpCustomizerSettings settings, bool speed, bool high, Color accent)
        {
            int count = high ? ConsciousnessPreviewGridLayout.HighSampleCount : LowSampleLevels.Length;
            int columns = ConsciousnessPreviewGridLayout.ResolveColumns(listing.ColumnWidth, high);
            Rect grid = listing.GetRect(ConsciousnessPreviewGridLayout.GridHeight(
                count, columns, ConsciousnessPreviewGridLayout.SimpleRowHeight));
            Color previousColor = GUI.color;
            GameFont previousFont = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;
            bool previousWordWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                for (int i = 0; i < count; i++)
                {
                    float level = high
                        ? ConsciousnessPreviewGridLayout.HighSampleAt(i) : LowSampleLevels[i];
                    float factor = GetSelectedPreviewFactor(level, settings, speed);
                    Rect cell = ConsciousnessPreviewGridLayout.CellRect(grid, i, columns,
                        ConsciousnessPreviewGridLayout.SimpleRowHeight);
                    float labelWidth = Mathf.Min(64f, cell.width * 0.42f);
                    GUI.color = previousColor;
                    Widgets.Label(new Rect(cell.x, cell.y, labelWidth, cell.height),
                        Mathf.RoundToInt(level * 100f) + "%");
                    GUI.color = accent;
                    Widgets.Label(new Rect(cell.x + labelWidth, cell.y,
                        Mathf.Max(0f, cell.width - labelWidth), cell.height),
                        "x" + factor.ToString("0.###", CultureInfo.InvariantCulture));
                }
            }
            finally
            {
                GUI.color = previousColor;
                Text.Font = previousFont;
                Text.Anchor = previousAnchor;
                Text.WordWrap = previousWordWrap;
            }
        }
    }
}
