using UnityEngine;
using Verse;

namespace XpCustomizer.UI
{
    internal static partial class SettingsUIHelpers
    {
        /// <summary>
        /// Draws an integer setting using minus/value/plus controls.
        /// </summary>
        public static int DrawIntegerSetting(
            Listing_Standard listing,
            string label,
            int value,
            int min,
            int max,
            bool showPlusSign)
        {
            Rect row =
                listing.GetRect(30f);

            Rect labelRect =
                new Rect(
                    row.x,
                    row.y,
                    row.width - 150f,
                    row.height);

            Widgets.Label(
                labelRect,
                label);


            const float buttonWidth = 36f;
            const float valueWidth = 70f;
            const float gap = 4f;


            Rect minusButton =
                new Rect(
                    row.xMax -
                    buttonWidth -
                    valueWidth -
                    buttonWidth -
                    gap * 2f,
                    row.y,
                    buttonWidth,
                    row.height);

            Rect valueRect =
                new Rect(
                    minusButton.xMax + gap,
                    row.y,
                    valueWidth,
                    row.height);

            Rect plusButton =
                new Rect(
                    valueRect.xMax + gap,
                    row.y,
                    buttonWidth,
                    row.height);


            if (Widgets.ButtonText(
                minusButton,
                "-"))
            {
                if (value > int.MinValue)
                    value--;
            }

            if (Widgets.ButtonText(
                plusButton,
                "+"))
            {
                if (value < int.MaxValue)
                    value++;
            }


            value =
                Mathf.Clamp(
                    value,
                    min,
                    max);


            TextAnchor oldAnchor =
                Text.Anchor;

            try
            {
                Text.Anchor =
                    TextAnchor.MiddleCenter;

                string displayValue =
                    showPlusSign && value > 0
                        ? "+" + value
                        : value.ToString();

                Widgets.Label(
                    valueRect,
                    displayValue);
            }
            finally
            {
                Text.Anchor =
                    oldAnchor;
            }


            return value;
        }

        /// <summary>
        /// Draws a direct numeric percentage-point input.
        ///
        /// The displayed value is stored directly as percentage points.
        /// Example:
        ///     15  = +15%
        ///     -10 = -10%
        ///
        /// A persistent text buffer must be supplied by the caller so the
        /// player can type partial values such as "-" without the field
        /// immediately resetting.
        /// </summary>
        public static float DrawPercentageInput(
            Listing_Standard listing,
            string label,
            float value,
            NumericInputState inputState,
            float min,
            float max)
        {
            Rect row =
                listing.GetRect(30f);

            const float fieldWidth = 90f;
            const float percentWidth = 24f;
            const float gap = 4f;


            Rect labelRect =
                new Rect(
                    row.x,
                    row.y,
                    row.width -
                    fieldWidth -
                    percentWidth -
                    gap,
                    row.height);

            Rect fieldRect =
                new Rect(
                    labelRect.xMax,
                    row.y,
                    fieldWidth,
                    row.height);

            Rect percentRect =
                new Rect(
                    fieldRect.xMax + gap,
                    row.y,
                    percentWidth,
                    row.height);


            Widgets.Label(
                labelRect,
                label);


            ProfileNumericFocus.Draw(
                fieldRect,
                ref value,
                inputState,
                min,
                max);


            TextAnchor oldAnchor =
                Text.Anchor;

            try
            {
                Text.Anchor =
                    TextAnchor.MiddleLeft;

                Widgets.Label(
                    percentRect,
                    "%");
            }
            finally
            {
                Text.Anchor =
                    oldAnchor;
            }


            return Mathf.Clamp(
                value,
                min,
                max);
        }

        /// <summary>
        /// Draws a direct numeric multiplier input.
        /// A persistent text buffer must be supplied by the caller.
        /// </summary>
        public static float DrawMultiplierInput(
            Listing_Standard listing,
            string label,
            float value,
            NumericInputState inputState,
            float min,
            float max)
        {
            Rect row =
                listing.GetRect(30f);

            const float fieldWidth = 90f;
            const float suffixWidth = 24f;
            const float gap = 4f;


            Rect labelRect =
                new Rect(
                    row.x,
                    row.y,
                    row.width -
                    fieldWidth -
                    suffixWidth -
                    gap,
                    row.height);

            Rect fieldRect =
                new Rect(
                    labelRect.xMax,
                    row.y,
                    fieldWidth,
                    row.height);

            Rect suffixRect =
                new Rect(
                    fieldRect.xMax + gap,
                    row.y,
                    suffixWidth,
                    row.height);


            Widgets.Label(
                labelRect,
                label);


            ProfileNumericFocus.Draw(
                fieldRect,
                ref value,
                inputState,
                min,
                max);


            TextAnchor oldAnchor =
                Text.Anchor;

            try
            {
                Text.Anchor =
                    TextAnchor.MiddleLeft;

                Widgets.Label(
                    suffixRect,
                    "x");
            }
            finally
            {
                Text.Anchor =
                    oldAnchor;
            }


            return Mathf.Clamp(
                value,
                min,
                max);
        }

    }
}
