using System;
using System.Collections.Generic;
using System.Globalization;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.Gws;

namespace XpCustomizer.UI
{
    internal static class GwsSettingsUI
    {
        // Cons R6 staged numeric-input pattern: keep raw edits until commit.
        // GWS-specific controls and state prevent cross-tab focus collisions.
        private sealed class GwsNumericEditState
        {
            internal readonly string ControlName;
            internal string Buffer;
            internal float LastValue = float.NaN;
            internal bool WasFocused;

            internal GwsNumericEditState(string name)
            {
                ControlName = "ZCN.XpCustomizer.GwsV2." + name;
            }

            internal void Reset()
            {
                Buffer = null;
                LastValue = float.NaN;
                WasFocused = false;
            }
        }
        private static readonly GwsNumericEditState lowAnchorInput = new GwsNumericEditState("LowAnchor");
        private static readonly GwsNumericEditState highAnchorInput = new GwsNumericEditState("HighAnchor");
        private static readonly GwsNumericEditState lowInput = new GwsNumericEditState("LowFactor");
        private static readonly GwsNumericEditState highInput = new GwsNumericEditState("HighFactor");
        private static readonly GwsNumericEditState limitInput = new GwsNumericEditState("DiminishingLimit");
        private static readonly GwsNumericEditState techInput = new GwsNumericEditState("SafetyLimit");
        // UI-only fold state: expanded by default, never saved in ModSettings.
        private static bool showSkills = true;
        internal const string SkillGridRevision = "GWS-SKILLGRID-DEFAULTS-PHASE4BR6-R1-20261103";
        private const int SkillGridColumns = 3;
        private const float SkillCellHeight = 34f;
        private const float SkillCellGapX = 8f;
        private const float SkillCellGapY = 5f;
        private const float SkillCellPaddingX = 9f;
        private const float SkillCheckboxSize = 24f;
#if DEBUG
        private static bool skillGridLogged;
        private static bool translationLogged;
#endif
        // Fixed comparison samples; selected editable anchor is shown separately.
        private static readonly float[] LowPreviewGws = { 0.30f, 0.50f, 0.65f, 0.80f };
        private static readonly float[] HighPreviewGws =
        {
            1.20f, 1.35f, 1.40f, 1.55f, 1.60f, 1.75f,
            1.80f, 2.00f, 3.00f, 5.00f, 7.50f, 10.00f
        };
        // GP1: the Low preview remains 1x4. High is 2x6 at normal width;
        // narrow settings windows use fewer columns rather than clipping text.
        private const int HighPreviewColumns = 6;
        private const float PreviewRowHeight = 28f;
        private const float PreviewRowGap = 5f;
        private const float PreviewMinCellWidth = 140f;
        private static readonly Color lowExampleColor = new Color(0.87f, 0.64f, 0.44f);
        private static readonly Color highExampleColor = new Color(0.62f, 0.80f, 1.00f);

        internal static bool IsGwsControl(string name)
        {
            return name != null && (
                String.Equals(name, lowAnchorInput.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, highAnchorInput.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, lowInput.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, highInput.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, limitInput.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, techInput.ControlName, StringComparison.Ordinal));
        }

        // N1: validate staged GWS drafts before their existing blur handler can
        // clamp an out-of-range value. No setting is changed by this inspection.
        private static bool ValidateNavigationEditor(GwsNumericEditState state,
            string focusedName, float min, float max, out string reason)
        {
            reason = "NONE";
            if (!ProfileNumericNavigation.IsActive(state.ControlName,
                state.WasFocused, focusedName)) return true;
            if (ProfileNumericNavigation.ValidFloat(state.Buffer, min, max, out reason))
                return true;
            ProfileNumericNavigation.RejectControl(state.ControlName);
            return false;
        }

        internal static bool ValidateNavigationDraft(XpCustomizerSettings settings,
            string focusedName, out string reason)
        {
            reason = "NONE";
            if (settings == null) { reason = "MISSING_SETTINGS"; return false; }
            return ValidateNavigationEditor(lowAnchorInput, focusedName,
                    GwsLearningService.LowAnchorMinimum * 100f,
                    GwsLearningService.LowAnchorMaximum * 100f, out reason) &&
                ValidateNavigationEditor(lowInput, focusedName, 0f, 1f, out reason) &&
                ValidateNavigationEditor(highAnchorInput, focusedName,
                    GwsLearningService.HighAnchorMinimum * 100f,
                    GwsLearningService.HighAnchorMaximum * 100f, out reason) &&
                ValidateNavigationEditor(highInput, focusedName, 1f, 5f, out reason) &&
                ValidateNavigationEditor(limitInput, focusedName,
                    settings.gwsHighFactor + 0.01f, 20f, out reason) &&
                ValidateNavigationEditor(techInput, focusedName,
                    settings.gwsHighFactor, 20f, out reason);
        }

        public static void Draw(Listing_Standard listing)
        {
            var s = XpCustomizerMod.Settings;
            if (s == null) return;
#if DEBUG
            if (!translationLogged)
            {
                translationLogged = true;
                Log.Message("[XPC-L10N-L3C2][KEYED_CONS_GWS] version=XPC-L10N-L3C2-CONS-GWS-20261112-R1; screen=GWS; keys=78; sampleKey=XPC_Gws_Title; sample=" +
                    "XPC_Gws_Title".Translate().ToString());
            }
#endif
            SettingsUIHelpers.DrawSectionTitle(listing, "XPC_Gws_Title".Translate().ToString());
            SettingsUIHelpers.DrawDescription(listing,
                "XPC_Gws_Description".Translate().ToString());
            listing.Gap(6f);
            bool enabled = s.gwsEnabled;
            SettingsUIHelpers.DrawRightAdjacentToggle(listing, "XPC_Gws_Enable".Translate().ToString(), ref enabled);
            s.gwsEnabled = enabled;
            if (!enabled)
            {
                SettingsUIHelpers.DrawReferenceText(listing, "XPC_Gws_DisabledHelp".Translate().ToString());
                DrawPresetButtons(listing);
                return;
            }

            // Select affected skills before adjusting the curve applied to them.
            listing.Gap(9f);
            listing.GapLine();
            DrawSkillSelection(listing, s);

            listing.Gap(10f);
            listing.GapLine();
            SettingsUIHelpers.DrawSectionTitle(listing, "XPC_Gws_LowTitle".Translate().ToString());
            bool low = s.gwsLowEnabled;
            SettingsUIHelpers.DrawRightAdjacentToggle(listing, "XPC_Gws_LowEnabled".Translate().ToString(), ref low);
            s.gwsLowEnabled = low;
            if (low)
            {
                DrawCurveChoice(listing, s, false);
                DrawNumber(listing, "XPC_Gws_LowAnchor".Translate().ToString(), ref s.gwsLowAnchor,
                    lowAnchorInput, GwsLearningService.LowAnchorMinimum * 100f,
                    GwsLearningService.LowAnchorMaximum * 100f,
                    GwsLearningService.DefaultLowAnchorGws, true);
                DrawNumber(listing, "XPC_Gws_LowFactor".Translate().ToString(), ref s.gwsLowFactor,
                    lowInput, 0f, 1f, GwsLearningService.DefaultLowFactor, false);
                if (s.gwsLowCurve != (int)GwsCurveMode.Linear)
                    DrawStrength(listing, "XPC_Curve_LowCurvature".Translate().ToString(), ref s.gwsLowCurvature);
                DrawPreviewStrip(listing, s, "XPC_Gws_LowPreview".Translate().ToString(), LowPreviewGws, lowExampleColor, 4);
                DrawAnchorReadout(listing, s.gwsLowAnchor, s.gwsLowFactor);
            }
            listing.Gap(10f);
            listing.GapLine();
            SettingsUIHelpers.DrawSectionTitle(listing, "XPC_Gws_HighTitle".Translate().ToString());
            bool high = s.gwsHighEnabled;
            SettingsUIHelpers.DrawRightAdjacentToggle(listing, "XPC_Gws_HighEnabled".Translate().ToString(), ref high);
            s.gwsHighEnabled = high;
            if (high)
            {
                DrawCurveChoice(listing, s, true);
                DrawNumber(listing, "XPC_Gws_HighAnchor".Translate().ToString(), ref s.gwsHighAnchor,
                    highAnchorInput, GwsLearningService.HighAnchorMinimum * 100f,
                    GwsLearningService.HighAnchorMaximum * 100f,
                    GwsLearningService.DefaultHighAnchorGws, true);
                DrawNumber(listing, "XPC_Gws_HighFactor".Translate().ToString(), ref s.gwsHighFactor,
                    highInput, 1f, 5f, GwsLearningService.DefaultHighFactor, false);
                if (s.gwsHighCurve != (int)GwsCurveMode.Linear)
                    DrawStrength(listing, "XPC_Curve_HighCurvature".Translate().ToString(), ref s.gwsHighCurvature);
                if (s.gwsHighCurve == (int)GwsCurveMode.Diminishing)
                {
                    DrawNumber(listing, "XPC_Gws_DiminishingLimit".Translate().ToString(), ref s.gwsHighDiminishingLimit,
                        limitInput, s.gwsHighFactor + 0.01f, 20f,
                        GwsLearningService.DefaultDiminishingLimit, false);
                    SettingsUIHelpers.DrawReferenceText(listing,
                        "XPC_Gws_LimitHelp".Translate().ToString());
                }
                else
                {
                    DrawNumber(listing, "XPC_Gws_SafetyLimit".Translate().ToString(), ref s.gwsHighTechnicalLimit,
                        techInput, s.gwsHighFactor, 20f,
                        GwsLearningService.DefaultAcceleratingLimit, false);
                    SettingsUIHelpers.DrawReferenceText(listing,
                        "XPC_Gws_SafetyHelp".Translate().ToString());
                }
                DrawPreviewStrip(listing, s, "XPC_Gws_HighPreview".Translate().ToString(), HighPreviewGws, highExampleColor, HighPreviewColumns);
                DrawAnchorReadout(listing, s.gwsHighAnchor, s.gwsHighFactor);
            }

            listing.Gap(10f);
            listing.GapLine();
            SettingsUIHelpers.DrawSectionTitle(listing, "XPC_Gws_ReadingTitle".Translate().ToString());
            bool reading = s.gwsApplyToReading;
            SettingsUIHelpers.DrawRightAdjacentToggle(listing, "XPC_Gws_ReadingEnabled".Translate().ToString(), ref reading);
            s.gwsApplyToReading = reading;
            SettingsUIHelpers.DrawReferenceText(listing,
                "XPC_Gws_ReadingHelp".Translate().ToString());
            DrawPresetButtons(listing);
        }


        // UI-only three-column grid: no changes to settings storage or XP logic.
        // Expanded by default; user can collapse it without writing new save keys.
        private static void DrawSkillSelection(Listing_Standard listing, XpCustomizerSettings s)
        {
            string title = (showSkills ? "▼ " : "▶ ") + "XPC_Gws_SkillsExpand".Translate().ToString();
            if (listing.ButtonText(title))
                showSkills = !showSkills;
            if (!showSkills)
                return;

            SettingsUIHelpers.DrawReferenceText(listing,
                "XPC_Gws_SkillsHelp".Translate().ToString());
            listing.Gap(3f);

            // 3 columns x 4 rows for the twelve vanilla skills.
            // Use the existing source enumeration: never alter skill ordering or definitions.
            const int rowCount = 4;
            Rect grid = listing.GetRect(rowCount * SkillCellHeight + (rowCount - 1) * SkillCellGapY);
            float width = (grid.width - (SkillGridColumns - 1) * SkillCellGapX) / SkillGridColumns;
            int index = 0;
            foreach (string skillName in GwsLearningService.Skills)
            {
                int column = index % SkillGridColumns;
                int row = index / SkillGridColumns;
                if (row >= rowCount)
                    break; // Guard against accidental future changes to the 12-skill list.

                Rect cell = new Rect(grid.x + column * (width + SkillCellGapX),
                    grid.y + row * (SkillCellHeight + SkillCellGapY), width, SkillCellHeight);
                DrawSkillCell(cell, skillName, s);
                index++;
            }
#if DEBUG
            if (!skillGridLogged)
            {
                skillGridLogged = true;
                Log.Message("[XP Customizer] GWS_SKILLGRID_UI=" + SkillGridRevision +
                    "; columns=3; rows=4; rendered=" + index +
                    "; defaultExpanded=True; collapsible=True; checkbox=cell-right; clickTargets=separate; " +
                    "savedSkillSettings=unchanged; GwsLearning=unchanged; Harmony=unchanged");
            }
#endif
            listing.Gap(6f);
            if (listing.ButtonText("XPC_Gws_SkillsReset".Translate().ToString()))
                s.gwsSkillEnabled = new Dictionary<string, bool>();
        }

        // The label area and the vanilla checkbox are non-overlapping click targets.
        // A single click changes exactly one skill's existing setting.
        private static void DrawSkillCell(Rect rect, string skillName, XpCustomizerSettings s)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.22f, 0.22f, 0.22f, 0.65f));
            Widgets.DrawHighlightIfMouseover(rect);

            float checkX = rect.xMax - SkillCellPaddingX - SkillCheckboxSize;
            float checkY = rect.y + (rect.height - SkillCheckboxSize) * 0.5f;
            Rect labelRect = new Rect(rect.x + SkillCellPaddingX, rect.y,
                Mathf.Max(0f, checkX - rect.x - SkillCellPaddingX - 5f), rect.height);

            bool enabled = GwsLearningService.IsSkillEnabled(s, skillName);
            bool original = enabled;
            if (Widgets.ButtonInvisible(labelRect))
                enabled = !enabled;

            GameFont originalFont = Text.Font;
            TextAnchor originalAnchor = Text.Anchor;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                SkillDef skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(skillName);
                Widgets.Label(labelRect, skillDef != null ? skillDef.LabelCap.ToString() : skillName);
            }
            finally
            {
                Text.Font = originalFont;
                Text.Anchor = originalAnchor;
            }
            Widgets.Checkbox(checkX, checkY, ref enabled, SkillCheckboxSize);
            if (enabled != original)
                GwsLearningService.SetSkillEnabled(s, skillName, enabled);
        }

        // Always visible, even when the GWS master is OFF.
        private static void DrawPresetButtons(Listing_Standard listing)
        {
            listing.Gap(10f);
            Rect row = listing.GetRect(36f);
            const float gap = 8f;
            float width = (row.width - gap) / 2f;
            Rect defaults = new Rect(row.x, row.y, width, row.height);
            Rect vanilla = new Rect(row.x + width + gap, row.y, width, row.height);
            if (Widgets.ButtonText(defaults, "XPC_Gws_ResetDefaults".Translate().ToString()))
            {
                SettingsPresetService.ResetGwsToModDefaults();
                NotifySettingsExternallyChanged();
            }
            if (Widgets.ButtonText(vanilla, "XPC_Gws_ResetVanilla".Translate().ToString()))
            {
                SettingsPresetService.ResetGwsToVanilla();
                NotifySettingsExternallyChanged();
            }
            TooltipHandler.TipRegion(defaults,
                "XPC_Gws_ResetDefaultsTip".Translate().ToString());
            TooltipHandler.TipRegion(vanilla,
                "XPC_Gws_ResetVanillaTip".Translate().ToString());
            listing.Gap(12f);
        }

        private static void DrawCurveChoice(Listing_Standard listing, XpCustomizerSettings s, bool high)
        {
            Rect row = listing.GetRect(32f);
            const float gap = 5f;
            float w = (row.width - gap * 2f) / 3f;
            int current = high ? s.gwsHighCurve : s.gwsLowCurve;
            DrawMode(new Rect(row.x, row.y, w, 30f), "XPC_Curve_Diminishing".Translate().ToString(), 0, current, s, high);
            DrawMode(new Rect(row.x + w + gap, row.y, w, 30f), "XPC_Curve_Linear".Translate().ToString(), 1, current, s, high);
            DrawMode(new Rect(row.x + (w + gap) * 2f, row.y, w, 30f), "XPC_Curve_Accelerating".Translate().ToString(), 2, current, s, high);
        }
        private static void DrawMode(Rect rect, string label, int id, int current,
            XpCustomizerSettings s, bool high)
        {
            if (Widgets.ButtonText(rect, (current == id ? "✓ " : "") + label))
            {
                if (high) s.gwsHighCurve = id; else s.gwsLowCurve = id;
            }
        }
        // Same staged edit policy as Cons R6, adapted for GWS fractions.
        // An incomplete edit is never clamped while the control has focus.
        internal static bool TryParseNumericValue(string raw, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value) &&
                !float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return false;
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
        internal static float ResolveNumericCommit(string raw, float prior, float min, float max)
        {
            float candidate;
            return TryParseNumericValue(raw, out candidate)
                ? Mathf.Clamp(candidate, min, max)
                : Mathf.Clamp(prior, min, max);
        }
        private static void DrawNumber(Listing_Standard listing, string label,
            ref float stored, GwsNumericEditState state, float min, float max,
            float fallback, bool gwsPercent)
        {
            float scaled = gwsPercent ? stored * 100f : stored;
            float defaultValue = gwsPercent ? fallback * 100f : fallback;
            scaled = GwsLearningService.NormalizeNumber(scaled, defaultValue, min, max);
            if (!state.WasFocused &&
                (state.Buffer == null || float.IsNaN(state.LastValue) ||
                 !Mathf.Approximately(state.LastValue, scaled)))
                state.Buffer = scaled.ToString("0.####", CultureInfo.InvariantCulture);

            // Keep all GWS numeric fields aligned, even when a mode hides one.
            float groupWidth = NumericRowLayout.MaxTranslatedWidth(
                "XPC_Gws_LowAnchor", "XPC_Gws_LowFactor",
                "XPC_Gws_HighAnchor", "XPC_Gws_HighFactor",
                "XPC_Gws_DiminishingLimit", "XPC_Gws_SafetyLimit");
            NumericRowLayout.Placement geometry = NumericRowLayout.Allocate(
                listing, label, 104f, 0f, 30f, 28f, groupWidth);
            Widgets.Label(geometry.Label, label);
            Rect fieldRect = geometry.Field;
            Event evt = Event.current;
            if (state.WasFocused && evt != null && evt.type == EventType.KeyDown &&
                (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter))
            {
                GUI.FocusControl(null);
                evt.Use();
            }
            GUI.SetNextControlName(state.ControlName);
            state.Buffer = Widgets.TextField(fieldRect, state.Buffer ?? string.Empty);
            bool focused = string.Equals(GUI.GetNameOfFocusedControl(),
                state.ControlName, StringComparison.Ordinal);
            float candidate;
            if (focused)
            {
                if (TryParseNumericValue(state.Buffer, out candidate) &&
                    candidate >= min && candidate <= max &&
                    !Mathf.Approximately(candidate, scaled))
                    stored = gwsPercent ? candidate / 100f : candidate;
            }
            else if (state.WasFocused)
            {
                float committed = ResolveNumericCommit(state.Buffer, scaled, min, max);
                if (!Mathf.Approximately(committed, scaled))
                    stored = gwsPercent ? committed / 100f : committed;
                state.Buffer = committed.ToString("0.####", CultureInfo.InvariantCulture);
            }
            state.WasFocused = focused;
            state.LastValue = gwsPercent ? stored * 100f : stored;
        }
        private static void DrawStrength(Listing_Standard listing, string label, ref float amount)
        {
            amount = GwsLearningService.NormalizeNumber(amount, 1f, 0f, 2f);
            Rect header = listing.GetRect(27f);
            Widgets.Label(new Rect(header.x, header.y, header.width - 100f, 27f), label);
            Widgets.Label(new Rect(header.xMax - 90f, header.y, 90f, 27f),
                Mathf.RoundToInt(amount * 100f) + "%");
            float changed = Widgets.HorizontalSlider(listing.GetRect(26f),
                amount * 100f, 0f, 200f, false, null, null, null, 5f);
            amount = Mathf.Clamp(Mathf.Round(changed / 5f) * 5f / 100f, 0f, 2f);
            SettingsUIHelpers.DrawReferenceText(listing,
                "XPC_Curve_GwsCurvatureHelp".Translate().ToString());
        }
        // GP1 layout math is detached and side-effect free for Debug regression tests.
        // High: 6 columns on normal-width settings. Shrink to 4/3/2 on narrow UI;
        // Low: keep the original four-sample single-row layout.
        internal static int ResolveHighPreviewColumns(float availableWidth)
        {
            if (float.IsNaN(availableWidth) || float.IsInfinity(availableWidth) ||
                availableWidth <= 0f) return 2;
            if (availableWidth >= PreviewMinCellWidth * 6f) return 6;
            if (availableWidth >= PreviewMinCellWidth * 4f) return 4;
            if (availableWidth >= PreviewMinCellWidth * 3f) return 3;
            return 2;
        }

        internal static float PreviewGridHeight(int sampleCount, int columns)
        {
            if (sampleCount <= 0 || columns <= 0) return 0f;
            int rows = (sampleCount + columns - 1) / columns;
            return rows * PreviewRowHeight + (rows - 1) * PreviewRowGap;
        }

        internal static Rect PreviewCellRect(Rect grid, int index, int columns)
        {
            float width = grid.width / columns;
            int column = index % columns;
            int row = index / columns;
            return new Rect(grid.x + width * column,
                grid.y + row * (PreviewRowHeight + PreviewRowGap),
                width, PreviewRowHeight);
        }

        // Compact UI-only samples sit directly below the curve they illustrate.
        // The Listing reserves ALL rows before painting; nothing is cached/saved.
        private static void DrawPreviewStrip(Listing_Standard listing, XpCustomizerSettings s,
            string title, float[] samples, Color accent, int preferredColumns)
        {
            listing.Gap(4f);
            SettingsUIHelpers.DrawReferenceText(listing,
                string.Format("XPC_Gws_PreviewRelation".Translate().ToString(), title));
            int columns = preferredColumns == HighPreviewColumns
                ? ResolveHighPreviewColumns(listing.ColumnWidth)
                : preferredColumns;
            Rect grid = listing.GetRect(PreviewGridHeight(samples.Length, columns));
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWordWrap = Text.WordWrap;
            Color oldColor = GUI.color;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                for (int i = 0; i < samples.Length; i++)
                {
                    float gws = samples[i];
                    float factor = GwsLearningService.Calculate(gws, s);
                    Rect cell = PreviewCellRect(grid, i, columns);
                    float labelWidth = Mathf.Min(64f, cell.width * 0.42f);
                    GUI.color = oldColor;
                    Widgets.Label(new Rect(cell.x, cell.y, labelWidth, cell.height),
                        (gws * 100f).ToString("0") + "%");
                    GUI.color = accent;
                    Widgets.Label(new Rect(cell.x + labelWidth, cell.y,
                        cell.width - labelWidth, cell.height), "x" + factor.ToString("0.###"));
                }
            }
            finally
            {
                GUI.color = oldColor;
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                Text.WordWrap = oldWordWrap;
            }
        }
        // Show the exact user-selected anchor even when it is absent from
        // the fixed comparison samples (GWS preview, not pawn runtime XP).
        private static void DrawAnchorReadout(Listing_Standard listing,
            float anchorGws, float anchorFactor)
        {
            SettingsUIHelpers.DrawReferenceText(listing,
                string.Format("XPC_Gws_SelectedAnchor".Translate().ToString(),
                (anchorGws * 100f).ToString("0.#"), anchorFactor.ToString("0.###")));
        }
        public static void NotifySettingsExternallyChanged()
        {
            lowAnchorInput.Reset(); highAnchorInput.Reset();
            lowInput.Reset(); highInput.Reset(); limitInput.Reset(); techInput.Reset();
        }
    }
}
