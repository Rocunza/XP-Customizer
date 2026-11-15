#if DEBUG
using System;

namespace XpCustomizer
{
    /// <summary>
    /// D2-D1A: isolated, Debug-only permission and XML-write latch.
    /// An attempted real runtime profile transition MUST NOT accidentally
    /// persist when vanilla closes Dialog_ModSettings. There is deliberately
    /// no live reset: restart the process to restore regular XP XML saving.
    /// Other mods and game saves are not intercepted.
    /// </summary>
    internal sealed class ProfileDebugLiveApplySessionGuard
    {
        internal const string Revision = "R6P1D2D1A-DEBUG-RUNTIME-ONLY-20261108";
        private static readonly ProfileDebugLiveApplySessionGuard Live =
            new ProfileDebugLiveApplySessionGuard();
        private bool armed;
        private bool permit;
        private bool consumed;

        internal static bool LiveWriteBlocked { get { return Live.armed; } }
        internal static bool LiveAttempted { get { return Live.armed; } }
        internal static bool BeginLiveAttempt() { return Live.BeginAttempt(); }
        internal static bool ConsumeLivePermit() { return Live.ConsumePermit(); }

        private bool BeginAttempt()
        {
            if (armed || permit || consumed) return false;
            // Arm BEFORE any live write. Never clear after a success/failure.
            armed = true;
            permit = true;
            return true;
        }

        private bool ConsumePermit()
        {
            if (!armed || !permit || consumed) return false;
            permit = false;
            consumed = true;
            return true;
        }

        // Pure state-machine regression; must NEVER touch Live on startup.
        internal static ProfileDebugLiveApplySessionGuard CreateIsolatedForAudit()
        { return new ProfileDebugLiveApplySessionGuard(); }
        internal bool BeginForIsolatedAudit() { return BeginAttempt(); }
        internal bool ConsumeForIsolatedAudit() { return ConsumePermit(); }
        internal bool IsArmedForIsolatedAudit { get { return armed; } }
        internal bool HasPermitForIsolatedAudit { get { return permit; } }
        internal bool IsConsumedForIsolatedAudit { get { return consumed; } }
    }
}
#endif
