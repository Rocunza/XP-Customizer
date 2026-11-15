using System;

namespace XpCustomizer.Progression
{
    /// <summary>
    /// Integer-allocation helper used by the career-XP distribution engine.
    ///
    /// Keeps a requested total exactly conserved while distributing rounding
    /// remainder and enforcing minimum per-level allocations.
    /// </summary>
    internal static class CareerXpAllocator
    {
        internal static int[] AllocateIntegerTotal(
            int total,
            double[] weights)
        {
            int count =
                weights.Length;

            int[] result =
                new int[count];

            if (count == 0 ||
                total <= 0)
            {
                return result;
            }


            double weightTotal = 0d;

            for (int i = 0;
                 i < count;
                 i++)
            {
                if (weights[i] > 0d)
                {
                    weightTotal +=
                        weights[i];
                }
            }


            if (weightTotal <= 0d)
            {
                result[count - 1] =
                    total;

                return result;
            }


            double[] fractions =
                new double[count];

            int allocated = 0;

            for (int i = 0;
                 i < count;
                 i++)
            {
                double positiveWeight =
                    weights[i] > 0d
                        ? weights[i]
                        : 0d;

                double raw =
                    total *
                    positiveWeight /
                    weightTotal;

                int whole =
                    (int)Math.Floor(raw);

                result[i] =
                    whole;

                fractions[i] =
                    raw - whole;

                allocated +=
                    whole;
            }


            int remainder =
                total - allocated;

            while (remainder > 0)
            {
                int bestIndex = 0;
                double bestFraction = double.MinValue;

                for (int i = 0;
                     i < count;
                     i++)
                {
                    if (fractions[i] > bestFraction)
                    {
                        bestFraction =
                            fractions[i];

                        bestIndex =
                            i;
                    }
                }

                result[bestIndex]++;
                fractions[bestIndex] = double.MinValue;
                remainder--;
            }


            return result;
        }


        internal static void EnsureMinimumAllocations(
            int[] values,
            int[] minimums)
        {
            for (int targetIndex = 0;
                 targetIndex < values.Length;
                 targetIndex++)
            {
                int deficit =
                    minimums[targetIndex] -
                    values[targetIndex];

                while (deficit > 0)
                {
                    int donorIndex =
                        FindLargestSurplusIndex(
                            values,
                            minimums,
                            targetIndex);

                    if (donorIndex < 0)
                    {
                        return;
                    }

                    values[donorIndex]--;
                    values[targetIndex]++;
                    deficit--;
                }
            }
        }


        private static int FindLargestSurplusIndex(
            int[] values,
            int[] minimums,
            int excludedIndex)
        {
            int bestIndex = -1;
            int bestSurplus = 0;

            for (int i = 0;
                 i < values.Length;
                 i++)
            {
                if (i == excludedIndex)
                {
                    continue;
                }

                int surplus =
                    values[i] -
                    minimums[i];

                if (surplus > bestSurplus)
                {
                    bestSurplus =
                        surplus;

                    bestIndex =
                        i;
                }
            }

            return bestIndex;
        }
    }
}
