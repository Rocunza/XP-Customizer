namespace XpCustomizer.UI
{
    /// <summary>
    /// Persistent editing state for numeric settings fields.
    ///
    /// RimWorld's numeric text fields require a text buffer that survives
    /// between draw calls. lastValue lets callers detect when another UI
    /// action, preset, or reset changed the underlying setting so a stale
    /// editing buffer can be discarded safely.
    /// </summary>
    internal sealed class NumericInputState
    {
        public string buffer;
        public float lastValue = float.NaN;

        // A1 observation only. No change to the vanilla numeric widget's
        // editing/commit behavior. Unobserved nonempty buffers fail closed.
        internal string FocusControlName;
        internal bool FocusObserved;
        internal bool WasFocused;
        internal string LastObservedBuffer;
        internal int IdleObservations;


        public void Reset()
        {
            buffer = null;
            lastValue = float.NaN;
            FocusObserved = false;
            WasFocused = false;
            LastObservedBuffer = null;
            IdleObservations = 0;
        }
    }
}
