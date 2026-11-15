using System;
using System.Collections.Generic;

namespace XpCustomizer.Progression
{
    internal static partial class CareerXpDistributionService
    {

        // Optional stage share locks
        // -1 means unlocked. A nonnegative number is the target fraction
        // of the entire career XP pool (e.g. 0.50 means 50%).
        // The UI guarantees at least one unlocked stage. Defensive handling
        // below also treats the last stage as free when saved data is invalid.

        public static float NormalizeLockedStageShare(float share)
        {
            if (float.IsNaN(share) || float.IsInfinity(share) || share < 0f)
            {
                return -1f;
            }
            return Math.Max(0f, Math.Min(1f, share));
        }


        private static bool HasActiveShareLocks(
            int stageCount, bool useCustom, IList<float> lockedShares)
        {
            if (!useCustom || stageCount < 2 || lockedShares == null)
            {
                return false;
            }
            for (int i = 0; i < stageCount - 1; i++)
            {
                if (i < lockedShares.Count &&
                    NormalizeLockedStageShare(lockedShares[i]) >= 0f)
                {
                    return true;
                }
            }
            // If only the last stage is locked, it is valid too.
            return lockedShares.Count >= stageCount &&
                NormalizeLockedStageShare(lockedShares[stageCount - 1]) >= 0f &&
                CountLocked(stageCount, lockedShares) < stageCount;
        }


        private static int CountLocked(int stageCount, IList<float> lockedShares)
        {
            int count = 0;
            if (lockedShares == null) return 0;
            for (int i = 0; i < stageCount && i < lockedShares.Count; i++)
            {
                if (NormalizeLockedStageShare(lockedShares[i]) >= 0f)
                    count++;
            }
            return count;
        }


        private static int[] AllocateShareLockedLevels(
            int totalUnits, int[] boundaries, double[] levelScores,
            IList<float> lockedShares)
        {
            int count = boundaries.Length - 1;
            int[] minimums = new int[count];
            double[] stageScores = new double[count];
            bool[] locked = new bool[count];
            int lockedCount = 0;
            for (int stage = 0; stage < count; stage++)
            {
                minimums[stage] = boundaries[stage + 1] - boundaries[stage];
                for (int level = boundaries[stage]; level < boundaries[stage + 1]; level++)
                    stageScores[stage] += levelScores[level];
                locked[stage] = stage < lockedShares.Count &&
                    NormalizeLockedStageShare(lockedShares[stage]) >= 0f;
                if (locked[stage]) lockedCount++;
            }
            // Corrupted/hand-edited configurations must never exhaust all
            // adjustable stages. Keep the last stage as a remainder bucket.
            if (lockedCount == count)
                locked[count - 1] = false;

            int[] stageUnits = new int[count];
            int remaining = totalUnits;
            for (int stage = 0; stage < count; stage++)
            {
                if (!locked[stage]) continue;
                int minOthers = 0;
                for (int other = 0; other < count; other++)
                    if (other != stage && stageUnits[other] == 0)
                        minOthers += minimums[other];

                double requestedShare = NormalizeLockedStageShare(lockedShares[stage]);
                int requested = (int)Math.Round(
                    totalUnits * requestedShare, MidpointRounding.AwayFromZero);
                int maxAllowed = remaining - minOthers;
                stageUnits[stage] = Math.Max(minimums[stage],
                    Math.Min(maxAllowed, requested));
                remaining -= stageUnits[stage];
            }

            int freeMin = 0;
            double[] freeScores = new double[count];
            for (int stage = 0; stage < count; stage++)
                if (!locked[stage])
                {
                    freeMin += minimums[stage];
                    freeScores[stage] = stageScores[stage];
                }

            int[] freeExtras = CareerXpAllocator.AllocateIntegerTotal(
                Math.Max(0, remaining - freeMin), freeScores);
            for (int stage = 0; stage < count; stage++)
                if (!locked[stage])
                    stageUnits[stage] = minimums[stage] + freeExtras[stage];

            int[] result = new int[LevelUpCount];
            for (int stage = 0; stage < count; stage++)
            {
                int first = boundaries[stage];
                int levelCount = minimums[stage];
                double[] scores = new double[levelCount];
                for (int offset = 0; offset < levelCount; offset++)
                    scores[offset] = levelScores[first + offset];
                int[] extras = CareerXpAllocator.AllocateIntegerTotal(
                    stageUnits[stage] - levelCount, scores);
                for (int offset = 0; offset < levelCount; offset++)
                    result[first + offset] = 1 + extras[offset];
            }
            return result;
        }
    }
}
