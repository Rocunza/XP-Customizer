using System.Collections.Generic;
using UnityEngine;
using XpCustomizer.Progression;

namespace XpCustomizer.UI
{
    /// <summary>
    /// Detached raw split draft. No GUI, settings, Scribe, save or global state.
    /// Raw indices stay stable until explicit normalize/apply/recommended.
    /// </summary>
    internal sealed class CareerStageBoundaryDraft
    {
        private readonly List<int> splits = new List<int>();

        internal int Count { get { return splits.Count; } }

        internal int this[int index]
        {
            get { return splits[index]; }
            set { splits[index] = value; }
        }

        internal void AddRaw(int value)
        {
            splits.Add(value);
        }

        internal void RemoveAt(int index)
        {
            splits.RemoveAt(index);
        }

        internal void Clear()
        {
            splits.Clear();
        }

        internal void ReplaceFromNormalized(IList<int> splitLevels)
        {
            int[] normalized =
                CareerXpDistributionService.NormalizeSplitLevels(splitLevels);

            splits.Clear();
            for (int i = 0; i < normalized.Length; i++)
                splits.Add(normalized[i]);
        }

        internal int[] GetNormalizedCopy()
        {
            return CareerXpDistributionService.NormalizeSplitLevels(splits);
        }

        internal bool TryAppendSuggested()
        {
            if (splits.Count >= 19)
                return false;

            splits.Add(FindSuggestedSplitLevel(splits));
            return true;
        }

        private static int FindSuggestedSplitLevel(
            IList<int> currentSplits)
        {
            int[] normalized =
                CareerXpDistributionService.NormalizeSplitLevels(
                    currentSplits);


            int bestStart =
                0;

            int bestEnd =
                20;

            int bestGap =
                -1;

            int previousBoundary =
                0;


            for (int i = 0;
                 i <= normalized.Length;
                 i++)
            {
                int nextBoundary =
                    i < normalized.Length
                        ? normalized[i]
                        : 20;

                int gap =
                    nextBoundary -
                    previousBoundary;


                if (gap > bestGap)
                {
                    bestStart =
                        previousBoundary;

                    bestEnd =
                        nextBoundary;

                    bestGap =
                        gap;
                }


                previousBoundary =
                    nextBoundary;
            }


            if (bestGap <= 1)
            {
                return FindFirstUnusedSplit(
                    normalized);
            }


            int suggested =
                bestStart +
                (bestGap / 2);


            if (suggested <= bestStart)
            {
                suggested =
                    bestStart + 1;
            }

            if (suggested >= bestEnd)
            {
                suggested =
                    bestEnd - 1;
            }


            return Mathf.Clamp(
                suggested,
                1,
                19);
        }


        private static int FindFirstUnusedSplit(
            int[] normalizedSplits)
        {
            for (int level = 1;
                 level < 20;
                 level++)
            {
                bool used =
                    false;


                for (int i = 0;
                     i < normalizedSplits.Length;
                     i++)
                {
                    if (normalizedSplits[i] == level)
                    {
                        used =
                            true;

                        break;
                    }
                }


                if (!used)
                {
                    return level;
                }
            }


            return 19;
        }


    }
}
