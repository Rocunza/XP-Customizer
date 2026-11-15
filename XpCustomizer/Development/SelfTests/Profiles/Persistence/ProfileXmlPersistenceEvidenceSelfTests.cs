#if DEBUG
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer
{
    /// <summary>
    /// D2-C2 isolated fault-injection tests. All filesystem writes are to a
    /// private, randomly named directory under Path.GetTempPath(). NEVER to
    /// RimWorld ModSettings XML, savegames or user profile JSON. No Scribe.
    /// </summary>
    internal static class ProfileXmlPersistenceEvidenceSelfTests
    {
        private const string HashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private static string testRoot;
        private static int scenarios;
        private static int checks;
        private static int fixtureCount;
        private static int isolatedTempWrites;

        private sealed class Fixture
        {
            internal string Target;
            internal string Backup;
            internal byte[] OldBytes;
            internal byte[] NewBytes;
            internal string OldSha;
            internal string NewSha;
            internal ProfileXmlPersistenceEvidencePolicy Policy;
        }

        private static void Assert(bool condition, string reason)
        {
            checks++;
            if (!condition)
                throw new InvalidOperationException("[XPC-R6P1D2C2] " + reason);
        }

        private static void Case(string name, Action action)
        {
            action();
            scenarios++;
        }

        private static string Sha(byte[] content)
        {
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] digest = algorithm.ComputeHash(content);
                StringBuilder s = new StringBuilder(64);
                foreach (byte b in digest) s.Append(b.ToString("x2"));
                return s.ToString();
            }
        }

        private static string FileSha(string path)
        {
            return File.Exists(path) ? Sha(File.ReadAllBytes(path)) : null;
        }

        private static void WriteFixture(string path, byte[] content)
        {
            File.WriteAllBytes(path, content);
            isolatedTempWrites++;
        }

        private static Fixture NewFixture()
        {
            int index = ++fixtureCount;
            Fixture f = new Fixture();
            f.Target = Path.Combine(testRoot, "fixture_" + index + ".xml");
            f.Backup = Path.Combine(testRoot, "fixture_" + index + ".backup");
            f.OldBytes = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><FakeXpSettings><Value>old-" + index + "</Value></FakeXpSettings>");
            f.NewBytes = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><FakeXpSettings><Value>new-" + index + "</Value></FakeXpSettings>");
            f.OldSha = Sha(f.OldBytes);
            f.NewSha = Sha(f.NewBytes);
            f.Policy = new ProfileXmlPersistenceEvidencePolicy();
            WriteFixture(f.Target, f.OldBytes);
            WriteFixture(f.Backup, File.ReadAllBytes(f.Target));
            return f;
        }

        private static Fixture Prepared()
        {
            Fixture f = NewFixture();
            Assert(f.Policy.Prepare(FileSha(f.Target), FileSha(f.Backup), f.NewSha, true) ==
                ProfileXmlEvidenceResult.Prepared, "verified backup permits prepare");
            Assert(f.Policy.Phase == ProfileXmlEvidencePhase.Prepared, "prepared phase");
            return f;
        }

        private static Fixture Writing()
        {
            Fixture f = Prepared();
            Assert(f.Policy.BeginWrite(FileSha(f.Target)) == ProfileXmlEvidenceResult.WriteAllowed,
                "begin write permitted");
            Assert(f.Policy.DiskStateUncertain && f.Policy.WriteAttempted,
                "mark uncertain BEFORE target open");
            return f;
        }

        private static ProfilePersistenceEvidence Candidate()
        {
            return new ProfilePersistenceEvidence
            {
                ProfileId = "00000000-0000-4000-8000-000000000091",
                SelectedSha256 = HashA,
                DisplayName = "D2-C2 isolated evidence",
                CreatedUtc = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc),
                UpdatedUtc = new DateTime(2026, 11, 7, 1, 0, 0, DateTimeKind.Utc),
                SourceGameVersion = "1.6.4871", ProfileFormatVersion = 1,
                SettingsSchemaVersion = 1, StrictBaselineFingerprint = HashA,
                DetachedCandidateFingerprint = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                PortableFieldsDiffer = true, SelectedHashMatchesDisk = true,
                MetadataMatchesDisk = true, StrictLiveValuesMatch = true,
                StrictLiveReferencesMatch = true, DetachedCandidateValid = true,
                CandidateIsDetached = true, NonPortableBaselinePreserved = true,
                UnknownSkillKeysPreserved = true, EmptyCareerSplitsPreserved = true,
                DisabledValuesPreserved = true, DictionaryNormalizationDetachedOnly = true,
                EditorState = ProfilePersistenceEditorState.Ready
            };
        }

        private static ProfileApplyPersistenceContract PureC1AtPersist()
        {
            ProfilePersistenceEvidence candidate = Candidate();
            ProfileApplyPersistenceContract c = new ProfileApplyPersistenceContract();
            Assert(c.Prepare(candidate) == ProfilePersistenceDecision.AllowProposed,
                "C1 prepare");
            Assert(c.Confirm(true) == ProfilePersistenceDecision.AllowProposed, "C1 confirm");
            Assert(c.Recheck(candidate.Copy()) == ProfilePersistenceDecision.AllowProposed,
                "C1 recheck");
            Assert(c.ObserveRuntime(ProfilePersistenceRuntimeOutcome.AppliedAndVerified) ==
                ProfilePersistenceDecision.AllowProposed, "C1 FAKE runtime verified");
            Assert(c.RequestPersistence() == ProfilePersistenceDecision.AllowProposed,
                "C1 persistence request");
            return c;
        }

        internal static void Run()
        {
            scenarios = 0;
            checks = 0;
            fixtureCount = 0;
            isolatedTempWrites = 0;
            XpCustomizerSettings originalSettings = XpCustomizerMod.Settings;
            bool originalRuntimeLock = RuntimeRecoverySessionGate.LiveIsBlocked;
            bool originalProfileWriteLock = ProfileWriteSessionGate.IsBlocked;
            testRoot = Path.Combine(Path.GetTempPath(),
                "XPC_R6P1D2C2_ISOLATED_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);
            try
            {
                Case("01 virgin state", delegate
                {
                    ProfileXmlPersistenceEvidencePolicy p = new ProfileXmlPersistenceEvidencePolicy();
                    Assert(p.Phase == ProfileXmlEvidencePhase.New, "01 new");
                    Assert(!p.DiskStateUncertain && !p.WriteAttempted, "01 no effects");
                    Assert(p.ToPersistenceContractOutcome() == ProfilePersistenceDiskOutcome.FailedOrUnknown,
                        "01 no proof");
                });
                Case("02 exact real fixture backup", delegate
                {
                    Fixture f = Prepared();
                    Assert(FileSha(f.Target) == f.OldSha && FileSha(f.Backup) == f.OldSha,
                        "02 target/backup hash");
                    Assert(!f.Policy.WriteAttempted && !f.Policy.DiskStateUncertain,
                        "02 no write requested");
                });
                Case("03 corrupt retained backup", delegate
                {
                    Fixture f = NewFixture();
                    WriteFixture(f.Backup, f.NewBytes);
                    Assert(f.Policy.Prepare(FileSha(f.Target), FileSha(f.Backup), f.NewSha, true) ==
                        ProfileXmlEvidenceResult.BlockedInvalidEvidence, "03 reject");
                    Assert(f.Policy.Phase == ProfileXmlEvidencePhase.Rejected && !f.Policy.WriteAttempted,
                        "03 no write");
                });
                Case("04 no read-back evidence", delegate
                {
                    Fixture f = NewFixture();
                    Assert(f.Policy.Prepare(f.OldSha, f.OldSha, f.NewSha, false) ==
                        ProfileXmlEvidenceResult.BlockedInvalidEvidence, "04 reject");
                });
                Case("05 same bytes no transaction", delegate
                {
                    Fixture f = NewFixture();
                    Assert(f.Policy.Prepare(f.OldSha, f.OldSha, f.OldSha, true) ==
                        ProfileXmlEvidenceResult.BlockedInvalidEvidence, "05 no write required");
                });
                Case("06 bad SHA syntax", delegate
                {
                    Fixture f = NewFixture();
                    Assert(f.Policy.Prepare("INVALID", f.OldSha, f.NewSha, true) ==
                        ProfileXmlEvidenceResult.BlockedInvalidEvidence, "06 malformed SHA rejected");
                });
                Case("07 uppercase digest rejected", delegate
                {
                    Fixture f = NewFixture();
                    Assert(f.Policy.Prepare(f.OldSha.ToUpperInvariant(), f.OldSha, f.NewSha, true) ==
                        ProfileXmlEvidenceResult.BlockedInvalidEvidence, "07 lowercase hash contract");
                });
                Case("08 repeated prepare blocked", delegate
                {
                    Fixture f = Prepared();
                    Assert(f.Policy.Prepare(f.OldSha, f.OldSha, f.NewSha, true) ==
                        ProfileXmlEvidenceResult.BlockedInvalidTransition, "08 once");
                    Assert(f.Policy.Phase == ProfileXmlEvidencePhase.Prepared, "08 state retained");
                });
                Case("09 changed target between backup and write", delegate
                {
                    Fixture f = Prepared();
                    WriteFixture(f.Target, f.NewBytes);
                    Assert(f.Policy.BeginWrite(FileSha(f.Target)) ==
                        ProfileXmlEvidenceResult.BlockedChangedTarget, "09 blocked external drift");
                    Assert(!f.Policy.WriteAttempted && f.Policy.Phase == ProfileXmlEvidencePhase.Rejected,
                        "09 no write by adapter");
                });
                Case("10 deleted target before write", delegate
                {
                    Fixture f = Prepared();
                    File.Delete(f.Target);
                    Assert(f.Policy.BeginWrite(FileSha(f.Target)) ==
                        ProfileXmlEvidenceResult.BlockedChangedTarget, "10 deleted target rejected");
                    Assert(!f.Policy.WriteAttempted, "10 no write");
                });
                Case("11 write uncertainty before open", delegate
                {
                    Fixture f = Writing();
                    Assert(f.Policy.UncertaintyEverObserved, "11 initial uncertainty marked");
                    Assert(FileSha(f.Target) == f.OldSha, "11 no IO yet");
                });
                Case("12 full write with digest read-back", delegate
                {
                    Fixture f = Writing();
                    WriteFixture(f.Target, f.NewBytes);
                    Assert(f.Policy.ObserveWriterReturn(true, FileSha(f.Target)) ==
                        ProfileXmlEvidenceResult.WrittenVerified, "12 written");
                    Assert(f.Policy.HasVerifiedNewXml && !f.Policy.DiskStateUncertain,
                        "12 verified post-write (not power-loss durability)");
                });
                Case("13 no write return cannot claim success", delegate
                {
                    Fixture f = Writing();
                    WriteFixture(f.Target, f.NewBytes);
                    Assert(f.Policy.ObserveWriterReturn(false, FileSha(f.Target)) ==
                        ProfileXmlEvidenceResult.PersistenceUncertain, "13 exception remains unsafe");
                });
                Case("14 false positive success wrong bytes", delegate
                {
                    Fixture f = Writing();
                    WriteFixture(f.Target, f.OldBytes);
                    Assert(f.Policy.ObserveWriterReturn(true, FileSha(f.Target)) ==
                        ProfileXmlEvidenceResult.PersistenceUncertain, "14 digest mismatch");
                });
                Case("15 cannot read after returned success", delegate
                {
                    Fixture f = Writing();
                    Assert(f.Policy.ObserveWriterReturn(true, null) ==
                        ProfileXmlEvidenceResult.PersistenceUncertain, "15 missing proof");
                });
                Case("16 duplicate result cannot revise verified", delegate
                {
                    Fixture f = Writing();
                    WriteFixture(f.Target, f.NewBytes);
                    f.Policy.ObserveWriterReturn(true, FileSha(f.Target));
                    Assert(f.Policy.ObserveWriterReturn(false, f.OldSha) ==
                        ProfileXmlEvidenceResult.BlockedInvalidTransition, "16 duplicate denied");
                    Assert(f.Policy.HasVerifiedNewXml, "16 terminal phase unchanged");
                });
                Case("17 proven before-open failure", delegate
                {
                    Fixture f = Writing();
                    Assert(f.Policy.ObserveWriteFailure(true, FileSha(f.Target)) ==
                        ProfileXmlEvidenceResult.FailedKnownUnchanged, "17 proven no-open");
                    Assert(!f.Policy.WriteAttempted && !f.Policy.DiskStateUncertain,
                        "17 known unchanged");
                });
                Case("18 before-open claim but bytes changed", delegate
                {
                    Fixture f = Writing();
                    WriteFixture(f.Target, f.NewBytes);
                    Assert(f.Policy.ObserveWriteFailure(true, FileSha(f.Target)) ==
                        ProfileXmlEvidenceResult.PersistenceUncertain, "18 reject forged no-open proof");
                    Assert(f.Policy.DiskStateUncertain, "18 uncertain");
                });
                Case("19 no proof before open even if hash matches", delegate
                {
                    Fixture f = Writing();
                    Assert(f.Policy.ObserveWriteFailure(false, FileSha(f.Target)) ==
                        ProfileXmlEvidenceResult.PersistenceUncertain, "19 uncertain by default");
                });
                Case("20 partial write injection", delegate
                {
                    Fixture f = Writing();
                    WriteFixture(f.Target, new byte[] { 60, 70, 65, 75, 69, 0, 12 });
                    Assert(f.Policy.ObserveWriteFailure(false, FileSha(f.Target)) ==
                        ProfileXmlEvidenceResult.PersistenceUncertain, "20 partial write unsafe");
                    Assert(FileSha(f.Target) != f.OldSha && f.Policy.DiskStateUncertain,
                        "20 partial bytes changed");
                });
                Case("21 full write then injected failure", delegate
                {
                    Fixture f = Writing();
                    WriteFixture(f.Target, f.NewBytes);
                    Assert(f.Policy.ObserveWriteFailure(false, FileSha(f.Target)) ==
                        ProfileXmlEvidenceResult.PersistenceUncertain, "21 no false success");
                });
                Case("22 read failure after write", delegate
                {
                    Fixture f = Writing();
                    WriteFixture(f.Target, f.NewBytes);
                    Assert(f.Policy.ObserveWriteFailure(false, null) ==
                        ProfileXmlEvidenceResult.PersistenceUncertain, "22 read error uncertain");
                });
                Case("23 restore old bytes from retained backup", delegate
                {
                    Fixture f = Writing();
                    WriteFixture(f.Target, new byte[] { 1, 2, 3 });
                    f.Policy.ObserveWriteFailure(false, FileSha(f.Target));
                    WriteFixture(f.Target, File.ReadAllBytes(f.Backup));
                    Assert(f.Policy.ObserveRestore(true, FileSha(f.Backup) == f.OldSha,
                        FileSha(f.Target)) == ProfileXmlEvidenceResult.RestoreVerifiedButUncertain,
                        "23 restored proof");
                    Assert(f.Policy.HasVerifiedOldXmlAfterCompensation && f.Policy.DiskStateUncertain,
                        "23 unknown-write stickiness");
                });
                Case("24 rollback cannot be called before failure", delegate
                {
                    Fixture f = Writing();
                    Assert(f.Policy.ObserveRestore(true, true, f.OldSha) ==
                        ProfileXmlEvidenceResult.BlockedInvalidTransition, "24 invalid restore");
                });
                Case("25 failed restore must not declare recovered", delegate
                {
                    Fixture f = Writing();
                    WriteFixture(f.Target, new byte[] { 42 });
                    f.Policy.ObserveWriteFailure(false, FileSha(f.Target));
                    Assert(f.Policy.ObserveRestore(false, true, FileSha(f.Target)) ==
                        ProfileXmlEvidenceResult.RestoreUnverified, "25 unverified restore");
                    Assert(f.Policy.Phase == ProfileXmlEvidencePhase.Uncertain,
                        "25 still uncertain");
                });
                Case("26 restored bytes but corrupted backup", delegate
                {
                    Fixture f = Writing();
                    WriteFixture(f.Target, new byte[] { 42 });
                    f.Policy.ObserveWriteFailure(false, FileSha(f.Target));
                    WriteFixture(f.Backup, f.NewBytes);
                    WriteFixture(f.Target, f.OldBytes);
                    Assert(f.Policy.ObserveRestore(true, FileSha(f.Backup) == f.OldSha,
                        FileSha(f.Target)) == ProfileXmlEvidenceResult.RestoreUnverified,
                        "26 corrupt backup fails proof");
                });
                Case("27 restored wrong contents", delegate
                {
                    Fixture f = Writing();
                    WriteFixture(f.Target, new byte[] { 42 });
                    f.Policy.ObserveWriteFailure(false, FileSha(f.Target));
                    WriteFixture(f.Target, f.NewBytes);
                    Assert(f.Policy.ObserveRestore(true, true, FileSha(f.Target)) ==
                        ProfileXmlEvidenceResult.RestoreUnverified, "27 hash mismatch");
                });
                Case("28 delete file during attempted recovery", delegate
                {
                    Fixture f = Writing();
                    WriteFixture(f.Target, new byte[] { 42 });
                    f.Policy.ObserveWriteFailure(false, FileSha(f.Target));
                    File.Delete(f.Target);
                    Assert(f.Policy.ObserveRestore(true, true, FileSha(f.Target)) ==
                        ProfileXmlEvidenceResult.RestoreUnverified, "28 missing target");
                });
                Case("29 repeat restore cannot clear uncertainty", delegate
                {
                    Fixture f = Writing();
                    f.Policy.ObserveWriteFailure(false, FileSha(f.Target));
                    Assert(f.Policy.ObserveRestore(true, true, f.OldSha) ==
                        ProfileXmlEvidenceResult.RestoreVerifiedButUncertain, "29 first restore");
                    Assert(f.Policy.ObserveRestore(true, true, f.OldSha) ==
                        ProfileXmlEvidenceResult.RestoreVerifiedButUncertain, "29 repeat still uncertain");
                    Assert(f.Policy.DiskStateUncertain, "29 sticky");
                });
                Case("30 first fault remains immutable", delegate
                {
                    Fixture f = Writing();
                    f.Policy.ObserveWriteFailure(false, FileSha(f.Target));
                    string reason = f.Policy.FirstFailure;
                    f.Policy.ObserveRestore(false, false, null);
                    f.Policy.BeginWrite(f.OldSha);
                    Assert(reason == f.Policy.FirstFailure && reason != "NONE", "30 first failure fixed");
                });
                Case("31 verified success maps to C1", delegate
                {
                    Fixture f = Writing();
                    WriteFixture(f.Target, f.NewBytes);
                    f.Policy.ObserveWriterReturn(true, FileSha(f.Target));
                    Assert(f.Policy.ToPersistenceContractOutcome() ==
                        ProfilePersistenceDiskOutcome.WrittenAndVerified, "31 success mapping");
                    ProfileApplyPersistenceContract c = PureC1AtPersist();
                    Assert(c.ObservePersistence(f.Policy.ToPersistenceContractOutcome()) ==
                        ProfilePersistenceDecision.AllowProposed, "31 C1 accept");
                    Assert(c.Finish() == ProfilePersistenceDecision.Complete && c.Completed,
                        "31 C1 FINISH after isolated proofs");
                });
                Case("32 before-open failed maps to C1 compensation", delegate
                {
                    Fixture f = Writing();
                    f.Policy.ObserveWriteFailure(true, FileSha(f.Target));
                    ProfileApplyPersistenceContract c = PureC1AtPersist();
                    Assert(c.ObservePersistence(f.Policy.ToPersistenceContractOutcome()) ==
                        ProfilePersistenceDecision.PersistenceFailedKnownUnchanged,
                        "32 failure mapping");
                    Assert(c.ObserveCompensation(true) == ProfilePersistenceDecision.RuntimeRollbackVerified,
                        "32 fake runtime compensated");
                    Assert(!c.Completed && c.Phase == ProfilePersistencePhase.Compensated,
                        "32 never a success");
                });
                Case("33 uncertain write plus restored disk maps to C1 blocked", delegate
                {
                    Fixture f = Writing();
                    WriteFixture(f.Target, new byte[] { 1, 2 });
                    f.Policy.ObserveWriteFailure(false, FileSha(f.Target));
                    WriteFixture(f.Target, File.ReadAllBytes(f.Backup));
                    f.Policy.ObserveRestore(true, FileSha(f.Backup) == f.OldSha, FileSha(f.Target));
                    ProfileApplyPersistenceContract c = PureC1AtPersist();
                    Assert(c.ObservePersistence(f.Policy.ToPersistenceContractOutcome()) ==
                        ProfilePersistenceDecision.BlockPersistenceUncertain, "33 uncertain blocked");
                    Assert(c.ObserveCompensation(true) == ProfilePersistenceDecision.BlockPersistenceUncertain,
                        "33 fake runtime recovery does not clear unknown disk");
                    Assert(!c.Completed && c.DiskStateUncertain, "33 sticky even after restore");
                });
                Case("34 restore and runtime rollback fail independently", delegate
                {
                    Fixture f = Writing();
                    f.Policy.ObserveWriteFailure(false, null);
                    ProfileApplyPersistenceContract c = PureC1AtPersist();
                    Assert(c.ObservePersistence(f.Policy.ToPersistenceContractOutcome()) ==
                        ProfilePersistenceDecision.BlockPersistenceUncertain, "34 disk failure");
                    Assert(c.ObserveCompensation(false) == ProfilePersistenceDecision.RuntimeUnrecovered,
                        "34 runtime failure separate");
                    Assert(c.Phase == ProfilePersistencePhase.RuntimeUnrecovered && c.DiskStateUncertain,
                        "34 both unsafe");
                });
                Case("35 no live settings or lock touched", delegate
                {
                    Assert(Object.ReferenceEquals(originalSettings, XpCustomizerMod.Settings),
                        "35 live settings object unchanged");
                    Assert(originalRuntimeLock == RuntimeRecoverySessionGate.LiveIsBlocked,
                        "35 runtime latch unchanged");
                    Assert(originalProfileWriteLock == ProfileWriteSessionGate.IsBlocked,
                        "35 profile file gate unchanged");
                    Assert(SettingsRuntimeApplyCoordinator.LiveApplyStillBlockedForAudit,
                        "35 real Apply still blocked");
                });
                Case("36 fake user config never opened", delegate
                {
                    // No production XML location is supplied to any fixture;
                    // exact isolated-root ownership is asserted, not inferred.
                    Fixture f = NewFixture();
                    string expected = Path.GetFullPath(testRoot) + Path.DirectorySeparatorChar;
                    Assert(Path.GetFullPath(f.Target).StartsWith(expected, StringComparison.Ordinal),
                        "36 target is inside temp test root");
                    Assert(Path.GetFullPath(f.Backup).StartsWith(expected, StringComparison.Ordinal),
                        "36 backup is inside temp test root");
                });
                Assert(scenarios == 36, "scenario count");
                Assert(Object.ReferenceEquals(originalSettings, XpCustomizerMod.Settings),
                    "post-test live root identity");
                Assert(originalRuntimeLock == RuntimeRecoverySessionGate.LiveIsBlocked,
                    "post-test live gate");
                Assert(originalProfileWriteLock == ProfileWriteSessionGate.IsBlocked,
                    "post-test profile storage gate");
                Assert(SettingsRuntimeApplyCoordinator.LiveApplyStillBlockedForAudit,
                    "post-test live Apply gate");
            }
            finally
            {
                Directory.Delete(testRoot, true);
            }

            Log.Message("[XPC-R6P1D2C2][XML_FAULT_BOUNDARY_AUDIT] status=PASS; " +
                "revision=" + ProfileXmlPersistenceEvidencePolicy.Revision +
                "; scenarios=" + scenarios + "; checks=" + checks +
                "; scope=ISOLATED_TEMP_XML_PLUS_PURE_POLICY; " +
                "isolatedTempWrites=" + isolatedTempWrites +
                "; realModXmlWrites=0; scribeCalls=0; geneDefWrites=0; " +
                "liveSettingsWrites=0; profileJsonWrites=0; " +
                "partialWrite=UNCERTAIN; restoredAfterWrite=STILL_UNCERTAIN; " +
                "normalWriteSettings=UNCHANGED; C1Contract=INTEGRATED_FAKE_PROOF; " +
                "realScribeAtomicity=NOT_VERIFIED; osDurability=NOT_VERIFIED; " +
                "liveGateUnaffected=True; profileStorageGateUnaffected=True; " +
                "productionApply=NOT_EXERCISED_BY_THIS_TEST; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
