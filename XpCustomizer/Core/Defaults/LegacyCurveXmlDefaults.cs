namespace XpCustomizer
{
    /// <summary>
    /// Historical Scribe_Values.Look fallbacks, NOT fresh Mod Defaults.
    /// Keep old omitted XML keys semantically stable when changing the
    /// editing defaults. Do not reuse these as constructor or preset values.
    /// </summary>
    internal static class LegacyCurveXmlDefaults
    {
        internal const float GwsHighAnchor = 1.80f;
        internal const float GwsHighFactor = 1.20f;
        internal const int GwsHighCurve = 1; // Linear
        internal const float GwsHighCurvature = 1f;
        internal const float GwsHighDiminishingLimit = 1.30f;

        internal const float ConsSpeedCapacityHighAnchor = 1.35f;
        internal const float ConsSpeedCapacityHighFactor = 1.10f;
        internal const float ConsFurtherHighAnchor = 1.35f;
        internal const float ConsFurtherHighFactor = 1.35f;
    }
}
