using System;
using System.Collections.Generic;
using System.IO;
using Verse;

namespace XpCustomizer.Profiles
{
    /// <summary>
    /// PNL1: UI-only translation for the two exact, shipped English preset notes.
    /// Notes in the profile JSON remain the source of truth. Never translate
    /// or rewrite arbitrary notes, nor infer official status from a display name.
    /// </summary>
    internal static class BundledProfileNotesLocalization
    {
        internal const string Revision = "XPC-PRESET-NOTES-L10N-PNL1-HF1-R1-20261115";
        // Exact source texts from the shipped v2 preset JSONs.
        // The Very Long source deliberately includes a space before the first newline.
        internal const string LongEnglishNotes = "Differences from Vanilla:\n- Total XP requirement ×3\n- Consciousness affects Learning Speed, Daily Learning Capacity, and Further Learning\n- GWS affects Learning Speed\n- Mental Threshold affects Further Learning\n- Biotech Aptitude: Awful -8 / Poor -4 / Strong +2 / Great +4";
        internal const string VeryLongEnglishNotes = "Differences from Vanilla: \n- Total XP requirement ×6\n- Consciousness affects Learning Speed, Daily Learning Capacity, and Further Learning\n- GWS affects Learning Speed\n- Mental Threshold affects Further Learning\n- Biotech Aptitude: Awful -8 / Poor -4 / Strong +1 / Great +2";

        // BP4 restores deleted bundled profiles under a fresh GUID. That new
        // GUID is durably recorded in BundledProfilesState.json, not in the
        // source JSON. An exact-source-ID-only guard incorrectly rejected it.
        // Read the journal lazily ONCE per session; never do disk IO per GUI frame.
        private static readonly object CacheSync = new object();
        private static readonly HashSet<string> LoggedDisplays =
            new HashSet<string>(StringComparer.Ordinal);
        private static bool journalLoaded;
        private static BundledProfileStateStore.State journalState;

        internal static void InvalidateJournalCache()
        {
            lock (CacheSync)
            {
                journalLoaded = false;
                journalState = null;
            }
        }

        private static string CurrentTrackedId(string presetKey)
        {
            lock (CacheSync)
            {
                if (!journalLoaded)
                {
                    try
                    {
                        string root = GenFilePaths.ConfigFolderPath;
                        if (String.IsNullOrWhiteSpace(root)) return null;
                        string marker = Path.Combine(root, "XpCustomizer", "BundledProfilesState.json");
                        journalState = BundledProfileStateStore.Read(marker);
                        journalLoaded = true;
                    }
                    catch (Exception)
                    {
                        // Missing/unsafe journal => original-ID-only fallback.
                        // Never disrupt a profile dialog or silently guess IDs.
                        journalState = null;
                        journalLoaded = true;
                    }
                }
                if (journalState == null || journalState.GetStatus(presetKey) != 'D')
                    return null;
                return journalState.GetId(presetKey);
            }
        }

        // A pure decision function shared by production and synthetic fixtures.
        private static string ResolveKey(string profileId, string notes,
            string trackedLongId, string trackedVeryLongId)
        {
            if (String.Equals(notes, LongEnglishNotes, StringComparison.Ordinal) &&
                (String.Equals(profileId, BundledProfileInstaller.LongId, StringComparison.Ordinal) ||
                 (trackedLongId != null && String.Equals(profileId, trackedLongId, StringComparison.Ordinal))))
                return "XPC_PresetNotes_OfficialLong";
            if (String.Equals(notes, VeryLongEnglishNotes, StringComparison.Ordinal) &&
                (String.Equals(profileId, BundledProfileInstaller.VeryLongId, StringComparison.Ordinal) ||
                 (trackedVeryLongId != null && String.Equals(profileId, trackedVeryLongId, StringComparison.Ordinal))))
                return "XPC_PresetNotes_OfficialVeryLong";
            return null;
        }

        internal static string GetTranslationKey(string profileId, string notes)
        {
            // Don't touch the journal for arbitrary user-authored notes.
            if (String.Equals(notes, LongEnglishNotes, StringComparison.Ordinal))
            {
                if (String.Equals(profileId, BundledProfileInstaller.LongId, StringComparison.Ordinal))
                    return "XPC_PresetNotes_OfficialLong";
                return ResolveKey(profileId, notes, CurrentTrackedId("LONG"), null);
            }
            if (String.Equals(notes, VeryLongEnglishNotes, StringComparison.Ordinal))
            {
                if (String.Equals(profileId, BundledProfileInstaller.VeryLongId, StringComparison.Ordinal))
                    return "XPC_PresetNotes_OfficialVeryLong";
                return ResolveKey(profileId, notes, null, CurrentTrackedId("VERY_LONG"));
            }
            return null;
        }

        internal static string Display(string profileId, string notes)
        {
            if (String.IsNullOrEmpty(notes)) return notes ?? "";
            string key = GetTranslationKey(profileId, notes);
            if (key == null) return notes;
            string translated = key.Translate().ToString();
            if (String.IsNullOrEmpty(translated) ||
                String.Equals(translated, key, StringComparison.Ordinal))
                return notes;
            // One log per rendered identity, not per Unity OnGUI event.
            lock (CacheSync)
            {
                if (LoggedDisplays.Add(key + ":" + profileId))
                    Log.Message("[XPC-PNL1-HF1][BUNDLED_NOTES_DISPLAY] status=LOCALIZED" +
                        "; preset=" + (key == "XPC_PresetNotes_OfficialLong" ? "LONG" : "VERY_LONG") +
                        "; identity=" + (String.Equals(profileId,
                            key == "XPC_PresetNotes_OfficialLong" ? BundledProfileInstaller.LongId :
                            BundledProfileInstaller.VeryLongId, StringComparison.Ordinal)
                            ? "ORIGINAL_ID" : "TRACKED_RESET_COPY") +
                        "; notesExact=True; profileJsonWrites=0; gameplayWrites=0; build=" +
                        XpCustomizerMod.BuildId);
            }
            return translated;
        }

#if DEBUG
        internal static void RunSelfAudit()
        {
            int cases = 0;
            Check(GetTranslationKey(BundledProfileInstaller.LongId, LongEnglishNotes) ==
                "XPC_PresetNotes_OfficialLong", "LONG_OFFICIAL", ref cases);
            Check(GetTranslationKey(BundledProfileInstaller.VeryLongId, VeryLongEnglishNotes) ==
                "XPC_PresetNotes_OfficialVeryLong", "VERY_LONG_OFFICIAL", ref cases);
            Check(GetTranslationKey(BundledProfileInstaller.LongId, VeryLongEnglishNotes) == null,
                "CROSS_PRESET_NOT_ADMITTED", ref cases);
            Check(GetTranslationKey(BundledProfileInstaller.VeryLongId, LongEnglishNotes) == null,
                "CROSS_PRESET_REVERSE", ref cases);
            Check(GetTranslationKey(Guid.NewGuid().ToString("D"), LongEnglishNotes) == null,
                "CUSTOM_ID_NOT_ADMITTED", ref cases);
            Check(GetTranslationKey(BundledProfileInstaller.LongId, LongEnglishNotes + " edited") == null,
                "EDITED_NOTES_NOT_ADMITTED", ref cases);
            Check(GetTranslationKey(BundledProfileInstaller.VeryLongId,
                VeryLongEnglishNotes.Replace(": \n", ":\n")) == null,
                "WHITESPACE_VARIANT_NOT_ADMITTED", ref cases);
            Check(GetTranslationKey(null, LongEnglishNotes) == null,
                "NULL_ID_NOT_ADMITTED", ref cases);
            Check(GetTranslationKey(BundledProfileInstaller.LongId, null) == null,
                "NULL_NOTES_NOT_ADMITTED", ref cases);
            Check(Display(Guid.Empty.ToString("D"), "User memo") == "User memo",
                "CUSTOM_DISPLAY_UNCHANGED", ref cases);
            Check(Display(BundledProfileInstaller.LongId, "User memo") == "User memo",
                "OFFICIAL_ID_CUSTOM_NOTES_UNCHANGED", ref cases);
            Check(Display(BundledProfileInstaller.LongId, "") == "",
                "EMPTY_NOTES_UNCHANGED", ref cases);
            Check(Display(null, null) == "", "NULL_NOTES_SAFE", ref cases);
            // BP4 restore copies have NEW ids, but the durable state journal
            // certifies which two ids are the current official copies.
            string restoredLong = "00000000-0000-4000-8000-000000000101";
            string restoredVeryLong = "00000000-0000-4000-8000-000000000102";
            Check(ResolveKey(restoredLong, LongEnglishNotes, restoredLong, restoredVeryLong) ==
                "XPC_PresetNotes_OfficialLong", "TRACKED_RESET_LONG", ref cases);
            Check(ResolveKey(restoredVeryLong, VeryLongEnglishNotes, restoredLong, restoredVeryLong) ==
                "XPC_PresetNotes_OfficialVeryLong", "TRACKED_RESET_VERY_LONG", ref cases);
            Check(ResolveKey(restoredLong, VeryLongEnglishNotes, restoredLong, restoredVeryLong) == null,
                "TRACKED_CROSS_NOTE_BLOCKED", ref cases);
            Check(ResolveKey(restoredVeryLong, LongEnglishNotes, restoredLong, restoredVeryLong) == null,
                "TRACKED_REVERSE_NOTE_BLOCKED", ref cases);
            Check(ResolveKey(restoredLong, LongEnglishNotes + " edited", restoredLong,
                restoredVeryLong) == null, "TRACKED_USER_EDIT_PRESERVED", ref cases);
            Check(ResolveKey(restoredLong, LongEnglishNotes, null, restoredVeryLong) == null,
                "UNTRACKED_RESET_ID_BLOCKED", ref cases);
            Check(ResolveKey("00000000-0000-4000-8000-000000000103", LongEnglishNotes,
                restoredLong, restoredVeryLong) == null, "ARBITRARY_ID_BLOCKED", ref cases);
            Check(ResolveKey(BundledProfileInstaller.LongId, LongEnglishNotes, null, null) ==
                "XPC_PresetNotes_OfficialLong", "ORIGINAL_WITHOUT_JOURNAL", ref cases);
            Check(ResolveKey(restoredLong, VeryLongEnglishNotes.Replace(": \n", ":\n"),
                restoredLong, restoredVeryLong) == null, "TRACKED_WHITESPACE_BLOCKED", ref cases);
            Log.Message("[XPC-PNL1-HF1][BUNDLED_NOTES_L10N_SELFTEST] status=PASS; cases=" + cases +
                "; official=2; customNotesPreserved=True; profileJsonWrites=0; " +
                "gameplayWrites=0; build=" + XpCustomizerMod.BuildId);
        }

        private static void Check(bool condition, string name, ref int cases)
        {
            cases++;
            if (!condition) throw new InvalidOperationException("PNL1_" + name);
        }
#endif
    }
}
