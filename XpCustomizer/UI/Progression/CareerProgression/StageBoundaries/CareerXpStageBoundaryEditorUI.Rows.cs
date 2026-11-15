using System.Collections.Generic;
using UnityEngine;
using Verse;
using XpCustomizer.Progression;

namespace XpCustomizer.UI
{
    internal static partial class CareerXpStageBoundaryEditorUI
    {
        // ================================================================
        // Split rows
        // ================================================================

        private static void DrawSplitRows(
            Listing_Standard listing)
        {
            EnsureInputStates(
                stageBoundaryDraft.Count);


            int removeIndex =
                -1;


            for (int i = 0;
                 i < stageBoundaryDraft.Count;
                 i++)
            {
                NumericInputState inputState =
                    stageBoundaryInputs[i];

                int value =
                    stageBoundaryDraft[i];


                if (!float.IsNaN(
                        inputState.lastValue) &&
                    !Mathf.Approximately(
                        value,
                        inputState.lastValue))
                {
                    inputState.buffer = null;
                }


                Rect row =
                    listing.GetRect(30f);

                GetSplitRowRects(
                    row,
                    out Rect labelRect,
                    out Rect fieldRect,
                    out Rect removeRect);


                Widgets.Label(
                    labelRect,
                    "XPC_L3B2_SplitAfterLevel".Translate().ToString());


                ProfileNumericFocus.Draw(
                    fieldRect,
                    ref value,
                    inputState,
                    1,
                    19);


                stageBoundaryDraft[i] =
                    value;

                inputState.lastValue =
                    value;


                if (Widgets.ButtonText(
                        removeRect,
                        "XPC_L3B2_Remove".Translate().ToString()))
                {
                    removeIndex =
                        i;
                }
            }


            if (removeIndex >= 0)
            {
                stageBoundaryDraft.RemoveAt(
                    removeIndex);

                if (removeIndex < stageBoundaryInputs.Count)
                {
                    stageBoundaryInputs.RemoveAt(
                        removeIndex);
                }
            }
        }


        private static void GetSplitRowRects(
            Rect row,
            out Rect labelRect,
            out Rect fieldRect,
            out Rect removeRect)
        {
            const float removeWidth = 92f;
            const float fieldWidth = 90f;
            const float columnGap = 8f;

            float labelWidth =
                Mathf.Max(
                    100f,
                    row.width -
                    removeWidth -
                    fieldWidth -
                    (columnGap * 2f));


            labelRect =
                new Rect(
                    row.x,
                    row.y,
                    labelWidth,
                    row.height);

            fieldRect =
                new Rect(
                    labelRect.xMax +
                    columnGap,
                    row.y,
                    fieldWidth,
                    row.height);

            removeRect =
                new Rect(
                    fieldRect.xMax +
                    columnGap,
                    row.y,
                    removeWidth,
                    row.height);
        }


    }
}
