using System.Collections.Generic;

namespace XpCustomizer
{
    // Read-only change detection. OFF->OFF does not acquire GeneDef ownership.
    internal static class SettingsRuntimeChangeDetector
    {
        internal static bool CareerCurveChanged(XpCustomizerSettings before, XpCustomizerSettings after)
        {
            return before.careerXpMultiplier != after.careerXpMultiplier ||
                before.careerXpUseCustomDistribution != after.careerXpUseCustomDistribution ||
                before.careerXpRoundingStep != after.careerXpRoundingStep ||
                !SequenceEqual(before.careerXpSplitLevels, after.careerXpSplitLevels) ||
                !SequenceEqual(before.careerXpStageWeights, after.careerXpStageWeights) ||
                !SequenceEqual(before.careerXpStageLockedShares, after.careerXpStageLockedShares);
        }

        internal static bool BiotechChanged(XpCustomizerSettings before, XpCustomizerSettings after)
        {
            if (!before.biotechCustomizationEnabled && !after.biotechCustomizationEnabled)
                return false; // Stored OFF values never take ownership of GeneDefs.
            return before.biotechCustomizationEnabled != after.biotechCustomizationEnabled ||
                before.biotechAwfulAptitude != after.biotechAwfulAptitude ||
                before.biotechPoorAptitude != after.biotechPoorAptitude ||
                before.biotechStrongAptitude != after.biotechStrongAptitude ||
                before.biotechGreatAptitude != after.biotechGreatAptitude ||
                before.biotechAwfulComplexity != after.biotechAwfulComplexity ||
                before.biotechPoorComplexity != after.biotechPoorComplexity ||
                before.biotechStrongComplexity != after.biotechStrongComplexity ||
                before.biotechGreatComplexity != after.biotechGreatComplexity ||
                before.biotechAwfulMetabolism != after.biotechAwfulMetabolism ||
                before.biotechPoorMetabolism != after.biotechPoorMetabolism ||
                before.biotechStrongMetabolism != after.biotechStrongMetabolism ||
                before.biotechGreatMetabolism != after.biotechGreatMetabolism;
        }

        private static bool SequenceEqual<T>(IList<T> left, IList<T> right)
        {
            if (left == null || right == null) return left == right;
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
                if (!EqualityComparer<T>.Default.Equals(left[i], right[i])) return false;
            return true;
        }

    }
}
