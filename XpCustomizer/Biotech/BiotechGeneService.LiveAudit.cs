using System;
using RimWorld;
using Verse;

namespace XpCustomizer.Biotech
{
    /// <summary>
    /// R6-P0-D4: read-only checks of real generated skill gene values and
    /// XP Customizer's ownership state. Does not change GeneDefs or pawns.
    /// These checks are diagnostics, not an enforcement loop: foreign mods
    /// are not automatically overwritten when a mismatch is detected.
    /// </summary>
    public static partial class BiotechGeneService
    {
        internal const string LiveAuditRevision = "R6P0D4-GENE-READONLY-AUDIT-20261106";

        internal static bool AuditLiveValues(XpCustomizerSettings settings,
            out int genesChecked, out string reason)
        {
            genesChecked = 0;
            reason = "NONE";
            if (!ModsConfig.BiotechActive)
            {
                reason = "BIOTECH_DLC_INACTIVE";
                return false;
            }
            if (settings == null)
            {
                reason = "SETTINGS_NULL";
                return false;
            }
            try
            {
                foreach (GeneDef gene in DefDatabase<GeneDef>.AllDefsListForReading)
                {
                    SkillGeneType type = GetSkillGeneType(gene);
                    if (type == SkillGeneType.None || gene.aptitudes.NullOrEmpty() ||
                        gene.aptitudes[0] == null || gene.aptitudes[0].skill == null)
                        continue;
                    genesChecked++;
                    if (settings.biotechCustomizationEnabled &&
                        (gene.aptitudes[0].level != GetAptitude(type, settings) ||
                         gene.biostatCpx != GetComplexity(type, settings) ||
                         gene.biostatMet != GetMetabolism(type, settings)))
                    {
                        reason = "ACTIVE_GENE_VALUE_MISMATCH:" + gene.defName;
                        return false;
                    }
                }
                if (genesChecked == 0)
                {
                    reason = "NO_GENERATED_SKILL_GENES";
                    return false;
                }
                if (settings.biotechCustomizationEnabled)
                {
                    if (!customValuesApplied || OriginalGeneStates.Count == 0)
                    {
                        reason = "ACTIVE_OWNERSHIP_MISSING";
                        return false;
                    }
                }
                else if (customValuesApplied || OriginalGeneStates.Count != 0)
                {
                    reason = "OFF_OWNERSHIP_NOT_RELEASED";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                reason = "READ_EXCEPTION:" + ex.GetType().Name;
                return false;
            }
        }
    }
}
