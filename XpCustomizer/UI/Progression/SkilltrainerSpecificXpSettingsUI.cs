using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using XpCustomizer.Skilltrainer;

namespace XpCustomizer.UI
{
    /// <summary>Optional S2 trainer payout editor. Independent from career XP.</summary>
    internal static class SkilltrainerSpecificXpSettingsUI
    {
        private const float VanillaReferenceXp = 50000f;
        private static bool expanded;
        private static bool revisionLogged;
        private static readonly Dictionary<string, NumericInputState> inputs =
            new Dictionary<string, NumericInputState>();

        public static void NotifySettingsExternallyChanged()
        {
            inputs.Clear();
        }

        public static void Draw(Listing_Standard listing, XpCustomizerSettings settings)
        {
            if (!revisionLogged)
            {
                revisionLogged = true;
                Log.Message("[XP Customizer] UI_REVISION=TRAINERXP-UI-CS1061-FIX1-20261031; source=UI-only; gameplay=unchanged");
            }
            listing.Gap(12f);
            int modified = SkilltrainerSpecificXpService.GetModifiedCount(settings);
            Rect header = listing.GetRect(32f);
            string title = (expanded ? "▼ " : "▶ ") +
                "XPC_L3B3_TrainerSkillTitle".Translate().ToString() +
                (modified > 0 ? string.Format("XPC_L3B3_ModifiedSuffix".Translate().ToString(), modified) : "");
            if (Widgets.ButtonText(header, title)) expanded = !expanded;
            if (!expanded) return;

            listing.Gap(4f);
            bool enabled = settings.skilltrainerSpecificEnabled;
            DrawCompactToggle(listing, "XPC_L3B3_TrainerSkillUse".Translate().ToString(), ref enabled);
            settings.skilltrainerSpecificEnabled = enabled;
            SettingsUIHelpers.DrawDescription(listing,
                "XPC_L3B3_TrainerSkillDesc".Translate().ToString());
            SettingsUIHelpers.DrawReferenceText(listing,
                "XPC_L3B3_TrainerSkillRange".Translate().ToString());
            listing.Gap(5f);
            Rect headings = listing.GetRect(27f);
            DrawHeaders(headings);

            List<SkillDef> defs = new List<SkillDef>(
                DefDatabase<SkillDef>.AllDefsListForReading);
            defs.Sort((a, b) => string.Compare(
                a != null ? (a.label ?? a.defName) : "",
                b != null ? (b.label ?? b.defName) : "",
                StringComparison.OrdinalIgnoreCase));
            for (int i = 0; i < defs.Count; i++)
            {
                SkillDef def = defs[i];
                if (def == null || string.IsNullOrEmpty(def.defName)) continue;
                Rect row = listing.GetRect(34f);
                if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                DrawRow(row, def, settings);
            }
            listing.Gap(5f);
            if (listing.ButtonText("XPC_L3B3_TrainerSkillReset".Translate().ToString()))
            {
                settings.skilltrainerSpecificMultipliers = new Dictionary<string, float>();
                NotifySettingsExternallyChanged();
            }
            listing.Gap(5f);
        }


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

        private static void DrawHeaders(Rect row)
        {
            float skill = row.width * 0.29f;
            float ratio = row.width * 0.20f;
            Widgets.Label(new Rect(row.x, row.y, skill, row.height), "XPC_L3B3_HeaderSkill".Translate().ToString());
            Widgets.Label(new Rect(row.x + skill, row.y, ratio, row.height), "XPC_L3B3_HeaderMultiplier".Translate().ToString());
            Widgets.Label(new Rect(row.x + skill + ratio, row.y,
                row.width - skill - ratio, row.height), "XPC_L3B3_TrainerSkillPreview".Translate().ToString());
        }

        private static void DrawRow(Rect row, SkillDef def,
            XpCustomizerSettings settings)
        {
            float skillWidth = row.width * 0.29f;
            float ratioWidth = row.width * 0.20f;
            float value = SkilltrainerSpecificXpService.GetConfiguredMultiplier(
                settings, def.defName);
            NumericInputState state;
            if (!inputs.TryGetValue(def.defName, out state))
            {
                state = new NumericInputState();
                inputs[def.defName] = state;
            }
            if (!float.IsNaN(state.lastValue) &&
                !Mathf.Approximately(value, state.lastValue)) state.buffer = null;
            Widgets.Label(new Rect(row.x, row.y + 3f, skillWidth - 8f,
                row.height - 3f), def.LabelCap);
            Rect input = new Rect(row.x + skillWidth, row.y + 2f,
                Mathf.Min(ratioWidth - 14f, 124f), 29f);
            float edited = value;
            ProfileNumericFocus.Draw(input, ref edited, state,
                SkilltrainerSpecificXpService.MinimumMultiplier,
                SkilltrainerSpecificXpService.MaximumMultiplier);
            if (!Mathf.Approximately(edited, value))
                SkilltrainerSpecificXpService.SetConfiguredMultiplier(settings,
                    def.defName, edited);
            state.lastValue = SkilltrainerSpecificXpService.GetConfiguredMultiplier(
                settings, def.defName);

            // Preview of the STORED custom value; actual payout uses the switch.
            float custom = VanillaReferenceXp * settings.skilltrainerXpMultiplier *
                state.lastValue;
            Widgets.Label(new Rect(row.x + skillWidth + ratioWidth,
                row.y + 3f, row.width - skillWidth - ratioWidth,
                row.height - 3f), custom.ToString("N0") + " XP" +
                (settings.skilltrainerSpecificEnabled ? "" : "XPC_L3B3_TrainerSkillOffPreview".Translate().ToString()));
        }
    }
}
