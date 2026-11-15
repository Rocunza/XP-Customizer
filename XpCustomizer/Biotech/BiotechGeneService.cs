using System;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;
using System.Text.RegularExpressions;
using HarmonyLib;
using RimWorld;
using Verse;

namespace XpCustomizer.Biotech
{
    public static partial class BiotechGeneService
    {
        private enum SkillGeneType
        {
            None,
            Awful,
            Poor,
            Strong,
            Great
        }


        private sealed class OriginalGeneState
        {
            public readonly Aptitude Aptitude;
            public readonly int AptitudeLevel;
            public readonly int Complexity;
            public readonly int Metabolism;
            public readonly string Description;


            public OriginalGeneState(
                Aptitude aptitude,
                GeneDef geneDef)
            {
                Aptitude = aptitude;
                AptitudeLevel = aptitude.level;
                Complexity = geneDef.biostatCpx;
                Metabolism = geneDef.biostatMet;
                Description = geneDef.description;
            }
        }


        // GeneDef.DescriptionFull is cached by RimWorld.
        // When we change or restore aptitude / complexity / metabolism /
        // description, this cache must be cleared.
        private static readonly FieldInfo CachedDescriptionField =
            AccessTools.Field(
                typeof(GeneDef),
                "cachedDescription");


        // Captured lazily immediately before XP Customizer first takes
        // control of the generated skill-gene defs. This is intentionally not
        // populated while customization is disabled so the vanilla-neutral
        // state remains completely no-touch.
        private static readonly Dictionary<GeneDef, OriginalGeneState>
            OriginalGeneStates =
                new Dictionary<GeneDef, OriginalGeneState>();

        private static bool customValuesApplied;


        // ============================================================
        // Apply current XP Customizer settings
        // ============================================================

        public static void ApplySettings()
        {
            if (!ModsConfig.BiotechActive)
            {
                return;
            }

            XpCustomizerSettings settings =
                XpCustomizerMod.Settings;

            if (settings == null)
            {
                return;
            }


            // OFF is the real vanilla-neutral state. If XP Customizer
            // has never taken ownership of these defs, do absolutely nothing.
            // If it previously did, restore the exact values that existed
            // immediately before customization was enabled.
            if (!settings.biotechCustomizationEnabled)
            {
                RestoreOriginalValues();
                return;
            }


            CaptureOriginalValuesIfNeeded();

            int changedGenes = 0;

            foreach (GeneDef geneDef in
                     DefDatabase<GeneDef>.AllDefsListForReading)
            {
                SkillGeneType geneType =
                    GetSkillGeneType(geneDef);

                if (geneType == SkillGeneType.None)
                {
                    continue;
                }

                if (geneDef.aptitudes.NullOrEmpty())
                {
                    continue;
                }

                Aptitude aptitude =
                    geneDef.aptitudes[0];

                if (aptitude == null ||
                    aptitude.skill == null)
                {
                    continue;
                }


                int newAptitude =
                    GetAptitude(
                        geneType,
                        settings);

                int newComplexity =
                    GetComplexity(
                        geneType,
                        settings);

                int newMetabolism =
                    GetMetabolism(
                        geneType,
                        settings);


                // ----------------------------------------------------
                // Actual gameplay values
                // ----------------------------------------------------

                aptitude.level = newAptitude;

                geneDef.biostatCpx =
                    newComplexity;

                geneDef.biostatMet =
                    newMetabolism;


                // ----------------------------------------------------
                // Description: keep the existing translated/modded text.
                // Only replace an unambiguous old aptitude number; never
                // rebuild the entire description in hard-coded English.
                // ----------------------------------------------------

                OriginalGeneState original;

                if (OriginalGeneStates.TryGetValue(
                        geneDef,
                        out original))
                {
                    geneDef.description =
                        UpdateAptitudeInDescription(
                            original.Description,
                            original.AptitudeLevel,
                            newAptitude);
                }

                ClearCachedDescription(
                    geneDef);

                changedGenes++;
            }


            customValuesApplied = true;

            // Existing pawns cache their total aptitude.
            // Clear that cache so current pawns immediately use
            // the new gene values.
            DirtyExistingPawnAptitudes();


            Log.Message(
                "[XP Customizer] Applied Biotech skill gene customization to " +
                changedGenes +
                " generated gene defs.");
        }


        // ============================================================
        // Ownership / restoration
        // ============================================================

        private static void CaptureOriginalValuesIfNeeded()
        {
            if (OriginalGeneStates.Count > 0)
            {
                return;
            }


            foreach (GeneDef geneDef in
                     DefDatabase<GeneDef>.AllDefsListForReading)
            {
                SkillGeneType geneType =
                    GetSkillGeneType(geneDef);

                if (geneType == SkillGeneType.None ||
                    geneDef.aptitudes.NullOrEmpty())
                {
                    continue;
                }

                Aptitude aptitude =
                    geneDef.aptitudes[0];

                if (aptitude == null ||
                    aptitude.skill == null)
                {
                    continue;
                }


                OriginalGeneStates.Add(
                    geneDef,
                    new OriginalGeneState(
                        aptitude,
                        geneDef));
            }
        }


        private static void RestoreOriginalValues()
        {
            if (!customValuesApplied ||
                OriginalGeneStates.Count == 0)
            {
                return;
            }


            foreach (KeyValuePair<GeneDef, OriginalGeneState> entry in
                     OriginalGeneStates)
            {
                GeneDef geneDef =
                    entry.Key;

                OriginalGeneState original =
                    entry.Value;

                if (geneDef == null ||
                    original == null ||
                    original.Aptitude == null)
                {
                    continue;
                }


                original.Aptitude.level =
                    original.AptitudeLevel;

                geneDef.biostatCpx =
                    original.Complexity;

                geneDef.biostatMet =
                    original.Metabolism;

                geneDef.description =
                    original.Description;

                ClearCachedDescription(
                    geneDef);
            }


            customValuesApplied = false;

            // Release the snapshot after restoring it. If customization is
            // enabled again later, capture the then-current values again so
            // changes made by other mods while we were OFF become the new
            // restoration baseline.
            OriginalGeneStates.Clear();

            DirtyExistingPawnAptitudes();


            Log.Message(
                "[XP Customizer] Released Biotech skill gene customization and restored original gene values.");
        }


        private static void ClearCachedDescription(
            GeneDef geneDef)
        {
            if (CachedDescriptionField != null)
            {
                CachedDescriptionField.SetValue(
                    geneDef,
                    null);
            }
        }


        // ============================================================
        // Detect Biotech skill gene family
        // ============================================================

        private static SkillGeneType GetSkillGeneType(
            GeneDef geneDef)
        {
            if (geneDef == null ||
                geneDef.defName.NullOrEmpty())
            {
                return SkillGeneType.None;
            }

            if (geneDef.defName.StartsWith(
                "AptitudeTerrible_"))
            {
                return SkillGeneType.Awful;
            }

            if (geneDef.defName.StartsWith(
                "AptitudePoor_"))
            {
                return SkillGeneType.Poor;
            }

            if (geneDef.defName.StartsWith(
                "AptitudeStrong_"))
            {
                return SkillGeneType.Strong;
            }

            if (geneDef.defName.StartsWith(
                "AptitudeRemarkable_"))
            {
                return SkillGeneType.Great;
            }

            return SkillGeneType.None;
        }


        // ============================================================
        // Aptitude
        // ============================================================

        private static int GetAptitude(
            SkillGeneType type,
            XpCustomizerSettings settings)
        {
            switch (type)
            {
                case SkillGeneType.Awful:
                    return settings.biotechAwfulAptitude;

                case SkillGeneType.Poor:
                    return settings.biotechPoorAptitude;

                case SkillGeneType.Strong:
                    return settings.biotechStrongAptitude;

                case SkillGeneType.Great:
                    return settings.biotechGreatAptitude;

                default:
                    return 0;
            }
        }


        // ============================================================
        // Complexity
        // ============================================================

        private static int GetComplexity(
            SkillGeneType type,
            XpCustomizerSettings settings)
        {
            switch (type)
            {
                case SkillGeneType.Awful:
                    return settings.biotechAwfulComplexity;

                case SkillGeneType.Poor:
                    return settings.biotechPoorComplexity;

                case SkillGeneType.Strong:
                    return settings.biotechStrongComplexity;

                case SkillGeneType.Great:
                    return settings.biotechGreatComplexity;

                default:
                    return 0;
            }
        }


        // ============================================================
        // Metabolic efficiency
        // ============================================================

        private static int GetMetabolism(
            SkillGeneType type,
            XpCustomizerSettings settings)
        {
            switch (type)
            {
                case SkillGeneType.Awful:
                    return settings.biotechAwfulMetabolism;

                case SkillGeneType.Poor:
                    return settings.biotechPoorMetabolism;

                case SkillGeneType.Strong:
                    return settings.biotechStrongMetabolism;

                case SkillGeneType.Great:
                    return settings.biotechGreatMetabolism;

                default:
                    return 0;
            }
        }


        // ============================================================
        // Gene description: surgical, localization-neutral replacement
        // ============================================================

        private static string UpdateAptitudeInDescription(
            string originalDescription,
            int originalAptitude,
            int newAptitude)
        {
            if (string.IsNullOrEmpty(originalDescription) ||
                originalAptitude == newAptitude ||
                originalAptitude == 0)
            {
                return originalDescription;
            }

            // GeneDef.DescriptionFull already lists the live aptitudes.
            // The plain description may also mention a fixed magnitude.
            // Keep its original language, wording, and any modded additions.
            string oldMagnitude =
                Math.Abs(originalAptitude).ToString(
                    CultureInfo.InvariantCulture);

            MatchCollection matches = Regex.Matches(
                originalDescription,
                @"(?<![\w.,])[+-]?" + oldMagnitude + @"(?![\w]|[.,]\d)");

            // Avoid guessing when the original text has no matching number
            // (e.g. other localizations), or repeats that number elsewhere.
            if (matches.Count != 1)
            {
                return originalDescription;
            }

            Match match = matches[0];
            string matchedNumber = match.Value;

            // A signed number must agree with the original aptitude.
            // Otherwise it may represent some unrelated value.
            if ((matchedNumber.StartsWith("-") && originalAptitude > 0) ||
                (matchedNumber.StartsWith("+") && originalAptitude < 0))
            {
                return originalDescription;
            }

            string replacement =
                Math.Abs(newAptitude).ToString(
                    CultureInfo.InvariantCulture);

            if (matchedNumber.StartsWith("-") && newAptitude < 0)
            {
                replacement = "-" + replacement;
            }
            else if (matchedNumber.StartsWith("+") && newAptitude > 0)
            {
                replacement = "+" + replacement;
            }

            return originalDescription.Substring(0, match.Index) +
                   replacement +
                   originalDescription.Substring(
                       match.Index + match.Length);
        }

        // ============================================================
        // Clear cached aptitude on existing pawns
        // ============================================================

        private static void DirtyExistingPawnAptitudes()
        {
            if (Current.Game == null)
            {
                return;
            }

            var pawns =
                PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead;

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn =
                    pawns[i];

                if (pawn != null &&
                    pawn.skills != null)
                {
                    pawn.skills.DirtyAptitudes();
                }
            }
        }
    }
}
