using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using XpCustomizer.Progression;

namespace XpCustomizer.UI
{
    /// <summary>Optional advanced per-skill multiplier editor.</summary>
    internal static class SkillSpecificXpSettingsUI
    {
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
                Log.Message("[XP Customizer] UI_REVISION=SKILLXP-UI-CS1061-FIX1-20261031; source=UI-only; gameplay=unchanged");
            }
            int modified = SkillSpecificXpService.GetModifiedCount(settings);
            Rect toggle = listing.GetRect(32f);
            string title = (expanded ? "▼ " : "▶ ") +
                "XPC_L3B3_SkillXpTitle".Translate().ToString() +
                (modified > 0 ? string.Format("XPC_L3B3_ModifiedSuffix".Translate().ToString(), modified) : "");
            if (Widgets.ButtonText(toggle, title)) expanded = !expanded;

            if (!expanded) return;

            listing.Gap(4f);
            bool enabled = settings.skillSpecificXpEnabled;
            DrawCompactToggle(listing, "XPC_L3B3_SkillXpUse".Translate().ToString(), ref enabled);
            settings.skillSpecificXpEnabled = enabled;
            SettingsUIHelpers.DrawDescription(listing,
                "XPC_L3B3_SkillXpDesc".Translate().ToString());
            SettingsUIHelpers.DrawReferenceText(listing,
                "XPC_L3B3_SkillXpRange".Translate().ToString());
            listing.Gap(6f);

            Rect header = listing.GetRect(25f);
            DrawColumns(header, "XPC_L3B3_HeaderSkill".Translate().ToString(), "XPC_L3B3_HeaderMultiplier".Translate().ToString(), "XPC_L3B3_SkillXpFinal".Translate().ToString(), "XPC_L3B3_SkillXpTotal".Translate().ToString());
            var defs = new List<SkillDef>(DefDatabase<SkillDef>.AllDefsListForReading);
            defs.Sort((a,b) => string.Compare(a.label ?? a.defName,
                b.label ?? b.defName, StringComparison.OrdinalIgnoreCase));
            float total = XpCurve.GetTotalXp(settings);
            float global = settings.careerXpMultiplier;
            for (int i = 0; i < defs.Count; i++)
            {
                SkillDef def = defs[i];
                if (def == null || string.IsNullOrEmpty(def.defName)) continue;
                Rect row = listing.GetRect(34f);
                if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                DrawSkillRow(row, def, total, global, settings);
            }

            listing.Gap(6f);
            if (listing.ButtonText("XPC_L3B3_SkillXpReset".Translate().ToString()))
            {
                settings.skillSpecificXpMultipliers = new Dictionary<string,float>();
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

        private static void DrawColumns(Rect row, string first,
            string second, string third, string fourth)
        {
            float col1 = row.width * 0.29f;
            float col2 = row.width * 0.20f;
            float col3 = row.width * 0.19f;
            Widgets.Label(new Rect(row.x, row.y, col1, row.height), first);
            Widgets.Label(new Rect(row.x + col1, row.y, col2, row.height), second);
            Widgets.Label(new Rect(row.x + col1 + col2, row.y, col3, row.height), third);
            Widgets.Label(new Rect(row.x + col1 + col2 + col3, row.y,
                row.width - col1 - col2 - col3, row.height), fourth);
        }

        private static void DrawSkillRow(Rect row, SkillDef def,
            float total, float global, XpCustomizerSettings settings)
        {
            float col1 = row.width * 0.29f;
            float col2 = row.width * 0.20f;
            float col3 = row.width * 0.19f;
            float value = SkillSpecificXpService.GetConfiguredMultiplier(
                settings, def.defName);

            NumericInputState state;
            if (!inputs.TryGetValue(def.defName, out state))
            {
                state = new NumericInputState();
                inputs[def.defName] = state;
            }
            if (!float.IsNaN(state.lastValue) &&
                !Mathf.Approximately(state.lastValue, value)) state.buffer = null;

            Widgets.Label(new Rect(row.x, row.y + 3f, col1 - 8f, row.height - 3f),
                def.LabelCap);
            Rect field = new Rect(row.x + col1, row.y + 2f,
                Mathf.Min(col2 - 14f, 124f), 29f);
            float edited = value;
            ProfileNumericFocus.Draw(field, ref edited, state,
                SkillSpecificXpService.MinimumMultiplier,
                SkillSpecificXpService.MaximumMultiplier);
            if (!Mathf.Approximately(edited, value))
                SkillSpecificXpService.SetConfiguredMultiplier(settings,
                    def.defName, edited);
            state.lastValue = SkillSpecificXpService.GetConfiguredMultiplier(
                settings, def.defName);

            float factor = SkillSpecificXpService.GetConfiguredMultiplier(
                settings, def.defName);
            Widgets.Label(new Rect(row.x + col1 + col2, row.y + 3f,
                col3 - 5f, row.height - 3f),
                "x" + (global * factor).ToString("0.##"));
            Widgets.Label(new Rect(row.x + col1 + col2 + col3,
                row.y + 3f, row.width - col1 - col2 - col3, row.height - 3f),
                (total * factor).ToString("N0") + " XP");
        }
    }
}
