using UnityEngine;
using Verse;

namespace XpCustomizer.UI
{
    internal static partial class SettingsUIHelpers
    {
        /// <summary>
        /// Compact, XP Customizer-owned checkbox immediately to the RIGHT of
        /// its label. Cons/GWS and the U1 settings tabs share this presentation.
        /// The label and checkbox remain separate click targets; no global
        /// vanilla GUI style or other mod is modified.
        /// </summary>
        internal static void DrawRightAdjacentToggle(
            Listing_Standard listing, string label, ref bool enabled)
        {
            Rect row = listing.GetRect(30f);
            float measuredLabelWidth = Text.CalcSize(label).x;
            Rect labelRect;
            Rect checkboxRect;
            ComputeRightAdjacentToggleRects(row, measuredLabelWidth,
                out labelRect, out checkboxRect);

            // A label click toggles exactly once; the vanilla checkbox owns
            // its own disjoint hitbox. Both respect the existing GUI.enabled.
            if (labelRect.width > 0f && Widgets.ButtonInvisible(labelRect))
                enabled = !enabled;

            TextAnchor originalAnchor = Text.Anchor;
            try
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, label);
            }
            finally
            {
                Text.Anchor = originalAnchor;
            }

            // When a longer translation cannot fit, expose the full wording
            // on hover without expanding the row or overlapping the checkbox.
            if (labelRect.width > 0f &&
                Mathf.Ceil(measuredLabelWidth) + 2f > labelRect.width)
                TooltipHandler.TipRegion(labelRect, label);

            if (checkboxRect.width > 0f)
                Widgets.Checkbox(checkboxRect.x, checkboxRect.y,
                    ref enabled, checkboxRect.width);
        }

        /// <summary>
        /// Pure layout for the XP-owned adjacent toggle, testable without an
        /// IMGUI event. At normal widths this preserves the original 24px box
        /// and 8px label gap. At very narrow widths it favors a visible,
        /// in-bounds checkbox over a truncated label.
        /// </summary>
        internal static void ComputeRightAdjacentToggleRects(
            Rect row, float measuredLabelWidth,
            out Rect labelRect, out Rect checkboxRect)
        {
            const float checkboxSize = 24f;
            const float labelGap = 8f;

            float available = Mathf.Max(0f, row.width);
            float drawnSize = Mathf.Min(checkboxSize, available);
            float gap = Mathf.Min(labelGap,
                Mathf.Max(0f, available - drawnSize));
            float labelBudget = Mathf.Max(0f, available - drawnSize - gap);
            float labelWidth = Mathf.Min(labelBudget,
                Mathf.Max(0f, Mathf.Ceil(measuredLabelWidth) + 2f));

            labelRect = new Rect(row.x, row.y, labelWidth, row.height);
            checkboxRect = new Rect(labelRect.xMax + gap,
                row.y + (row.height - drawnSize) * 0.5f,
                drawnSize, drawnSize);
        }

    }
}
