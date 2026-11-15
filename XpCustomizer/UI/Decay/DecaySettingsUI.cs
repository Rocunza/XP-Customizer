using UnityEngine;
using Verse;
using XpCustomizer.Decay;

namespace XpCustomizer.UI
{
    internal static class DecaySettingsUI
    {
        private static readonly string[] xpBuffers = new string[21];
        private static readonly string[] ratioBuffers = new string[21];
        private static readonly float[] lastValues = new float[21];
        private static bool buffersInitialized;
        private static Vector2 levelTableScrollPosition = Vector2.zero;
        private const float LevelRowHeight = 36f;
        private const float TableHeaderHeight = 34f;
        private const float TableBodyHeight = 385f;
        private const float MaxTableWidth = 760f;
        private const string UiBuildId = "DECAY-UI-RW16-D4-MAP-TARGET-R1-20261101";
        private static bool widthLogged;

        private static readonly Color VanillaColor = new Color(0.96f, 0.82f, 0.48f, 1f);

        internal static void NotifySettingsExternallyChanged()
        {
            for (int i = 0; i < 21; i++)
            {
                xpBuffers[i] = null;
                ratioBuffers[i] = null;
                lastValues[i] = float.NaN;
                ProfileNumericFocus.ForgetRaw("DecaySettingsUI.xpBuffers[" + i + "]");
                ProfileNumericFocus.ForgetRaw("DecaySettingsUI.ratioBuffers[" + i + "]");
            }
            buffersInitialized = false;
            DecayModifiersSettingsUI.NotifySettingsExternallyChanged();
        }

        private static void SynchronizeBuffers(int level, float value)
        {
            xpBuffers[level] = value.ToString("0.##");
            float vanilla = SkillDecayService.VanillaDaily[level];
            ratioBuffers[level] = vanilla > 0f ? (value / vanilla).ToString("0.####") : null;
            lastValues[level] = value;
        }

        public static void Draw(Listing_Standard listing)
        {
            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            if (settings == null) return;
            SkillDecayService.Sanitize(settings);

            // The memory modifiers are the first section in Decay.
            // Keep the level-decay heading and explanation together with its toggle.
            DecayModifiersSettingsUI.DrawMemorySection(listing, settings);
            listing.Gap(15f);
            listing.GapLine();
            SettingsUIHelpers.DrawSectionTitle(listing, "XPC_Decay_LevelTitle".Translate().ToString());
            SettingsUIHelpers.DrawDescription(listing,
                "XPC_Decay_LevelDescription".Translate().ToString());
            listing.Gap(8f);

            bool enabled = settings.decayByLevelEnabled;
            SettingsUIHelpers.DrawRightAdjacentToggle(listing, "XPC_Decay_LevelEnable".Translate().ToString(), ref enabled);
            settings.decayByLevelEnabled = enabled;
            SettingsUIHelpers.DrawReferenceText(listing,
                enabled ? "XPC_Decay_LevelOn".Translate().ToString() :
                          "XPC_Decay_LevelOff".Translate().ToString());
            listing.Gap(10f);

            if (!buffersInitialized)
            {
                for (int i = 0; i < 21; i++) lastValues[i] = float.NaN;
                buffersInitialized = true;
            }

            // The table owns its own scroll view. The outer Decay tab scroll remains
            // untouched, so future Decay sections can be appended below this section.
            Rect tableSlot = listing.GetRect(TableHeaderHeight + TableBodyHeight + 2f);
            // Keep the inner table compact so its scrollbar is visually and
            // physically separated from the outer Decay-tab scrollbar.
            // On narrower windows, fit within the available tab width.
            Rect table = new Rect(tableSlot.x, tableSlot.y,
                Mathf.Min(tableSlot.width, MaxTableWidth), tableSlot.height);
            // Keep the independent scroll bar away from the outer tab scrollbar.
            // Log once so users can distinguish an updated DLL from a stale build.
            if (!widthLogged && Event.current.type == EventType.Repaint)
            {
                widthLogged = true;
                Log.Message("[XPC-L10N-L3D][KEYED_DECAY_BIOTECH] version=XPC-L10N-L3D-DECAY-BIOTECH-20261112-R1; screen=DECAY; keys=50; sampleKey=XPC_Decay_LevelTitle; sample=" +
                    "XPC_Decay_LevelTitle".Translate().ToString());
                Log.Message("[XP Customizer][Decay UI] " + UiBuildId +
                    "; availableWidth=" + tableSlot.width.ToString("0.##") +
                    "; actualWidth=" + table.width.ToString("0.##") +
                    "; outerGap=" + (tableSlot.width - table.width).ToString("0.##"));
            }
            Widgets.DrawBox(table);
            Rect header = new Rect(table.x + 6f, table.y + 3f, table.width - 24f, TableHeaderHeight - 3f);
            Rect body = new Rect(table.x + 6f, table.y + TableHeaderHeight,
                table.width - 12f, TableBodyHeight);
            Rect viewport = new Rect(0f, 0f, Mathf.Max(100f, body.width - 18f),
                21f * LevelRowHeight + 4f);

            // One shared column layout for the fixed header and scrolling rows.
            float tableWidth = Mathf.Min(viewport.width, 890f);
            float levelWidth = Mathf.Min(145f, tableWidth * 0.20f);
            float vanillaWidth = Mathf.Min(155f, tableWidth * 0.22f);
            const float symbolWidth = 27f;
            float ratioWidth = Mathf.Min(165f, tableWidth * 0.21f);
            float levelX = 0f;
            float vanillaX = levelX + levelWidth;
            float timesX = vanillaX + vanillaWidth;
            float ratioX = timesX + symbolWidth;
            float equalsX = ratioX + ratioWidth;
            float customX = equalsX + symbolWidth;
            float customWidth = Mathf.Min(185f, Mathf.Max(75f, tableWidth - customX - 8f));

            // Header is deliberately outside the inner scroll view.
            Widgets.Label(new Rect(header.x + levelX, header.y, levelWidth, 26f), "XPC_Decay_LevelHeader".Translate().ToString());
            Color oldColor = GUI.color;
            GUI.color = VanillaColor;
            Widgets.Label(new Rect(header.x + vanillaX, header.y, vanillaWidth, 26f), "XPC_Decay_VanillaDailyHeader".Translate().ToString());
            GUI.color = oldColor;
            Widgets.Label(new Rect(header.x + ratioX, header.y, ratioWidth, 26f), "XPC_Decay_RatioHeader".Translate().ToString());
            Widgets.Label(new Rect(header.x + customX, header.y, customWidth + 15f, 26f), "XPC_Decay_CustomDailyHeader".Translate().ToString());

            Widgets.BeginScrollView(body, ref levelTableScrollPosition, viewport);
            for (int i = 0; i <= 20; i++)
            {
                Rect row = new Rect(0f, i * LevelRowHeight, viewport.width, LevelRowHeight);
                DrawLevelRow(row, settings, enabled, i,
                    levelX, levelWidth, vanillaX, vanillaWidth, timesX, symbolWidth,
                    ratioX, ratioWidth, equalsX, customX, customWidth);
            }
            Widgets.EndScrollView();

            listing.Gap(7f);
            SettingsUIHelpers.DrawReferenceText(listing,
                "XPC_Decay_RatioDescription".Translate().ToString());
            listing.Gap(12f);
            DecayModifiersSettingsUI.DrawSkillSection(listing, settings);
            listing.Gap(12f);
            if (listing.ButtonText("XPC_Decay_ResetAll".Translate().ToString()))
            {
                SettingsPresetService.ResetDecayToVanilla();
                NotifySettingsExternallyChanged();
            }
            listing.Gap(16f);
        }

        private static void DrawLevelRow(Rect row, XpCustomizerSettings settings,
            bool enabled, int i, float levelX, float levelWidth, float vanillaX,
            float vanillaWidth, float timesX, float symbolWidth, float ratioX, float ratioWidth,
            float equalsX, float customX, float customWidth)
        {
            Widgets.DrawHighlightIfMouseover(row);
            Widgets.Label(new Rect(levelX, row.y + 5f, levelWidth, 26f), string.Format("XPC_Decay_LevelRow".Translate().ToString(), i));

            Color previousColor = GUI.color;
            GUI.color = VanillaColor;
            Widgets.Label(new Rect(vanillaX, row.y + 5f, vanillaWidth, 26f),
                SkillDecayService.VanillaDaily[i].ToString("0.##"));
            GUI.color = previousColor;

            float vanilla = SkillDecayService.VanillaDaily[i];
            if (vanilla > 0f)
            {
                Widgets.Label(new Rect(timesX, row.y + 5f, symbolWidth, 26f), "×");
                Widgets.Label(new Rect(equalsX, row.y + 5f, symbolWidth, 26f), "=");
            }
            if (i == 0)
            {
                Widgets.Label(new Rect(ratioX, row.y + 5f, ratioWidth, 26f), "—");
                Widgets.Label(new Rect(customX, row.y + 5f, customWidth + 20f, 26f), "XPC_Decay_LevelZero".Translate().ToString());
                return;
            }

            float value = settings.decayDailyByLevel[i];
            if (xpBuffers[i] == null || lastValues[i] != value)
                SynchronizeBuffers(i, value);

            Rect ratioRect = new Rect(ratioX, row.y + 2f,
                Mathf.Max(65f, ratioWidth - 16f), 29f);
            Rect customRect = new Rect(customX, row.y + 2f, customWidth, 29f);

            if (!enabled)
            {
                // R6-P1-C2-B2-A1.1: these buffers exist even when OFF, but
                // TextFieldNumeric is not rendered. Observe their *disabled*
                // state only when their text still matches committed settings.
                // A focused/unfinished draft stays blocked across the OFF
                // transition until the control is re-enabled and settled.
                ProfileNumericFocus.ObserveInactive(
                    "DecaySettingsUI.xpBuffers[" + i + "]", xpBuffers[i],
                    value.ToString("0.##"));
                if (vanilla > 0f)
                    ProfileNumericFocus.ObserveInactive(
                        "DecaySettingsUI.ratioBuffers[" + i + "]", ratioBuffers[i],
                        (value / vanilla).ToString("0.####"));
                Widgets.Label(new Rect(ratioRect.x, ratioRect.y + 4f, ratioRect.width, 25f),
                    vanilla <= 0f ? "-" : (value / vanilla).ToString("0.####") + "x");
                Widgets.Label(new Rect(customRect.x, customRect.y + 4f, customRect.width, 25f),
                    value.ToString("0.##"));
                return;
            }

            if (vanilla > 0f)
            {
                float ratio = value / vanilla;
                float originalRatio = ratio;
                ProfileNumericFocus.Draw(ratioRect, ref ratio, ref ratioBuffers[i],
                    "DecaySettingsUI.ratioBuffers[" + i + "]",
                    0f, SkillDecayService.MaxDaily / vanilla);
                if (ratio != originalRatio)
                {
                    float next = Mathf.Clamp(vanilla * ratio, 0f, SkillDecayService.MaxDaily);
                    settings.decayDailyByLevel[i] = next;
                    xpBuffers[i] = next.ToString("0.##");
                    lastValues[i] = next;
                    value = next;
                }
            }
            else
            {
                Widgets.Label(new Rect(ratioRect.x, ratioRect.y + 4f, ratioRect.width, 24f), "—");
            }

            float oldValue = value;
            ProfileNumericFocus.Draw(customRect, ref value, ref xpBuffers[i],
                "DecaySettingsUI.xpBuffers[" + i + "]",
                0f, SkillDecayService.MaxDaily);
            if (value != oldValue)
            {
                settings.decayDailyByLevel[i] = value;
                lastValues[i] = value;
                if (vanilla > 0f)
                    ratioBuffers[i] = (value / vanilla).ToString("0.####");
            }
        }
    }
}
