#if DEBUG
using System;
using System.Collections.Generic;
using RimWorld;
using XpCustomizer.BookXp;

namespace XpCustomizer
{
    internal static class BookXpSelfTests
    {
        private static void Ensure(bool ok, string scenario)
        {
            if (!ok) throw new InvalidOperationException(
                "[XP Customizer] BOOKXP-V1 regression failed: " + scenario);
        }
        private static void Near(float got, float expected, string scenario)
        {
            Ensure(Math.Abs(got - expected) < 0.001f,
                scenario + " expected=" + expected + " got=" + got);
        }
        internal static void Run()
        {
            var s = new XpCustomizerSettings();
            Ensure(!s.bookXpEnabled && !s.bookIntellectualEnabled &&
                !s.bookQualityEnabled && !s.bookSkillEnabled,
                "default master and section switches OFF");
            // Older V1 configs had no master key: any enabled sub-feature must
            // still work after migration (without re-enabling later on purpose).
            var legacy = new XpCustomizerSettings();
            legacy.bookIntellectualEnabled = true;
            legacy.NormalizeBookXpMasterSettings();
            Ensure(legacy.bookXpEnabled, "migrate active V1 reading setting to master ON");
            legacy.bookXpEnabled = false;
            legacy.NormalizeBookXpMasterSettings();
            Ensure(!legacy.bookXpEnabled, "deliberate master OFF survives subsequent loads");
            var legacyOff = new XpCustomizerSettings();
            legacyOff.NormalizeBookXpMasterSettings();
            Ensure(!legacyOff.bookXpEnabled, "migrate vanilla-neutral V1 to master OFF");
            Near(BookXpService.NormalizeIntellectual(s.bookIntellectualXpPerHour) / 2500f,
                0.1f, "vanilla default intellectual input");
            float[] hourly = { 125f, 187.5f, 250f, 312.5f, 375f, 437.5f, 500f };
            for (int i = 0; i < hourly.Length; i++)
            {
                QualityCategory q = (QualityCategory)i;
                Near(BookXpService.VanillaQualityXpPerHour(q), hourly[i], "quality #" + i);
                Near(BookXpService.GetEffectiveTextbookMultiplier(s, q, "Shooting"),
                    1f, "all default neutral #" + i);
            }
            s.bookQualityEnabled = true;
            s.bookSkillEnabled = true;
            BookXpService.SetQualityMultiplier(s, QualityCategory.Legendary, 1.5f);
            BookXpService.SetSkillMultiplier(s, "Shooting", 1.3f);
            Near(BookXpService.GetEffectiveTextbookMultiplier(s, QualityCategory.Legendary,
                "Shooting"), 1f, "master OFF blocks active child sections");
            s.bookXpEnabled = true;
            Near(BookXpService.GetEffectiveTextbookMultiplier(s, QualityCategory.Legendary,
                "Shooting"), 1.95f, "quality * skill independent");
            Near(BookXpService.GetEffectiveTextbookMultiplier(s, QualityCategory.Legendary,
                "UnknownThirdPartySkill"), 1.5f, "other skill neutral");
            Near(BookXpService.GetEffectiveTextbookMultiplier(s, QualityCategory.Normal,
                "Shooting"), 1.3f, "other quality neutral");
            s.bookQualityEnabled = false;
            Near(BookXpService.GetEffectiveTextbookMultiplier(s, QualityCategory.Legendary,
                "Shooting"), 1.3f, "quality switch OFF");
            s.bookSkillEnabled = false;
            Near(BookXpService.GetEffectiveTextbookMultiplier(s, QualityCategory.Legendary,
                "Shooting"), 1f, "both switches OFF");
            s.bookXpEnabled = false;
            s.bookQualityEnabled = true;
            Near(BookXpService.GetEffectiveTextbookMultiplier(s, QualityCategory.Legendary,
                "Shooting"), 1f, "master OFF neutral despite enabled quality");
            s.bookXpEnabled = true;
            s.bookQualityEnabled = false;
            Ensure(s.bookQualityMultipliers.Count == 1 && s.bookSkillMultipliers.Count == 1,
                "disabled settings preserved");
            BookXpService.SetSkillMultiplier(s, "Melee", 0f);
            s.bookSkillEnabled = true;
            Near(BookXpService.GetEffectiveTextbookMultiplier(s, QualityCategory.Good,
                "Melee"), 0f, "explicit zero possible");
            BookXpService.SetSkillMultiplier(s, "UnknownCustomSkill", 4f);
            Ensure(!s.bookSkillMultipliers.ContainsKey("UnknownCustomSkill"),
                "only the 12 vanilla skills selectable");
            s.bookQualityMultipliers["UnknownQualityFromMod"] = 1.7f;
            s.bookSkillMultipliers["MissingDefFromMod"] = 2f;
            s.bookSkillMultipliers["Shooting"] = float.NaN;
            s.bookIntellectualXpPerHour = float.PositiveInfinity;
            BookXpService.NormalizeSettings(s);
            Ensure(s.bookQualityMultipliers.ContainsKey("UnknownQualityFromMod") &&
                s.bookSkillMultipliers.ContainsKey("MissingDefFromMod"),
                "unknown saved keys retained");
            Ensure(!s.bookSkillMultipliers.ContainsKey("Shooting"),
                "bad numeric normalized neutral");
            Near(s.bookIntellectualXpPerHour, 250f, "bad intellectual normalized");
            Near(BookXpService.NormalizeIntellectual(8000f), 5000f,
                "intellectual max clamp");
            Near(BookXpService.NormalizeMultiplier(-1f), 0f, "ratio min clamp");
            Near(BookXpService.NormalizeMultiplier(99f), 20f, "ratio max clamp");
            int medfixChecks = VerifyMedicineMigrationAndMultiplier();
            Verse.Log.Message("[XPC-BOOKXP-MEDFIX] phase=SELFTEST; status=PASS; checks=" +
                medfixChecks + "; legacyZero=PASS; neutral=PASS; canonicalWins=PASS" +
                "; skillGate=PASS; qualityIndependent=PASS; runtimeReading=NOT_TESTED" +
                "; build=" + XpCustomizerMod.BuildId);
            Verse.Log.Message("[XP Customizer] BOOKXP-V1-R3 pure tests PASS: vanilla defaults, " +
                "master migration/gating, 7 quality anchors, 12 skills, quality*skill, " +
                "zero, persistence, invalid values and bounds. Runtime reading not simulated.");
        }

        private static int VerifyMedicineMigrationAndMultiplier()
        {
            int checks = 0;
            var unique = new HashSet<string>(StringComparer.Ordinal);
            int medicineCount = 0;
            foreach (string skill in BookXpService.VanillaSkillNames)
            {
                Ensure(unique.Add(skill), "BookXp 12 vanilla skill IDs unique: " + skill); checks++;
                Ensure(skill != "Medical", "BookXp list has no legacy display label"); checks++;
                if (skill == "Medicine") medicineCount++;
            }
            Ensure(unique.Count == 12 && medicineCount == 1 &&
                BookXpService.IsVanillaSkill("Medicine") &&
                !BookXpService.IsVanillaSkill("Medical") &&
                !BookXpService.IsVanillaSkill("medicine") &&
                !BookXpService.IsVanillaSkill("UnknownThirdPartySkill"),
                "only canonical Medicine is recognized"); checks++;

            var s = new XpCustomizerSettings();
            s.bookXpEnabled = true;
            s.bookSkillEnabled = true;
            BookXpService.SetSkillMultiplier(s, "Medicine", 1.7f);
            Near(BookXpService.GetEffectiveTextbookMultiplier(s, QualityCategory.Legendary,
                "Medicine"), 1.7f, "Medicine skill-only multiplier"); checks++;
            BookXpService.SetQualityMultiplier(s, QualityCategory.Legendary, 1.5f);
            s.bookQualityEnabled = true;
            Near(BookXpService.GetEffectiveTextbookMultiplier(s, QualityCategory.Legendary,
                "Medicine"), 2.55f, "Medicine quality x skill"); checks++;
            Near(BookXpService.GetEffectiveTextbookMultiplier(s, QualityCategory.Legendary,
                "Medical"), 1.5f, "legacy display label no longer selectable"); checks++;
            s.bookSkillEnabled = false;
            Near(BookXpService.GetEffectiveTextbookMultiplier(s, QualityCategory.Legendary,
                "Medicine"), 1.5f, "Medicine skill switch OFF"); checks++;
            s.bookSkillEnabled = true;
            s.bookQualityEnabled = false;
            BookXpService.SetSkillMultiplier(s, "Medicine", 0f);
            Near(BookXpService.GetEffectiveTextbookMultiplier(s, QualityCategory.Normal,
                "Medicine"), 0f, "Medicine zero means XP blocked"); checks++;
            BookXpService.SetSkillMultiplier(s, "Medicine", 20f);
            Near(BookXpService.GetEffectiveTextbookMultiplier(s, QualityCategory.Normal,
                "Medicine"), 20f, "Medicine upper bound"); checks++;
            BookXpService.SetSkillMultiplier(s, "Medicine", 1f);
            Ensure(!s.bookSkillMultipliers.ContainsKey("Medicine"), "Medicine neutral removed"); checks++;
            s.bookXpEnabled = false;
            BookXpService.SetSkillMultiplier(s, "Medicine", 1.7f);
            Near(BookXpService.GetEffectiveTextbookMultiplier(s, QualityCategory.Normal,
                "Medicine"), 1f, "master OFF gates Medicine"); checks++;
            BookXpService.SetSkillMultiplier(s, "Medical", 5f);
            Ensure(!s.bookSkillMultipliers.ContainsKey("Medical"),
                "legacy display label cannot create new settings"); checks++;

            float[] legacy = { 0f, 1f, 1.7f, 20f, float.NaN, float.PositiveInfinity, -5f, 99f };
            float[] expected = { 0f, 1f, 1.7f, 20f, 1f, 1f, 0f, 20f };
            for (int i = 0; i < legacy.Length; ++i)
            {
                var old = new XpCustomizerSettings();
                old.bookSkillMultipliers["Medical"] = legacy[i];
                old.bookSkillMultipliers["UnknownThirdPartySkill"] = 2.25f;
                old.bookQualityMultipliers["Legendary"] = 1.5f;
                bool won;
                Ensure(BookXpService.MigrateLegacyMedicalKey(old.bookSkillMultipliers, out won) &&
                    !won && !old.bookSkillMultipliers.ContainsKey("Medical") &&
                    old.bookSkillMultipliers.ContainsKey("Medicine"),
                    "legacy migration moved exact name case=" + i); checks++;
                Near(BookXpService.NormalizeMultiplier(old.bookSkillMultipliers["Medicine"]),
                    expected[i], "migrated value case=" + i); checks++;
                Ensure(!BookXpService.MigrateLegacyMedicalKey(old.bookSkillMultipliers, out won),
                    "idempotent migration case=" + i); checks++;
                old.bookSkillMultipliers.Remove("Medicine");
                old.bookSkillMultipliers["Medical"] = legacy[i];
                BookXpService.NormalizeSettings(old);
                Ensure(!old.bookSkillMultipliers.ContainsKey("Medical") &&
                    old.bookSkillMultipliers.ContainsKey("UnknownThirdPartySkill") &&
                    old.bookQualityMultipliers.ContainsKey("Legendary"),
                    "settings normalize preserves unrelated data case=" + i); checks++;
                if (expected[i] == 1f)
                    Ensure(!old.bookSkillMultipliers.ContainsKey("Medicine"),
                        "neutral migration omitted case=" + i);
                else
                    Near(old.bookSkillMultipliers["Medicine"], expected[i],
                        "nonneutral value persisted case=" + i);
                checks++;
            }
            // Canonical key wins even if the old value is zero or nonfinite.
            var conflict = new Dictionary<string, float>(StringComparer.Ordinal);
            conflict["Medicine"] = 1.4f;
            conflict["Medical"] = 0f;
            conflict["medical"] = 3f;
            bool canonicalWon;
            Ensure(BookXpService.MigrateLegacyMedicalKey(conflict, out canonicalWon) &&
                canonicalWon && conflict["Medicine"] == 1.4f &&
                !conflict.ContainsKey("Medical") && conflict["medical"] == 3f,
                "canonical wins; other case preserved"); checks++;
            var mixed = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            mixed["Medical"] = 0f;
            Ensure(BookXpService.MigrateLegacyMedicalKey(mixed, out canonicalWon) &&
                !canonicalWon && mixed.ContainsKey("Medicine") &&
                mixed["Medicine"] == 0f && mixed.Comparer == StringComparer.OrdinalIgnoreCase,
                "nonordinal comparer migration preserves zero"); checks++;
            var lowerOnly = new Dictionary<string, float>(StringComparer.Ordinal);
            lowerOnly["medical"] = 2f;
            Ensure(!BookXpService.MigrateLegacyMedicalKey(lowerOnly, out canonicalWon) &&
                lowerOnly.ContainsKey("medical") && !lowerOnly.ContainsKey("Medicine"),
                "lowercase foreign name untouched"); checks++;
            Ensure(!BookXpService.MigrateLegacyMedicalKey(null, out canonicalWon),
                "null map no-op"); checks++;
            return checks;
        }
    }
}
#endif
