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
        private void DrawHeader(Rect rect)
        {
            GameFont original = Text.Font;
            try
            {
                Text.Font = GameFont.Medium;
                float buttonWidth = Mathf.Min(146f, Mathf.Max(110f, rect.width * 0.34f));
                Rect create = new Rect(rect.xMax - buttonWidth, rect.y + 3f,
                    buttonWidth, 32f);
                Widgets.Label(new Rect(rect.x, rect.y, Mathf.Max(0f, rect.width - buttonWidth - 8f),
                    29f), "XPC_Profile_Title".Translate().ToString());
                Text.Font = GameFont.Small;
                string subtitle = "XPC_Profile_Subtitle".Translate().ToString();
                Rect subtitleRect = new Rect(rect.x, rect.y + 31f,
                    Mathf.Max(0f, rect.width - buttonWidth - 8f), 22f);
                bool oldWrap = Text.WordWrap;
                try
                {
                    Text.WordWrap = false;
                    Widgets.Label(subtitleRect, subtitle);
                }
                finally { Text.WordWrap = oldWrap; }
                // Full text remains available when a compact window clips the line.
                TooltipHandler.TipRegion(subtitleRect, subtitle);
                bool wasEnabled = GUI.enabled;
                bool createClicked;
                try
                {
                    GUI.enabled = wasEnabled && !ProfileWriteSessionGate.IsBlocked;
                    createClicked = Widgets.ButtonText(create, "XPC_Profile_SaveCurrent".Translate().ToString());
                }
                finally { GUI.enabled = wasEnabled; }
                if (createClicked)
                {
                    Find.WindowStack.Add(new Dialog_CreateXpProfile(OnProfileCreated));
                    Log.Message("[XPC-R6P1C2A][PROFILE_CREATE_UI] action=OPEN; " +
                        "applies=0; scribeCalls=0; build=" + XpCustomizerMod.BuildId);
                }
            }
            finally { Text.Font = original; }
        }

        private void DrawToolbar(Rect rect)
        {
            const float gap = 6f;
            float refreshWidth = Mathf.Min(96f, Mathf.Max(66f, rect.width * 0.19f));
            float purgeWidth = state.InTrash ? Mathf.Min(130f, Mathf.Max(90f, rect.width * 0.25f)) : 0f;
            float available = rect.width - refreshWidth - (state.InTrash ? purgeWidth : 0f) -
                (state.InTrash ? 3f : 2f) * gap;
            float modeWidth = Mathf.Max(1f, Mathf.Min(112f, available / 2f));
            Rect active = new Rect(rect.x, rect.y, modeWidth, rect.height);
            Rect trash = new Rect(active.xMax + gap, rect.y, modeWidth, rect.height);
            Rect refresh = new Rect(rect.xMax - refreshWidth,
                rect.y, refreshWidth, rect.height);
            bool purgeShown = state.InTrash;
            Rect purge = new Rect(refresh.x - gap - purgeWidth, rect.y, purgeWidth, rect.height);
            if (Widgets.ButtonText(active, state.InTrash ? "XPC_Profile_Active".Translate().ToString() : "XPC_Profile_ActiveSelected".Translate().ToString()))
                ChangeView(false);
            if (Widgets.ButtonText(trash, state.InTrash ? "XPC_Profile_TrashSelected".Translate().ToString() : "XPC_Profile_Trash".Translate().ToString()))
                ChangeView(true);
            if (purgeShown)
            {
                bool old = GUI.enabled;
                bool pressed;
                try
                {
                    GUI.enabled = old && purgeController.CanOpen();
                    pressed = Widgets.ButtonText(purge, "XPC_Profile_EmptyTrash".Translate().ToString());
                }
                finally { GUI.enabled = old; }
                if (pressed) purgeController.Open();
                else if (!purgeController.CanOpen())
                    TooltipHandler.TipRegion(purge,
                        state.VisibleEntries.Count == 0 && state.ListingCode == ProfileStorageCode.Ok
                            ? "XPC_Profile_TrashEmpty".Translate().ToString()
                            : "XPC_Profile_TrashDisabledTip".Translate().ToString());
            }
            if (Widgets.ButtonText(refresh, "XPC_Profile_Refresh".Translate().ToString()))
            {
                state.Refresh();
                ResetSelectionVisual();
                creationNotice = null;
                LogAction("REFRESH", state.ListingCode);
            }
        }

        private void DrawFooter(Rect rect, Action close)
        {
            float buttonWidth = Mathf.Min(110f, rect.width * 0.23f);
            Rect closeRect = new Rect(rect.xMax - buttonWidth, rect.y,
                buttonWidth, rect.height);
            Rect statusRect = new Rect(rect.x, rect.y,
                Mathf.Max(0f, rect.width - buttonWidth - 10f), rect.height);
            GameFont old = Text.Font;
            bool oldWrap = Text.WordWrap;
            try
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = false;
                string fullStatus = ProfileWriteSessionGate.IsBlocked
                    ? "XPC_Profile_RecoveryLock".Translate().ToString()
                    : creationNotice ?? state.DisplayStatus;
                if (String.IsNullOrEmpty(fullStatus)) fullStatus = "(empty)";
                fullStatus = fullStatus.Replace('\r', ' ').Replace('\n', ' ');
                string visibleStatus = fullStatus;
                if (statusRect.width > 0f && Text.CalcSize(fullStatus).x > statusRect.width)
                {
                    const string ellipsis = "...";
                    int low = 0;
                    int high = fullStatus.Length;
                    while (low < high)
                    {
                        int mid = low + (high - low + 1) / 2;
                        if (Text.CalcSize(fullStatus.Substring(0, mid) + ellipsis).x <= statusRect.width)
                            low = mid;
                        else
                            high = mid - 1;
                    }
                    visibleStatus = low == 0 && Text.CalcSize(ellipsis).x > statusRect.width
                        ? String.Empty : fullStatus.Substring(0, low) + ellipsis;
                }
                Widgets.Label(statusRect, visibleStatus);
                if (visibleStatus != fullStatus)
                    TooltipHandler.TipRegion(statusRect, fullStatus);
            }
            finally
            {
                Text.WordWrap = oldWrap;
                Text.Font = old;
            }
            if (Widgets.ButtonText(closeRect, "XPC_Profile_Close".Translate().ToString()) && close != null) close();
        }

        private static string Clip(string text, int limit)
        {
            if (String.IsNullOrEmpty(text)) return "(empty)";
            string clean = text.Replace('\r', ' ').Replace('\n', ' ');
            return clean.Length <= limit ? clean : clean.Substring(0, limit - 3) + "...";
        }

        private static void LogAction(string action, ProfileStorageCode code)
        {
            Log.Message("[XPC-R6P1C15][PROFILE_WINDOW_READ] action=" + action +
                "; status=" + code + "; writes=0; profileApplyByThisAction=NOT_INVOKED; build=" +
                XpCustomizerMod.BuildId);
        }
    }
}
