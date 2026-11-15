namespace XpCustomizer
{
    /// <summary>
    /// Isolated, deterministic one-shot state machine for this mod's own XML.
    /// A test instance never changes the live gate. No Scribe or file I/O here.
    /// </summary>
    internal sealed class ProfileProductionWriteState
    {
        private bool inFlight;
        private bool pending;
        private bool claimed;
        private bool writerReturned;
        private bool uncertain;

        internal bool IsUncertain { get { return uncertain; } }
        internal bool IsBusy { get { return inFlight; } }
        internal bool OwnXmlWriteBlocked { get { return uncertain || inFlight; } }
        internal bool WriterClaimed { get { return claimed; } }
        internal bool WriterReturned { get { return claimed && writerReturned; } }

        internal bool Begin()
        {
            if (inFlight || pending || uncertain) return false;
            inFlight = true;
            claimed = false;
            writerReturned = false;
            return true;
        }

        internal bool Reserve()
        {
            if (!inFlight || pending || claimed || uncertain) return false;
            pending = true;
            return true;
        }

        internal bool TryClaim()
        {
            if (!inFlight || !pending || claimed || uncertain) return false;
            pending = false;
            claimed = true;
            return true;
        }

        internal void FinishWrite(bool returned)
        {
            if (!inFlight || !claimed) return;
            writerReturned = returned;
            if (!returned) uncertain = true;
        }

        internal bool CompleteVerified()
        {
            if (!inFlight || pending || !claimed || !writerReturned || uncertain) return false;
            inFlight = false;
            return true;
        }

        internal bool AbortKnownUnchanged()
        {
            if (!inFlight || claimed || uncertain) return false;
            pending = false;
            inFlight = false;
            return true;
        }

        internal void LatchUncertain()
        {
            pending = false;
            inFlight = false;
            uncertain = true;
        }

        internal void CancelUnclaimed()
        {
            pending = false;
        }
    }

    /// <summary>
    /// R6-P1-D2-D3 / F1: only the original XpCustomizerMod.WriteSettings
    /// consumes this singleton's scoped permit. No vanilla or foreign mod edits.
    /// </summary>
    internal static class ProfileProductionWriteGate
    {
        internal const string Revision = "R6P1D2D3-F1-ISOLATED-STATE-20261108";
        private static readonly ProfileProductionWriteState Live = new ProfileProductionWriteState();

        internal static bool IsUncertain { get { return Live.IsUncertain; } }
        internal static bool IsBusy { get { return Live.IsBusy; } }
        internal static bool OwnXmlWriteBlocked { get { return Live.OwnXmlWriteBlocked; } }
        internal static bool WriterClaimed { get { return Live.WriterClaimed; } }
        internal static bool WriterReturned { get { return Live.WriterReturned; } }
        internal static bool Begin() { return Live.Begin(); }
        internal static bool Reserve() { return Live.Reserve(); }
        internal static bool TryClaim() { return Live.TryClaim(); }
        internal static void FinishWrite(bool returned) { Live.FinishWrite(returned); }
        internal static bool CompleteVerified() { return Live.CompleteVerified(); }
        internal static bool AbortKnownUnchanged() { return Live.AbortKnownUnchanged(); }
        internal static void LatchUncertain() { Live.LatchUncertain(); }
        internal static void CancelUnclaimed() { Live.CancelUnclaimed(); }
    }
}
