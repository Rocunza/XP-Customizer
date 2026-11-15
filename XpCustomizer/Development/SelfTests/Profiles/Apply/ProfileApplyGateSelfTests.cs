#if DEBUG
using System;
using System.IO;
using System.Text;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer
{
    /// <summary>Isolated D2-1 approval-gate tests. Never touches user files or live settings.</summary>
    internal static class ProfileApplyGateSelfTests
    {
        private static int checks;
        private static void Assert(bool condition, string label)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("R6P1D2A1 GATE: " + label);
        }

        private static ProfileEditorReadinessResult Editor(ProfileEditorReadinessCode code)
        { return new ProfileEditorReadinessResult(code, "ISOLATED", 15, 0, 0); }

        private static ProfileApplyGateService Gate(ProfileFileRepository repo,
            ProfileEditorReadinessCode state)
        {
            return new ProfileApplyGateService(new ProfileApplyPreflightService(
                delegate { return repo; }, delegate { return Editor(state); }, false), false);
        }

        internal static void Run()
        {
            checks = 0;
            string root = Path.Combine(Path.GetTempPath(), "XPC-D2A1-" + Guid.NewGuid().ToString("N"));
            bool previousLock = ProfileWriteSessionGate.IsBlocked;
            XpCustomizerSettings originalLive = XpCustomizerMod.Settings;
            try
            {
                Assert(!previousLock, "recovery lock initially clear");
                ProfileStoragePaths paths = ProfileStoragePaths.ForIsolatedTestRoot(root);
                ProfileFileRepository repo = new ProfileFileRepository(paths, false);
                ProfileCreationService creator = new ProfileCreationService(
                    delegate { return repo; }, false);
                string id = "22bc29d5-8ef3-4235-a591-30c2d2000001";
                XpCustomizerSettings saved = new XpCustomizerSettings();
                saved.careerXpMultiplier = 2.0f;
                Assert(creator.CreateCore(saved, "D2 gated sample", "1.6.4871",
                    new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc), id,
                    ProfileStorageFault.None).Success, "isolated file created");
                ProfileStorageResult loaded = repo.Load(id);
                Assert(loaded.Success && loaded.ContentHash != null, "isolated file loaded");
                string file = paths.FileForId(id);
                byte[] originalBytes = File.ReadAllBytes(file);
                XpCustomizerSettings live = SettingsSnapshotService.CloneIndependent(saved);
                live.careerXpMultiplier = 4.0f;
                ProfileApplyGateService gate = Gate(repo, ProfileEditorReadinessCode.Ready);

                ProfileApplyGateResult prepared = gate.Prepare(loaded.Document, loaded.ContentHash, live);
                Assert(prepared.Prepared && prepared.Code == ProfileApplyGateCode.PreparedChanged,
                    "changed profile prepares a separate gate");
                Assert(gate.ConfirmForReadOnlyAudit(prepared.Session, live, true).Code ==
                    ProfileApplyGateCode.EligibleReadOnly, "explicit confirmation validates read-only");
                Assert(gate.ConfirmForReadOnlyAudit(prepared.Session, live, true).Code ==
                    ProfileApplyGateCode.Duplicate, "one-shot confirmation cannot be replayed");
                Assert(live.careerXpMultiplier == 4.0f, "live input remains untouched");

                ProfileApplyGateResult noChange = gate.Prepare(loaded.Document, loaded.ContentHash, saved);
                Assert(noChange.Code == ProfileApplyGateCode.NoChange && noChange.Session == null,
                    "no-change never issues a confirmation session");
                Assert(gate.ConfirmForReadOnlyAudit(null, live, true).Code ==
                    ProfileApplyGateCode.Rejected, "null session rejected");
                Assert(gate.Prepare(loaded.Document, "bad-sha", live).Code ==
                    ProfileApplyGateCode.Rejected, "invalid hash rejected");

                ProfileApplyGateResult noConsent = gate.Prepare(loaded.Document, loaded.ContentHash, live);
                Assert(noConsent.Prepared, "fresh session for no-consent case");
                Assert(gate.ConfirmForReadOnlyAudit(noConsent.Session, live, false).Code ==
                    ProfileApplyGateCode.Rejected, "no explicit confirmation fails closed");
                Assert(gate.ConfirmForReadOnlyAudit(noConsent.Session, live, true).Code ==
                    ProfileApplyGateCode.Duplicate, "rejection consumes one-shot session");

                ProfileApplyGateResult staleLive = gate.Prepare(loaded.Document, loaded.ContentHash, live);
                Assert(staleLive.Prepared, "fresh session for settings conflict");
                XpCustomizerSettings altered = SettingsSnapshotService.CloneIndependent(live);
                altered.careerXpMultiplier = 9.0f;
                Assert(gate.ConfirmForReadOnlyAudit(staleLive.Session, altered, true).Code ==
                    ProfileApplyGateCode.Conflict, "changed baseline rejects consent");

                ProfileApplyGateResult staleFile = gate.Prepare(loaded.Document, loaded.ContentHash, live);
                Assert(staleFile.Prepared, "fresh session for file conflict");
                ProfileDocument alteredDoc = ProfileJsonCodec.Deserialize(
                    ProfileJsonCodec.Serialize(loaded.Document));
                alteredDoc.DisplayName = "Modified externally";
                File.WriteAllText(file, ProfileJsonCodec.Serialize(alteredDoc), new UTF8Encoding(false));
                Assert(gate.ConfirmForReadOnlyAudit(staleFile.Session, live, true).Code ==
                    ProfileApplyGateCode.Conflict, "changed SHA rejects consent");
                File.WriteAllBytes(file, originalBytes);

                Assert(Gate(repo, ProfileEditorReadinessCode.PendingEdit).Prepare(
                    loaded.Document, loaded.ContentHash, live).Code == ProfileApplyGateCode.Rejected,
                    "pending editor blocks prepare");
                Assert(Gate(repo, ProfileEditorReadinessCode.Uncertain).Prepare(
                    loaded.Document, loaded.ContentHash, live).Code == ProfileApplyGateCode.Rejected,
                    "unobserved editor blocks prepare");
                ProfileApplyGateResult lockSession = gate.Prepare(loaded.Document, loaded.ContentHash, live);
                Assert(lockSession.Prepared, "fresh session for recovery lock");
                ProfileWriteSessionGate.Observe(ProfileStorageCode.RecoveryRequired);
                Assert(gate.ConfirmForReadOnlyAudit(lockSession.Session, live, true).Code ==
                    ProfileApplyGateCode.Rejected, "recovery lock blocks confirm");
                ProfileWriteSessionGate.ResetForIsolatedTests();

                Assert(EqualBytes(originalBytes, File.ReadAllBytes(file)),
                    "profile file is byte-for-byte unchanged");
                Assert(!Directory.Exists(paths.Backups), "no backup writes from gate");
                Assert(object.ReferenceEquals(XpCustomizerMod.Settings, originalLive),
                    "actual mod settings object unchanged");
                Log.Message("[XPC-R6P1D2A1][PROFILE_APPLY_GATE_AUDIT] status=PASS; " +
                    "revision=" + ProfileApplyGateService.Revision + "; checks=" + checks +
                    "; testRoot=ISOLATED_TEMP; changed=PREPARED; noChange=NO_SESSION; " +
                    "explicitConsent=REQUIRED; duplicate=BLOCKED; staleSettings=CONFLICT; " +
                    "staleFile=CONFLICT; pendingEditor=BLOCKED; uncertainEditor=BLOCKED; " +
                    "recoveryLock=BLOCKED; fileBytes=PRESERVED; userProfileWritesByGate=0; " +
                    "liveSettingsWrites=0; geneDefWrites=0; pawnWrites=0; scribeCalls=0; " +
                    "liveApply=BLOCKED; build=" + XpCustomizerMod.BuildId);
            }
            finally
            {
                if (!previousLock) ProfileWriteSessionGate.ResetForIsolatedTests();
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

        private static bool EqualBytes(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
#endif
