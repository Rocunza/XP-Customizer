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
        private void DrawList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(10f);
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f),
                state.InTrash ? "XPC_Profile_DeletedList".Translate().ToString() : "XPC_Profile_SavedList".Translate().ToString());
            IList<ProfileStorageEntry> entries = state.VisibleEntries;
            int valid = 0;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Result.Success) valid++;
            GameFont oldFont = Text.Font;
            bool oldCountWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = false;
                Rect countsRect = new Rect(inner.x, inner.y + 26f, inner.width, 24f);
                string counts = string.Format("XPC_Profile_ListCounts".Translate().ToString(),
                    valid, entries.Count - valid);
                Widgets.Label(countsRect, counts);
                TooltipHandler.TipRegion(countsRect, counts);
            }
            finally
            {
                Text.WordWrap = oldCountWrap;
                Text.Font = oldFont;
            }
            Rect scroll = new Rect(inner.x, inner.y + ProfileManagerLayout.ListHeaderOffset, inner.width,
                Mathf.Max(0f, inner.height - ProfileManagerLayout.ListHeaderOffset));
            if (entries.Count == 0)
            {
                string message = state.ListingCode != ProfileStorageCode.Ok
                    ? string.Format("XPC_Profile_ListReadFailure".Translate().ToString(), ProfileManagementUiState.LocalizedExplain(state.ListingCode))
                    : state.InTrash ? "XPC_Profile_TrashEmpty".Translate().ToString() :
                      "XPC_Profile_NoProfiles".Translate().ToString();
                Widgets.Label(scroll.ContractedBy(6f), message);
                return;
            }
            float rowHeight = ProfileManagerLayout.ListRowHeight;
            Rect view = new Rect(0f, 0f, Mathf.Max(1f, scroll.width - 18f),
                Mathf.Max(scroll.height, entries.Count * (rowHeight + ProfileManagerLayout.ListRowGap)));
            Widgets.BeginScrollView(scroll, ref listScroll, view);
            try
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    ProfileStorageEntry entry = entries[i];
                    bool ok = entry.Result.Success;
                    Rect row = new Rect(0f, i * (rowHeight + ProfileManagerLayout.ListRowGap), view.width, rowHeight);
                    if (i == selectedIndex) Widgets.DrawHighlight(row);
                    else Widgets.DrawHighlightIfMouseover(row);
                    if (Widgets.ButtonInvisible(row))
                    {
                        selectedIndex = i;
                        creationNotice = null;
                        detailScroll = Vector2.zero;
                        ProfileStorageCode result = state.Select(i);
                        BuildDetailLines();
                        LogAction("SELECT", result);
                    }
                    string label = ok ? entry.Result.Document.DisplayName : "! " + entry.FileName;
                    Rect nameRect = new Rect(row.x + 5f, row.y + 2f,
                        Mathf.Max(0f, row.width - 10f), 26f);
                    bool nameWrap = Text.WordWrap;
                    try
                    {
                        Text.WordWrap = false;
                        Widgets.Label(nameRect, Clip(label, 40));
                    }
                    finally { Text.WordWrap = nameWrap; }
                    TooltipHandler.TipRegion(nameRect, label);
                    GameFont old = Text.Font;
                    try
                    {
                        Text.Font = GameFont.Small;
                        string description = ok ? string.Format("XPC_Profile_RowFormat".Translate().ToString(), entry.Result.Document.FormatVersion, entry.Result.Document.ProfileId.Substring(0, 8)) :
                            ProfileManagementUiState.LocalizedExplain(entry.Result.Code);
                        Rect metaRect = new Rect(row.x + 5f, row.y + 29f,
                            Mathf.Max(0f, row.width - 10f), 24f);
                        bool oldWrap = Text.WordWrap;
                        try
                        {
                            Text.WordWrap = false;
                            Widgets.Label(metaRect, Clip(description, 48));
                        }
                        finally { Text.WordWrap = oldWrap; }
                        // Entries with long metadata remain inspectable at narrow widths.
                        TooltipHandler.TipRegion(metaRect, description);
                    }
                    finally { Text.Font = old; }
                }
            }
            finally { Widgets.EndScrollView(); }
        }
    }
}
