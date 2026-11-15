using UnityEngine;
using Verse;

namespace XpCustomizer.UI
{
    /// <summary>
    /// UL1: layout geometry only. No settings mutations, focus changes,
    /// serialization, gameplay logic or custom numeric editors.
    /// </summary>
    internal static class NumericRowLayout
    {
        internal const string Revision = "UI-LAYOUT-UL1-R1-20261113";
        private static bool installedLogged;
        private static bool fallbackLogged;

        internal struct Placement
        {
            internal Rect Label;
            internal Rect Field;
            internal Rect Suffix;
            internal bool Wrapped;
            internal float ExtraHeight;
        }

        /// <summary>
        /// Measure *all* possible member labels, including labels hidden by a
        /// curve-mode toggle, so fields do not jump when modes are switched.
        /// Must be called using the font that is used to draw the labels.
        /// </summary>
        internal static float MaxTranslatedWidth(params string[] translationKeys)
        {
            float width = 0f;
            if (translationKeys == null) return width;
            for (int i = 0; i < translationKeys.Length; i++)
            {
                if (string.IsNullOrEmpty(translationKeys[i])) continue;
                width = Mathf.Max(width,
                    Text.CalcSize(translationKeys[i].Translate().ToString()).x + 4f);
            }
            return width;
        }

        /// <summary>Pure arithmetic; safe for detached Debug self-tests.</summary>
        internal static Placement Compute(Rect row, float naturalLabelWidth,
            float sharedLabelWidth, float fieldWidth, float suffixWidth,
            float lineHeight, float fieldHeight, float gap, float wrappedLabelHeight)
        {
            Placement result = new Placement();
            float available = Mathf.Max(0f, row.width);
            float labelWidth = Mathf.Max(0f, Mathf.Max(naturalLabelWidth, sharedLabelWidth));
            float field = Mathf.Max(0f, fieldWidth);
            float suffix = Mathf.Max(0f, suffixWidth);
            float space = Mathf.Max(0f, gap);
            float fieldLine = Mathf.Max(0f, lineHeight);
            float editHeight = Mathf.Min(fieldLine, Mathf.Max(0f, fieldHeight));
            float suffixGap = suffix > 0f ? 6f : 0f;
            result.Wrapped = labelWidth + space + field + suffixGap + suffix > available;
            if (!result.Wrapped)
            {
                result.Label = new Rect(row.x, row.y, labelWidth, fieldLine);
                result.Field = new Rect(row.x + labelWidth + space,
                    row.y + (fieldLine - editHeight) / 2f, field, editHeight);
                result.Suffix = new Rect(result.Field.xMax + suffixGap,
                    row.y + (fieldLine - editHeight) / 2f, suffix, editHeight);
                result.ExtraHeight = 0f;
                return result;
            }

            // The field is placed on a second line instead of overlapping a
            // long localized label. All rectangles stay within the listing row.
            float labelHeight = Mathf.Max(fieldLine, Mathf.Max(0f, wrappedLabelHeight));
            float suffixActual = Mathf.Min(suffix, available);
            float suffixSpace = suffixActual > 0f ? Mathf.Min(suffixGap,
                Mathf.Max(0f, available - suffixActual)) : 0f;
            float fieldActual = Mathf.Min(field,
                Mathf.Max(0f, available - suffixActual - suffixSpace));
            // If even the suffix cannot fit, drop its gap rather than overflow.
            result.Label = new Rect(row.x, row.y, available, labelHeight);
            result.Field = new Rect(row.x, row.y + labelHeight +
                (fieldLine - editHeight) / 2f, fieldActual, editHeight);
            result.Suffix = new Rect(result.Field.xMax + suffixSpace,
                result.Field.y, suffixActual, editHeight);
            result.ExtraHeight = labelHeight;
            return result;
        }

        /// <summary>
        /// Reserve one normal Listing row, plus a second only for narrow windows.
        /// Draw the returned Rects using the existing input/label code.
        /// </summary>
        internal static Placement Allocate(Listing_Standard listing, string label,
            float fieldWidth, float suffixWidth, float lineHeight,
            float fieldHeight, float sharedLabelWidth = 0f, float gap = 12f)
        {
            Rect row = listing.GetRect(lineHeight);
            float measured = Text.CalcSize(label).x + 4f;
            float labelHeight = 0f;
            if (Mathf.Max(measured, sharedLabelWidth) + gap + fieldWidth +
                (suffixWidth > 0f ? 6f : 0f) + suffixWidth > row.width)
            {
                // Text.CalcHeight uses the same language and font as drawing.
                labelHeight = Text.CalcHeight(label, Mathf.Max(1f, row.width));
            }
            Placement result = Compute(row, measured, sharedLabelWidth,
                fieldWidth, suffixWidth, lineHeight, fieldHeight, gap, labelHeight);
            if (result.Wrapped)
            {
                listing.Gap(result.ExtraHeight);
                if (!fallbackLogged)
                {
                    fallbackLogged = true;
                    Log.Message("[XPC-UL1][LAYOUT_FALLBACK] first=narrow_window_wrap" +
                        "; revision=" + Revision + "; build=" + XpCustomizerMod.BuildId);
                }
            }
            if (!installedLogged)
            {
                installedLogged = true;
                Log.Message("[XPC-UL1][LAYOUT_INSTALLED] mode=SINGLE_ADJACENT_OR_GROUP_COLUMN" +
                    "; revision=" + Revision + "; gameplay=UNCHANGED" +
                    "; build=" + XpCustomizerMod.BuildId);
            }
            return result;
        }
    }
}
