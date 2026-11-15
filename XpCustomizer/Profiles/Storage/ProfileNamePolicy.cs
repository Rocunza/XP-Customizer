using System;

namespace XpCustomizer.Profiles
{
    /// <summary>
    /// R6-P1-C2-B1: shared name policy for Create and Rename.
    /// Keeps the exact C2-A trim, 1..80 UTF-16-unit, control and surrogate rules.
    /// Display names are NEVER used as disk paths or identifiers.
    /// </summary>
    internal static class ProfileNamePolicy
    {
        internal const string Revision = "R6P1C2B1-NAME-POLICY-20261107";

        internal static bool TryCanonical(string proposed, out string canonical)
        {
            canonical = proposed == null ? null : proposed.Trim();
            if (String.IsNullOrWhiteSpace(canonical) || canonical.Length > 80)
                return false;
            for (int i = 0; i < canonical.Length; i++)
            {
                char c = canonical[i];
                if (Char.IsControl(c)) return false;
                if (Char.IsHighSurrogate(c))
                {
                    if (i + 1 >= canonical.Length || !Char.IsLowSurrogate(canonical[i + 1]))
                        return false;
                    i++;
                }
                else if (Char.IsLowSurrogate(c)) return false;
            }
            return true;
        }
    }
}
