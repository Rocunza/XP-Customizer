using System.Collections.Generic;
using UnityEngine;
using Verse;
using XpCustomizer.Progression;

namespace XpCustomizer.UI
{
    /// <summary>
    /// Draws and manages the editor for dynamic career-XP stage boundaries.
    ///
    /// Responsibilities:
    /// - Draft split levels from 1 to 19.
    /// - Add/remove/edit split points without changing gameplay settings live.
    /// - Apply normalized split levels.
    /// - Reset stage weights when boundaries change.
    /// - Reset all stage weights to 1.00x on request.
    ///
    /// Level 0 and level 20 are fixed endpoints.
    /// </summary>
    internal static partial class CareerXpStageBoundaryEditorUI
    {
        internal const string Revision = "CBE1-BOUNDARY-SPLIT-20261110";
        private static readonly CareerStageBoundaryDraft stageBoundaryDraft =
            new CareerStageBoundaryDraft();

        private static readonly List<NumericInputState> stageBoundaryInputs =
            new List<NumericInputState>();

        private static bool editorOpen;

#if DEBUG
        // L3-B2 presentation-only version probe: once on the first editor open.
        private static bool l3B2TranslationLogged;
#endif


        // ================================================================
        // Public UI
        // ================================================================

        public static bool Draw(
            Listing_Standard listing,
            XpCustomizerSettings settings,
            int[] normalizedSplits,
            out bool weightsReset)
        {
            weightsReset =
                false;


            Rect buttonsRect =
                listing.GetRect(30f);

            const float buttonGap = 8f;

            float buttonWidth =
                Mathf.Min(
                    190f,
                    (buttonsRect.width - buttonGap) * 0.5f);

            Rect editButtonRect =
                new Rect(
                    buttonsRect.x,
                    buttonsRect.y,
                    buttonWidth,
                    buttonsRect.height);

            Rect resetWeightsButtonRect =
                new Rect(
                    editButtonRect.xMax +
                    buttonGap,
                    buttonsRect.y,
                    buttonWidth,
                    buttonsRect.height);


            if (Widgets.ButtonText(
                    editButtonRect,
                    editorOpen
                        ? "XPC_L3B2_CloseEditor".Translate().ToString()
                        : "XPC_L3B2_EditBoundaries".Translate().ToString()))
            {
                if (editorOpen)
                {
                    CloseEditor();
                }
                else
                {
                    OpenEditor(
                        normalizedSplits);
                }
            }


            if (Widgets.ButtonText(
                    resetWeightsButtonRect,
                    "XPC_L3B2_ResetWeights".Translate().ToString()))
            {
                ResetStageWeights(
                    settings,
                    normalizedSplits.Length + 1);

                Verse.Log.Message(
                    "[XPC-CBE1][BOUNDARY_EDITOR] action=RESET_WEIGHTS; stageCount=" +
                    (normalizedSplits.Length + 1) +
                    "; locksReset=FALSE; settingsApplied=" + (settings != null) +
                    "; xmlPersisted=NOT_WRITTEN_BY_EDITOR; build=" + XpCustomizerMod.BuildId);

                weightsReset =
                    true;
            }


            if (!editorOpen)
            {
                return false;
            }


            listing.Gap(4f);

            SettingsUIHelpers.DrawReferenceText(
                listing,
                "XPC_L3B2_EditRangeHelp".Translate().ToString());

            SettingsUIHelpers.DrawReferenceText(
                listing,
                "XPC_L3B2_ApplyResetNotice".Translate().ToString());

            SettingsUIHelpers.DrawReferenceText(
                listing,
                "XPC_L3B2_DeduplicateNotice".Translate().ToString());

            listing.Gap(4f);


            DrawSplitRows(
                listing);

            listing.Gap(4f);


            bool actionResetWeights;

            bool applied =
                DrawActionButtons(
                    listing,
                    settings,
                    out actionResetWeights);

            if (actionResetWeights)
            {
                weightsReset =
                    true;
            }


            if (applied)
            {
                return true;
            }


            listing.Gap(4f);

            int[] draftNormalized =
                stageBoundaryDraft.GetNormalizedCopy();

            SettingsUIHelpers.DrawReferenceText(
                listing,
                string.Format(
                    "XPC_L3B2_PendingBoundaries".Translate().ToString(),
                    FormatBoundaries(
                        draftNormalized)));


            return false;
        }


        public static void NotifySettingsExternallyChanged()
        {
            CloseEditor();
        }


        // ================================================================
        // Draft lifecycle
        // ================================================================

        private static void OpenEditor(
            int[] normalizedSplits)
        {
            SetDraft(
                normalizedSplits);

            editorOpen =
                true;

#if DEBUG
            if (!l3B2TranslationLogged)
            {
                l3B2TranslationLogged = true;
                Verse.Log.Message(
                    "[XPC-L10N-L3B2][KEYED_BOUNDARY] " +
                    "version=XPC-L10N-L3B2-BOUNDARY-20261112-R1; " +
                    "phase=B2; keys=13; sampleKey=XPC_L3B2_EditBoundaries; sample=" +
                    "XPC_L3B2_EditBoundaries".Translate().ToString() +
                    "; build=" + XpCustomizerMod.BuildId);
            }
#endif
            Verse.Log.Message(
                "[XPC-CBE1][BOUNDARY_EDITOR] action=OPEN; rawCount=" +
                stageBoundaryDraft.Count +
                "; settingsApplied=FALSE; xmlPersisted=NOT_WRITTEN_BY_EDITOR; build=" +
                XpCustomizerMod.BuildId);
        }


        private static void CloseEditor()
        {
            editorOpen =
                false;

            stageBoundaryDraft.Clear();

            stageBoundaryInputs.Clear();
        }


        private static void SetDraft(
            IList<int> splitLevels)
        {
            stageBoundaryDraft.ReplaceFromNormalized(
                splitLevels);


            stageBoundaryInputs.Clear();

            EnsureInputStates(
                stageBoundaryDraft.Count);
        }


        private static void EnsureInputStates(
            int splitCount)
        {
            while (stageBoundaryInputs.Count < splitCount)
            {
                stageBoundaryInputs.Add(
                    new NumericInputState());
            }


            while (stageBoundaryInputs.Count > splitCount)
            {
                stageBoundaryInputs.RemoveAt(
                    stageBoundaryInputs.Count - 1);
            }
        }


        // ================================================================
        // Apply / reset adapters (original UI signatures preserved)
        // ================================================================

        private static void ApplyDraft(
            XpCustomizerSettings settings)
        {
            int[] normalized =
                stageBoundaryDraft.GetNormalizedCopy();

            CareerStageBoundarySettingsCommands.ApplyNormalizedBoundaries(
                settings,
                normalized);

            CloseEditor();

            Verse.Log.Message(
                "[XPC-CBE1][BOUNDARY_EDITOR] action=APPLY; splitCount=" +
                normalized.Length +
                "; weightsReset=TRUE; locksReset=TRUE; settingsApplied=TRUE" +
                "; xmlPersisted=NOT_WRITTEN_BY_EDITOR; build=" + XpCustomizerMod.BuildId);
        }


        private static void ResetStageWeights(
            XpCustomizerSettings settings,
            int stageCount)
        {
            CareerStageBoundarySettingsCommands.ResetWeights(
                settings,
                stageCount);
        }


        // ================================================================
        // Formatting
        // ================================================================

        private static string FormatBoundaries(
            int[] splitLevels)
        {
            string result =
                "0";


            if (splitLevels != null)
            {
                for (int i = 0;
                     i < splitLevels.Length;
                     i++)
                {
                    result +=
                        " | " +
                        splitLevels[i];
                }
            }


            result +=
                " | 20";


            return result;
        }
    }
}
