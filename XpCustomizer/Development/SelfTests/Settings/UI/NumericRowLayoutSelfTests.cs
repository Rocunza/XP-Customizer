#if DEBUG
using System;
using UnityEngine;
using Verse;
using XpCustomizer.UI;

namespace XpCustomizer
{
    internal static class NumericRowLayoutSelfTests
    {
        private static int checks;
        private static void Check(bool condition, string label)
        {
            checks++;
            if (!condition)
                throw new InvalidOperationException("UL1_LAYOUT_SELFTEST:" + label);
        }

        private static void Within(float a, float b, string label)
        {
            Check(Mathf.Abs(a - b) <= 0.001f, label);
        }

        internal static void Run()
        {
            checks = 0;
            float[] rowWidths = { 0f, 24f, 80f, 140f, 250f, 450f, 800f, 1200f };
            float[] labelWidths = { 40f, 155f, 275f };
            float[] suffixWidths = { 0f, 24f };
            foreach (float width in rowWidths)
            foreach (float label in labelWidths)
            foreach (float suffix in suffixWidths)
            {
                Rect row = new Rect(30f, 40f, width, 30f);
                NumericRowLayout.Placement p = NumericRowLayout.Compute(row,
                    label, 275f, 105f, suffix, 30f, 28f, 12f, 36f);
                Check(p.Field.width >= 0f && p.Suffix.width >= 0f &&
                    p.Label.width >= 0f, "NONNEGATIVE");
                Check(p.Label.x >= row.x && p.Label.xMax <= row.xMax + 0.001f,
                    "LABEL_BOUNDS");
                Check(p.Field.x >= row.x && p.Field.xMax <= row.xMax + 0.001f,
                    "FIELD_BOUNDS");
                Check(p.Suffix.x >= row.x && p.Suffix.xMax <= row.xMax + 0.001f,
                    "SUFFIX_BOUNDS");
                Check(p.Suffix.x >= p.Field.xMax - 0.001f, "SUFFIX_AFTER_FIELD");
                Check(p.Wrapped == (width < 275f + 12f + 105f +
                    (suffix > 0f ? 6f + suffix : 0f)), "WRAP_POLICY");
                if (p.Wrapped)
                {
                    Check(p.Field.y >= p.Label.yMax - 0.001f,
                        "SECOND_LINE_NO_OVERLAP");
                    Check(p.ExtraHeight >= 30f, "HEIGHT_RESERVED");
                }
                else
                {
                    Within(p.Field.x, row.x + 275f + 12f, "GROUP_COLUMN");
                    Within(p.ExtraHeight, 0f, "SINGLE_HEIGHT");
                }
            }

            // Group peers share x, even when natural label widths differ.
            Rect wideRow = new Rect(12f, 17f, 650f, 30f);
            var shortLabel = NumericRowLayout.Compute(wideRow,
                60f, 240f, 104f, 0f, 30f, 28f, 12f, 30f);
            var longLabel = NumericRowLayout.Compute(wideRow,
                220f, 240f, 104f, 0f, 30f, 28f, 12f, 30f);
            Within(shortLabel.Field.x, longLabel.Field.x, "GROUP_STABLE_X");
            Within(shortLabel.Field.x, 264f, "GROUP_ADJACENT_X");

            var single = NumericRowLayout.Compute(wideRow,
                150f, 0f, 100f, 24f, 30f, 28f, 12f, 30f);
            Within(single.Field.x, 174f, "TITLE_RIGHT_ADJACENT");
            Check(!single.Wrapped, "TITLE_NOT_WRAPPED");

            // UL1-HF1: Level 20 Reserve uses the same 90px field, 24px
            // suffix and 30px row as Skilltrainer; only its label width varies.
            Rect reserveRow = new Rect(20f, 55f, 750f, 30f);
            var reserve = NumericRowLayout.Compute(reserveRow,
                162f, 0f, 90f, 24f, 30f, 30f, 12f, 30f);
            Check(!reserve.Wrapped, "HF1_RESERVE_SINGLE_LINE");
            Within(reserve.Field.x, reserveRow.x + 162f + 12f,
                "HF1_RESERVE_ADJACENT");
            Within(reserve.Field.width, 90f, "HF1_RESERVE_FIELD_WIDTH");
            Within(reserve.Suffix.width, 24f, "HF1_RESERVE_SUFFIX_WIDTH");
            Within(reserve.Suffix.x, reserve.Field.xMax + 6f,
                "HF1_RESERVE_SUFFIX_GAP");
            var narrowReserve = NumericRowLayout.Compute(
                new Rect(20f, 55f, 210f, 30f),
                162f, 0f, 90f, 24f, 30f, 30f, 12f, 30f);
            Check(narrowReserve.Wrapped, "HF1_RESERVE_NARROW_WRAP");
            Check(narrowReserve.Field.y >= narrowReserve.Label.yMax,
                "HF1_RESERVE_NARROW_NO_OVERLAP");
            Check(narrowReserve.Suffix.xMax <= 230.001f,
                "HF1_RESERVE_NARROW_SUFFIX_BOUNDS");
            Log.Message("[XPC-UL1][LAYOUT_SELFTEST] status=PASS; checks=" + checks +
                "; scope=PURE_GEOMETRY; liveIMGUI=NOT_RUN" +
                "; translatedWidths=NOT_EXERCISED; inputState=UNCHANGED" +
                "; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
