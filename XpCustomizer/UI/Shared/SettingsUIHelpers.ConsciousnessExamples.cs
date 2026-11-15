using UnityEngine;
using Verse;

namespace XpCustomizer.UI
{
    internal static partial class SettingsUIHelpers
    {
        /// <summary>
        /// Compact Consciousness examples, shared by learning speed,
        /// daily capacity and Further Learning. Values are pure service
        /// results; this draws only UI and does not affect gameplay.
        /// </summary>
        public static void DrawConsciousnessExampleStrip(
            Listing_Standard listing,
            float at50,
            float at80,
            float at120,
            float at170,
            string limit,
            bool percentagePoints,
            string limitTooltip)
        {
            listing.Gap(7f);
            Rect row = listing.GetRect(32f);
            bool narrow = row.width < 760f;
            float titleWidth = percentagePoints ? 147f : 118f;
            Rect titleRect = new Rect(row.x, row.y, titleWidth, row.height);
            DrawExampleTitle(titleRect, percentagePoints);
            TooltipHandler.TipRegion(titleRect, percentagePoints
                ? "Consciousness adjustment in percentage points (pp), not final Further Learning efficiency."
                : "Consciousness multiplier for this feature at each example level.");

            float x = row.x + titleWidth;
            float available = Mathf.Max(0f, row.xMax - x);
            int firstCount = narrow ? 4 : 5;
            float cellWidth = available / firstCount;
            float neutral = percentagePoints ? 0f : 1f;

            DrawExampleCell(new Rect(x, row.y, cellWidth, row.height),
                "50%", FormatExample(at50, percentagePoints), Color.white);
            DrawExampleCell(new Rect(x + cellWidth, row.y, cellWidth, row.height),
                "80%", FormatExample(at80, percentagePoints), at80 < neutral - 0.0001f
                    ? new Color(0.87f, 0.64f, 0.44f) : Color.white);
            DrawExampleCell(new Rect(x + cellWidth * 2f, row.y, cellWidth, row.height),
                "120%", FormatExample(at120, percentagePoints), at120 > neutral + 0.0001f
                    ? new Color(0.62f, 0.80f, 1.00f) : Color.white);
            DrawExampleCell(new Rect(x + cellWidth * 3f, row.y, cellWidth, row.height),
                "170%", FormatExample(at170, percentagePoints), at170 > neutral + 0.0001f
                    ? new Color(0.96f, 0.84f, 0.60f) : Color.white);

            Rect limitCell;
            if (narrow)
            {
                Rect lastRow = listing.GetRect(30f);
                limitCell = new Rect(lastRow.x + titleWidth, lastRow.y,
                    Mathf.Max(0f, lastRow.width - titleWidth), lastRow.height);
            }
            else
            {
                limitCell = new Rect(x + cellWidth * 4f, row.y,
                    cellWidth, row.height);
            }
            DrawExampleCell(limitCell, "Limit", limit, Color.white);
            TooltipHandler.TipRegion(limitCell, limitTooltip);
        }

        private static void DrawExampleTitle(Rect rect, bool percentagePoints)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            try
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = new Color(0.78f, 0.78f, 0.78f);
                Widgets.Label(rect, percentagePoints
                    ? "Cons. examples (pp):" : "Cons. examples:");
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;
            }
        }

        private static void DrawExampleCell(Rect rect, string anchor,
            string value, Color valueColor)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            try
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                const float gap = 4f;
                float anchorWidth = anchor == "Limit" ? 33f : 30f;
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.76f, 0.76f, 0.76f);
                Widgets.Label(new Rect(rect.x, rect.y,
                    Mathf.Min(anchorWidth, rect.width), rect.height), anchor);
                Text.Font = GameFont.Small;
                GUI.color = valueColor;
                Widgets.Label(new Rect(rect.x + anchorWidth + gap, rect.y,
                    Mathf.Max(0f, rect.width - anchorWidth - gap), rect.height), value);
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;
            }
        }

        private static string FormatExample(float value, bool percentagePoints)
        {
            return percentagePoints
                ? (value > 0f ? "+" : "") + value.ToString("0.##")
                : "x" + value.ToString("0.00");
        }

    }
}
