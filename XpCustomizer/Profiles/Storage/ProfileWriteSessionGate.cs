namespace XpCustomizer.Profiles
{
    /// <summary>
    /// R6-P1-C2-B1: a post-commit verification failure may leave the profile file
    /// in an uncertain state. Stop subsequent profile UI writes until restart/manual
    /// recovery. This does NOT affect normal game settings, Scribe or vanilla UI.
    /// </summary>
    internal static class ProfileWriteSessionGate
    {
        private static bool recoveryRequired;

        internal static bool IsBlocked { get { return recoveryRequired; } }

        internal static void Observe(ProfileStorageCode code)
        {
            if (code == ProfileStorageCode.RecoveryRequired)
                recoveryRequired = true;
        }

#if DEBUG
        internal static void ResetForIsolatedTests()
        {
            recoveryRequired = false;
        }
#endif
    }
}
