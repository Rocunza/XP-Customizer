#if DEBUG
using System;
using System.IO;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer
{
    /// <summary>Disposable file-system fixtures only. Never touch current XP settings or user profiles.</summary>
    internal static class ProfileNotesSelfTests
    {
        private static int checks;
        private static void Check(bool condition, string label)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("PN1_NOTES_SELFTEST:" + label);
        }

        internal static void Run()
        {
            checks = 0;
            string root = Path.Combine(Path.GetTempPath(), "XPC-PN1-" + Guid.NewGuid().ToString("N"));
            XpCustomizerSettings live = XpCustomizerMod.Settings;
            float career = live == null ? 0f : live.careerXpMultiplier;
            bool oldGate = ProfileWriteSessionGate.IsBlocked;
            try
            {
                Check(!oldGate, "no recovery lock at test start");
                ProfileStoragePaths paths = ProfileStoragePaths.ForIsolatedTestRoot(root);
                ProfileFileRepository repo = new ProfileFileRepository(paths, false);
                ProfileNotesService service = new ProfileNotesService(delegate { return repo; }, false);
                string id = "f2b35c74-e224-4084-9d69-981100aa0131";
                DateTime utc = new DateTime(2026, 11, 10, 12, 30, 0, DateTimeKind.Utc);
                XpCustomizerSettings original = new XpCustomizerSettings();
                original.careerXpMultiplier = 3f;
                ProfileDocument v2 = ProfileMapper.Export(original, "Long (x3)", "1.6.4871", utc, id);
                Check(v2.FormatVersion == 2 && v2.Notes == "", "new exports use metadata v2");
                string newJson = ProfileJsonCodec.Serialize(v2);
                Check(newJson.Contains("\"notes\":\"\"") &&
                    ProfileJsonCodec.Serialize(ProfileJsonCodec.Deserialize(newJson)) == newJson,
                    "v2 notes included with deterministic roundtrip");

                // The shipped v1 schema is still accepted and roundtrips byte-for-byte.
                v2.FormatVersion = 1;
                string legacyJson = ProfileJsonCodec.Serialize(v2);
                Check(!legacyJson.Contains("\"notes\"") &&
                    ProfileJsonCodec.Deserialize(legacyJson).FormatVersion == 1 &&
                    ProfileJsonCodec.Serialize(ProfileJsonCodec.Deserialize(legacyJson)) == legacyJson,
                    "legacy v1 no-notes format unchanged");
                Check(repo.Create(v2).Success, "legacy fixture created in isolated directory");
                ProfileStorageResult before = repo.Load(id);
                Check(before.Success && before.Document.FormatVersion == 1, "legacy file loaded");
                string notes = "바닐라와 다른 점:\n- 총 경험치 x3\n- Cons ON\n- GWS ON";
                Check(service.Save(id, new string('0', 64), notes).Code == ProfileNotesCode.Conflict &&
                    repo.Load(id).ContentHash == before.ContentHash, "stale SHA blocks notes edit");
                Check(service.Save(id, before.ContentHash, new string('X', 501)).Code ==
                    ProfileNotesCode.InvalidInput, "501-character note rejected");
                Check(service.Save(id, before.ContentHash, "\uD800").Code ==
                    ProfileNotesCode.InvalidInput, "invalid surrogate rejected");
                Check(!Directory.Exists(paths.Backups), "invalid edits create no backup");

                Check(service.Save(id, before.ContentHash, notes, ProfileStorageFault.BeforeReplace).Code ==
                    ProfileNotesCode.StorageFailure && repo.Load(id).ContentHash == before.ContentHash,
                    "injected notes update failure preserves old JSON");
                ProfileNotesResult saved = service.Save(id, before.ContentHash, notes);
                ProfileStorageResult after = repo.Load(id);
                Check(saved.Code == ProfileNotesCode.Saved && after.Success &&
                    after.Document.FormatVersion == 2 && after.Document.Notes == notes,
                    "v1 upgrades to v2 only on actual notes save");
                Check(after.Document.CreatedUtc == before.Document.CreatedUtc &&
                    after.Document.UpdatedUtc == before.Document.UpdatedUtc &&
                    after.Document.SourceGameVersion == before.Document.SourceGameVersion &&
                    after.Document.SourceBuildId == before.Document.SourceBuildId,
                    "Saved date, Game and source build preserved");
                ProfileDocument normalized = ProfileJsonCodec.Deserialize(
                    ProfileJsonCodec.Serialize(after.Document));
                normalized.FormatVersion = 1;
                normalized.Notes = "";
                Check(ProfileJsonCodec.Serialize(normalized) == legacyJson,
                    "every other portable setting and metadata byte-for-byte preserved");
                Check(service.Save(id, after.ContentHash, notes).Code == ProfileNotesCode.NoChange &&
                    repo.Load(id).ContentHash == after.ContentHash,
                    "same note does not create an update");
                Check(Directory.GetFiles(paths.Backups, "*.bak").Length == 1,
                    "single verified metadata replacement keeps backup");

                var preflight = new ProfileOverwritePreflightService(delegate { return repo; },
                    delegate { return new ProfileEditorReadinessResult(
                        ProfileEditorReadinessCode.Ready, "PN1_TEST", 15, 0, 0); }, false,
                    delegate { return false; });
                XpCustomizerSettings edited = SettingsSnapshotService.CloneIndependent(original);
                edited.careerXpMultiplier = 6f;
                ProfileOverwritePreflightResult proposal = preflight.Check(after.Document,
                    after.ContentHash, edited);
                Check(proposal.Success && proposal.ContentDiffers && proposal.Candidate.Notes == notes &&
                    proposal.Candidate.FormatVersion == 2, "overwrite candidate retains notes");
                ProfileStorageResult overwritten = repo.Update(proposal.Candidate, after.ContentHash);
                Check(overwritten.Success && overwritten.Document.Notes == notes &&
                    overwritten.Document.Settings.Sections["career"]["careerXpMultiplier"] is float &&
                    (float)overwritten.Document.Settings.Sections["career"]["careerXpMultiplier"] == 6f &&
                    overwritten.Document.UpdatedUtc > after.Document.UpdatedUtc,
                    "overwrite advances snapshot Saved and preserves Notes");

                var renamed = new ProfileUpdateService(delegate { return repo; }, false);
                ProfileRenameResult rename = renamed.Rename(id, overwritten.ContentHash, "Renamed Long");
                ProfileStorageResult renamedDoc = repo.Load(id);
                Check(rename.Success && renamedDoc.Document.Notes == notes,
                    "Rename preserves Notes");
                ProfileStorageResult trashed = repo.SoftDelete(id, renamedDoc.ContentHash);
                Check(trashed.Success && repo.Load(id).Code == ProfileStorageCode.NotFound,
                    "Trash transfers notes-bearing JSON");
                ProfileStorageResult restored = repo.Restore(trashed.TrashToken, trashed.ContentHash);
                Check(restored.Success && repo.Load(id).Document.Notes == notes,
                    "restore retains Notes");
                ProfileNotesResult clear = service.Save(id, restored.ContentHash, "");
                Check(clear.Code == ProfileNotesCode.Saved && repo.Load(id).Document.Notes == "",
                    "empty Notes allowed and saved");
                ProfileStorageResult empty = repo.Load(id);
                Check(service.Save(id, empty.ContentHash, new string('A', 500)).Code == ProfileNotesCode.Saved,
                    "500-character Notes allowed");
                Check(ProfileJsonCodec.Deserialize(ProfileJsonCodec.Serialize(repo.Load(id).Document)).Notes.Length == 500,
                    "500-character JSON roundtrip");
                Check(object.ReferenceEquals(XpCustomizerMod.Settings, live) &&
                    (live == null || live.careerXpMultiplier == career),
                    "user game settings untouched");
                Log.Message("[XPC-PN1][NOTES_SELFTEST] status=PASS; checks=" + checks +
                    "; legacyV1=ACCEPTED; v2=ROUNDTRIP; notesOnly=VERIFIED; " +
                    "overwriteRenameTrashRestore=PASS; realGameClicks=NOT_VERIFIED; " +
                    "userXmlWrites=0; runtimeApplies=0; build=" + XpCustomizerMod.BuildId);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    try { Directory.Delete(root, true); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                if (!oldGate && ProfileWriteSessionGate.IsBlocked)
                    ProfileWriteSessionGate.ResetForIsolatedTests();
            }
        }
    }
}
#endif
