#if DEBUG
using System;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer
{
    /// <summary>
    /// D2-C1: pure policy assertions. No profile JSON, Scribe, runtime refresh,
    /// live ModSettings, game pawns or GeneDefs are touched by these scenarios.
    /// The fabricated 'verified disk' outcome is a mock label, NOT real XML I/O.
    /// </summary>
    internal static class ProfileApplyPersistenceContractSelfTests
    {
        private static int scenarios;
        private static int checks;
        private const string HashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string HashB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string HashC = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
        private const string IdA = "00000000-0000-4000-8000-000000000011";
        private const string IdB = "00000000-0000-4000-8000-000000000012";

        private static ProfilePersistenceEvidence Evidence()
        {
            return new ProfilePersistenceEvidence
            {
                ProfileId = IdA,
                SelectedSha256 = HashA,
                DisplayName = "D2-C1 isolated profile",
                CreatedUtc = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc),
                UpdatedUtc = new DateTime(2026, 11, 7, 1, 0, 0, DateTimeKind.Utc),
                SourceGameVersion = "1.6.4871",
                ProfileFormatVersion = 1,
                SettingsSchemaVersion = 1,
                StrictBaselineFingerprint = HashB,
                DetachedCandidateFingerprint = HashC,
                PortableFieldsDiffer = true,
                SelectedHashMatchesDisk = true,
                MetadataMatchesDisk = true,
                StrictLiveValuesMatch = true,
                StrictLiveReferencesMatch = true,
                DetachedCandidateValid = true,
                CandidateIsDetached = true,
                NonPortableBaselinePreserved = true,
                UnknownSkillKeysPreserved = true,
                EmptyCareerSplitsPreserved = true,
                DisabledValuesPreserved = true,
                DictionaryNormalizationDetachedOnly = true,
                EditorState = ProfilePersistenceEditorState.Ready
            };
        }

        private static void Assert(bool condition, string name)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("[XPC-R6P1D2C1] " + name);
        }

        private static void Case(string name, Action body)
        {
            body();
            scenarios++;
        }

        private static void ExpectInitial(string name, Action<ProfilePersistenceEvidence> modify,
            ProfilePersistenceDecision expected)
        {
            Case(name, delegate
            {
                ProfilePersistenceEvidence p = Evidence();
                modify(p);
                ProfileApplyPersistenceContract c = new ProfileApplyPersistenceContract();
                Assert(c.Prepare(p) == expected, name + " returned " + c.LastDecision);
                Assert(!c.RuntimeMutationMayHaveOccurred && !c.PersistenceMayHaveBeenAttempted &&
                    !c.DiskVerified && !c.Completed, name + " no side effects");
                Assert(c.Phase != ProfilePersistencePhase.ExplicitConfirmation,
                    name + " not eligible");
            });
        }

        private static ProfileApplyPersistenceContract Rechecked(ProfilePersistenceEvidence p)
        {
            ProfileApplyPersistenceContract c = new ProfileApplyPersistenceContract();
            Assert(c.Prepare(p) == ProfilePersistenceDecision.AllowProposed, "prepare valid");
            Assert(c.Confirm(true) == ProfilePersistenceDecision.AllowProposed, "consent valid");
            Assert(c.Recheck(p.Copy()) == ProfilePersistenceDecision.AllowProposed, "recheck valid");
            return c;
        }

        private static ProfileApplyPersistenceContract RuntimeVerified(ProfilePersistenceEvidence p)
        {
            ProfileApplyPersistenceContract c = Rechecked(p);
            Assert(c.ObserveRuntime(ProfilePersistenceRuntimeOutcome.AppliedAndVerified) ==
                ProfilePersistenceDecision.AllowProposed, "fake runtime verified");
            return c;
        }

        private static ProfileApplyPersistenceContract PersistRequested(ProfilePersistenceEvidence p)
        {
            ProfileApplyPersistenceContract c = RuntimeVerified(p);
            Assert(c.RequestPersistence() == ProfilePersistenceDecision.AllowProposed,
                "persistence request permitted");
            return c;
        }

        internal static void Run()
        {
            scenarios = 0;
            checks = 0;
            XpCustomizerSettings liveRoot = XpCustomizerMod.Settings;
            bool liveRuntimeLock = RuntimeRecoverySessionGate.LiveIsBlocked;
            bool profileWriteLock = ProfileWriteSessionGate.IsBlocked;

            Case("01 valid proposed commit", delegate
            {
                ProfileApplyPersistenceContract c = new ProfileApplyPersistenceContract();
                Assert(c.Prepare(Evidence()) == ProfilePersistenceDecision.AllowProposed, "01 allow");
                Assert(c.Phase == ProfilePersistencePhase.ExplicitConfirmation &&
                    !c.ConfirmationConsumed, "01 pending consent");
            });
            ExpectInitial("02 no portable changes", p => p.PortableFieldsDiffer = false,
                ProfilePersistenceDecision.NoChange);
            Case("03 cancel consumes consent", delegate
            {
                ProfileApplyPersistenceContract c = new ProfileApplyPersistenceContract();
                Assert(c.Prepare(Evidence()) == ProfilePersistenceDecision.AllowProposed, "03 prepare");
                Assert(c.Confirm(false) == ProfilePersistenceDecision.Cancelled, "03 cancel");
                Assert(c.ConfirmationConsumed && c.Phase == ProfilePersistencePhase.Cancelled,
                    "03 consumed and terminal");
                Assert(!c.RuntimeMutationMayHaveOccurred && !c.PersistenceMayHaveBeenAttempted,
                    "03 no effects");
            });
            Case("04 double confirmation denied", delegate
            {
                ProfileApplyPersistenceContract c = new ProfileApplyPersistenceContract();
                Assert(c.Prepare(Evidence()) == ProfilePersistenceDecision.AllowProposed, "04 prepared");
                Assert(c.Confirm(true) == ProfilePersistenceDecision.AllowProposed, "04 first");
                Assert(c.Confirm(true) == ProfilePersistenceDecision.BlockAlreadyConsumed,
                    "04 second blocked");
                Assert(c.Phase == ProfilePersistencePhase.Rejected, "04 no next stage");
            });
            ExpectInitial("05 stale SHA", p => p.SelectedHashMatchesDisk = false,
                ProfilePersistenceDecision.BlockStaleProfileHash);
            ExpectInitial("06 future JSON format", p => p.ProfileFormatVersion = 3,
                ProfilePersistenceDecision.BlockUnsupportedSchema);
            ExpectInitial("07 invalid profile ID", p => p.ProfileId = "../unknown",
                ProfilePersistenceDecision.BlockInvalidCandidate);
            ExpectInitial("08 unknown skills cannot be lost", p => p.UnknownSkillKeysPreserved = false,
                ProfilePersistenceDecision.BlockInvalidCandidate);
            ExpectInitial("09 nonportable fields stay baseline", p => p.NonPortableBaselinePreserved = false,
                ProfilePersistenceDecision.BlockInvalidCandidate);
            ExpectInitial("10 empty splits stay empty", p => p.EmptyCareerSplitsPreserved = false,
                ProfilePersistenceDecision.BlockInvalidCandidate);
            ExpectInitial("11 disabled values stay", p => p.DisabledValuesPreserved = false,
                ProfilePersistenceDecision.BlockInvalidCandidate);
            ExpectInitial("12 comparer normalization detached only", p => p.DictionaryNormalizationDetachedOnly = false,
                ProfilePersistenceDecision.BlockInvalidCandidate);
            ExpectInitial("13 candidate must be detached", p => p.CandidateIsDetached = false,
                ProfilePersistenceDecision.BlockInvalidCandidate);
            ExpectInitial("14 strict settings values", p => p.StrictLiveValuesMatch = false,
                ProfilePersistenceDecision.BlockStaleLiveSettings);
            ExpectInitial("15 strict reference identities", p => p.StrictLiveReferencesMatch = false,
                ProfilePersistenceDecision.BlockStaleLiveSettings);
            ExpectInitial("16 pending editor", p => p.EditorState = ProfilePersistenceEditorState.Pending,
                ProfilePersistenceDecision.BlockPendingEditor);
            ExpectInitial("17 uncertain editor", p => p.EditorState = ProfilePersistenceEditorState.Uncertain,
                ProfilePersistenceDecision.BlockUncertainEditor);
            ExpectInitial("18 invalid imported candidate", p => p.DetachedCandidateValid = false,
                ProfilePersistenceDecision.BlockInvalidCandidate);
            ExpectInitial("19 profile write lock", p => p.ProfileStorageRecoveryBlocked = true,
                ProfilePersistenceDecision.BlockProfileStorageRecovery);
            ExpectInitial("20 runtime recovery lock", p => p.RuntimeRecoveryBlocked = true,
                ProfilePersistenceDecision.BlockRuntimeRecovery);
            ExpectInitial("21 separate disk uncertainty", p => p.PersistenceUncertain = true,
                ProfilePersistenceDecision.BlockPersistenceUncertain);
            Case("22 first error sticky", delegate
            {
                ProfilePersistenceEvidence p = Evidence();
                p.EditorState = ProfilePersistenceEditorState.Pending;
                ProfileApplyPersistenceContract c = new ProfileApplyPersistenceContract();
                Assert(c.Prepare(p) == ProfilePersistenceDecision.BlockPendingEditor, "22 first");
                string reason = c.FirstFailureReason;
                Assert(c.Confirm(true) == ProfilePersistenceDecision.BlockInvalidTransition,
                    "22 later rejected");
                Assert(c.FirstFailure == ProfilePersistenceDecision.BlockPendingEditor &&
                    c.FirstFailureReason == reason, "22 immutable origin");
            });
            ExpectInitial("23 reentrancy", p => p.Reentrant = true,
                ProfilePersistenceDecision.BlockReentrant);
            Case("24 failed preflight never persists", delegate
            {
                ProfileApplyPersistenceContract c = new ProfileApplyPersistenceContract();
                ProfilePersistenceEvidence p = Evidence(); p.SelectedHashMatchesDisk = false;
                Assert(c.Prepare(p) == ProfilePersistenceDecision.BlockStaleProfileHash, "24 fail");
                Assert(c.RequestPersistence() == ProfilePersistenceDecision.BlockInvalidTransition,
                    "24 no persist request");
                Assert(!c.PersistenceMayHaveBeenAttempted && !c.RuntimeMutationMayHaveOccurred,
                    "24 zero effects");
            });
            Case("25 healthy normal XML save independent", delegate
            {
                Assert(ProfileApplyPersistenceContract.NormalSettingsSavePolicy(false),
                    "25 existing healthy write path permitted");
            });
            Case("26 runtime lock blocks normal XML", delegate
            {
                Assert(!ProfileApplyPersistenceContract.NormalSettingsSavePolicy(true),
                    "26 preserve B2B suppression");
            });
            Case("27 synthetic failures cannot latch live", delegate
            {
                RuntimeRecoverySessionGate fake = RuntimeRecoverySessionGate.CreateIsolatedForAudit();
                fake.ReportVerificationFailureForIsolatedAudit(RuntimeRecoveryFailureStage.Biotech,
                    RuntimeRecoveryGateSource.LiveDiagnostic);
                Assert(fake.IsBlockedForIsolatedAudit, "27 fake latch");
                Assert(RuntimeRecoverySessionGate.LiveIsBlocked == liveRuntimeLock,
                    "27 live latch unchanged");
            });
            Case("28 pure contract cannot touch user's files", delegate
            {
                ProfileApplyPersistenceContract c = PersistRequested(Evidence());
                Assert(c.PersistenceMayHaveBeenAttempted && !c.DiskVerified,
                    "28 request is policy marker only");
                Assert(object.ReferenceEquals(liveRoot, XpCustomizerMod.Settings),
                    "28 actual settings pointer unchanged");
            });
            Case("29 fake disk failure never completes", delegate
            {
                ProfileApplyPersistenceContract c = PersistRequested(Evidence());
                Assert(c.ObservePersistence(ProfilePersistenceDiskOutcome.FailedOrUnknown) ==
                    ProfilePersistenceDecision.BlockPersistenceUncertain, "29 unknown disk");
                Assert(!c.Completed && !c.DiskVerified, "29 no success");
            });
            Case("30 disk result before runtime is impossible", delegate
            {
                ProfileApplyPersistenceContract c = Rechecked(Evidence());
                Assert(c.ObservePersistence(ProfilePersistenceDiskOutcome.WrittenAndVerified) ==
                    ProfilePersistenceDecision.BlockInvalidTransition, "30 wrong order");
                Assert(!c.Completed && !c.DiskVerified && !c.PersistenceMayHaveBeenAttempted,
                    "30 prevented phantom commit");
            });
            Case("31 verified runtime rollback no XML", delegate
            {
                ProfileApplyPersistenceContract c = Rechecked(Evidence());
                Assert(c.ObserveRuntime(ProfilePersistenceRuntimeOutcome.RolledBackAndVerified) ==
                    ProfilePersistenceDecision.RuntimeRollbackVerified, "31 recovered");
                Assert(c.Phase == ProfilePersistencePhase.Compensated && !c.Completed &&
                    !c.PersistenceMayHaveBeenAttempted, "31 rollback not success");
            });
            Case("32 unknown disk sticky after rollback", delegate
            {
                ProfileApplyPersistenceContract c = PersistRequested(Evidence());
                c.ObservePersistence(ProfilePersistenceDiskOutcome.FailedOrUnknown);
                Assert(c.ObserveCompensation(true) == ProfilePersistenceDecision.BlockPersistenceUncertain,
                    "32 disk uncertainty retained");
                Assert(c.Phase == ProfilePersistencePhase.PersistenceUncertain && !c.Completed &&
                    c.DiskStateUncertain, "32 not complete; XML still uncertain");
            });
            Case("33 denied consent one shot", delegate
            {
                ProfileApplyPersistenceContract c = new ProfileApplyPersistenceContract();
                c.Prepare(Evidence()); c.Confirm(false);
                Assert(c.Confirm(true) == ProfilePersistenceDecision.BlockAlreadyConsumed,
                    "33 cannot revive cancelled consent");
                Assert(c.Phase == ProfilePersistencePhase.Cancelled, "33 remains cancelled");
            });
            ExpectInitial("34 invalid metadata", p => p.DisplayName = "",
                ProfilePersistenceDecision.BlockInvalidCandidate);
            Case("35 fake complete requires BOTH proofs", delegate
            {
                ProfileApplyPersistenceContract c = PersistRequested(Evidence());
                Assert(c.ObservePersistence(ProfilePersistenceDiskOutcome.WrittenAndVerified) ==
                    ProfilePersistenceDecision.AllowProposed, "35 fake disk proof");
                Assert(c.Finish() == ProfilePersistenceDecision.Complete, "35 fake completed");
                Assert(c.Completed && c.RuntimeVerified && c.DiskVerified && !c.DiskStateUncertain,
                    "35 both verified");
            });
            Case("36 double XML request denied", delegate
            {
                ProfileApplyPersistenceContract c = PersistRequested(Evidence());
                Assert(c.RequestPersistence() == ProfilePersistenceDecision.BlockInvalidTransition,
                    "36 second request blocked");
                Assert(!c.Completed, "36 no complete");
            });
            Case("37 failed known unchanged + rollback", delegate
            {
                ProfileApplyPersistenceContract c = PersistRequested(Evidence());
                Assert(c.ObservePersistence(ProfilePersistenceDiskOutcome.FailedKnownUnchanged) ==
                    ProfilePersistenceDecision.PersistenceFailedKnownUnchanged, "37 I/O failed");
                Assert(c.ObserveCompensation(true) == ProfilePersistenceDecision.RuntimeRollbackVerified,
                    "37 compensation verified");
                Assert(c.Phase == ProfilePersistencePhase.Compensated && !c.Completed,
                    "37 safely aborted is not commit");
            });
            Case("38 failed compensation", delegate
            {
                ProfileApplyPersistenceContract c = PersistRequested(Evidence());
                c.ObservePersistence(ProfilePersistenceDiskOutcome.FailedKnownUnchanged);
                Assert(c.ObserveCompensation(false) == ProfilePersistenceDecision.RuntimeUnrecovered,
                    "38 runtime unrecovered");
                Assert(c.Phase == ProfilePersistencePhase.RuntimeUnrecovered && !c.Completed,
                    "38 do not claim recovery");
            });
            Case("39 runtime unverified", delegate
            {
                ProfileApplyPersistenceContract c = Rechecked(Evidence());
                Assert(c.ObserveRuntime(ProfilePersistenceRuntimeOutcome.AppliedButUnverified) ==
                    ProfilePersistenceDecision.RuntimeUnrecovered, "39 unverified");
                Assert(!c.PersistenceMayHaveBeenAttempted && !c.Completed, "39 no XML write");
            });
            Case("40 unknown disk + failed compensation", delegate
            {
                ProfileApplyPersistenceContract c = PersistRequested(Evidence());
                c.ObservePersistence(ProfilePersistenceDiskOutcome.FailedOrUnknown);
                Assert(c.ObserveCompensation(false) == ProfilePersistenceDecision.RuntimeUnrecovered,
                    "40 runtime emergency");
                Assert(c.FirstFailure == ProfilePersistenceDecision.BlockPersistenceUncertain &&
                    c.DiskStateUncertain, "40 first disk failure + uncertainty preserved");
            });
            Case("41 source SHA changed during review", delegate
            {
                ProfilePersistenceEvidence p = Evidence();
                ProfileApplyPersistenceContract c = new ProfileApplyPersistenceContract();
                c.Prepare(p); c.Confirm(true);
                ProfilePersistenceEvidence changed = p.Copy(); changed.SelectedSha256 = HashB;
                Assert(c.Recheck(changed) == ProfilePersistenceDecision.BlockStaleProfileHash,
                    "41 snapshot hash frozen");
            });
            Case("42 live fingerprint changed during review", delegate
            {
                ProfilePersistenceEvidence p = Evidence();
                ProfileApplyPersistenceContract c = new ProfileApplyPersistenceContract();
                c.Prepare(p); c.Confirm(true);
                ProfilePersistenceEvidence changed = p.Copy(); changed.StrictBaselineFingerprint = HashA;
                Assert(c.Recheck(changed) == ProfilePersistenceDecision.BlockStaleLiveSettings,
                    "42 strict values frozen");
            });
            Case("43 candidate fingerprint changed during review", delegate
            {
                ProfilePersistenceEvidence p = Evidence();
                ProfileApplyPersistenceContract c = new ProfileApplyPersistenceContract();
                c.Prepare(p); c.Confirm(true);
                ProfilePersistenceEvidence changed = p.Copy(); changed.DetachedCandidateFingerprint = HashA;
                Assert(c.Recheck(changed) == ProfilePersistenceDecision.BlockStaleLiveSettings,
                    "43 candidate immutable");
            });
            Case("44 metadata changed during review", delegate
            {
                ProfilePersistenceEvidence p = Evidence();
                ProfileApplyPersistenceContract c = new ProfileApplyPersistenceContract();
                c.Prepare(p); c.Confirm(true);
                ProfilePersistenceEvidence changed = p.Copy(); changed.DisplayName = "changed name";
                Assert(c.Recheck(changed) == ProfilePersistenceDecision.BlockStaleProfileMetadata,
                    "44 metadata frozen");
            });
            Case("45 unsupported final schema", delegate
            {
                ProfilePersistenceEvidence p = Evidence();
                ProfileApplyPersistenceContract c = new ProfileApplyPersistenceContract();
                c.Prepare(p); c.Confirm(true);
                ProfilePersistenceEvidence changed = p.Copy(); changed.SettingsSchemaVersion = 9;
                Assert(c.Recheck(changed) == ProfilePersistenceDecision.BlockUnsupportedSchema,
                    "45 reject future schema");
            });
            Case("46 source ID changed during review", delegate
            {
                ProfilePersistenceEvidence p = Evidence();
                ProfileApplyPersistenceContract c = new ProfileApplyPersistenceContract();
                c.Prepare(p); c.Confirm(true);
                ProfilePersistenceEvidence changed = p.Copy(); changed.ProfileId = IdB;
                Assert(c.Recheck(changed) == ProfilePersistenceDecision.BlockStaleProfileHash,
                    "46 foreign ID denied");
            });
            Case("47 complete cannot be completed twice", delegate
            {
                ProfileApplyPersistenceContract c = PersistRequested(Evidence());
                c.ObservePersistence(ProfilePersistenceDiskOutcome.WrittenAndVerified);
                c.Finish();
                Assert(c.Finish() == ProfilePersistenceDecision.BlockInvalidTransition,
                    "47 duplicate completion blocked");
                Assert(c.Completed, "47 already completed remains complete");
            });
            Case("48 wrong phase cannot be resumed", delegate
            {
                ProfileApplyPersistenceContract c = new ProfileApplyPersistenceContract();
                Assert(c.Confirm(true) == ProfilePersistenceDecision.BlockInvalidTransition,
                    "48 confirm without prepare");
                Assert(c.Prepare(Evidence()) == ProfilePersistenceDecision.BlockInvalidTransition,
                    "48 fail closed after misuse");
            });
            Case("49 reject before runtime has no side effects", delegate
            {
                ProfileApplyPersistenceContract c = Rechecked(Evidence());
                Assert(c.ObserveRuntime(ProfilePersistenceRuntimeOutcome.RejectedWithoutWrites) ==
                    ProfilePersistenceDecision.BlockInvalidCandidate, "49 rejected");
                Assert(!c.RuntimeMutationMayHaveOccurred && !c.PersistenceMayHaveBeenAttempted,
                    "49 zero writes");
            });
            Case("50 source cannot mutate frozen evidence", delegate
            {
                ProfilePersistenceEvidence p = Evidence();
                ProfileApplyPersistenceContract c = new ProfileApplyPersistenceContract();
                c.Prepare(p); c.Confirm(true);
                p.DisplayName = "mutated in place";
                Assert(c.Recheck(p) == ProfilePersistenceDecision.BlockStaleProfileMetadata,
                    "50 stored copy unaffected");
            });
            Case("51 XML verified without Finish not complete", delegate
            {
                ProfileApplyPersistenceContract c = PersistRequested(Evidence());
                c.ObservePersistence(ProfilePersistenceDiskOutcome.WrittenAndVerified);
                Assert(c.Phase == ProfilePersistencePhase.PersistVerified && !c.Completed,
                    "51 requires explicit final transition");
            });
            Case("52 in-flight writer is uncertain", delegate
            {
                ProfileApplyPersistenceContract c = PersistRequested(Evidence());
                Assert(c.Phase == ProfilePersistencePhase.PersistAttempt &&
                    c.DiskStateUncertain && !c.DiskVerified && !c.Completed,
                    "52 pending real verification");
            });
            Case("53 real gates and settings remain untouched", delegate
            {
                Assert(object.ReferenceEquals(liveRoot, XpCustomizerMod.Settings), "53 settings identity");
                Assert(RuntimeRecoverySessionGate.LiveIsBlocked == liveRuntimeLock, "53 runtime gate");
                Assert(ProfileWriteSessionGate.IsBlocked == profileWriteLock, "53 profile gate");
                Assert(SettingsRuntimeApplyCoordinator.LiveApplyStillBlockedForAudit, "53 live apply OFF");
            });

            Log.Message("[XPC-R6P1D2C1][PROFILE_PERSISTENCE_CONTRACT_AUDIT] " +
                "status=PASS; revision=" + ProfileApplyPersistenceContract.Revision +
                "; scenarios=" + scenarios + "; checks=" + checks +
                "; scope=ISOLATED_PURE; runtimeWrites=0; geneDefWrites=0; " +
                "scribeCalls=0; profileJsonWrites=0; liveApply=BLOCKED; " +
                "existingWriteSettings=UNCHANGED; fakeDiskResult=NOT_REAL_XML_PROOF; " +
                "liveGateUnaffected=True; profileStorageGateUnaffected=True; " +
                "build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
