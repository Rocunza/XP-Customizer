using System;
using System.Collections.Generic;

namespace XpCustomizer.Progression
{
    internal static partial class CareerXpDistributionService
    {

        // Rounding selection and fallback. Methods moved without behavioral changes.

        public static int NormalizeRoundingStep(
            int roundingStep)
        {
            switch (roundingStep)
            {
                case XpRounding100:
                case XpRounding500:
                case XpRounding1000:
                    return roundingStep;

                default:
                    return XpRoundingExact;
            }
        }


        public static int GetEffectiveRoundingStep(
            int requestedRoundingStep,
            int targetTotalXp)
        {
            int normalizedStep =
                NormalizeRoundingStep(
                    requestedRoundingStep);

            if (normalizedStep == XpRoundingExact)
            {
                return 1;
            }


            // Keep every one of the 20 level-up requirements positive.
            // If the requested step is too coarse for a very small total,
            // automatically fall back to the next supported smaller step.
            int[] candidates =
            {
                XpRounding1000,
                XpRounding500,
                XpRounding100
            };

            for (int i = 0;
                 i < candidates.Length;
                 i++)
            {
                int candidate =
                    candidates[i];

                if (candidate > normalizedStep)
                {
                    continue;
                }

                if (targetTotalXp >=
                    candidate * LevelUpCount)
                {
                    return candidate;
                }
            }


            // With the current 0.01x minimum multiplier, 100 XP always fits.
            // Keep a 1-XP fallback anyway so future range changes stay safe.
            return 1;
        }
    }
}
