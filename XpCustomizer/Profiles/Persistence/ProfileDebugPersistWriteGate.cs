#if DEBUG
namespace XpCustomizer
{
    /// <summary>
    /// D2-D1B: one invocation of this mod's original vanilla writer is allowed
    /// only after an independently approved persistence test. The D1A runtime
    /// lock is NEVER reset. All other XP settings writes remain blocked until
    /// restart. No global Harmony patch or other-mod intercept is installed.
    /// </summary>
    internal sealed class ProfileDebugPersistWriteGate
    {
        internal const string Revision = "R6P1D2D1B-ONE-SHOT-VANILLA-WRITER-20261108";
        private static readonly ProfileDebugPersistWriteGate Live =
            new ProfileDebugPersistWriteGate();
        private bool testAttempted;
        private bool requested;
        private bool pending;
        private bool claimed;
        private bool finished;
        private bool returned;

        internal static bool LiveTestAttempted { get { return Live.testAttempted; } }
        internal static bool LiveAttempted { get { return Live.requested; } }
        internal static bool BeginLiveTest() { return Live.BeginTest(); }
        internal static bool LiveWriterClaimed { get { return Live.claimed; } }
        internal static bool LiveWriterReturned { get { return Live.finished && Live.returned; } }
        internal static bool ReserveLiveWrite() { return Live.Reserve(); }
        internal static bool TryClaimLiveWrite() { return Live.TryClaim(); }
        internal static void FinishLiveWrite(bool success) { Live.Finish(success); }
        internal static void CancelUnclaimedLiveWrite() { Live.CancelUnclaimed(); }

        private bool BeginTest()
        {
            if (testAttempted || requested || pending || claimed || finished) return false;
            // The RUN intent itself locks this mod's automatic XML save,
            // even when preflight rejects before D1A begins its runtime latch.
            testAttempted = true;
            return true;
        }
        private bool Reserve()
        {
            if (!testAttempted || requested || pending || claimed || finished) return false;
            requested = true; // one-way latch, even for a later failure
            pending = true;
            return true;
        }
        private bool TryClaim()
        {
            if (!pending || claimed || finished || !requested) return false;
            pending = false;
            claimed = true;
            return true;
        }
        private void Finish(bool success)
        {
            if (!claimed || finished) return;
            finished = true;
            returned = success;
        }
        private void CancelUnclaimed()
        {
            // A failed service must never leave an authorization for the
            // automatic Dialog_ModSettings.PreClose invocation.
            pending = false;
        }

        // Pure state-machine tests; never touch the real game's static latch.
        internal static ProfileDebugPersistWriteGate CreateIsolatedForAudit()
        { return new ProfileDebugPersistWriteGate(); }
        internal bool BeginTestIsolated() { return BeginTest(); }
        internal bool ReserveIsolated() { return Reserve(); }
        internal bool ClaimIsolated() { return TryClaim(); }
        internal void FinishIsolated(bool success) { Finish(success); }
        internal void CancelIsolated() { CancelUnclaimed(); }
        internal bool TestAttemptedIsolated { get { return testAttempted; } }
        internal bool RequestedIsolated { get { return requested; } }
        internal bool PendingIsolated { get { return pending; } }
        internal bool ClaimedIsolated { get { return claimed; } }
        internal bool FinishedIsolated { get { return finished; } }
        internal bool ReturnedIsolated { get { return returned; } }
    }
}
#endif
