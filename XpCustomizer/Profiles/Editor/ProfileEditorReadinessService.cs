using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using XpCustomizer.UI;

namespace XpCustomizer.Profiles
{
    /// <summary>
    /// R6-P1-C2-B2-A1: read-only inspection of XP Customizer numeric editors.
    /// A valid buffer can be Ready only after its actual XP-owned text control
    /// has been observed unfocused with the same buffer. Unseen/stale editors
    /// still fail closed. The inspection never focuses, commits or saves.
    /// </summary>
    internal enum ProfileEditorReadinessCode { Ready, PendingEdit, Uncertain }

    internal sealed class ProfileEditorReadinessResult
    {
        internal readonly ProfileEditorReadinessCode Code;
        internal readonly string Source;
        internal readonly int CoveredGroups;
        internal readonly int ProbedBuffers;
        internal readonly int UntrackedFocusBuffers;
        internal readonly int InactiveDecayBuffers;
        internal ProfileEditorReadinessResult(ProfileEditorReadinessCode code, string source,
            int coveredGroups, int probedBuffers, int untrackedFocusBuffers,
            int inactiveDecayBuffers = 0)
        {
            Code = code;
            Source = source ?? "NONE";
            CoveredGroups = coveredGroups;
            ProbedBuffers = probedBuffers;
            UntrackedFocusBuffers = untrackedFocusBuffers;
            InactiveDecayBuffers = inactiveDecayBuffers;
        }
    }

    internal static partial class ProfileEditorReadinessService
    {
        internal const string Revision = "R6P1C2B2A11-DISABLED-DECAY-OBSERVED-20261107";
        // Fixed allowlist of every settings UI class holding numeric input state.
        // When adding a new numeric editor, extend this list and its audit.
        private static readonly Type[] Sources = {
            typeof(CareerProgressionSettingsUI),
            typeof(CareerXpStageBoundaryEditorUI),
            typeof(BookXpSettingsUI),
            typeof(DecaySettingsUI),
            typeof(DecayModifiersSettingsUI),
            typeof(SkillSpecificXpSettingsUI),
            typeof(SkilltrainerSpecificXpSettingsUI),
            typeof(SkilltrainerSettingsUI),
            typeof(Level20ReserveSettingsUI),
            typeof(LearningCapacitySettingsUI),
            typeof(FurtherLearningSettingsUI),
            typeof(GwsSettingsUI),
            typeof(ConsciousnessAnchorV2SettingsUI),
            typeof(ConsciousnessFurtherLearningSettingsUI),
            typeof(BiotechSettingsUI)
        };
        internal static int KnownSourceCount { get { return Sources.Length; } }

        private sealed class Probe
        {
            internal int Buffers;
            internal int UntrackedFocus;
            internal int InactiveDecay;
            internal bool Pending;
            internal bool Uncertain;
            internal string Source;
            internal readonly HashSet<object> Seen = new HashSet<object>();

            internal void Mark(ProfileEditorReadinessCode code, string source)
            {
                if (code == ProfileEditorReadinessCode.PendingEdit)
                {
                    Pending = true;
                    Source = source;
                }
                else if (code == ProfileEditorReadinessCode.Uncertain && !Pending)
                {
                    Uncertain = true;
                    if (Source == null) Source = source;
                }
            }
        }

        internal static ProfileEditorReadinessResult Check()
        {
            Probe p = new Probe();
            int covered = 0;
            try
            {
                foreach (Type source in Sources)
                {
                    int holders = 0;
                    foreach (FieldInfo field in source.GetFields(BindingFlags.Static |
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (IsEditorHolder(field.FieldType, field.Name, source))
                        {
                            holders++;
                            Visit(field.GetValue(null), field.FieldType, source.Name,
                                source.Name + "." + field.Name, p, 0);
                        }
                        if (source == typeof(CareerXpStageBoundaryEditorUI) &&
                            field.Name == "editorOpen" && (bool)field.GetValue(null))
                            p.Mark(ProfileEditorReadinessCode.PendingEdit, "CareerStageDraft");
                    }
                    // Empty dictionaries/lists still count as monitored if their
                    // owning field exists. A deleted/renamed holder fails closed.
                    if (holders == 0)
                        return new ProfileEditorReadinessResult(
                            ProfileEditorReadinessCode.Uncertain,
                            "MISSING_EDITOR_SOURCE_" + source.Name, covered,
                            p.Buffers, p.UntrackedFocus, p.InactiveDecay);
                    covered++;
                }
            }
            catch (Exception)
            {
                return new ProfileEditorReadinessResult(ProfileEditorReadinessCode.Uncertain,
                    "EDITOR_INSPECTION_FAILED", covered, p.Buffers, p.UntrackedFocus,
                    p.InactiveDecay);
            }
            // A1 only observes focus; no profile write route exists. Any buffer
            // that was not drawn/observed since its latest change fails closed.
            if (p.Pending)
                return new ProfileEditorReadinessResult(ProfileEditorReadinessCode.PendingEdit,
                    p.Source, covered, p.Buffers, p.UntrackedFocus, p.InactiveDecay);
            if (p.Uncertain || p.UntrackedFocus > 0)
                return new ProfileEditorReadinessResult(ProfileEditorReadinessCode.Uncertain,
                    p.Source ?? "TEXTFIELDNUMERIC_FOCUS_UNTRACKED", covered,
                    p.Buffers, p.UntrackedFocus, p.InactiveDecay);
            return new ProfileEditorReadinessResult(ProfileEditorReadinessCode.Ready,
                "ALL_TRACKED_IDLE", covered, p.Buffers, 0, p.InactiveDecay);
        }

        // Pure policy used by DEBUG audit: the result never mutates the input.
        internal static ProfileEditorReadinessCode ClassifyTextBuffer(string raw,
            bool hasFocusState, bool wasFocused)
        {
            if (wasFocused) return ProfileEditorReadinessCode.PendingEdit;
            if (raw == null) return ProfileEditorReadinessCode.Ready;
            string text = raw.Trim();
            if (text.Length == 0 || text == "+" || text == "-" ||
                text.EndsWith(".", StringComparison.Ordinal) ||
                text.EndsWith(",", StringComparison.Ordinal) ||
                text.EndsWith("e", StringComparison.OrdinalIgnoreCase) ||
                text.EndsWith("e+", StringComparison.OrdinalIgnoreCase) ||
                text.EndsWith("e-", StringComparison.OrdinalIgnoreCase))
                return ProfileEditorReadinessCode.PendingEdit;
            float value;
            if (!(float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture,
                out value) || float.TryParse(text, NumberStyles.Float,
                CultureInfo.InvariantCulture, out value)) ||
                float.IsNaN(value) || float.IsInfinity(value))
                return ProfileEditorReadinessCode.PendingEdit;
            // A value looking valid can still be only a prefix of the user's
            // intended value if the control's focus is not instrumented.
            return hasFocusState ? ProfileEditorReadinessCode.Ready :
                ProfileEditorReadinessCode.Uncertain;
        }

        private static bool IsEditorHolder(Type type, string name, Type source)
        {
            if (type == typeof(NumericInputState)) return true;
            if (name.IndexOf("buffer", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (type == typeof(string) || type == typeof(string[]))) return true;
            if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string) &&
                (name.IndexOf("input", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 name.IndexOf("buffer", StringComparison.OrdinalIgnoreCase) >= 0)) return true;
            return (type.DeclaringType == source ||
                (type.DeclaringType != null && source.DeclaringType != null &&
                 type.DeclaringType == source.DeclaringType)) &&
                (type.Name.IndexOf("Editor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 type.Name.IndexOf("Buffer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 type.Name.IndexOf("State", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // Pure policy: a nonempty raw buffer must be tied to an observed,
        // unfocused XP Customizer control, not merely a parseable number.
        internal static ProfileEditorReadinessCode ClassifyObservedText(
            string raw, bool observed, bool focused, bool sameBuffer)
        {
            if (focused) return ProfileEditorReadinessCode.PendingEdit;
            if (raw == null) return ProfileEditorReadinessCode.Ready;
            // A malformed/partial draft is pending regardless of whether a
            // focus observation has been established yet.
            if (ClassifyTextBuffer(raw, true, false) ==
                ProfileEditorReadinessCode.PendingEdit)
                return ProfileEditorReadinessCode.PendingEdit;
            if (!observed || !sameBuffer) return ProfileEditorReadinessCode.Uncertain;
            return ProfileEditorReadinessCode.Ready;
        }

        private static void Visit(object item, Type type, string origin,
            string identity, Probe p, int depth)
        {
            if (item == null || depth > 5) return;
            if (item is string)
            {
                p.Buffers++;
                bool focused, same;
                bool observed = ProfileNumericFocus.TryGetRaw(identity,
                    (string)item, out focused, out same);
                if (origin == "DecaySettingsUI" &&
                    ProfileNumericFocus.IsInactiveRaw(identity))
                    p.InactiveDecay++;
                var code = ClassifyObservedText((string)item, observed, focused, same);
                if (code == ProfileEditorReadinessCode.Uncertain) p.UntrackedFocus++;
                p.Mark(code, origin);
                return;
            }
            if (item is NumericInputState)
            {
                NumericInputState numeric = (NumericInputState)item;
                p.Buffers++;
                var code = ClassifyObservedText(numeric.buffer,
                    numeric.FocusObserved, numeric.WasFocused,
                    string.Equals(numeric.LastObservedBuffer, numeric.buffer,
                        StringComparison.Ordinal));
                if (code == ProfileEditorReadinessCode.Uncertain) p.UntrackedFocus++;
                p.Mark(code, origin);
                return;
            }
            if (type.IsPrimitive || type.IsEnum) return;
            if (!p.Seen.Add(item)) return;
            IDictionary dictionary = item as IDictionary;
            if (dictionary != null)
            {
                foreach (DictionaryEntry entry in dictionary)
                    Visit(entry.Value, entry.Value == null ? typeof(object) :
                        entry.Value.GetType(), origin,
                        identity + "[" + (entry.Key == null ? "null" : entry.Key.ToString()) + "]",
                        p, depth + 1);
                return;
            }
            IEnumerable sequence = item as IEnumerable;
            if (sequence != null)
            {
                int index = 0;
                foreach (object entry in sequence)
                {
                    Visit(entry, entry == null ? typeof(object) : entry.GetType(),
                        origin, identity + "[" + index + "]", p, depth + 1);
                    index++;
                }
                return;
            }
            // Traverse only our own nested private editor holders, never
            // RimWorld, Unity or other mod game objects.
            if (type.DeclaringType == null || type.DeclaringType.Namespace != "XpCustomizer.UI")
                return;
            bool hasFocus = false;
            bool wasFocused = false;
            string buffer = null;
            bool hasBuffer = false;
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (field.Name == "WasFocused" && field.FieldType == typeof(bool))
                { hasFocus = true; wasFocused = (bool)field.GetValue(item); }
                if (field.Name == "Buffer" && field.FieldType == typeof(string))
                { hasBuffer = true; buffer = field.GetValue(item) as string; }
            }
            if (hasBuffer && hasFocus)
            {
                p.Buffers++;
                p.Mark(ClassifyTextBuffer(buffer, true, wasFocused), origin);
                return;
            }
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (IsEditorHolder(field.FieldType, field.Name, type))
                    Visit(field.GetValue(item), field.FieldType, origin,
                        identity + "." + field.Name, p, depth + 1);
        }
    }
}
