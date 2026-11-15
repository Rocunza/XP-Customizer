#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer
{
    /// <summary>
    /// B1 regression: one isolated temporary repository per scenario; no
    /// Current.Game, Scribe, live GeneDef, pawn or game settings mutation.
    /// Real UI and real profile application are deliberately NOT exercised.
    /// </summary>
    internal static class ProfileApplyBridgeSelfTests
    {
        private static int checks;
        private static int scenarios;
        private static int scenarioId;

        private sealed class Fixture
        {
            internal readonly ProfileStoragePaths Paths;
            internal readonly ProfileFileRepository Repository;
            internal readonly ProfileApplyBridgeService Bridge;
            internal readonly string FileName;
            internal readonly ProfileDocument Document;
            internal readonly string Hash;
            internal readonly XpCustomizerSettings Saved;
            internal readonly XpCustomizerSettings Live;
            internal ProfileEditorReadinessCode EditorState = ProfileEditorReadinessCode.Ready;

            internal Fixture(string suiteRoot, Action<XpCustomizerSettings> editSaved = null,
                Action<XpCustomizerSettings> editLive = null)
            {
                string dir = Path.Combine(suiteRoot, "s" + (++scenarioId).ToString("D3"));
                Paths = ProfileStoragePaths.ForIsolatedTestRoot(dir);
                Repository = new ProfileFileRepository(Paths, false);
                Saved = new XpCustomizerSettings();
                Saved.careerXpMultiplier = 2f;
                Saved.biotechCustomizationEnabled = true;
                Saved.biotechAwfulAptitude = -7;
                if (editSaved != null) editSaved(Saved);
                Live = SettingsSnapshotService.CloneIndependent(Saved);
                Live.careerXpMultiplier = 4f;
                Live.biotechCustomizationEnabled = false;
                if (editLive != null) editLive(Live);
                string id = Guid.NewGuid().ToString("D");
                ProfileCreationService creation = new ProfileCreationService(
                    delegate { return Repository; }, false);
                Assert(creation.CreateCore(Saved, "B1 isolated profile", "1.6.4871",
                    new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc), id,
                    ProfileStorageFault.None).Success, "fixture creation");
                ProfileStorageResult result = Repository.Load(id);
                Assert(result.Success && result.Document != null, "fixture load");
                Document = result.Document;
                Hash = result.ContentHash;
                FileName = Paths.FileForId(id);
                Bridge = new ProfileApplyBridgeService(delegate { return Repository; },
                    delegate { return new ProfileEditorReadinessResult(EditorState,
                        "B1_ISOLATED", 15, 0, 0); }, false);
            }

            internal ProfileApplyBridgeSession Prepare()
            {
                ProfileApplyBridgeResult result = Bridge.Prepare(Document, Hash, Live);
                Assert(result.Code == ProfileApplyBridgeCode.Prepared && result.Session != null,
                    "fixture prepare " + result.Code + "/" + result.Reason);
                return result.Session;
            }

            internal ProfileApplyBridgeResult Execute(ProfileApplyBridgeSession session,
                ProfileApplyBridgeFakeActions fake, bool biotechnology = true,
                bool consent = true, int dataFaultField = -1)
            {
                return Bridge.ConfirmAndSimulate(session, Live, consent, fake,
                    biotechnology, dataFaultField);
            }
        }

        private static void Assert(bool ok, string label)
        {
            checks++;
            if (!ok) throw new InvalidOperationException("[XPC-R6P1D2B1] " + label);
        }
        private static void CheckCode(ProfileApplyBridgeResult r, ProfileApplyBridgeCode expected,
            string label)
        {
            Assert(r != null && r.Code == expected,
                label + " expected=" + expected + " actual=" +
                (r == null ? "null" : r.Code + ":" + r.Reason));
        }
        private static void Count() { scenarios++; }
        private static bool ByteSame(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
        private static void AssertOrder(ProfileApplyBridgeFakeActions fake, params string[] expected)
        {
            Assert(fake.Calls.Count == expected.Length,
                "fake callback count " + fake.Calls.Count + " expected " + expected.Length);
            for (int i = 0; i < expected.Length; i++)
                Assert(fake.Calls[i] == expected[i], "fake callback order " + i);
        }
        private static void AssertUnchanged(Fixture f, XpCustomizerSettings baseline,
            byte[] fileBytes, XpCustomizerSettings realSettings)
        {
            string mismatch;
            Assert(SettingsApplyCoordinator.Equivalent(f.Live, baseline, out mismatch),
                "external input mutated:" + mismatch);
            Assert(ByteSame(fileBytes, File.ReadAllBytes(f.FileName)), "profile bytes mutated");
            Assert(object.ReferenceEquals(XpCustomizerMod.Settings, realSettings),
                "real mod settings root changed");
        }
        internal static void Run()
        {
            checks = 0;
            scenarios = 0;
            scenarioId = 0;
            string suite = Path.Combine(Path.GetTempPath(), "XPC-D2B1-" + Guid.NewGuid().ToString("N"));
            bool priorRecoveryLock = ProfileWriteSessionGate.IsBlocked;
            XpCustomizerSettings realSettings = XpCustomizerMod.Settings;
            try
            {
                Assert(!priorRecoveryLock, "initial recovery lock clear");

                // 01. Standard consent + real detached DATA/CACHE/BIOTECH/UI dispatch.
                {
                    Fixture f = new Fixture(suite);
                    XpCustomizerSettings before = SettingsSnapshotService.CloneIndependent(f.Live);
                    byte[] bytes = File.ReadAllBytes(f.FileName);
                    ProfileApplyBridgeFakeActions fake = new ProfileApplyBridgeFakeActions();
                    ProfileApplyBridgeResult result = f.Execute(f.Prepare(), fake);
                    CheckCode(result, ProfileApplyBridgeCode.TestApplyPass, "01 changed+consent");
                    Assert(result.BaselineFingerprint.Length == 64 &&
                        result.CandidateFingerprint.Length == 64 &&
                        result.BaselineFingerprint != result.CandidateFingerprint,
                        "01 stable SHA256 fingerprints");
                    AssertOrder(fake, "CAPTURE_GENE", "CACHE", "BIOTECH", "UI");
                    AssertUnchanged(f, before, bytes, realSettings);
                    Count();
                }
                // 02. Portable no-change does not issue a session / no side effects.
                {
                    Fixture f = new Fixture(suite, null, delegate(XpCustomizerSettings x)
                    { x.careerXpMultiplier = 2f; x.biotechCustomizationEnabled = true; });
                    CheckCode(f.Bridge.Prepare(f.Document, f.Hash, f.Live),
                        ProfileApplyBridgeCode.NoChange, "02 no change");
                    Count();
                }
                // 03. Missing user consent consumes the session.
                {
                    Fixture f = new Fixture(suite);
                    ProfileApplyBridgeSession session = f.Prepare();
                    ProfileApplyBridgeFakeActions fake = new ProfileApplyBridgeFakeActions();
                    CheckCode(f.Execute(session, fake, true, false), ProfileApplyBridgeCode.Rejected,
                        "03 no consent");
                    Assert(fake.Calls.Count == 0, "03 no effect");
                    Count();
                }
                // 04. An attempted session cannot be confirmed again.
                {
                    Fixture f = new Fixture(suite);
                    ProfileApplyBridgeSession session = f.Prepare();
                    f.Execute(session, new ProfileApplyBridgeFakeActions(), true, false);
                    CheckCode(f.Execute(session, new ProfileApplyBridgeFakeActions()),
                        ProfileApplyBridgeCode.Duplicate, "04 duplicate");
                    Count();
                }
                // 05. Null session.
                {
                    Fixture f = new Fixture(suite);
                    CheckCode(f.Execute(null, new ProfileApplyBridgeFakeActions()),
                        ProfileApplyBridgeCode.Rejected, "05 null");
                    Count();
                }
                // 06. Invalid selected hash is rejected during prepare.
                {
                    Fixture f = new Fixture(suite);
                    CheckCode(f.Bridge.Prepare(f.Document, "bad", f.Live),
                        ProfileApplyBridgeCode.Rejected, "06 invalid hash");
                    Count();
                }
                // 07. Deleting an already selected file causes conflict.
                {
                    Fixture f = new Fixture(suite);
                    ProfileApplyBridgeSession session = f.Prepare();
                    File.Delete(f.FileName);
                    CheckCode(f.Execute(session, new ProfileApplyBridgeFakeActions()),
                        ProfileApplyBridgeCode.Conflict, "07 deleted file");
                    Count();
                }
                // 08. External file content replacement after selection.
                {
                    Fixture f = new Fixture(suite);
                    ProfileApplyBridgeSession session = f.Prepare();
                    ProfileDocument changed = ProfileJsonCodec.Deserialize(
                        ProfileJsonCodec.Serialize(f.Document));
                    changed.DisplayName = "Changed outside";
                    File.WriteAllText(f.FileName, ProfileJsonCodec.Serialize(changed),
                        new UTF8Encoding(false));
                    CheckCode(f.Execute(session, new ProfileApplyBridgeFakeActions()),
                        ProfileApplyBridgeCode.Conflict, "08 stale file SHA");
                    Count();
                }
                // 09. Metadata tampered in the selected in-memory list row.
                {
                    Fixture f = new Fixture(suite);
                    ProfileDocument spoof = ProfileJsonCodec.Deserialize(
                        ProfileJsonCodec.Serialize(f.Document));
                    spoof.DisplayName = "In-memory spoof";
                    CheckCode(f.Bridge.Prepare(spoof, f.Hash, f.Live),
                        ProfileApplyBridgeCode.Conflict, "09 stale metadata");
                    Count();
                }
                // 10. Portable live field changes after review.
                {
                    Fixture f = new Fixture(suite);
                    ProfileApplyBridgeSession session = f.Prepare();
                    f.Live.careerXpMultiplier = 5f;
                    CheckCode(f.Execute(session, new ProfileApplyBridgeFakeActions()),
                        ProfileApplyBridgeCode.Conflict, "10 changed live portable");
                    Count();
                }
                // 11. Non-portable legacy field changes are still a conflict.
                {
                    Fixture f = new Fixture(suite);
                    ProfileApplyBridgeSession session = f.Prepare();
                    f.Live.furtherLearningHardWorkerModifier += 0.1f;
                    CheckCode(f.Execute(session, new ProfileApplyBridgeFakeActions()),
                        ProfileApplyBridgeCode.Conflict, "11 changed live nonportable");
                    Count();
                }
                // 12-13. Uncommitted and unknown editor states both reject.
                foreach (ProfileEditorReadinessCode editor in new [] {
                    ProfileEditorReadinessCode.PendingEdit, ProfileEditorReadinessCode.Uncertain })
                {
                    Fixture f = new Fixture(suite);
                    ProfileApplyBridgeSession session = f.Prepare();
                    f.EditorState = editor;
                    CheckCode(f.Execute(session, new ProfileApplyBridgeFakeActions()),
                        editor == ProfileEditorReadinessCode.PendingEdit ?
                        ProfileApplyBridgeCode.PendingEdit : ProfileApplyBridgeCode.Uncertain,
                        "12/13 editor becomes unsafe");
                    Count();
                }
                // 14. Storage recovery gate blocks the chosen profile.
                {
                    Fixture f = new Fixture(suite);
                    ProfileApplyBridgeSession session = f.Prepare();
                    ProfileWriteSessionGate.Observe(ProfileStorageCode.RecoveryRequired);
                    CheckCode(f.Execute(session, new ProfileApplyBridgeFakeActions()),
                        ProfileApplyBridgeCode.RecoveryRequired, "14 recovery lock");
                    ProfileWriteSessionGate.ResetForIsolatedTests();
                    Count();
                }
                // 15. Corrupt or unsupported file cannot bypass file validation.
                {
                    Fixture f = new Fixture(suite);
                    ProfileApplyBridgeSession session = f.Prepare();
                    File.WriteAllText(f.FileName, "{not-valid-json", new UTF8Encoding(false));
                    CheckCode(f.Execute(session, new ProfileApplyBridgeFakeActions()),
                        ProfileApplyBridgeCode.Rejected, "15 corrupt file");
                    Count();
                }
                // 16. Exported unknown SkillDef key survives detached simulation.
                {
                    Fixture f = new Fixture(suite, delegate(XpCustomizerSettings x)
                    { x.skillSpecificXpMultipliers["UnknownMod.Skill999"] = 1.7f; });
                    ProfileApplyBridgeFakeActions fake = new ProfileApplyBridgeFakeActions();
                    CheckCode(f.Execute(f.Prepare(), fake), ProfileApplyBridgeCode.TestApplyPass,
                        "16 unknown SkillDef");
                    Assert(fake.DetachedTarget.skillSpecificXpMultipliers.ContainsKey("UnknownMod.Skill999") &&
                        fake.DetachedTarget.skillSpecificXpMultipliers["UnknownMod.Skill999"] == 1.7f,
                        "16 unknown SkillDef preserved");
                    Count();
                }
                // 17. Disabled options retain their saved subordinate values.
                {
                    Fixture f = new Fixture(suite, delegate(XpCustomizerSettings x)
                    { x.skillSpecificXpEnabled = false; x.skillSpecificXpMultipliers["Shooting"] = 1.45f; });
                    ProfileApplyBridgeFakeActions fake = new ProfileApplyBridgeFakeActions();
                    CheckCode(f.Execute(f.Prepare(), fake), ProfileApplyBridgeCode.TestApplyPass,
                        "17 disabled subordinate");
                    Assert(!fake.DetachedTarget.skillSpecificXpEnabled &&
                        fake.DetachedTarget.skillSpecificXpMultipliers["Shooting"] == 1.45f,
                        "17 OFF stored value preserved");
                    Count();
                }
                // 18. Empty career splits survive, even if custom split is OFF.
                {
                    Fixture f = new Fixture(suite, delegate(XpCustomizerSettings x)
                    { x.careerXpUseCustomDistribution = false; x.careerXpSplitLevels.Clear(); });
                    ProfileApplyBridgeFakeActions fake = new ProfileApplyBridgeFakeActions();
                    CheckCode(f.Execute(f.Prepare(), fake), ProfileApplyBridgeCode.TestApplyPass,
                        "18 empty splits");
                    Assert(fake.DetachedTarget.careerXpSplitLevels.Count == 0,
                        "18 empty splits preserved");
                    Count();
                }
                // 19. Default/Ordinal map comparers align only in detached candidate.
                {
                    Fixture f = new Fixture(suite);
                    Assert(object.ReferenceEquals(f.Live.skillSpecificXpMultipliers.Comparer,
                        EqualityComparer<string>.Default), "19 baseline Default comparer");
                    ProfileApplyBridgeFakeActions fake = new ProfileApplyBridgeFakeActions();
                    CheckCode(f.Execute(f.Prepare(), fake), ProfileApplyBridgeCode.TestApplyPass,
                        "19 default/ordinal parity");
                    Assert(object.ReferenceEquals(fake.DetachedTarget.skillSpecificXpMultipliers.Comparer,
                        f.Live.skillSpecificXpMultipliers.Comparer), "19 comparer aligned");
                    Count();
                }
                // 20. Ignore-case comparer changes semantics; do not silently convert.
                {
                    Fixture f = new Fixture(suite, null, delegate(XpCustomizerSettings x)
                    { x.skillSpecificXpMultipliers = new Dictionary<string, float>(
                        x.skillSpecificXpMultipliers, StringComparer.OrdinalIgnoreCase); });
                    ProfileApplyBridgeFakeActions fake = new ProfileApplyBridgeFakeActions();
                    CheckCode(f.Execute(f.Prepare(), fake), ProfileApplyBridgeCode.Rejected,
                        "20 unsupported comparer");
                    Assert(fake.Calls.Count == 0, "20 no fake effects");
                    Count();
                }
                // 21. Only cache+UI for a pure career curve change.
                {
                    Fixture f = new Fixture(suite, delegate(XpCustomizerSettings x)
                    { x.biotechCustomizationEnabled = false; });
                    ProfileApplyBridgeFakeActions fake = new ProfileApplyBridgeFakeActions();
                    CheckCode(f.Execute(f.Prepare(), fake), ProfileApplyBridgeCode.TestApplyPass,
                        "21 cache only");
                    AssertOrder(fake, "CACHE", "UI");
                    Count();
                }
                // 22. Only Biotech+UI when career curve matches the profile.
                {
                    Fixture f = new Fixture(suite, null, delegate(XpCustomizerSettings x)
                    { x.careerXpMultiplier = 2f; });
                    ProfileApplyBridgeFakeActions fake = new ProfileApplyBridgeFakeActions();
                    CheckCode(f.Execute(f.Prepare(), fake), ProfileApplyBridgeCode.TestApplyPass,
                        "22 biotech only");
                    AssertOrder(fake, "CAPTURE_GENE", "BIOTECH", "UI");
                    Count();
                }
                // 23. With DLC unavailable: no GeneDef simulation callbacks.
                {
                    Fixture f = new Fixture(suite);
                    ProfileApplyBridgeFakeActions fake = new ProfileApplyBridgeFakeActions();
                    CheckCode(f.Execute(f.Prepare(), fake, false), ProfileApplyBridgeCode.TestApplyPass,
                        "23 DLC OFF");
                    AssertOrder(fake, "CACHE", "UI");
                    Count();
                }
                // 24. ON->OFF transition requests owned-gene restoration.
                {
                    Fixture f = new Fixture(suite, delegate(XpCustomizerSettings x)
                    { x.biotechCustomizationEnabled = false; }, delegate(XpCustomizerSettings x)
                    { x.biotechCustomizationEnabled = true; x.careerXpMultiplier = 2f; });
                    ProfileApplyBridgeFakeActions fake = new ProfileApplyBridgeFakeActions();
                    CheckCode(f.Execute(f.Prepare(), fake), ProfileApplyBridgeCode.TestApplyPass,
                        "24 biotech ON-to-OFF");
                    AssertOrder(fake, "CAPTURE_GENE", "BIOTECH", "UI");
                    Count();
                }
                // 25. DATA partial write injection: rollback values and references.
                {
                    Fixture f = new Fixture(suite);
                    ProfileApplyBridgeFakeActions fake = new ProfileApplyBridgeFakeActions();
                    CheckCode(f.Execute(f.Prepare(), fake, true, true, 40),
                        ProfileApplyBridgeCode.RollbackPass, "25 DATA rollback");
                    AssertOrder(fake, "CAPTURE_GENE");
                    Assert(fake.OriginalReferencesRestored(), "25 DATA references restored");
                    Count();
                }
                // 26-28. CACHE, BIOTECH, UI fault injection + verified rollback.
                foreach (ProfileApplyBridgeFakeFault fault in new [] {
                    ProfileApplyBridgeFakeFault.Cache, ProfileApplyBridgeFakeFault.Biotech,
                    ProfileApplyBridgeFakeFault.Ui })
                {
                    Fixture f = new Fixture(suite);
                    ProfileApplyBridgeFakeActions fake = new ProfileApplyBridgeFakeActions();
                    fake.FailAt = fault;
                    CheckCode(f.Execute(f.Prepare(), fake), ProfileApplyBridgeCode.RollbackPass,
                        "26-28 external rollback " + fault);
                    Assert(fake.OriginalReferencesRestored() && fake.FakeGeneValue == 135,
                        "26-28 reference and fake gene restored");
                    Count();
                }
                // 29. Gene restoration failure explicitly requires recovery.
                {
                    Fixture f = new Fixture(suite);
                    ProfileApplyBridgeFakeActions fake = new ProfileApplyBridgeFakeActions();
                    fake.FailAt = ProfileApplyBridgeFakeFault.Biotech;
                    fake.FailGeneRestore = true;
                    CheckCode(f.Execute(f.Prepare(), fake), ProfileApplyBridgeCode.RecoveryRequired,
                        "29 gene restore failure");
                    Assert(fake.FakeGeneValue == 911 && fake.RestoreAttempts == 1,
                        "29 failure detectable, never pass");
                    Count();
                }
                // 30. Persistent UI restore failure explicitly requires recovery.
                {
                    Fixture f = new Fixture(suite);
                    ProfileApplyBridgeFakeActions fake = new ProfileApplyBridgeFakeActions();
                    fake.FailAt = ProfileApplyBridgeFakeFault.Ui;
                    fake.FailUiRecovery = true;
                    CheckCode(f.Execute(f.Prepare(), fake), ProfileApplyBridgeCode.RecoveryRequired,
                        "30 persistent UI failure");
                    Count();
                }
                // 31. Attempt another bridge execution from within fake cache.
                {
                    Fixture f = new Fixture(suite);
                    ProfileApplyBridgeSession session = f.Prepare();
                    ProfileApplyBridgeSession nestedSession = f.Prepare();
                    ProfileApplyBridgeFakeActions fake = new ProfileApplyBridgeFakeActions();
                    fake.OnCache = delegate
                    {
                        ProfileApplyBridgeFakeActions nested = new ProfileApplyBridgeFakeActions();
                        CheckCode(f.Execute(nestedSession, nested), ProfileApplyBridgeCode.Rejected,
                            "31 reentrant apply");
                        Assert(nested.Calls.Count == 0, "31 no nested writes");
                    };
                    CheckCode(f.Execute(session, fake), ProfileApplyBridgeCode.TestApplyPass,
                        "31 outer transaction succeeds");
                    Count();
                }
                // 32. A fatal outcome never authorises a second execution.
                {
                    Fixture f = new Fixture(suite);
                    ProfileApplyBridgeSession session = f.Prepare();
                    ProfileApplyBridgeFakeActions fake = new ProfileApplyBridgeFakeActions();
                    fake.FailAt = ProfileApplyBridgeFakeFault.Ui;
                    fake.FailUiRecovery = true;
                    CheckCode(f.Execute(session, fake), ProfileApplyBridgeCode.RecoveryRequired,
                        "32 unrecovered first call");
                    ProfileApplyBridgeFakeActions retry = new ProfileApplyBridgeFakeActions();
                    CheckCode(f.Execute(session, retry), ProfileApplyBridgeCode.Duplicate,
                        "32 same session retry denied");
                    Assert(retry.Calls.Count == 0, "32 zero post-fatal callbacks");
                    Count();
                }
                // 33. Failed consent/abort never modifies the source profile file.
                {
                    Fixture f = new Fixture(suite);
                    byte[] old = File.ReadAllBytes(f.FileName);
                    ProfileApplyBridgeSession session = f.Prepare();
                    ProfileApplyBridgeFakeActions fake = new ProfileApplyBridgeFakeActions();
                    CheckCode(f.Execute(session, fake, true, false), ProfileApplyBridgeCode.Rejected,
                        "33 aborted consent");
                    Assert(ByteSame(old, File.ReadAllBytes(f.FileName)) &&
                        fake.Calls.Count == 0, "33 file bytes and fake actions untouched");
                    Count();
                }
                // 34. No fake actions allowed when final callback is not supplied.
                {
                    Fixture f = new Fixture(suite);
                    CheckCode(f.Execute(f.Prepare(), null), ProfileApplyBridgeCode.Rejected,
                        "34 no adapter");
                    Count();
                }
                // 35. Fake Biotech snapshot unavailable prevents data write.
                {
                    Fixture f = new Fixture(suite);
                    ProfileApplyBridgeFakeActions fake = new ProfileApplyBridgeFakeActions();
                    fake.FailCapture = true;
                    CheckCode(f.Execute(f.Prepare(), fake), ProfileApplyBridgeCode.Rejected,
                        "35 snapshot failure");
                    Assert(fake.OriginalReferencesRestored(), "35 no original reference changes");
                    Count();
                }
                // 36. Strict comparator contract was not changed by portable alignment.
                {
                    XpCustomizerSettings a = new XpCustomizerSettings();
                    XpCustomizerSettings b = SettingsSnapshotService.CloneIndependent(a);
                    b.skillSpecificXpMultipliers = new Dictionary<string, float>(
                        a.skillSpecificXpMultipliers, StringComparer.Ordinal);
                    string mismatch;
                    Assert(!SettingsApplyCoordinator.Equivalent(a, b, out mismatch) &&
                        mismatch == "skillSpecificXpMultipliers",
                        "36 rollback comparator remains strict");
                    Count();
                }

                Assert(scenarios >= 32, "scenario minimum");
                Assert(object.ReferenceEquals(XpCustomizerMod.Settings, realSettings),
                    "global game settings identity unchanged");
                Assert(SettingsRuntimeApplyCoordinator.LiveApplyStillBlockedForAudit,
                    "release/live apply remains sealed");
                Assert(!ProfileWriteSessionGate.IsBlocked, "fixture recovery lock reset");
                Log.Message("[XPC-R6P1D2B1][PROFILE_APPLY_BRIDGE_AUDIT] status=PASS; " +
                    "revision=" + ProfileApplyBridgeService.Revision +
                    "; scenarios=" + scenarios + "; checks=" + checks +
                    "; testRoot=ISOLATED_TEMP; oneShot=PASS; consent=REQUIRED_BUT_NOT_LIVE_TOKEN; " +
                    "noChange=ZERO_ACTION; staleHash=CONFLICT; metadata=CONFLICT; " +
                    "portableAndNonPortableBaseline=CHECKED; editor=FAIL_CLOSED; " +
                    "dictionaryDefaultOrdinal=NORMALIZED_DETACHED_ONLY; strictRollback=UNCHANGED; " +
                    "unknownSkillKeys=KEPT; disabledValues=KEPT; emptySplits=KEPT; " +
                    "fakeStageOrder=DATA_CACHE_BIOTECH_UI; dataFault=RECOVERED; " +
                    "externalFaults=3; geneAndUiRestoreFailure=DETECTED; " +
                    "reentrant=BLOCKED; originalProfileFiles=NO_GATE_WRITES; " +
                    "userProfileWritesByBridge=0; liveSettingsWrites=0; geneDefWrites=0; " +
                    "pawnWrites=0; liveCacheWrites=0; scribeCalls=0; " +
                    "productionApply=NOT_EXERCISED_BY_THIS_TEST; realIMGUI=NOT_TESTED; build=" + XpCustomizerMod.BuildId);
            }
            finally
            {
                if (!priorRecoveryLock) ProfileWriteSessionGate.ResetForIsolatedTests();
                if (Directory.Exists(suite))
                {
                    try
                    {
                        if ((File.GetAttributes(suite) & FileAttributes.ReparsePoint) == 0)
                            Directory.Delete(suite, true);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }
    }
}
#endif
