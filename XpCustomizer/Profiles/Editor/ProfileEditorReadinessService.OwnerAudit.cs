using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using XpCustomizer.UI;

namespace XpCustomizer.Profiles
{
    /// <summary>
    /// Read-only, per-owner diagnostics. Unlike the legacy Check().Source, an
    /// owner report never hides a second simultaneous pending editor. It is
    /// NOT an authorization to discard, settle or open a profile.
    /// </summary>
    internal sealed class ProfileEditorOwnerObservation
    {
        internal readonly Type OwnerType;
        internal readonly int PendingFields;
        internal readonly int UncertainFields;
        internal readonly int ExaminedFields;
        internal readonly string OnlyPendingField;
        internal readonly string OnlyPendingOwnedControl;
        internal readonly bool InspectionComplete;

        internal ProfileEditorOwnerObservation(Type ownerType, int pending, int uncertain,
            int examined, string onlyField, string onlyControl, bool complete)
        {
            OwnerType = ownerType;
            PendingFields = pending;
            UncertainFields = uncertain;
            ExaminedFields = examined;
            OnlyPendingField = pending == 1 ? onlyField : null;
            OnlyPendingOwnedControl = pending == 1 ? onlyControl : null;
            InspectionComplete = complete;
        }
    }

    internal static partial class ProfileEditorReadinessService
    {
        private sealed class OwnerAuditProbe
        {
            internal int Examined;
            internal int Pending;
            internal int Uncertain;
            internal string OnlyField;
            internal string OnlyControl;
            internal readonly HashSet<object> Seen = new HashSet<object>();

            internal void Mark(ProfileEditorReadinessCode result,
                string fieldId, string controlName)
            {
                Examined++;
                if (result == ProfileEditorReadinessCode.PendingEdit)
                {
                    Pending++;
                    if (Pending == 1)
                    {
                        OnlyField = fieldId;
                        OnlyControl = ProfileNumericFocus.IsNumericControl(controlName)
                            ? controlName : null;
                    }
                    else
                    {
                        OnlyField = null;
                        OnlyControl = null;
                    }
                }
                else if (result == ProfileEditorReadinessCode.Uncertain)
                {
                    Uncertain++;
                }
            }
        }

        // Defensive snapshot for comparing the explicit recovery registry to
        // the existing readiness allowlist. No mutable internal array escapes.
        internal static Type[] CopyKnownSourceTypes()
        {
            return (Type[])Sources.Clone();
        }

        // Called on an explicit blocked navigation only; not each IMGUI frame.
        // Check() is intentionally not refactored: its original decisions and
        // 2x Ready / 32x Repaint state machine remain byte-for-byte unchanged.
        internal static ProfileEditorOwnerObservation[] InspectOwnersReadOnly()
        {
            ProfileEditorOwnerObservation[] results =
                new ProfileEditorOwnerObservation[Sources.Length];
            for (int i = 0; i < Sources.Length; ++i)
            {
                Type owner = Sources[i];
                OwnerAuditProbe probe = new OwnerAuditProbe();
                bool complete = true;
                int holders = 0;
                try
                {
                    foreach (FieldInfo field in owner.GetFields(BindingFlags.Static |
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (IsEditorHolder(field.FieldType, field.Name, owner))
                        {
                            holders++;
                            ObserveOwnerField(field.GetValue(null), field.FieldType,
                                owner.Name, owner.Name + "." + field.Name, probe, 0);
                        }
                        if (owner == typeof(CareerXpStageBoundaryEditorUI) &&
                            field.Name == "editorOpen" && (bool)field.GetValue(null))
                            probe.Mark(ProfileEditorReadinessCode.PendingEdit,
                                "CareerStageDraft", null);
                    }
                    if (holders == 0) complete = false;
                }
                catch (Exception)
                {
                    complete = false;
                }
                // Fail closed on a missing holder or reflection fault, never
                // identify the partially scanned owner as a safe candidate.
                results[i] = new ProfileEditorOwnerObservation(owner, probe.Pending,
                    probe.Uncertain, probe.Examined,
                    probe.OnlyField, probe.OnlyControl, complete);
            }
            return results;
        }

        private static void ObserveOwnerField(object item, Type type,
            string origin, string identity, OwnerAuditProbe p, int depth)
        {
            if (item == null || depth > 5) return;
            string raw = item as string;
            if (raw != null)
            {
                bool focused, same;
                bool observed = ProfileNumericFocus.TryGetRaw(identity, raw,
                    out focused, out same);
                p.Mark(ClassifyObservedText(raw, observed, focused, same), identity, null);
                return;
            }
            NumericInputState numeric = item as NumericInputState;
            if (numeric != null)
            {
                p.Mark(ClassifyObservedText(numeric.buffer, numeric.FocusObserved,
                    numeric.WasFocused, String.Equals(numeric.LastObservedBuffer,
                        numeric.buffer, StringComparison.Ordinal)),
                    identity, numeric.FocusControlName);
                return;
            }
            if (type.IsPrimitive || type.IsEnum || !p.Seen.Add(item)) return;
            IDictionary dict = item as IDictionary;
            if (dict != null)
            {
                foreach (DictionaryEntry entry in dict)
                    ObserveOwnerField(entry.Value,
                        entry.Value == null ? typeof(object) : entry.Value.GetType(),
                        origin, identity + "[" + (entry.Key == null ? "null" :
                            entry.Key.ToString()) + "]", p, depth + 1);
                return;
            }
            IEnumerable list = item as IEnumerable;
            if (list != null)
            {
                int index = 0;
                foreach (object entry in list)
                    ObserveOwnerField(entry,
                        entry == null ? typeof(object) : entry.GetType(), origin,
                        identity + "[" + index++ + "]", p, depth + 1);
                return;
            }
            if (type.DeclaringType == null ||
                type.DeclaringType.Namespace != "XpCustomizer.UI") return;

            bool hasFocus = false, wasFocused = false, hasBuffer = false;
            string buffer = null;
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (field.Name == "WasFocused" && field.FieldType == typeof(bool))
                { hasFocus = true; wasFocused = (bool)field.GetValue(item); }
                if (field.Name == "Buffer" && field.FieldType == typeof(string))
                { hasBuffer = true; buffer = field.GetValue(item) as string; }
            }
            if (hasFocus && hasBuffer)
            {
                p.Mark(ClassifyTextBuffer(buffer, true, wasFocused), identity, null);
                return;
            }
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (IsEditorHolder(field.FieldType, field.Name, type))
                    ObserveOwnerField(field.GetValue(item), field.FieldType, origin,
                        identity + "." + field.Name, p, depth + 1);
        }
    }
}
