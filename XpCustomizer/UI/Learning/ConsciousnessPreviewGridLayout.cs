using UnityEngine;

namespace XpCustomizer.UI
{
    // CP1: shared geometry and fixed comparison levels ONLY. This class does
    // not read game settings, change an anchor, or access a gameplay service.
    internal static class ConsciousnessPreviewGridLayout
    {
        internal const float MinCellWidth = 140f;
        internal const float RowGap = 5f;
        internal const float SimpleRowHeight = 30f;
        internal const float DetailedRowHeight = 55f;

        private static readonly float[] HighLevels =
        {
            1.05f, 1.10f, 1.15f, 1.20f, 1.30f, 1.35f,
            1.50f, 1.70f, 2.50f, 5.00f, 7.50f, 10.00f
        };

        internal static int HighSampleCount { get { return HighLevels.Length; } }
        internal static float HighSampleAt(int index) { return HighLevels[index]; }

        // Preserve the legacy Low 1x4 row; High uses the same width policy as GP1.
        internal static int ResolveColumns(float width, bool high)
        {
            if (!high) return 4;
            if (float.IsNaN(width) || float.IsInfinity(width) || width <= 0f)
                return 2;
            if (width >= 6f * MinCellWidth) return 6;
            if (width >= 4f * MinCellWidth) return 4;
            if (width >= 3f * MinCellWidth) return 3;
            return 2;
        }

        internal static float GridHeight(int count, int columns, float rowHeight)
        {
            if (count <= 0 || columns <= 0 || rowHeight <= 0f) return 0f;
            int rows = (count + columns - 1) / columns;
            return rows * rowHeight + (rows - 1) * RowGap;
        }

        internal static Rect CellRect(Rect grid, int index, int columns, float rowHeight)
        {
            float width = grid.width / columns;
            return new Rect(grid.x + (index % columns) * width,
                grid.y + (index / columns) * (rowHeight + RowGap),
                width, rowHeight);
        }
    }
}
