using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace XpCustomizer.LearningCapacity
{
    /// <summary>
    /// All nine V4 capacity effects and Slow Study Auto projection in fixed order.
    /// LC1 preserves existing numerical and gameplay semantics.
    /// </summary>
    internal static class LearningCapacitySourceEvaluator
    {
        internal static float GetSourceCapacityModifier(
            Pawn pawn,
            SkillDef skill,
            XpCustomizerSettings settings,
            List<LearningCapacityContribution> contributions)
        {
            if (pawn == null)
            {
                return 0f;
            }


            float totalPercentagePoints =
                0f;

            // Slow Study is a multiplicative vanilla Global Learning Factor
            // source (x0.5), not a -50 percentage-point offset. The Auto
            // projection adjusts other AUTO offsets by this factor, while a
            // manual Slow Study final-value override disables its projected
            // factor effect without changing the Pawn's actual vanilla stat.
            float autoGlobalFactor =
                LearningCapacitySourceDetector.HasActiveGene(pawn, LearningCapacitySourceDefs.SlowStudyGene) &&
                !settings.learningCapacityOverrideSlowStudy
                    ? 0.5f : 1f;

            LearningCapacitySourceAccumulator.AddTraitModifier(
                pawn,
                LearningCapacitySourceDefs.SlowLearnerTrait,
                LearningCapacityNormalization.GetV4SourcePercentagePoints(settings.learningCapacityOverrideSlowLearner, settings.learningCapacityCustomSlowLearner, LearningCapacityService.VanillaLfSlowLearner, settings.learningCapacitySourceStrength, autoGlobalFactor),
                contributions,
                ref totalPercentagePoints);

            LearningCapacitySourceAccumulator.AddTraitModifier(
                pawn,
                LearningCapacitySourceDefs.FastLearnerTrait,
                LearningCapacityNormalization.GetV4SourcePercentagePoints(settings.learningCapacityOverrideFastLearner, settings.learningCapacityCustomFastLearner, LearningCapacityService.VanillaLfFastLearner, settings.learningCapacitySourceStrength, autoGlobalFactor),
                contributions,
                ref totalPercentagePoints);

            LearningCapacitySourceAccumulator.AddTraitModifier(
                pawn,
                LearningCapacitySourceDefs.TooSmartTrait,
                LearningCapacityNormalization.GetV4SourcePercentagePoints(settings.learningCapacityOverrideTooSmart, settings.learningCapacityCustomTooSmart, LearningCapacityService.VanillaLfTooSmart, settings.learningCapacitySourceStrength, autoGlobalFactor),
                contributions,
                ref totalPercentagePoints);


            LearningCapacitySourceAccumulator.AddHediffModifier(
                pawn,
                LearningCapacitySourceDefs.BlissLobotomyHediff,
                LearningCapacityNormalization.GetV4SourcePercentagePoints(settings.learningCapacityOverrideBlissLobotomy, settings.learningCapacityCustomBlissLobotomy, LearningCapacityService.VanillaLfBlissLobotomy, settings.learningCapacitySourceStrength, autoGlobalFactor),
                contributions,
                ref totalPercentagePoints);


            LearningCapacitySourceAccumulator.AddGeneModifier(
                pawn,
                LearningCapacitySourceDefs.SlowStudyGene,
                LearningCapacityNormalization.GetV4SourcePercentagePoints(settings.learningCapacityOverrideSlowStudy, settings.learningCapacityCustomSlowStudy, LearningCapacityService.VanillaLfSlowStudy, settings.learningCapacitySourceStrength),
                contributions,
                ref totalPercentagePoints);

            LearningCapacitySourceAccumulator.AddGeneModifier(
                pawn,
                LearningCapacitySourceDefs.QuickStudyGene,
                LearningCapacityNormalization.GetV4SourcePercentagePoints(settings.learningCapacityOverrideQuickStudy, settings.learningCapacityCustomQuickStudy, LearningCapacityService.VanillaLfQuickStudy, settings.learningCapacitySourceStrength, autoGlobalFactor),
                contributions,
                ref totalPercentagePoints);


            LearningCapacitySourceAccumulator.AddHediffModifier(
                pawn,
                LearningCapacitySourceDefs.NeuralSuperchargeHediff,
                LearningCapacityNormalization.GetV4SourcePercentagePoints(settings.learningCapacityOverrideNeuralSupercharge, settings.learningCapacityCustomNeuralSupercharge, LearningCapacityService.VanillaLfNeuralSupercharge, settings.learningCapacitySourceStrength, autoGlobalFactor),
                contributions,
                ref totalPercentagePoints);


            if (skill == SkillDefOf.Animals)
            {
                LearningCapacitySourceAccumulator.AddPreceptModifier(
                    pawn,
                    LearningCapacitySourceDefs.RanchingCentralPrecept,
                    LearningCapacityNormalization.GetV4SourcePercentagePoints(settings.learningCapacityOverrideRanchingCentral, settings.learningCapacityCustomRanchingCentral, LearningCapacityService.VanillaLfRanchingCentral, settings.learningCapacitySourceStrength),
                    contributions,
                    ref totalPercentagePoints);
            }


            LearningCapacitySourceAccumulator.AddHediffModifier(
                pawn,
                LearningCapacitySourceDefs.LearningAssistantHediff,
                LearningCapacityNormalization.GetV4SourcePercentagePoints(settings.learningCapacityOverrideLearningAssistant, settings.learningCapacityCustomLearningAssistant, LearningCapacityService.VanillaLfLearningAssistant, settings.learningCapacitySourceStrength, autoGlobalFactor),
                contributions,
                ref totalPercentagePoints);


            return totalPercentagePoints /
                   100f;
        }
    }
}
