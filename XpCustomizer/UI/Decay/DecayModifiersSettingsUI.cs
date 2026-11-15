using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using XpCustomizer.Decay;

namespace XpCustomizer.UI
{
    /// <summary>D3: memory percentages without toggle switches and per-skill editor.</summary>
    internal static class DecayModifiersSettingsUI
    {
        private static bool skillExpanded;
        private static bool compactToggleRevisionLogged;
        private static string greatBuffer;
        private static string perfectBuffer;
        private static float lastGreat = float.NaN;
        private static float lastPerfect = float.NaN;
        private static readonly Dictionary<string, NumericInputState> skillBuffers =
            new Dictionary<string, NumericInputState>();

        internal static void NotifySettingsExternallyChanged()
        {
            greatBuffer = perfectBuffer = null;
            lastGreat = lastPerfect = float.NaN;
            skillBuffers.Clear();
        }

        // Display memory modifiers at the TOP of the Decay tab.
        internal static void DrawMemorySection(Listing_Standard listing, XpCustomizerSettings settings)
        {
            SettingsUIHelpers.DrawSectionTitle(listing, "XPC_Decay_MemoryTitle".Translate().ToString());
            SettingsUIHelpers.DrawDescription(listing,
                "XPC_Decay_MemoryDescription".Translate().ToString());
            listing.Gap(6f);
            DrawMemoryRow(listing, "XPC_Decay_GreatMemory".Translate().ToString(),
                ref settings.greatMemoryDecayReduction, ref greatBuffer, ref lastGreat, 50f,
                "DecayModifiersSettingsUI.greatBuffer");
            DrawMemoryRow(listing, "XPC_Decay_PerfectMemory".Translate().ToString(),
                ref settings.perfectMemoryDecayReduction, ref perfectBuffer, ref lastPerfect, 100f,
                "DecayModifiersSettingsUI.perfectBuffer");
            SettingsUIHelpers.DrawReferenceText(listing,
                "XPC_Decay_MemoryRange".Translate().ToString());
        }

        // Skill multipliers remain below the independent level-decay table.
        internal static void DrawSkillSection(Listing_Standard listing, XpCustomizerSettings settings)
        {
            if (!compactToggleRevisionLogged)
            {
                compactToggleRevisionLogged = true;
                Log.Message("[XP Customizer][Decay UI] UI_REVISION=DECAY-SKILL-TOGGLE-COMPACT-R1-20261101; source=UI-only; gameplay=unchanged");
            }
            listing.Gap(12f);
            listing.GapLine();
            SettingsUIHelpers.DrawSectionTitle(listing, "XPC_Decay_SkillTitle".Translate().ToString());
            int changed = DecayModifiersService.ModifiedCount(settings);
            Rect expand = listing.GetRect(32f);
            string title = (skillExpanded ? "▼ " : "▶ ") +
                "XPC_Decay_SkillTitle".Translate().ToString() +
                (changed > 0 ? string.Format("XPC_Decay_SkillModified".Translate().ToString(), changed) : "");
            if (Widgets.ButtonText(expand, title)) skillExpanded = !skillExpanded;
            if (!skillExpanded) return;

            listing.Gap(7f);
            bool enabled = settings.decaySkillMultipliersEnabled;
            DrawCompactToggle(listing, "XPC_Decay_SkillEnable".Translate().ToString(), ref enabled);
            settings.decaySkillMultipliersEnabled = enabled;
            SettingsUIHelpers.DrawDescription(listing,
                "XPC_Decay_SkillDescription".Translate().ToString());
            SettingsUIHelpers.DrawReferenceText(listing,
                "XPC_Decay_SkillRange".Translate().ToString());
            listing.Gap(7f);
            Rect header = listing.GetRect(27f);
            float col1 = header.width * 0.34f;
            float col2 = header.width * 0.20f;
            Widgets.Label(new Rect(header.x, header.y, col1, header.height), "XPC_Decay_SkillHeader".Translate().ToString());
            Widgets.Label(new Rect(header.x + col1, header.y, col2, header.height), "XPC_Decay_MultiplierHeader".Translate().ToString());
            Widgets.Label(new Rect(header.x + col1 + col2, header.y,
                header.width - col1 - col2, header.height), "XPC_Decay_PreviewHeader".Translate().ToString());

            var defs = new List<SkillDef>(DefDatabase<SkillDef>.AllDefsListForReading);
            defs.Sort((a, b) => string.Compare(a == null ? "" : (a.label ?? a.defName),
                b == null ? "" : (b.label ?? b.defName), StringComparison.OrdinalIgnoreCase));
            foreach (SkillDef def in defs)
            {
                if (def == null || string.IsNullOrEmpty(def.defName)) continue;
                Rect row = listing.GetRect(34f);
                if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                DrawSkillRow(row, def, settings);
            }
            listing.Gap(5f);
            if (listing.ButtonText("XPC_Decay_SkillReset".Translate().ToString()))
            {
                settings.decaySkillMultipliers = new Dictionary<string, float>();
                skillBuffers.Clear();
            }
        }

        // Keep the decay ON/OFF control next to its label, matching Progression.
        private static void DrawCompactToggle(Listing_Standard listing, string label, ref bool value)
        {
            Rect row = listing.GetRect(28f);
            float labelWidth = Mathf.Min(Text.CalcSize(label).x + 6f, row.width - 40f);
            Rect labelRect = new Rect(row.x, row.y, labelWidth, row.height);
            Widgets.Label(labelRect, label);

            float checkSize = 24f;
            float checkX = labelRect.xMax + 6f;
            Rect checkRect = new Rect(checkX, row.y + (row.height - checkSize) / 2f, checkSize, checkSize);
            Widgets.Checkbox(checkRect.x, checkRect.y, ref value, checkSize);

            if (Widgets.ButtonInvisible(labelRect))
            {
                value = !value;
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
            }
        }

        private static void DrawMemoryRow(Listing_Standard listing, string label,
            ref float reduction, ref string buffer,
            ref float previousValue, float vanillaReduction, string focusIdentity)
        {
            // Two memory entries share one nearby, fixed input column.
            // No extra enable checkbox: a neutral percentage is the vanilla effect.
            float groupWidth = NumericRowLayout.MaxTranslatedWidth(
                "XPC_Decay_GreatMemory", "XPC_Decay_PerfectMemory");
            NumericRowLayout.Placement geometry = NumericRowLayout.Allocate(
                listing, label, 107f, 35f, 36f, 29f, groupWidth);
            Rect field = geometry.Field;
            Widgets.Label(geometry.Label, label);
            if (buffer == null || !Mathf.Approximately(previousValue, reduction))
            {
                buffer = reduction.ToString("0.00");
                previousValue = reduction;
            }
            float edited = reduction;
            ProfileNumericFocus.Draw(field, ref edited, ref buffer,
                focusIdentity, 0f, 100f);
            if (!Mathf.Approximately(edited, reduction))
                reduction = DecayModifiersService.NormalizeReduction(edited, vanillaReduction);
            previousValue = reduction;
            Widgets.Label(geometry.Suffix, "%");
        }

        private static void DrawSkillRow(Rect row, SkillDef def, XpCustomizerSettings settings)
        {
            float col1 = row.width * 0.34f;
            float col2 = row.width * 0.20f;
            float value = DecayModifiersService.ConfiguredSkillMultiplier(settings, def.defName);
            NumericInputState state;
            if (!skillBuffers.TryGetValue(def.defName, out state))
            {
                state = new NumericInputState();
                skillBuffers[def.defName] = state;
            }
            if (!float.IsNaN(state.lastValue) && !Mathf.Approximately(state.lastValue, value))
                state.buffer = null;
            Widgets.Label(new Rect(row.x, row.y + 4f, col1 - 10f, 27f), def.LabelCap);
            Rect field = new Rect(row.x + col1, row.y + 2f,
                Mathf.Min(col2 - 16f, 125f), 29f);
            float edited = value;
            ProfileNumericFocus.Draw(field, ref edited, state,
                DecayModifiersService.MinimumSkillMultiplier,
                DecayModifiersService.MaximumSkillMultiplier);
            if (!Mathf.Approximately(edited, value))
                DecayModifiersService.SetSkillMultiplier(settings, def.defName, edited);
            state.lastValue = DecayModifiersService.ConfiguredSkillMultiplier(settings, def.defName);

            float baseDaily = settings.decayByLevelEnabled &&
                settings.decayDailyByLevel != null && settings.decayDailyByLevel.Count == 21
                ? settings.decayDailyByLevel[20] : SkillDecayService.VanillaDaily[20];
            float preview = Mathf.Min(SkillDecayService.MaxDaily, baseDaily * state.lastValue);
            Widgets.Label(new Rect(row.x + col1 + col2, row.y + 4f,
                row.width - col1 - col2, 27f), string.Format("XPC_Decay_SkillPreviewValue".Translate().ToString(), preview.ToString("N2")));
        }
    }
}
