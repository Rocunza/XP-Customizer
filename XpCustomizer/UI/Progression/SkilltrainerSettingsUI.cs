using UnityEngine;
using Verse;

namespace XpCustomizer.UI
{
    /// <summary>
    /// Draws the Skilltrainer strength settings section.
    /// </summary>
    internal static class SkilltrainerSettingsUI
    {
        private const float VanillaSkilltrainerXp = 50000f;

        private static readonly NumericInputState multiplierInput =
            new NumericInputState();

#if DEBUG
        private static bool l3B3TranslationLogged;
#endif


        public static void Draw(
            Listing_Standard listing)
        {
            XpCustomizerSettings settings =
                XpCustomizerMod.Settings;

#if DEBUG
            if (!l3B3TranslationLogged && Event.current != null &&
                Event.current.type == EventType.Repaint)
            {
                l3B3TranslationLogged = true;
                Log.Message("[XPC-L10N-L3B3][KEYED_PROGRESSION] " +
                    "version=XPC-L10N-L3B3-SKILL-TRAINER-BOOK-20261112-R1; " +
                    "phase=B3; keys=54; sampleKey=XPC_L3B3_TrainerTitle; sample=" +
                    "XPC_L3B3_TrainerTitle".Translate().ToString() +
                    "; build=" + XpCustomizerMod.BuildId);
            }
#endif


            SettingsUIHelpers.DrawSectionTitle(
                listing,
                "XPC_L3B3_TrainerTitle".Translate().ToString());

            SettingsUIHelpers.DrawDescription(
                listing,
                "XPC_L3B3_TrainerDesc".Translate().ToString());

            listing.Gap(4f);


            float value =
                settings.skilltrainerXpMultiplier;


            if (!float.IsNaN(
                    multiplierInput.lastValue) &&
                !Mathf.Approximately(
                    value,
                    multiplierInput.lastValue))
            {
                multiplierInput.buffer = null;
            }


            // UL1: this one caller uses adjacent layout. The shared
            // DrawMultiplierInput contract (also used by Level20Reserve) stays intact.
            string label = "XPC_L3B3_TrainerMultiplier".Translate().ToString();
            NumericRowLayout.Placement geometry = NumericRowLayout.Allocate(
                listing, label, 90f, 24f, 30f, 30f);
            Widgets.Label(geometry.Label, label);
            ProfileNumericFocus.Draw(geometry.Field, ref value, multiplierInput,
                XpCustomizerDefaults.ProgressionMultiplierMin,
                XpCustomizerDefaults.ProgressionMultiplierMax);
            TextAnchor previousAnchor = Text.Anchor;
            try
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(geometry.Suffix, "x");
            }
            finally { Text.Anchor = previousAnchor; }


            settings.skilltrainerXpMultiplier =
                Mathf.Clamp(
                    value,
                    XpCustomizerDefaults.ProgressionMultiplierMin,
                    XpCustomizerDefaults.ProgressionMultiplierMax);

            multiplierInput.lastValue =
                settings.skilltrainerXpMultiplier;


            VanillaReferenceAccentUI.DrawProgressionRange(listing,
                "XPC_L3F3_TrainerRangeMuted", "XPC_L3F3_TrainerRangeVanilla",
                "XPC_L3B3_TrainerRange");

            listing.Gap(6f);


            VanillaReferenceAccentUI.DrawVanillaLabel(listing,
                string.Format("XPC_L3B3_TrainerVanillaBase".Translate().ToString(),
                    VanillaSkilltrainerXp.ToString("N0")));

            listing.Label(
                string.Format("XPC_L3B3_TrainerCustomBase".Translate().ToString(),
                    (VanillaSkilltrainerXp * settings.skilltrainerXpMultiplier).ToString("N0")));

            SkilltrainerSpecificXpSettingsUI.Draw(listing, settings);
        }


        // S2: the owner exposes ONLY its single multiplier draft. The per-skill
        // SkilltrainerSpecificXpSettingsUI editor is intentionally untouched.
        internal static bool TryPrepareMultiplierRecovery(
            out SkilltrainerNumericRecoveryTicket ticket)
        {
            return SkilltrainerNumericRecoveryTicket.TryCapture(multiplierInput,
                XpCustomizerMod.Settings, out ticket);
        }

        internal static bool TryDiscardMultiplierRecovery(
            SkilltrainerNumericRecoveryTicket ticket)
        {
            return ticket != null && ticket.TryDiscard(multiplierInput,
                XpCustomizerMod.Settings);
        }

        public static void NotifySettingsExternallyChanged()
        {
            multiplierInput.Reset();
            SkilltrainerSpecificXpSettingsUI.NotifySettingsExternallyChanged();
        }
    }
}
