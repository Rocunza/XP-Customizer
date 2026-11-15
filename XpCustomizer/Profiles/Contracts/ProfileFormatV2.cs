using System;

namespace XpCustomizer.Profiles
{
    /// <summary>Portable document metadata revision; XP setting schema remains v1.</summary>
    internal static class ProfileFormatV2
    {
        internal const int FormatVersion = 2;
        internal const int MaxNotesCharacters = 500;

        internal static bool ValidNotes(string notes)
        {
            if (notes == null || notes.Length > MaxNotesCharacters) return false;
            for (int i = 0; i < notes.Length; i++)
            {
                char c = notes[i];
                if (Char.IsSurrogate(c))
                {
                    if (Char.IsHighSurrogate(c) && i + 1 < notes.Length &&
                        Char.IsLowSurrogate(notes[i + 1])) { i++; continue; }
                    return false;
                }
                if (Char.IsControl(c) && c != '\n' && c != '\t') return false;
            }
            return true;
        }
    }
}
