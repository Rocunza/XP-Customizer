#if DEBUG
using System;
using UnityEngine;
using Verse;
using XpCustomizer.UI;

namespace XpCustomizer
{
    internal static class ToggleAlignmentU1SelfTests
    {
        internal static void Run()
        {
            int checks = 0;
            Action<bool, string> check = delegate(bool condition, string caseName)
            {
                checks++;
                if (!condition)
                    throw new InvalidOperationException("U1 toggle layout: " + caseName);
            };

            float[] widths = { 1200f, 760f, 480f, 240f, 96f,
                64f, 40f, 32f, 28f, 24f, 16f, 0f };
            foreach (float width in widths)
            {
                Rect row = new Rect(13f, 7f, width, 30f);
                Rect label;
                Rect checkbox;
                SettingsUIHelpers.ComputeRightAdjacentToggleRects(
                    row, 600f, out label, out checkbox);

                check(label.width >= 0f && checkbox.width >= 0f,
                    "nonnegative geometry " + width);
                check(label.x == row.x && checkbox.x >= label.xMax,
                    "disjoint click targets " + width);
                check(checkbox.xMax <= row.xMax + 0.001f,
                    "checkbox stays inside listing " + width);
                check(Math.Abs(checkbox.center.y - row.center.y) < 0.001f,
                    "checkbox vertically centered " + width);
                if (width >= 32f)
                    check(Math.Abs(checkbox.width - 24f) < 0.001f &&
                          Math.Abs(checkbox.x - label.xMax - 8f) < 0.001f,
                        "preserve vanilla checkbox size and gap " + width);
            }

            Rect roomy = new Rect(10f, 20f, 500f, 30f);
            Rect shortLabel;
            Rect shortCheckbox;
            SettingsUIHelpers.ComputeRightAdjacentToggleRects(
                roomy, 140f, out shortLabel, out shortCheckbox);
            check(shortLabel.width == 142f, "short label adjacent measured width");
            check(shortCheckbox.x == 160f, "short label checkbox position");
            Rect clippedLabel;
            Rect clippedCheckbox;
            SettingsUIHelpers.ComputeRightAdjacentToggleRects(
                roomy, 1000f, out clippedLabel, out clippedCheckbox);
            check(clippedLabel.width == 468f, "long label clipped to available room");
            check(clippedCheckbox.xMax == roomy.xMax, "long label box stays in row");

            Log.Message("[XPC-U1][TOGGLE_ALIGNMENT_SELFTEST] status=PASS; checks=" +
                checks + "; scope=PURE_GEOMETRY_NO_IMGUI; areas=9; " +
                "labelCheckboxOverlap=NONE; widthBounds=PASS; " +
                "gameClickTest=MANUAL; settingsWrites=0; profileJsonWrites=0; " +
                "configXmlWrites=0; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
