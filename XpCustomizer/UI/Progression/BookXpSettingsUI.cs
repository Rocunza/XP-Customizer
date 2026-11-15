using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.BookXp;

namespace XpCustomizer.UI
{
    /// <summary>
    /// R4: Decay-style, bidirectionally editable Book XP tables.
    /// Only existing saved XP/hour and multiplier settings are changed by this UI.
    /// The R3 master/child gameplay gates, fold and migration remain untouched.
    /// </summary>
    internal static class BookXpSettingsUI
    {
        private const float TableMaxWidth = 760f;
        private const float HeaderHeight = 34f;
        private const float RowHeight = 36f;
        private const float TablePadding = 6f;
        private const int VisibleSkillRows = 8;
        private static readonly Color VanillaColor = new Color(0.96f, 0.82f, 0.48f, 1f);
        private static readonly Dictionary<string, NumericInputState> ratioInputs =
            new Dictionary<string, NumericInputState>();
        private static readonly Dictionary<string, NumericInputState> customInputs =
            new Dictionary<string, NumericInputState>();

        private static bool skillRowsExpanded;
        private static bool revisionLogged;
        private static Vector2 skillTableScrollPosition = Vector2.zero;

        public static void NotifySettingsExternallyChanged()
        {
            ratioInputs.Clear();
            customInputs.Clear();
        }

        public static void Draw(Listing_Standard listing)
        {
            if (!revisionLogged && Event.current.type == EventType.Repaint)
            {
                revisionLogged = true;
                Log.Message("[XP Customizer] UI_REVISION=BOOKXP-V1-R4-DECAY-TABLE-20261102; " +
                    "columns=VanillaXP/h*Ratio=CustomXP/h; custom=editable; " +
                    "skillBaseline=Normal-single-skill-250; savedSchema=V1; R3master=preserved");
            }

            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            if (settings == null) return;

            listing.Gap(8f);
            SettingsUIHelpers.DrawSectionTitle(listing, "XPC_L3B3_BookTitle".Translate().ToString());
            bool master = settings.bookXpEnabled;
            SettingsUIHelpers.DrawRightAdjacentToggle(listing, "XPC_L3B3_BookEnable".Translate().ToString(), ref master);
            settings.bookXpEnabled = master;

            if (!master)
            {
                SettingsUIHelpers.DrawReferenceText(listing,
                    "XPC_L3B3_BookOffNotice".Translate().ToString());
                listing.Gap(7f);
                return;
            }

            SettingsUIHelpers.DrawDescription(listing,
                "XPC_L3B3_BookDesc".Translate().ToString());
            listing.Gap(6f);

            // All-books passive Intellectual XP. Stored as XP/hour, never as a new ratio key.
            SettingsUIHelpers.DrawSectionTitle(listing, "XPC_L3B3_BookIntTitle".Translate().ToString());
            bool intellectual = settings.bookIntellectualEnabled;
            SettingsUIHelpers.DrawRightAdjacentToggle(listing, "XPC_L3B3_BookIntEnable".Translate().ToString(), ref intellectual);
            settings.bookIntellectualEnabled = intellectual;
            SettingsUIHelpers.DrawReferenceText(listing,
                "XPC_L3B3_BookIntBase".Translate().ToString());
            DrawTable(listing, TableKind.Intellectual, settings);
            listing.Gap(8f);

            SettingsUIHelpers.DrawSectionTitle(listing, "XPC_L3B3_BookQualityTitle".Translate().ToString());
            bool quality = settings.bookQualityEnabled;
            SettingsUIHelpers.DrawRightAdjacentToggle(listing, "XPC_L3B3_BookQualityEnable".Translate().ToString(), ref quality);
            settings.bookQualityEnabled = quality;
            SettingsUIHelpers.DrawReferenceText(listing,
                "XPC_L3B3_BookQualityDesc".Translate().ToString());
            DrawTable(listing, TableKind.Quality, settings);
            if (listing.ButtonText("XPC_L3B3_BookQualityReset".Translate().ToString()))
            {
                settings.bookQualityMultipliers = new Dictionary<string, float>();
                NotifySettingsExternallyChanged();
            }
            listing.Gap(8f);

            string skillHeader = (skillRowsExpanded ? "▼ " : "▶ ") +
                "XPC_L3B3_BookSkillTitle".Translate().ToString() +
                (settings.bookSkillEnabled ?
                    "XPC_L3B3_BookOnTag".Translate().ToString() :
                    "XPC_L3B3_BookOffTag".Translate().ToString());
            if (listing.ButtonText(skillHeader)) skillRowsExpanded = !skillRowsExpanded;
            if (skillRowsExpanded)
            {
                bool skill = settings.bookSkillEnabled;
                SettingsUIHelpers.DrawRightAdjacentToggle(listing, "XPC_L3B3_BookSkillEnable".Translate().ToString(), ref skill);
                settings.bookSkillEnabled = skill;
                SettingsUIHelpers.DrawReferenceText(listing,
                    "XPC_L3B3_BookSkillDesc".Translate().ToString());
                DrawTable(listing, TableKind.Skill, settings);
                if (listing.ButtonText("XPC_L3B3_BookSkillReset".Translate().ToString()))
                {
                    settings.bookSkillMultipliers = new Dictionary<string, float>();
                    NotifySettingsExternallyChanged();
                }
            }
            listing.Gap(7f);
            if (listing.ButtonText("XPC_L3B3_BookAllReset".Translate().ToString()))
            {
                SettingsPresetService.ResetBookXpToVanilla();
                NotifySettingsExternallyChanged();
            }
            listing.Gap(10f);
        }

        private enum TableKind { Intellectual, Quality, Skill }

        private static void DrawTable(Listing_Standard listing, TableKind kind,
            XpCustomizerSettings settings)
        {
            int count = kind == TableKind.Intellectual ? 1 :
                kind == TableKind.Quality ? 7 : BookXpService.VanillaSkillNames.Length;
            bool scroll = kind == TableKind.Skill;
            int visible = scroll ? VisibleSkillRows : count;
            float height = HeaderHeight + visible * RowHeight + 8f;
            Rect slot = listing.GetRect(height);
            Rect table = new Rect(slot.x, slot.y,
                Mathf.Min(TableMaxWidth, slot.width), height);
            Widgets.DrawBox(table);

            float headerWidth = table.width - 24f;
            Rect header = new Rect(table.x + TablePadding,
                table.y + 4f, headerWidth, HeaderHeight - 4f);
            // Reserve scrollbar width for the header as well, so headers and rows align.
            float viewWidth = table.width - 24f - (scroll ? 18f : 0f);
            TableColumns columns = new TableColumns(viewWidth);
            DrawHeader(header, columns,
                kind == TableKind.Quality ? "XPC_L3B3_BookHeaderQuality".Translate().ToString() :
                kind == TableKind.Skill ? "XPC_L3B3_HeaderSkill".Translate().ToString() :
                "XPC_L3B3_BookHeaderSource".Translate().ToString());

            Rect body = new Rect(table.x + TablePadding, table.y + HeaderHeight,
                table.width - 2f * TablePadding, visible * RowHeight);
            if (scroll)
            {
                Rect viewport = new Rect(0f, 0f, viewWidth, count * RowHeight + 2f);
                Widgets.BeginScrollView(body, ref skillTableScrollPosition, viewport);
                for (int i = 0; i < count; i++)
                    DrawTableRow(kind, i, new Rect(0f, i * RowHeight, viewWidth, RowHeight),
                        columns, settings);
                Widgets.EndScrollView();
            }
            else
            {
                for (int i = 0; i < count; i++)
                    DrawTableRow(kind, i,
                        new Rect(body.x, body.y + i * RowHeight, viewWidth, RowHeight),
                        columns, settings);
            }
        }

        private struct TableColumns
        {
            public readonly float Label;
            public readonly float Vanilla;
            public readonly float Times;
            public readonly float Ratio;
            public readonly float Equals;
            public readonly float Custom;
            public readonly float RatioWidth;
            public readonly float CustomWidth;

            public TableColumns(float width)
            {
                Label = 0f;
                float labelWidth = width * 0.22f;
                Vanilla = labelWidth;
                float vanillaWidth = width * 0.22f;
                Times = Vanilla + vanillaWidth;
                float symbolWidth = 22f;
                Ratio = Times + symbolWidth;
                RatioWidth = width * 0.23f;
                Equals = Ratio + RatioWidth;
                Custom = Equals + symbolWidth;
                CustomWidth = Mathf.Max(54f, width - Custom - 4f);
            }
        }

        private static void DrawHeader(Rect row, TableColumns cols, string first)
        {
            Widgets.Label(new Rect(row.x + cols.Label, row.y, cols.Vanilla - cols.Label, 26f), first);
            Color previous = GUI.color;
            GUI.color = VanillaColor;
            Widgets.Label(new Rect(row.x + cols.Vanilla, row.y, cols.Times - cols.Vanilla,
                26f), "XPC_L3B3_BookHeaderVanilla".Translate().ToString());
            GUI.color = previous;
            Widgets.Label(new Rect(row.x + cols.Ratio, row.y, cols.RatioWidth, 26f), "XPC_L3B3_BookHeaderRatio".Translate().ToString());
            Widgets.Label(new Rect(row.x + cols.Custom, row.y, cols.CustomWidth + 8f,
                26f), "XPC_L3B3_BookHeaderCustom".Translate().ToString());
        }

        private static void DrawTableRow(TableKind kind, int index, Rect row,
            TableColumns cols, XpCustomizerSettings settings)
        {
            string key;
            string label;
            float vanilla;
            float ratio;
            if (kind == TableKind.Intellectual)
            {
                key = "book.intellectual";
                label = "XPC_L3B3_BookIntLabel".Translate().ToString();
                vanilla = BookXpService.VanillaIntellectualXpPerHour;
                ratio = BookXpService.NormalizeIntellectual(settings.bookIntellectualXpPerHour) / vanilla;
            }
            else if (kind == TableKind.Quality)
            {
                QualityCategory quality = (QualityCategory)index;
                key = "book.quality." + BookXpService.QualityKey(quality);
                label = ("XPC_L3B3_Quality_" + quality.ToString()).Translate().ToString();
                vanilla = BookXpService.VanillaQualityXpPerHour(quality);
                ratio = BookXpService.GetQualityMultiplier(settings, quality);
            }
            else
            {
                string skillName = BookXpService.VanillaSkillNames[index];
                key = "book.skill." + skillName;
                SkillDef def = DefDatabase<SkillDef>.GetNamedSilentFail(skillName);
                label = def != null ? def.LabelCap.ToString() : skillName;
                vanilla = BookXpService.VanillaQualityXpPerHour(QualityCategory.Normal);
                ratio = BookXpService.GetSkillMultiplier(settings, skillName);
            }

            float editedRatio = DrawEquationRow(row, cols, key, label, vanilla, ratio);
            if (Mathf.Approximately(ratio, editedRatio)) return;

            if (kind == TableKind.Intellectual)
                settings.bookIntellectualXpPerHour = BookXpService.NormalizeIntellectual(
                    editedRatio * vanilla);
            else if (kind == TableKind.Quality)
                BookXpService.SetQualityMultiplier(settings, (QualityCategory)index, editedRatio);
            else
                BookXpService.SetSkillMultiplier(settings,
                    BookXpService.VanillaSkillNames[index], editedRatio);
        }

        private static NumericInputState GetInput(Dictionary<string, NumericInputState> states,
            string key)
        {
            NumericInputState state;
            if (!states.TryGetValue(key, out state))
            {
                state = new NumericInputState();
                states.Add(key, state);
            }
            return state;
        }

        // Ratio and Custom XP/h are two editing surfaces for ONE saved value.
        // No second setting is persisted, so the table cannot develop divergent values.
        private static float DrawEquationRow(Rect row, TableColumns cols, string key,
            string label, float vanilla, float savedRatio)
        {
            NumericInputState ratioInput = GetInput(ratioInputs, key);
            NumericInputState customInput = GetInput(customInputs, key);
            if (float.IsNaN(ratioInput.lastValue) ||
                !Mathf.Approximately(savedRatio, ratioInput.lastValue))
            {
                ratioInput.buffer = savedRatio.ToString("0.####");
                customInput.buffer = (vanilla * savedRatio).ToString("0.####");
            }

            Widgets.DrawHighlightIfMouseover(row);
            Widgets.Label(new Rect(row.x + cols.Label, row.y + 5f,
                cols.Vanilla - cols.Label - 5f, 27f), label);
            Color old = GUI.color;
            GUI.color = VanillaColor;
            Widgets.Label(new Rect(row.x + cols.Vanilla, row.y + 5f,
                cols.Times - cols.Vanilla - 3f, 27f), vanilla.ToString("0.##"));
            GUI.color = old;
            Widgets.Label(new Rect(row.x + cols.Times, row.y + 5f, 20f, 27f), "×");
            Widgets.Label(new Rect(row.x + cols.Equals, row.y + 5f, 20f, 27f), "=");

            float nextRatio = savedRatio;
            float maxRatio = BookXpService.MaxMultiplier;
            Rect ratioRect = new Rect(row.x + cols.Ratio, row.y + 2f,
                Mathf.Max(46f, cols.RatioWidth - 9f), 29f);
            ProfileNumericFocus.Draw(ratioRect, ref nextRatio, ratioInput,
                BookXpService.MinMultiplier, maxRatio);
            nextRatio = BookXpService.NormalizeMultiplier(nextRatio);

            if (!Mathf.Approximately(savedRatio, nextRatio))
                customInput.buffer = (vanilla * nextRatio).ToString("0.####");

            float nextCustom = vanilla * nextRatio;
            float beforeCustom = nextCustom;
            Rect customRect = new Rect(row.x + cols.Custom, row.y + 2f,
                cols.CustomWidth, 29f);
            ProfileNumericFocus.Draw(customRect, ref nextCustom, customInput,
                0f, vanilla * maxRatio);
            if (!Mathf.Approximately(beforeCustom, nextCustom))
            {
                nextRatio = vanilla > 0f
                    ? BookXpService.NormalizeMultiplier(nextCustom / vanilla) : 0f;
                ratioInput.buffer = nextRatio.ToString("0.####");
            }
            ratioInput.lastValue = nextRatio;
            customInput.lastValue = nextRatio * vanilla;
            return nextRatio;
        }
    }
}
