using System.Collections.Generic;
using UnityEngine;
using Verse;
using XpCustomizer.Progression;

namespace XpCustomizer.UI
{
    internal static partial class CareerXpStageBoundaryEditorUI
    {
        // ================================================================
        // Actions
        // ================================================================

        private static bool DrawActionButtons(
            Listing_Standard listing,
            XpCustomizerSettings settings,
            out bool weightsReset)
        {
            weightsReset =
                false;


            Rect row =
                listing.GetRect(30f);

            const float gap = 8f;

            float buttonWidth =
                Mathf.Max(
                    100f,
                    (row.width - (gap * 3f)) / 4f);


            Rect addRect =
                new Rect(
                    row.x,
                    row.y,
                    buttonWidth,
                    row.height);

            Rect recommendedRect =
                new Rect(
                    addRect.xMax +
                    gap,
                    row.y,
                    buttonWidth,
                    row.height);

            Rect applyRect =
                new Rect(
                    recommendedRect.xMax +
                    gap,
                    row.y,
                    buttonWidth,
                    row.height);

            Rect cancelRect =
                new Rect(
                    applyRect.xMax +
                    gap,
                    row.y,
                    buttonWidth,
                    row.height);


            bool oldGuiEnabled =
                GUI.enabled;

            GUI.enabled =
                oldGuiEnabled &&
                stageBoundaryDraft.Count < 19;


            if (Widgets.ButtonText(
                    addRect,
                    "XPC_L3B2_AddSplit".Translate().ToString()))
            {
                if (stageBoundaryDraft.TryAppendSuggested())
                {
                    stageBoundaryInputs.Add(
                        new NumericInputState());
                }
            }


            GUI.enabled =
                oldGuiEnabled;


            if (Widgets.ButtonText(
                    recommendedRect,
                    "XPC_L3B2_Recommended".Translate().ToString()))
            {
                SetDraft(
                    CareerXpDistributionService
                        .GetRecommendedSplitLevels());
            }


            if (Widgets.ButtonText(
                    applyRect,
                    "XPC_L3B2_Apply".Translate().ToString()))
            {
                ApplyDraft(
                    settings);

                weightsReset =
                    true;

                return true;
            }


            if (Widgets.ButtonText(
                    cancelRect,
                    "XPC_L3B2_Cancel".Translate().ToString()))
            {
                CloseEditor();
            }


            return false;
        }


    }
}
