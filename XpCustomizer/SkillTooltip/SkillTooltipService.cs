using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.FurtherLearning;
using XpCustomizer.Gws;
using XpCustomizer.LearningCapacity;

namespace XpCustomizer.SkillTooltip
{
    /// <summary>
    /// Builds XP Customizer information used by RimWorld's
    /// skill tooltip.
    ///
    /// Presentation / formatting only.
    /// Gameplay calculations always come from their owning services.
    /// </summary>
    internal static partial class SkillTooltipService
    {
        // ================================================================
        // Presentation activation
        // ================================================================

        private static bool tooltipUiRevisionLogged;

        public static bool HasAnyPresentationCustomization(SkillRecord skill)
        {
            return
                IsWorkFactorLearningSpeedPresentationActive(skill) ||
                IsConsciousnessLearningSpeedPresentationActive() ||
                IsDailyCapacityPresentationActive() ||
                IsFurtherLearningPresentationActive();
        }


        public static bool ShouldReplaceVanillaSaturatedLine()
        {
            return
                IsDailyCapacityPresentationActive() ||
                IsFurtherLearningPresentationActive();
        }


        public static bool IsDailyCapacityPresentationActive()
        {
            return LearningCapacityService
                .IsConfiguredToChangeDailyCap();
        }


        public static bool IsFurtherLearningPresentationActive()
        {
            return FurtherLearningService.IsConfiguredToChangeGameplay();
        }


        private static bool IsConsciousnessLearningSpeedPresentationActive()
        {
            return IsConsciousnessLearningSpeedPresentationActive(
                XpCustomizerMod.Settings);
        }

        // Exposed internally for Phase 4A DEBUG route/tooltip parity tests.
        // The actual tooltip and the XP calculation share one selector.
        internal static bool IsConsciousnessLearningSpeedPresentationActive(
            XpCustomizerSettings settings)
        {
            return ConsciousnessCurveDispatcher.HasConfiguredSpeedEffect(settings);
        }

        // Order is intentional: GW speed immediately ABOVE Cons.
        public static string BuildLearningSpeedModifierLines(SkillRecord skill)
        {
            string work = BuildWorkFactorLearningLine(skill);
            string consc = BuildConsciousnessLearningLine(skill);
            if (string.IsNullOrEmpty(work)) return consc;
            if (string.IsNullOrEmpty(consc)) return work;
            return work + "\n" + consc;
        }


        // ================================================================
        // Supplemental block
        // ================================================================

        public static string BuildXpCustomizerBlock(
            SkillRecord skill,
            bool includeLearningSpeedFallback)
        {
            if (skill == null ||
                skill.Pawn == null)
            {
                return string.Empty;
            }


            // UI-only revision stamp: printed once on the first valid tooltip.
            // No gameplay state, XP values, or Harmony patches are modified.
            if (!tooltipUiRevisionLogged)
            {
                tooltipUiRevisionLogged = true;
                Log.Message("[XP Customizer][Skill Tooltip UI] UI_REVISION=" +
                    "TOOLTIP-NEUTRAL-HIDE-R5F4+GTL1-20261115; " +
                    "order=GW speed>Cons; gwsSourceRatio=HIDDEN_UI_ONLY; consSourceRatio=PRESERVED; " +
                    "gwsSource=WorkSpeedGlobal; consSource=Consciousness; " +
                    "factorUi=xROUND_PERCENT; furtherOrder=BASE+WILLPOWER_THEN_CONS; " +
                    "subtotalLine=REMOVED_R5F2; neutralEffect=HIDDEN_IF_EXACT_X1_BEFORE_ROUNDING; " +
                    "diagnosticCounters=UNCHANGED; gameplayFormula=UNCHANGED_R5F1; rimhudPatch=NONE; " +
                    "build=" + XpCustomizerMod.BuildId);
            }

            StringBuilder builder =
                new StringBuilder();


            // If another mod changes the vanilla breakdown structure,
            // preserve both our modifier lines together in the fallback.
            // The line order remains GW speed, then Cons (if non-neutral).
            if (includeLearningSpeedFallback)
            {
                string learningLines = BuildLearningSpeedModifierLines(skill);
                if (!string.IsNullOrEmpty(learningLines))
                {
                    builder.Append(learningLines);
                }
            }


            if (IsDailyCapacityPresentationActive())
            {
                AppendDailyCapacity(
                    builder,
                    skill);
            }


            if (IsFurtherLearningPresentationActive())
            {
                AppendFurtherLearning(
                    builder,
                    skill);
            }


            return builder
                .ToString()
                .Trim();
        }


        // ================================================================
        // Section spacing
        // ================================================================

        private static void AppendSectionSpacing(
            StringBuilder builder)
        {
            if (builder.Length > 0)
            {
                builder.Append(
                    "\n\n");
            }
        }
    }
}
