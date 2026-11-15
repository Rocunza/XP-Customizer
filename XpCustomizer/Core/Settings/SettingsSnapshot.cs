using System;

namespace XpCustomizer
{
    /// <summary>
    /// An isolated in-memory capture of XP Customizer settings.
    /// This is NOT a saved profile or an apply operation (R6-P0-A/B only).
    /// The only way to retrieve the data is to receive another independent copy.
    /// </summary>
    internal sealed class SettingsSnapshot
    {
        private readonly XpCustomizerSettings captured;

        internal SettingsSnapshot(XpCustomizerSettings independentCapture)
        {
            if (independentCapture == null)
                throw new ArgumentNullException("independentCapture");

            captured = independentCapture;
        }

        internal XpCustomizerSettings CreateDetachedCopy()
        {
            return SettingsSnapshotService.CloneIndependent(captured);
        }
    }
}
