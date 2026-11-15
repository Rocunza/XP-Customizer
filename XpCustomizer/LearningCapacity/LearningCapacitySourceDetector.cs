using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace XpCustomizer.LearningCapacity
{
    /// <summary>
    /// Read-only Pawn source detection; never cache or mutate Pawn state.
    /// LC1 preserves existing numerical and gameplay semantics.
    /// </summary>
    internal static class LearningCapacitySourceDetector
    {
        internal static bool HasActiveTrait(
            Pawn pawn,
            string defName)
        {
            if (pawn?.story?.traits == null)
            {
                return false;
            }


            foreach (Trait trait in
                     pawn.story.traits.allTraits)
            {
                if (trait == null ||
                    trait.Suppressed ||
                    trait.def == null)
                {
                    continue;
                }


                if (trait.def.defName ==
                    defName)
                {
                    return true;
                }
            }


            return false;
        }


        internal static string GetActiveTraitLabel(
            Pawn pawn,
            string defName)
        {
            if (pawn?.story?.traits == null)
            {
                return defName;
            }


            foreach (Trait trait in
                     pawn.story.traits.allTraits)
            {
                if (trait == null ||
                    trait.Suppressed ||
                    trait.def == null ||
                    trait.def.defName != defName)
                {
                    continue;
                }


                TraitDegreeData data =
                    trait.CurrentData;


                if (data != null)
                {
                    string label =
                        data.LabelCap.ToString();


                    if (!string.IsNullOrEmpty(
                            label))
                    {
                        return label;
                    }
                }


                return defName;
            }


            return defName;
        }


        internal static bool HasActiveGene(
            Pawn pawn,
            string defName)
        {
            if (!ModsConfig.BiotechActive ||
                pawn?.genes == null)
            {
                return false;
            }


            foreach (Gene gene in
                     pawn.genes.GenesListForReading)
            {
                if (gene == null ||
                    !gene.Active ||
                    gene.def == null)
                {
                    continue;
                }


                if (gene.def.defName ==
                    defName)
                {
                    return true;
                }
            }


            return false;
        }


        internal static bool HasActiveHediff(
            Pawn pawn,
            string defName)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return false;
            }


            foreach (Hediff hediff in
                     pawn.health.hediffSet.hediffs)
            {
                if (hediff?.def == null ||
                    hediff.CurStage == null)
                {
                    continue;
                }


                if (hediff.def.defName ==
                    defName)
                {
                    return true;
                }
            }


            return false;
        }


        internal static bool HasActivePrecept(
            Pawn pawn,
            string defName)
        {
            if (!ModsConfig.IdeologyActive ||
                pawn?.Ideo == null)
            {
                return false;
            }


            foreach (Precept precept in
                     pawn.Ideo.PreceptsListForReading)
            {
                if (precept?.def == null)
                {
                    continue;
                }


                if (precept.def.defName ==
                    defName)
                {
                    return true;
                }
            }


            return false;
        }
    }
}
