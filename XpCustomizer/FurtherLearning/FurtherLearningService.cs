using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.ConsciousnessLearning;

namespace XpCustomizer.FurtherLearning
{
    /// <summary>
    /// Post-cap factor = (base + Mental Break Threshold percentage points)
    /// * independent Consciousness multiplier. The mental stat is read through
    /// RimWorld so traits, genes and other mods can contribute normally.
    /// Legacy trait fields are saved for rollback, but never affect gameplay.
    /// </summary>
    public static class FurtherLearningService
    {
        public const float VanillaFactor = 0.20f;
        public const float NeutralThreshold = 0.35f;
        public const float StableThreshold = 0.17f;
        public const float UnstableThreshold = 0.50f;
        public const float MaxAnchorAdjustment = 10f; // percentage points at 100% strength

        private static StatDef mentalThresholdDef;
        private static bool missingStatWarningLogged;

        public static bool IsConfiguredToChangeGameplay()
        {
            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            if (settings == null) return false;

            if (!Mathf.Approximately(
                NormalizeBaseFactor(settings.furtherLearningBaseFactor), VanillaFactor))
                return true;

            if (settings.furtherLearningMentalThresholdEnabled &&
                !Mathf.Approximately(
                    NormalizeThresholdStrength(settings.furtherLearningMentalThresholdStrength), 0f))
                return true;

            return ConsciousnessFurtherLearningService.HasConfiguredEffect(settings);
        }

        public static float GetFactor(SkillRecord skill) => GetFactor(skill?.Pawn);

        public static float GetFactor(Pawn pawn)
        {
            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            if (settings == null) return VanillaFactor;
            float baseFactor, rawFactor;
            return CalculateFactor(pawn, settings, null, out baseFactor, out rawFactor);
        }

        public static FurtherLearningBreakdown GetBreakdown(SkillRecord skill) =>
            GetBreakdown(skill?.Pawn);

        public static FurtherLearningBreakdown GetBreakdown(Pawn pawn)
        {
            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            if (settings == null)
                return new FurtherLearningBreakdown(
                    VanillaFactor, new List<FurtherLearningContribution>(), VanillaFactor, VanillaFactor);

            var contributions = new List<FurtherLearningContribution>();
            float baseFactor, rawFactor;
            float factor = CalculateFactor(pawn, settings, contributions,
                out baseFactor, out rawFactor);
            return new FurtherLearningBreakdown(baseFactor, contributions, rawFactor, factor);
        }

        private static float CalculateFactor(
            Pawn pawn, XpCustomizerSettings settings,
            List<FurtherLearningContribution> contributions,
            out float baseFactor, out float rawFactor)
        {
            baseFactor = NormalizeBaseFactor(settings.furtherLearningBaseFactor);
            float consMultiplier = 1f;
            float mentalPoints = 0f;

            if (pawn != null)
            {
                // Consciousness multiplies the subtotal (base + MTH points).
                // This must match the independent 4C-C oracle and tooltip.
                if (settings.consciousnessAffectsLearning &&
                    settings.consciousnessAffectsFurtherLearning &&
                    pawn.health?.capacities != null)
                {
                    consMultiplier = ConsciousnessFurtherLearningService.GetMultiplier(pawn);
                }

                // Read vanilla's actual stat, including other mods' effects.
                // This remains humanlike-only and additive in pp.
                if (settings.furtherLearningMentalThresholdEnabled &&
                    pawn.RaceProps != null && pawn.RaceProps.Humanlike &&
                    !Mathf.Approximately(settings.furtherLearningMentalThresholdStrength, 0f))
                {
                    float threshold;
                    if (TryGetMentalThreshold(pawn, out threshold))
                    {
                        mentalPoints = CalculateThresholdAdjustment(
                            threshold, settings.furtherLearningMentalThresholdStrength);
                        if (contributions != null)
                            AddContributionIfNonZero(contributions,
                                "Mental threshold " + Mathf.RoundToInt(threshold * 100f) + "%",
                                mentalPoints);
                    }
                }

                // Present the multiplier AFTER additive MTH points, matching
                // the new gameplay order and independent proof.
                if (contributions != null && !Mathf.Approximately(consMultiplier, 1f))
                {
                    float consciousness = Mathf.Max(0f,
                        pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness));
                    contributions.Add(FurtherLearningContribution.CreateMultiplier(
                        "Cons " + TooltipSourceRatioFormatter.Format(consciousness),
                        consMultiplier));
                }
            }

            rawFactor = CalculateRawFactor(baseFactor, consMultiplier, mentalPoints);
            return Mathf.Max(rawFactor, 0f);
        }

        // Same arithmetic as the live Pawn path, exposed for pure regression
        // tests. The raw value may be below zero; only gameplay floors it.
        internal static float CalculateRawFactor(float baseFactor,
            float consciousnessMultiplier, float mentalAdjustmentPoints)
        {
            return (baseFactor + mentalAdjustmentPoints / 100f) * consciousnessMultiplier;
        }

        private static bool TryGetMentalThreshold(Pawn pawn, out float threshold)
        {
            threshold = NeutralThreshold;
            if (pawn == null) return false;

            // Do not overwrite or patch the stat itself. This call includes
            // active source changes from other mods and vanilla game systems.
            if (mentalThresholdDef == null)
                mentalThresholdDef = DefDatabase<StatDef>.GetNamedSilentFail("MentalBreakThreshold");
            if (mentalThresholdDef == null)
            {
                if (!missingStatWarningLogged)
                {
                    missingStatWarningLogged = true;
                    Log.Warning("[XP Customizer] MTH-V1 stat resolution failed: MentalBreakThreshold missing. Mental effect skipped without modifying vanilla stats.");
                }
                return false;
            }

            threshold = Mathf.Clamp(pawn.GetStatValue(mentalThresholdDef), 0.01f, 0.50f);
            return true;
        }

        /// <summary>
        /// Piecewise linear curve: 17% => +10pp, 35% => 0pp, 50% => -10pp
        /// at strength 100% (1.0). This asymmetry is necessary because the
        /// anchor distances are 18pp below and 15pp above 35%.
        /// </summary>
        public static float CalculateThresholdAdjustment(float threshold, float strength)
        {
            // Limit the reward to the specified 17% anchor even when a
            // different mod pushes the mental threshold below vanilla's
            // Iron-willed level. Prevents runaway stacking at 1%.
            threshold = Mathf.Clamp(threshold, StableThreshold, UnstableThreshold);
            strength = NormalizeThresholdStrength(strength);
            float proportion = threshold <= NeutralThreshold
                ? (NeutralThreshold - threshold) / (NeutralThreshold - StableThreshold)
                : -(threshold - NeutralThreshold) / (UnstableThreshold - NeutralThreshold);
            return MaxAnchorAdjustment * proportion * strength;
        }

        public static float NormalizeThresholdStrength(float strength) =>
            Mathf.Clamp(strength,
                XpCustomizerDefaults.FurtherLearningMentalStrengthMin,
                XpCustomizerDefaults.FurtherLearningMentalStrengthMax);

        public static float GetBaseFactor()
        {
            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            return settings == null ? VanillaFactor :
                NormalizeBaseFactor(settings.furtherLearningBaseFactor);
        }

        public static float NormalizeBaseFactor(float value) =>
            Mathf.Clamp(value,
                XpCustomizerDefaults.FurtherLearningBaseMin,
                XpCustomizerDefaults.FurtherLearningBaseMax);

        private static void AddContributionIfNonZero(
            List<FurtherLearningContribution> contributions, string label, float points)
        {
            if (contributions != null && !Mathf.Approximately(points, 0f))
                contributions.Add(new FurtherLearningContribution(label, points));
        }

        /// <summary>
        /// Legacy settings inspection ONLY. This is used for one-time old
        /// settings migration; traits do not contribute to gameplay now.
        /// </summary>
        public static bool HasNonzeroTraitModifiers(XpCustomizerSettings s)
        {
            if (s == null) return false;
            return s.furtherLearningVolatileModifier != 0f ||
                s.furtherLearningNervousModifier != 0f ||
                s.furtherLearningSteadfastModifier != 0f ||
                s.furtherLearningIronWilledModifier != 0f ||
                s.furtherLearningSlothfulModifier != 0f ||
                s.furtherLearningLazyModifier != 0f ||
                s.furtherLearningHardWorkerModifier != 0f ||
                s.furtherLearningIndustriousModifier != 0f ||
                s.furtherLearningNeuroticModifier != 0f ||
                s.furtherLearningVeryNeuroticModifier != 0f;
        }
    }
}
