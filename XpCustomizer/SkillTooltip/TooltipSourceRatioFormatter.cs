using System.Globalization;

namespace XpCustomizer
{
    /// <summary>
    /// Presentation ONLY: represent source stat ratios with one required
    /// decimal and at most two decimal places, using a consistent dot.
    /// A formatted value must never feed gameplay or diagnostic arithmetic.
    /// Examples: 0.89, 1.0, 1.2, 1.35.
    /// </summary>
    internal static class TooltipSourceRatioFormatter
    {
        internal static string Format(float sourceRatio)
        {
            if (float.IsNaN(sourceRatio) || float.IsInfinity(sourceRatio))
                return "N/A";
            return sourceRatio.ToString("0.0#", CultureInfo.InvariantCulture);
        }
    }
}
