#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Verse;
using XpCustomizer.Profiles;
using XpCustomizer.BookXp;

namespace XpCustomizer
{
    internal static class ProfileApplyPreflightSelfTests
    {
        private static int checks;
        private static void Assert(bool ok, string label)
        {
            checks++;
            if (!ok) throw new InvalidOperationException("R6P1D1 APPLY REVIEW: " + label);
        }

        private static ProfileEditorReadinessResult Editor(ProfileEditorReadinessCode code)
        { return new ProfileEditorReadinessResult(code, "TEST", 15, 0, 0); }

        internal static void Run()
        {
            checks = 0;
            string root = Path.Combine(Path.GetTempPath(), "XPC-R6P1D1-" + Guid.NewGuid().ToString("N"));
            bool lockWasSet = ProfileWriteSessionGate.IsBlocked;
            XpCustomizerSettings originalLive = XpCustomizerMod.Settings;
            try
            {
                Assert(!lockWasSet, "no pre-existing recovery lock");
                ProfileStoragePaths paths = ProfileStoragePaths.ForIsolatedTestRoot(root);
                ProfileFileRepository repo = new ProfileFileRepository(paths, false);
                ProfileCreationService creator = new ProfileCreationService(delegate { return repo; }, false);
                string id = "22bc29d5-8ef3-4235-a591-30c2d1000001";
                XpCustomizerSettings saved = new XpCustomizerSettings();
                saved.careerXpMultiplier = 1.5f;
                Assert(creator.CreateCore(saved, "D1 Review Sample", "1.6.4871",
                    new DateTime(2026,11,7,0,0,0,DateTimeKind.Utc), id,
                    ProfileStorageFault.None).Success, "create isolated sample");
                ProfileStorageResult loaded = repo.Load(id);
                Assert(loaded.Success && loaded.Document != null && loaded.ContentHash.Length == 64,
                    "load strict profile with hash");
                string rawFile = paths.FileForId(id);
                byte[] priorBytes = File.ReadAllBytes(rawFile);

                // D1-FIX2: the JSON reader constructs Ordinal maps while
                // XpCustomizerSettings defaults to EqualityComparer<string>.Default.
                // Those are equivalent for XP skill keys, regardless of the
                // comparer OBJECT reference. Keep non-ordinal changes distinct.
                string firstMismatch;
                XpCustomizerSettings importedSame = ProfileMapper.ImportDetached(
                    loaded.Document, saved);
                Assert(ProfileApplySettingsComparison.PortableEquivalent(
                    saved, importedSame, out firstMismatch) && firstMismatch == "NONE",
                    "portable same after disk JSON roundtrip: " + firstMismatch);
                string strictMismatch;
                Assert(SettingsApplyCoordinator.Equivalent(saved, saved,
                    out strictMismatch) && strictMismatch == "NONE",
                    "strict transaction comparator still accepts an identical source");
                XpCustomizerSettings canonical = SettingsSnapshotService.CloneIndependent(saved);
                canonical.skillSpecificXpMultipliers =
                    new Dictionary<string, float>(StringComparer.Ordinal);
                Assert(ProfileApplySettingsComparison.PortableEquivalent(saved, canonical,
                    out firstMismatch) && firstMismatch == "NONE",
                    "default and ordinal dictionary comparer both mean ordinal keys");
                XpCustomizerSettings extraKey = SettingsSnapshotService.CloneIndependent(saved);
                extraKey.skillSpecificXpMultipliers["Shooting"] = 1.15f;
                Assert(!ProfileApplySettingsComparison.PortableEquivalent(saved, extraKey,
                    out firstMismatch) && firstMismatch == "career.skillSpecificXpMultipliers",
                    "first mismatch identifies changed dictionary field");
                XpCustomizerSettings changedScalar = SettingsSnapshotService.CloneIndependent(saved);
                changedScalar.careerXpMultiplier = 7f;
                Assert(!ProfileApplySettingsComparison.PortableEquivalent(saved, changedScalar,
                    out firstMismatch) && firstMismatch == "career.careerXpMultiplier",
                    "first mismatch identifies changed scalar field");
                XpCustomizerSettings changedCurve = SettingsSnapshotService.CloneIndependent(saved);
                changedCurve.consciousnessAnchorSpeed.LowFactor += 0.1f;
                Assert(!ProfileApplySettingsComparison.PortableEquivalent(saved, changedCurve,
                    out firstMismatch) && firstMismatch == "consciousness.speedProfile.LowFactor",
                    "first mismatch identifies changed curve field");
                XpCustomizerSettings changedLegacy = SettingsSnapshotService.CloneIndependent(saved);
                changedLegacy.consciousnessFurtherBonusAt170 += 0.1f;
                Assert(ProfileApplySettingsComparison.PortableEquivalent(saved, changedLegacy,
                    out firstMismatch) && firstMismatch == "NONE",
                    "non-portable legacy setting excluded from profile difference");
                XpCustomizerSettings customComparer = SettingsSnapshotService.CloneIndependent(saved);
                customComparer.skillSpecificXpMultipliers =
                    new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                Assert(!ProfileApplySettingsComparison.PortableEquivalent(saved, customComparer,
                    out firstMismatch) && firstMismatch == "career.skillSpecificXpMultipliers",
                    "nonordinal dictionary semantics are not silently equal");
                Assert(!SettingsApplyCoordinator.Equivalent(saved, customComparer,
                    out strictMismatch) && strictMismatch == "skillSpecificXpMultipliers",
                    "rollback comparator still rejects changed dictionary semantics");
                Assert(!ProfileApplySettingsComparison.PortableEquivalent(saved, null,
                    out firstMismatch) && firstMismatch == "NULL_SETTINGS",
                    "null candidate rejected by portable comparison");
                Log.Message("[XPC-R6P1D1FIX2][PROFILE_APPLY_COMPARISON_AUDIT] " +
                    "status=PASS; scenarios=10; jsonRoundtrip=EQUIVALENT; " +
                    "defaultVsOrdinal=EQUIVALENT; comparerSemanticsChange=DIFFERENT; " +
                    "scalarMapCurve=DIFFERENT_WITH_FIELD; nonPortable=IGNORED; " +
                    "strictRollbackComparator=UNCHANGED; fileWritesByComparison=0; " +
                    "productionApply=NOT_EXERCISED_BY_THIS_TEST; build=" + XpCustomizerMod.BuildId);

                XpCustomizerSettings live = SettingsSnapshotService.CloneIndependent(saved);
                live.careerXpMultiplier = 2.5f;
                var ready = new ProfileApplyPreflightService(delegate { return repo; },
                    delegate { return Editor(ProfileEditorReadinessCode.Ready); }, false);
                ProfileApplyReviewResult open = ready.Open(loaded.Document, loaded.ContentHash, live);
                Assert(open.Code == ProfileApplyReviewCode.ReadyChanged && open.Session != null,
                    "different candidate opens read-only session");
                Assert(open.Session.ProfileId == id && open.Session.ExpectedHash == loaded.ContentHash &&
                    open.Session.DisplayName == loaded.Document.DisplayName,
                    "frozen selected identity and name");
                Assert(live.careerXpMultiplier == 2.5f && saved.careerXpMultiplier == 1.5f,
                    "initial import leaves both input objects unchanged");
                Assert(ready.ConfirmDryRun(open.Session, live).Code == ProfileApplyReviewCode.ReadyChanged,
                    "confirm repeats read-only checks");
                Assert(ready.ConfirmDryRun(open.Session, live).Code == ProfileApplyReviewCode.InvalidSelection,
                    "duplicate confirmation refused");
                Assert(EqualBytes(priorBytes, File.ReadAllBytes(rawFile)) &&
                    !Directory.Exists(paths.Backups), "dry review writes no profile or backup");

                ProfileApplyReviewResult same = ready.Open(loaded.Document, loaded.ContentHash, saved);
                Assert(same.Code == ProfileApplyReviewCode.ReadyNoChange &&
                    same.Session != null && !same.Session.ContentDiffers,
                    "no change classified, actual=" + same.Code + ", detail=" + same.Detail);
                Assert(ready.ConfirmDryRun(same.Session, saved).Code == ProfileApplyReviewCode.ReadyNoChange,
                    "no change dry confirmation allowed");
                Assert(ready.Open(null, loaded.ContentHash, live).Code == ProfileApplyReviewCode.InvalidSelection,
                    "null profile rejected");
                Assert(ready.Open(loaded.Document, "invalid", live).Code == ProfileApplyReviewCode.InvalidSelection,
                    "invalid hash rejected");
                Assert(ready.Open(loaded.Document, new string('0', 64), live).Code == ProfileApplyReviewCode.Conflict,
                    "stale expected SHA rejected");
                var pending = new ProfileApplyPreflightService(delegate { return repo; },
                    delegate { return Editor(ProfileEditorReadinessCode.PendingEdit); }, false);
                Assert(pending.Open(loaded.Document, loaded.ContentHash, live).Code ==
                    ProfileApplyReviewCode.PendingEdit, "pending numeric editor blocks open");
                var uncertain = new ProfileApplyPreflightService(delegate { return repo; },
                    delegate { return Editor(ProfileEditorReadinessCode.Uncertain); }, false);
                Assert(uncertain.Open(loaded.Document, loaded.ContentHash, live).Code ==
                    ProfileApplyReviewCode.Uncertain, "uncertain editor blocks open");
                var nullEditor = new ProfileApplyPreflightService(delegate { return repo; },
                    delegate { return null; }, false);
                Assert(nullEditor.Open(loaded.Document, loaded.ContentHash, live).Code ==
                    ProfileApplyReviewCode.Uncertain, "missing editor result fails closed");

                XpCustomizerSettings invalid = SettingsSnapshotService.CloneIndependent(live);
                invalid.careerXpMultiplier = float.NaN;
                Assert(ready.Open(loaded.Document, loaded.ContentHash, invalid).Code ==
                    ProfileApplyReviewCode.InvalidSettings, "invalid baseline blocks open");
                // A mid-check mutation by another owner must not yield Ready.
                XpCustomizerSettings racing = SettingsSnapshotService.CloneIndependent(live);
                int gateCalls = 0;
                var settingsRace = new ProfileApplyPreflightService(delegate { return repo; },
                    delegate
                    {
                        gateCalls++;
                        if (gateCalls == 2) racing.careerXpMultiplier = 8f;
                        return Editor(ProfileEditorReadinessCode.Ready);
                    }, false);
                Assert(settingsRace.Open(loaded.Document, loaded.ContentHash, racing).Code ==
                    ProfileApplyReviewCode.Conflict,
                    "mid-review settings mutation rejected after second editor observation");
                ProfileApplyReviewResult openedAgain = ready.Open(loaded.Document, loaded.ContentHash, live);
                Assert(openedAgain.Success, "fresh session for mutation test");
                XpCustomizerSettings changedLive = SettingsSnapshotService.CloneIndependent(live);
                changedLive.careerXpMultiplier = 9f;
                Assert(ready.ConfirmDryRun(openedAgain.Session, changedLive).Code ==
                    ProfileApplyReviewCode.Conflict, "live settings changed after open rejected");
                Assert(live.careerXpMultiplier == 2.5f, "live input not modified on rejection");

                ProfileApplyReviewResult openedForFile = ready.Open(loaded.Document, loaded.ContentHash, live);
                Assert(openedForFile.Success, "fresh session for external edit test");
                ProfileDocument changed = ProfileJsonCodec.Deserialize(ProfileJsonCodec.Serialize(loaded.Document));
                changed.DisplayName = "External rename";
                File.WriteAllText(rawFile, ProfileJsonCodec.Serialize(changed), new UTF8Encoding(false));
                Assert(ready.ConfirmDryRun(openedForFile.Session, live).Code ==
                    ProfileApplyReviewCode.Conflict, "external file change rejects confirmation");
                File.WriteAllBytes(rawFile, priorBytes);
                Assert(ready.Open(loaded.Document, loaded.ContentHash, live).Success,
                    "reselect after restore works");
                Assert(ready.Open(changed, loaded.ContentHash, live).Code ==
                    ProfileApplyReviewCode.Conflict, "stale selected metadata rejected");

                ProfileApplyReviewResult openedForDeletion = ready.Open(loaded.Document, loaded.ContentHash, live);
                Assert(openedForDeletion.Success, "open before file missing test");
                File.Delete(rawFile);
                Assert(ready.ConfirmDryRun(openedForDeletion.Session, live).Code ==
                    ProfileApplyReviewCode.Conflict, "removed file after open rejects confirmation");
                File.WriteAllBytes(rawFile, priorBytes);
                Assert(ready.Open(loaded.Document, loaded.ContentHash, live).Success,
                    "restored original can reopen");

                ProfileWriteSessionGate.Observe(ProfileStorageCode.RecoveryRequired);
                Assert(ready.Open(loaded.Document, loaded.ContentHash, live).Code ==
                    ProfileApplyReviewCode.RecoveryRequired, "session recovery lock blocks open");
                ProfileWriteSessionGate.ResetForIsolatedTests();
                Assert(ready.ConfirmDryRun(null, live).Code == ProfileApplyReviewCode.InvalidSelection,
                    "null confirmation session rejected");
                VerifyLegacyMedicineProfile(repo, id, rawFile, priorBytes, loaded.Document, saved);
                VerifyLegacyBookXpMedicineProfile(repo, id, rawFile, priorBytes,
                    loaded.Document, saved);
                Assert(EqualBytes(priorBytes, File.ReadAllBytes(rawFile)) &&
                    !Directory.Exists(paths.Backups), "final bytes and backup directory preserved");
                Assert(object.ReferenceEquals(XpCustomizerMod.Settings, originalLive),
                    "real settings reference unchanged");
                Log.Message("[XPC-R6P1D1][PROFILE_APPLY_REVIEW_AUDIT] status=PASS; " +
                    "revision=R6P1D1-FIX2-PORTABLE-DIFF-20261107; " +
                    "checks=" + checks + "; testRoot=ISOLATED_TEMP; changes=DETECTED; " +
                    "noChange=PASS; pending=BLOCKED; uncertain=BLOCKED; " +
                    "staleHash=CONFLICT; baselineMutation=REJECTED; " +
                    "duplicateConfirm=BLOCKED; sessionRecoveryLock=PASS; " +
                    "fileWritesByD1=0; userConfigWrites=0; liveSettingsWrites=0; " +
                    "geneDefWrites=0; scribeCalls=0; productionApply=NOT_EXERCISED_BY_THIS_TEST; " +
                    "realIMGUI=MANUAL; build=" + XpCustomizerMod.BuildId);
            }
            finally
            {
                if (!lockWasSet) ProfileWriteSessionGate.ResetForIsolatedTests();
                if (Directory.Exists(root))
                {
                    try
                    {
                        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) == 0)
                            Directory.Delete(root, true);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        /// <summary>Only an isolated profile fixture may be written by this test.</summary>
        private static void VerifyLegacyMedicineProfile(ProfileFileRepository repo,
            string id, string rawFile, byte[] originalBytes,
            ProfileDocument original, XpCustomizerSettings source)
        {
            ProfileDocument legacy = ProfileJsonCodec.Deserialize(ProfileJsonCodec.Serialize(original));
            var oldMap = (Dictionary<string, bool>)legacy.Settings.Sections["gws"]["gwsSkillEnabled"];
            oldMap.Add("Medical", false);
            legacy.Settings.Sections["gws"]["gwsHighAnchor"] = 2.5f;
            string before = ProfileJsonCodec.Serialize(legacy);
            XpCustomizerSettings converted = ProfileMapper.ImportDetached(legacy, source);
            Assert(!converted.gwsSkillEnabled["Medicine"] &&
                !converted.gwsSkillEnabled.ContainsKey("Medical"),
                "legacy JSON OFF converted on detached candidate");
            Assert(converted.gwsHighAnchor == 2.5f &&
                source.gwsHighAnchor != converted.gwsHighAnchor,
                "import changes no other GWS curve values");
            Assert(ProfileJsonCodec.Serialize(legacy) == before &&
                oldMap.ContainsKey("Medical") && !oldMap.ContainsKey("Medicine"),
                "legacy source JSON and document stay untouched");
            var same = SettingsSnapshotService.CloneIndependent(source);
            same.gwsSkillEnabled["Medicine"] = false;
            same.gwsHighAnchor = 2.5f;
            string mismatch;
            Assert(ProfileApplySettingsComparison.PortableEquivalent(same, converted, out mismatch) &&
                mismatch == "NONE", "canonical current state equals migrated legacy profile");

            // A legacy explicit ON must mean the same as the new implicit ON.
            oldMap["Medical"] = true;
            XpCustomizerSettings on = ProfileMapper.ImportDetached(legacy, source);
            Assert(!on.gwsSkillEnabled.ContainsKey("Medical") &&
                !on.gwsSkillEnabled.ContainsKey("Medicine"),
                "legacy ON normalizes to implicit canonical default");
            oldMap["Medicine"] = false;
            XpCustomizerSettings conflict = ProfileMapper.ImportDetached(legacy, source);
            Assert(!conflict.gwsSkillEnabled["Medicine"] &&
                !conflict.gwsSkillEnabled.ContainsKey("Medical"),
                "explicit canonical OFF wins legacy ON");
            oldMap["Medicine"] = true;
            oldMap["Medical"] = false;
            XpCustomizerSettings conflictOn = ProfileMapper.ImportDetached(legacy, source);
            Assert(conflictOn.gwsSkillEnabled.ContainsKey("Medicine") &&
                conflictOn.gwsSkillEnabled["Medicine"] &&
                !conflictOn.gwsSkillEnabled.ContainsKey("Medical"),
                "explicit canonical ON wins legacy OFF");
            oldMap.Remove("Medicine");
            oldMap["Medical"] = false;
            // Write only the isolated TEST repository, never a real user file.
            string oldJson = ProfileJsonCodec.Serialize(legacy);
            try
            {
                File.WriteAllText(rawFile, oldJson, new UTF8Encoding(false));
                ProfileStorageResult disk = repo.Load(id);
                Assert(disk.Success && disk.Document != null, "old JSON loads from isolated repository");
                var review = new ProfileApplyPreflightService(delegate { return repo; },
                    delegate { return Editor(ProfileEditorReadinessCode.Ready); }, false);
                ProfileApplyReviewResult changed = review.Open(disk.Document, disk.ContentHash, source);
                Assert(changed.Code == ProfileApplyReviewCode.ReadyChanged && changed.Session != null,
                    "old JSON gets ReadyChanged, without applying");
                Assert(review.ConfirmDryRun(changed.Session, source).Code ==
                    ProfileApplyReviewCode.ReadyChanged, "old JSON confirm has identical verdict");
                ProfileApplyReviewResult noChange = review.Open(disk.Document, disk.ContentHash, same);
                Assert(noChange.Code == ProfileApplyReviewCode.ReadyNoChange &&
                    noChange.Session != null, "already migrated user state classifies NoChange");
                Assert(review.ConfirmDryRun(noChange.Session, same).Code ==
                    ProfileApplyReviewCode.ReadyNoChange, "NoChange confirm remains stable");
                Assert(oldJson == File.ReadAllText(rawFile, Encoding.UTF8),
                    "Review and Confirm never rewrite legacy profile JSON");
            }
            finally
            {
                File.WriteAllBytes(rawFile, originalBytes);
            }
            Assert(EqualBytes(originalBytes, File.ReadAllBytes(rawFile)),
                "isolated profile fixture restored after legacy review");
            Log.Message("[XPC-GWS-MEDFIX] phase=PROFILE_TEST; status=PASS; " +
                "legacyOff=PASS; legacyOn=PASS; canonicalWins=PASS; " +
                "reviewAndConfirm=PASS; noChange=PASS; sourceMutated=false; " +
                "testRoot=ISOLATED_TEMP; productionApply=NOT_TESTED; build=" +
                XpCustomizerMod.BuildId);
        }

        /// <summary>Exercises only an isolated old Book XP profile fixture.</summary>
        private static void VerifyLegacyBookXpMedicineProfile(ProfileFileRepository repo,
            string id, string rawFile, byte[] originalBytes,
            ProfileDocument original, XpCustomizerSettings source)
        {
            ProfileDocument legacy = ProfileJsonCodec.Deserialize(ProfileJsonCodec.Serialize(original));
            var oldBook = (Dictionary<string, float>)legacy.Settings.Sections["bookXp"]["bookSkillMultipliers"];
            var oldGws = (Dictionary<string, bool>)legacy.Settings.Sections["gws"]["gwsSkillEnabled"];
            oldBook.Add("Medical", 1.7f);
            legacy.Settings.Sections["bookXp"]["bookXpEnabled"] = true;
            legacy.Settings.Sections["bookXp"]["bookSkillEnabled"] = true;
            oldGws.Add("Medical", false);
            string before = ProfileJsonCodec.Serialize(legacy);
            XpCustomizerSettings moved = ProfileMapper.ImportDetached(legacy, source);
            Assert(moved.bookSkillMultipliers.ContainsKey("Medicine") &&
                moved.bookSkillMultipliers["Medicine"] == 1.7f &&
                !moved.bookSkillMultipliers.ContainsKey("Medical") &&
                moved.bookXpEnabled && moved.bookSkillEnabled,
                "old Book XP profile ratio migrated to canonical Medicine");
            Assert(moved.gwsSkillEnabled.ContainsKey("Medicine") &&
                !moved.gwsSkillEnabled["Medicine"] &&
                !moved.gwsSkillEnabled.ContainsKey("Medical"),
                "independent GWS legacy OFF still migrates from same JSON");
            Assert(BookXpService.GetEffectiveTextbookMultiplier(moved,
                RimWorld.QualityCategory.Normal, "Medicine") == 1.7f,
                "migrated Book XP ratio reaches actual Medicine skill path");
            Assert(ProfileJsonCodec.Serialize(legacy) == before &&
                oldBook.ContainsKey("Medical") && !oldBook.ContainsKey("Medicine") &&
                oldGws.ContainsKey("Medical") && !oldGws.ContainsKey("Medicine"),
                "import never mutates either source profile dictionary");

            var equivalent = SettingsSnapshotService.CloneIndependent(source);
            equivalent.bookXpEnabled = true;
            equivalent.bookSkillEnabled = true;
            equivalent.bookSkillMultipliers["Medicine"] = 1.7f;
            equivalent.gwsSkillEnabled["Medicine"] = false;
            string mismatch;
            Assert(ProfileApplySettingsComparison.PortableEquivalent(equivalent, moved,
                out mismatch) && mismatch == "NONE",
                "canonical current state equals both migrated features");

            oldBook["Medical"] = 0f;
            XpCustomizerSettings zero = ProfileMapper.ImportDetached(legacy, source);
            Assert(zero.bookSkillMultipliers["Medicine"] == 0f &&
                !zero.bookSkillMultipliers.ContainsKey("Medical"),
                "old Book XP zero remains explicit zero");
            oldBook["Medical"] = 1f;
            XpCustomizerSettings neutral = ProfileMapper.ImportDetached(legacy, source);
            Assert(!neutral.bookSkillMultipliers.ContainsKey("Medical") &&
                !neutral.bookSkillMultipliers.ContainsKey("Medicine"),
                "old Book XP x1 is a neutral absent entry");
            oldBook["Medical"] = 1.7f;
            oldBook["Medicine"] = 1.4f;
            XpCustomizerSettings conflicting = ProfileMapper.ImportDetached(legacy, source);
            Assert(conflicting.bookSkillMultipliers["Medicine"] == 1.4f &&
                !conflicting.bookSkillMultipliers.ContainsKey("Medical") &&
                !conflicting.gwsSkillEnabled["Medicine"],
                "canonical Book XP ratio wins without changing GWS OFF");
            oldBook.Remove("Medical");
            XpCustomizerSettings alreadyCanonical = ProfileMapper.ImportDetached(legacy, source);
            Assert(alreadyCanonical.bookSkillMultipliers["Medicine"] == 1.4f &&
                !alreadyCanonical.bookSkillMultipliers.ContainsKey("Medical"),
                "canonical-only Book XP profile remains intact");
            oldBook.Remove("Medicine");
            oldBook["Medical"] = 1.7f;

            string oldJson = ProfileJsonCodec.Serialize(legacy);
            try
            {
                // Only the TEST repository is written, never any user profile.
                File.WriteAllText(rawFile, oldJson, new UTF8Encoding(false));
                ProfileStorageResult disk = repo.Load(id);
                Assert(disk.Success && disk.Document != null, "old Book XP JSON loads from temp repository");
                var review = new ProfileApplyPreflightService(delegate { return repo; },
                    delegate { return Editor(ProfileEditorReadinessCode.Ready); }, false);
                ProfileApplyReviewResult changed = review.Open(disk.Document, disk.ContentHash, source);
                Assert(changed.Code == ProfileApplyReviewCode.ReadyChanged && changed.Session != null,
                    "old Book XP JSON review detects changed settings");
                Assert(review.ConfirmDryRun(changed.Session, source).Code ==
                    ProfileApplyReviewCode.ReadyChanged,
                    "old Book XP Review and Confirm agree");
                ProfileApplyReviewResult same = review.Open(disk.Document, disk.ContentHash, equivalent);
                Assert(same.Code == ProfileApplyReviewCode.ReadyNoChange && same.Session != null,
                    "old Book XP profile equals already migrated current settings");
                Assert(review.ConfirmDryRun(same.Session, equivalent).Code ==
                    ProfileApplyReviewCode.ReadyNoChange,
                    "Book XP no-change confirm remains stable");
                Assert(oldJson == File.ReadAllText(rawFile, Encoding.UTF8),
                    "Book XP Review and Confirm do not rewrite old JSON");
            }
            finally
            {
                File.WriteAllBytes(rawFile, originalBytes);
            }
            Assert(EqualBytes(originalBytes, File.ReadAllBytes(rawFile)) &&
                ProfileJsonCodec.Serialize(legacy) == oldJson,
                "isolated Book XP profile fixture restored, source document unchanged");
            Log.Message("[XPC-BOOKXP-MEDFIX] phase=PROFILE_TEST; status=PASS" +
                "; legacyZero=PASS; legacyRatio=PASS; canonicalWins=PASS" +
                "; gwsCoexist=PASS; reviewAndConfirm=PASS; noChange=PASS" +
                "; sourceMutated=false; testRoot=ISOLATED_TEMP" +
                "; productionApply=NOT_TESTED; build=" + XpCustomizerMod.BuildId);
        }

        private static bool EqualBytes(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
#endif
