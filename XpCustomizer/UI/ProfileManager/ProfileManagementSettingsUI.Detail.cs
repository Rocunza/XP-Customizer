using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    internal sealed partial class ProfileManagementSettingsUI
    {
        private void DrawDetail(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(11f);
            float renameWidth = Mathf.Min(108f, Mathf.Max(74f, inner.width * 0.23f));
            float notesWidth = Mathf.Min(112f, Mathf.Max(82f, inner.width * 0.26f));
            float headerButtonsWidth = state.InTrash ? renameWidth : renameWidth + notesWidth + 6f;
            Widgets.Label(new Rect(inner.x, inner.y,
                Mathf.Max(0f, inner.width - headerButtonsWidth - 6f), 26f),
                "XPC_Profile_DetailsTitle".Translate().ToString());
            bool wasEnabled = GUI.enabled;
            if (!state.InTrash)
            {
                Rect renameRect = new Rect(inner.xMax - renameWidth - notesWidth - 6f,
                    inner.y, renameWidth, 27f);
                bool renameClicked;
                try
                {
                    GUI.enabled = wasEnabled && CanStartSelectedActiveProfileAction();
                    renameClicked = Widgets.ButtonText(renameRect, "XPC_Profile_RenameButton".Translate().ToString());
                }
                finally { GUI.enabled = wasEnabled; }
                if (renameClicked) OpenRename();
                Rect notesButton = new Rect(renameRect.xMax + 6f,
                    inner.y, notesWidth, 27f);
                bool editNotes;
                try
                {
                    GUI.enabled = wasEnabled && CanStartSelectedActiveProfileAction();
                    editNotes = Widgets.ButtonText(notesButton,
                        "XPC_Profile_EditNotes".Translate().ToString());
                }
                finally { GUI.enabled = wasEnabled; }
                if (editNotes) OpenNotesEditor();
            }
            float gap = 6f;
            float half = Mathf.Max(1f, (inner.width - gap) / 2f);
            Rect left = new Rect(inner.x, inner.y + 32f, half, 27f);
            Rect right = new Rect(inner.x + half + gap, inner.y + 32f, half, 27f);
            if (state.InTrash)
            {
                bool restoreClicked;
                try
                {
                    GUI.enabled = wasEnabled && CanRestore();
                    restoreClicked = Widgets.ButtonText(right, "XPC_Profile_RestoreButton".Translate().ToString());
                }
                finally { GUI.enabled = wasEnabled; }
                if (restoreClicked) RestoreSelected();
            }
            else
            {
                bool preflightClicked;
                bool deleteClicked;
                try
                {
                    GUI.enabled = wasEnabled && CanStartSelectedActiveProfileAction();
                    preflightClicked = Widgets.ButtonText(left,
                        half < 190f ? "XPC_Profile_UpdateShort".Translate().ToString() : "XPC_Profile_UpdateLong".Translate().ToString());
                    deleteClicked = Widgets.ButtonText(right, "XPC_Profile_DeleteButton".Translate().ToString());
                }
                finally { GUI.enabled = wasEnabled; }
                if (preflightClicked) OpenOverwriteConfirmation();
                if (deleteClicked) OpenDeleteConfirmation();
            }
            float top = inner.y + 65f;
            // A bounded notes preview above the existing technical preview.
            // The full notes are accessible in the editor and review window.
            if (selectedIndex >= 0 && !state.HasErrorSelection &&
                state.Selected != null && inner.height >= 165f)
            {
                float notesHeight = Mathf.Min(105f, Mathf.Max(46f, inner.height * 0.26f));
                string value = String.IsNullOrEmpty(state.Selected.Notes)
                    ? "XPC_Profile_NotesEmpty".Translate().ToString()
                    : BundledProfileNotesLocalization.Display(
                        state.Selected.ProfileId, state.Selected.Notes);
                Widgets.Label(new Rect(inner.x + 3f, top, inner.width - 6f, 22f),
                    "XPC_Profile_NotesLabel".Translate().ToString());
                string preview = value.Length <= 180 ? value : value.Substring(0, 177) + "...";
                Rect noteRect = new Rect(inner.x + 3f, top + 23f,
                    Mathf.Max(1f, inner.width - 6f), Mathf.Max(1f, notesHeight - 24f));
                bool oldWrap = Text.WordWrap;
                Text.WordWrap = true;
                try { Widgets.Label(noteRect, preview); }
                finally { Text.WordWrap = oldWrap; }
                if (value != preview) TooltipHandler.TipRegion(noteRect, value);
                top += notesHeight + 5f;
            }
            Rect scroll = new Rect(inner.x, top, inner.width,
                Mathf.Max(0f, inner.yMax - top));
            if (selectedIndex < 0)
            {
                Widgets.Label(scroll.ContractedBy(8f),
                    "XPC_Profile_SelectHelp".Translate().ToString());
                return;
            }
            if (state.HasErrorSelection)
            {
                Widgets.Label(scroll.ContractedBy(8f),
                    string.Format("XPC_Profile_PreviewError".Translate().ToString(), ProfileManagementUiState.LocalizedExplain(state.SelectedError)));
                return;
            }
            Rect view = new Rect(0f, 0f, Mathf.Max(1f, scroll.width - 18f),
                Mathf.Max(scroll.height, detailRows.Count * 23f + 8f));
            Widgets.BeginScrollView(scroll, ref detailScroll, view);
            try
            {
                GameFont old = Text.Font;
                try
                {
                    Text.Font = GameFont.Small;
                    for (int i = 0; i < detailRows.Count; i++)
                        Widgets.Label(new Rect(4f, i * 23f, Mathf.Max(1f, view.width - 8f), 22f),
                            Clip(detailRows[i], 104));
                }
                finally { Text.Font = old; }
            }
            finally { Widgets.EndScrollView(); }
        }
    }
}
