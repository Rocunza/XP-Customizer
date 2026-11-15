#if DEBUG
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XpCustomizer.Profiles;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// D2-C4-B3-A: OS-temp-only proof of a file replacement boundary.
    /// Consumes bytes ALREADY saved, raw-validated and reloaded by B2-C3.
    /// NEVER locates or writes any real Config/Profile/save or mod file.
    /// Does not change vanilla WriteSettings, install Harmony or call Scribe.
    /// </summary>
    internal static class DetachedTempFileTransactionAudit
    {
        internal const string Revision = "R6P1D2C4B3A-TEMP-COMMIT-EVIDENCE-20261107";
        internal const string Tag = "[XPC-R6P1D2C4B3A][TEMP_COMMIT_AUDIT]";
        private const string FixtureXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><SettingsBlock><ModSettings Class=\"XpCustomizer.XpCustomizerSettings\"/></SettingsBlock>";
        private static readonly byte[] OriginalBytes = Encoding.UTF8.GetBytes(FixtureXml);
        private const string TargetName = "baseline_target.xml";
        private const string ProposedName = "verified_proposed.xml";
        private const string RetainedName = "retained_original.xml";
        private const string ReplaceBackupName = "os_replace_backup.xml";

        internal sealed class Report
        {
            internal string Status = "REJECTED";
            internal string FirstFailure = "NONE";
            internal string PrepareResult = "NOT_RUN";
            internal string StaleResult = "NOT_RUN";
            internal string ReplaceResult = "NOT_RUN";
            internal string ReadbackResult = "NOT_RUN";
            internal string OriginalSha = "NONE";
            internal string ProposedSha = "NONE";
            internal string BackupSha = "NONE";
            internal string ReadbackSha = "NONE";
            internal string PolicyPhase = "NOT_RUN";
            internal string PolicyFirstFailure = "NONE";
            internal bool BackupRetained;
            internal bool BackupShaMatchesOriginal;
            internal bool ExpectedTargetShaMatches;
            internal bool WriterReturned;
            internal bool CommittedShaMatchesProposed;
            internal bool CommittedXmlValid;
            internal bool ExistingBackupUnmodified;
            internal bool StaleTargetBlocked;
            internal bool BackupDamageBlocked;
            internal bool BeforeBackupBlocked;
            internal bool PreReplaceFailureBlocked;
            internal bool InjectedUnknownClassified;
            internal bool InjectedBadReadbackClassified;
            internal bool TempCleaned;
            internal bool SandboxCreated;
            internal bool FailureClassificationsFailClosed;
        }

        internal static string Sha(byte[] bytes)
        {
            if (bytes == null) return "NONE";
            using (SHA256 sha = SHA256.Create())
            {
                StringBuilder text = new StringBuilder(64);
                foreach (byte b in sha.ComputeHash(bytes))
                    text.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }

        // Windows paths are case-insensitive; Unix paths must NOT be folded.
        // No culture-specific comparison may decide a filesystem boundary.
        private static readonly StringComparison PathComparison =
            Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase :
            StringComparison.Ordinal;

        // Normalize both operands identically. Preserve filesystem roots:
        // '/' / 'C:\\' / '\\\\server\\share\\' may not be trimmed to an empty
        // path, drive label or UNC server. No filesystem access or mutation.
        private static bool TryCanonicalPath(string path, out string canonical)
        {
            canonical = null;
            if (String.IsNullOrWhiteSpace(path)) return false;
            try
            {
                string full = Path.GetFullPath(path);
                string root = Path.GetPathRoot(full);
                if (String.IsNullOrEmpty(root)) return false;
                int end = full.Length;
                while (end > root.Length &&
                    (full[end - 1] == Path.DirectorySeparatorChar ||
                     full[end - 1] == Path.AltDirectorySeparatorChar))
                    end--;
                canonical = full.Substring(0, end);
                return true;
            }
            catch (Exception) { return false; }
        }

        // Only internally generated fixed filenames and GUID children are used.
        // This is a path safety gate, not a proof against external TOCTOU/races.
        internal static bool IsDirectChild(string child, string parent)
        {
            string fullParent, fullChild;
            if (!TryCanonicalPath(parent, out fullParent) ||
                !TryCanonicalPath(child, out fullChild)) return false;
            if (String.Equals(fullChild, fullParent, PathComparison)) return false;
            try
            {
                string directParent = Path.GetDirectoryName(fullChild);
                string canonicalDirectParent;
                return TryCanonicalPath(directParent, out canonicalDirectParent) &&
                    String.Equals(canonicalDirectParent, fullParent, PathComparison);
            }
            catch (Exception) { return false; }
        }

        private static bool SameOrDescendant(string child, string root)
        {
            string full, basePath;
            // This method is a *rejection* predicate for overlaps. If input
            // cannot be canonicalized, conservatively report overlap.
            if (!TryCanonicalPath(child, out full) ||
                !TryCanonicalPath(root, out basePath)) return true;
            if (String.Equals(full, basePath, PathComparison)) return true;
            string prefix = basePath.EndsWith(Path.DirectorySeparatorChar.ToString(),
                StringComparison.Ordinal) ? basePath :
                basePath + Path.DirectorySeparatorChar;
            return full.StartsWith(prefix, PathComparison);
        }

        private static string RequireChild(string parent, string filename)
        {
            if (String.IsNullOrEmpty(filename) || filename.IndexOfAny(new char[] {
                '/', '\\', ':', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar
            }) >= 0 || filename == "." || filename == "..")
                throw new InvalidOperationException("BAD_FIXTURE_NAME");
            string child = Path.GetFullPath(Path.Combine(parent, filename));
            if (!IsDirectChild(child, parent))
                throw new InvalidOperationException("FIXTURE_ESCAPE");
            return child;
        }

        private static void CheckNoLink(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("REPARSE_POINT_BLOCKED");
        }

        private static byte[] ReadOwn(string path)
        {
            CheckNoLink(path);
            FileInfo info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0 || info.Length > DetachedXmlStageAudit.MaximumBytes)
                throw new InvalidOperationException("FIXTURE_SIZE_INVALID");
            // Read using exclusive sharing when available. It is NOT an OS-wide
            // atomic compare-and-replace and cannot prevent a future race.
            using (FileStream reader = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.None))
            {
                byte[] bytes = new byte[checked((int)info.Length)];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int n = reader.Read(bytes, offset, bytes.Length - offset);
                    if (n == 0) throw new EndOfStreamException("SHORT_FIXTURE_READ");
                    offset += n;
                }
                if (reader.ReadByte() != -1)
                    throw new InvalidOperationException("FIXTURE_GREW_DURING_READ");
                return bytes;
            }
        }

        private static void NewFile(string path, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 ||
                bytes.Length > DetachedXmlStageAudit.MaximumBytes || File.Exists(path))
                throw new InvalidOperationException("BAD_NEW_FIXTURE");
            using (FileStream writer = new FileStream(path, FileMode.CreateNew,
                FileAccess.Write, FileShare.None))
            {
                writer.Write(bytes, 0, bytes.Length);
                writer.Flush(true);
            }
        }

        private static string ScenarioFolder(string sandbox, string name)
        {
            string path = RequireChild(sandbox, name);
            if (Directory.Exists(path) || File.Exists(path))
                throw new InvalidOperationException("SCENARIO_COLLISION");
            Directory.CreateDirectory(path);
            CheckNoLink(path);
            return path;
        }

        // These short failure checks run on independent, disposable temp fixtures.
        // They DO NOT induce engine/Scribe errors or a real File.Replace fault.
        private static void CheckSafePreCommitFailures(string sandbox, Report r)
        {
            string a = ScenarioFolder(sandbox, "before_backup");
            string at = RequireChild(a, TargetName);
            NewFile(at, OriginalBytes);
            string baselineSha = Sha(ReadOwn(at));
            // Inject a stop before any backup/replace call.
            r.BeforeBackupBlocked = Sha(ReadOwn(at)) == baselineSha &&
                !File.Exists(RequireChild(a, ProposedName));

            string b = ScenarioFolder(sandbox, "backup_damage");
            string bt = RequireChild(b, TargetName);
            string bb = RequireChild(b, RetainedName);
            NewFile(bt, OriginalBytes);
            File.Copy(bt, bb, false);
            // Simulate a corrupt retained backup, then reject before replace.
            File.WriteAllBytes(bb, Encoding.UTF8.GetBytes("CORRUPTED_PRIVATE_BACKUP"));
            r.BackupDamageBlocked = Sha(ReadOwn(bt)) == baselineSha &&
                Sha(ReadOwn(bb)) != baselineSha &&
                !File.Exists(RequireChild(b, ProposedName));

            string c = ScenarioFolder(sandbox, "stale_target");
            string ct = RequireChild(c, TargetName);
            string cb = RequireChild(c, RetainedName);
            NewFile(ct, OriginalBytes);
            File.Copy(ct, cb, false);
            ProfileXmlPersistenceEvidencePolicy stale =
                new ProfileXmlPersistenceEvidencePolicy();
            bool prepared = stale.Prepare(Sha(ReadOwn(ct)), Sha(ReadOwn(cb)),
                r.ProposedSha, true) == ProfileXmlEvidenceResult.Prepared;
            byte[] externalFixture = Encoding.UTF8.GetBytes(
                "<SettingsBlock><ModSettings Class=\"external-temp-change\"/></SettingsBlock>");
            File.WriteAllBytes(ct, externalFixture);
            string changedSha = Sha(ReadOwn(ct));
            r.StaleTargetBlocked = prepared &&
                stale.BeginWrite(changedSha) == ProfileXmlEvidenceResult.BlockedChangedTarget &&
                stale.Phase == ProfileXmlEvidencePhase.Rejected &&
                Sha(ReadOwn(ct)) == changedSha && changedSha != baselineSha;

            string d = ScenarioFolder(sandbox, "before_replace");
            string dt = RequireChild(d, TargetName);
            string db = RequireChild(d, RetainedName);
            NewFile(dt, OriginalBytes);
            File.Copy(dt, db, false);
            ProfileXmlPersistenceEvidencePolicy before =
                new ProfileXmlPersistenceEvidencePolicy();
            bool preparedD = before.Prepare(Sha(ReadOwn(dt)), Sha(ReadOwn(db)),
                r.ProposedSha, true) == ProfileXmlEvidenceResult.Prepared;
            bool beganD = preparedD && before.BeginWrite(Sha(ReadOwn(dt))) ==
                ProfileXmlEvidenceResult.WriteAllowed;
            // Known not-opened failure: no File.Replace invocation occurs here.
            r.PreReplaceFailureBlocked = beganD &&
                before.ObserveWriteFailure(true, Sha(ReadOwn(dt))) ==
                ProfileXmlEvidenceResult.FailedKnownUnchanged &&
                before.Phase == ProfileXmlEvidencePhase.FailedKnownUnchanged &&
                Sha(ReadOwn(dt)) == baselineSha;

            // Synthetic integration checks of the EXISTING D2-C2 policy.
            // These do not claim an actual OS call failed.
            ProfileXmlPersistenceEvidencePolicy unknown =
                new ProfileXmlPersistenceEvidencePolicy();
            bool up = unknown.Prepare(baselineSha, baselineSha, r.ProposedSha, true) ==
                ProfileXmlEvidenceResult.Prepared;
            bool ub = up && unknown.BeginWrite(baselineSha) ==
                ProfileXmlEvidenceResult.WriteAllowed;
            r.InjectedUnknownClassified = ub &&
                unknown.ObserveWriteFailure(false, baselineSha) ==
                ProfileXmlEvidenceResult.PersistenceUncertain &&
                unknown.ObserveRestore(true, true, baselineSha) ==
                ProfileXmlEvidenceResult.RestoreVerifiedButUncertain &&
                unknown.UncertaintyEverObserved &&
                unknown.Phase == ProfileXmlEvidencePhase.RestoredButUncertain;

            ProfileXmlPersistenceEvidencePolicy badReadback =
                new ProfileXmlPersistenceEvidencePolicy();
            bool rp = badReadback.Prepare(baselineSha, baselineSha, r.ProposedSha, true) ==
                ProfileXmlEvidenceResult.Prepared;
            bool rb = rp && badReadback.BeginWrite(baselineSha) ==
                ProfileXmlEvidenceResult.WriteAllowed;
            r.InjectedBadReadbackClassified = rb &&
                badReadback.ObserveWriterReturn(true, baselineSha) ==
                ProfileXmlEvidenceResult.PersistenceUncertain &&
                badReadback.UncertaintyEverObserved;

            r.FailureClassificationsFailClosed = r.BeforeBackupBlocked &&
                r.BackupDamageBlocked && r.StaleTargetBlocked &&
                r.PreReplaceFailureBlocked && r.InjectedUnknownClassified &&
                r.InjectedBadReadbackClassified;
        }

        private static void Fail(Report result, string status, string reason)
        {
            result.Status = status;
            if (result.FirstFailure == "NONE") result.FirstFailure = reason;
        }

        internal static Report Execute(byte[] proposedBytes, string stageSha,
            XpCustomizerSettings expected)
        {
            Report r = new Report();
            string sandbox = null;
            try
            {
                r.ProposedSha = Sha(proposedBytes);
                r.OriginalSha = Sha(OriginalBytes);
                if (expected == null || proposedBytes == null ||
                    proposedBytes.Length == 0 ||
                    proposedBytes.Length > DetachedXmlStageAudit.MaximumBytes ||
                    !String.Equals(stageSha, r.ProposedSha, StringComparison.Ordinal) ||
                    !DetachedXmlStageAudit.InspectXml(proposedBytes, expected).Valid ||
                    !DetachedXmlSemanticContract.Inspect(proposedBytes, expected).Valid ||
                    String.Equals(r.ProposedSha, r.OriginalSha, StringComparison.Ordinal))
                {
                    Fail(r, "REJECTED", "VERIFIED_BYTES_HANDOFF_INVALID");
                    return r;
                }

                string temp = Path.GetFullPath(Path.GetTempPath());
                string config = Path.GetFullPath(GenFilePaths.ConfigFolderPath);
                string saveRoot = Path.GetFullPath(GenFilePaths.SaveDataFolderPath);
                if (SameOrDescendant(temp, config) ||
                    SameOrDescendant(temp, saveRoot) ||
                    SameOrDescendant(config, temp) ||
                    SameOrDescendant(saveRoot, temp))
                {
                    Fail(r, "REJECTED", "TEMP_AND_GAME_STORAGE_OVERLAP");
                    return r;
                }
                CheckNoLink(temp);
                sandbox = RequireChild(temp, "XPC_B3A_" + Guid.NewGuid().ToString("N"));
                if (Directory.Exists(sandbox) || File.Exists(sandbox))
                {
                    Fail(r, "REJECTED", "TEMP_DIRECTORY_COLLISION");
                    return r;
                }
                Directory.CreateDirectory(sandbox);
                r.SandboxCreated = true;
                CheckNoLink(sandbox);
                string test = ScenarioFolder(sandbox, "commit_success");
                string target = RequireChild(test, TargetName);
                string retained = RequireChild(test, RetainedName);
                string pending = RequireChild(test, ProposedName);
                string replaceBackup = RequireChild(test, ReplaceBackupName);
                if (Path.GetPathRoot(target) != Path.GetPathRoot(pending) ||
                    !IsDirectChild(target, test) || !IsDirectChild(pending, test) ||
                    !IsDirectChild(retained, test) || !IsDirectChild(replaceBackup, test))
                    throw new InvalidOperationException("NOT_SAME_PRIVATE_VOLUME");
                NewFile(target, OriginalBytes);
                File.Copy(target, retained, false);
                CheckNoLink(retained);
                r.BackupSha = Sha(ReadOwn(retained));
                r.BackupRetained = File.Exists(retained);
                r.BackupShaMatchesOriginal = r.BackupRetained &&
                    String.Equals(r.BackupSha, r.OriginalSha, StringComparison.Ordinal);
                if (!r.BackupShaMatchesOriginal)
                {
                    Fail(r, "REJECTED", "BACKUP_READBACK_MISMATCH");
                    return r;
                }
                ProfileXmlPersistenceEvidencePolicy policy =
                    new ProfileXmlPersistenceEvidencePolicy();
                r.PrepareResult = policy.Prepare(Sha(ReadOwn(target)), r.BackupSha,
                    r.ProposedSha, true).ToString();
                r.PolicyPhase = policy.Phase.ToString();
                if (r.PrepareResult != ProfileXmlEvidenceResult.Prepared.ToString())
                {
                    Fail(r, "REJECTED", "POLICY_PREPARE_REJECTED");
                    return r;
                }
                // A separate new proposed file. Never use the existing Scribe
                // stage file as a File.Replace source (the loader used it).
                NewFile(pending, proposedBytes);
                if (Sha(ReadOwn(pending)) != r.ProposedSha)
                {
                    Fail(r, "REJECTED", "PROPOSAL_READBACK_INVALID");
                    return r;
                }

                CheckSafePreCommitFailures(sandbox, r);
                if (!r.FailureClassificationsFailClosed)
                {
                    Fail(r, "REJECTED", "FAILURE_CLASSIFICATION_TEST_FAILED");
                    return r;
                }
                // Re-verify the proposed bytes and retained backup as late as
                // possible; never let a changed stage/back-up reach File.Replace.
                if (Sha(ReadOwn(pending)) != r.ProposedSha ||
                    Sha(ReadOwn(retained)) != r.OriginalSha)
                {
                    Fail(r, "REJECTED", "STAGED_OR_RETAINED_BYTES_CHANGED");
                    return r;
                }
                r.StaleResult = policy.BeginWrite(Sha(ReadOwn(target))).ToString();
                r.ExpectedTargetShaMatches =
                    r.StaleResult == ProfileXmlEvidenceResult.WriteAllowed.ToString();
                r.PolicyPhase = policy.Phase.ToString();
                if (!r.ExpectedTargetShaMatches)
                {
                    Fail(r, "BLOCKED_STALE_TARGET", "TARGET_SHA_CHANGED_BEFORE_WRITE");
                    return r;
                }
                // A background process could still race after this last read.
                // This probe makes NO claim of a race-free real-world commit.
                try
                {
                    File.Replace(pending, target, replaceBackup);
                    r.WriterReturned = true;
                    r.ReplaceResult = "RETURNED";
                }
                catch (PlatformNotSupportedException)
                {
                    // AFTER BeginWrite the OS may already have touched target.
                    // Only report UNSUPPORTED if we can prove it remains intact.
                    r.ReplaceResult = "UNSUPPORTED";
                    string after = Sha(ReadOwn(target));
                    bool unchanged = after == r.OriginalSha &&
                        Sha(ReadOwn(retained)) == r.OriginalSha;
                    policy.ObserveWriteFailure(unchanged, after);
                    r.PolicyPhase = policy.Phase.ToString();
                    if (!unchanged) Fail(r, "UNCERTAIN", "UNSUPPORTED_WITH_UNVERIFIED_TARGET");
                    else Fail(r, "UNSUPPORTED", "FILE_REPLACE_NOT_SUPPORTED");
                    return r;
                }
                catch (NotSupportedException)
                {
                    r.ReplaceResult = "UNSUPPORTED";
                    string after = Sha(ReadOwn(target));
                    bool unchanged = after == r.OriginalSha &&
                        Sha(ReadOwn(retained)) == r.OriginalSha;
                    policy.ObserveWriteFailure(unchanged, after);
                    r.PolicyPhase = policy.Phase.ToString();
                    if (!unchanged) Fail(r, "UNCERTAIN", "UNSUPPORTED_WITH_UNVERIFIED_TARGET");
                    else Fail(r, "UNSUPPORTED", "FILE_REPLACE_NOT_SUPPORTED");
                    return r;
                }
                catch (Exception ex)
                {
                    r.ReplaceResult = "THREW_" + ex.GetType().Name;
                    // File.Replace could have changed the target before throwing.
                    policy.ObserveWriteFailure(false, "NONE");
                    r.PolicyPhase = policy.Phase.ToString();
                    Fail(r, "UNCERTAIN", "FILE_REPLACE_THROWN_OUTCOME_UNKNOWN");
                    return r;
                }

                byte[] committed = ReadOwn(target);
                r.ReadbackSha = Sha(committed);
                r.ReadbackResult = policy.ObserveWriterReturn(true,
                    r.ReadbackSha).ToString();
                r.PolicyPhase = policy.Phase.ToString();
                r.PolicyFirstFailure = policy.FirstFailure;
                r.CommittedShaMatchesProposed =
                    r.ReadbackResult == ProfileXmlEvidenceResult.WrittenVerified.ToString() &&
                    r.ReadbackSha == r.ProposedSha;
                r.CommittedXmlValid = r.CommittedShaMatchesProposed &&
                    DetachedXmlStageAudit.InspectXml(committed, expected).Valid &&
                    DetachedXmlSemanticContract.Inspect(committed, expected).Valid;
                r.ExistingBackupUnmodified = r.BackupRetained &&
                    Sha(ReadOwn(retained)) == r.OriginalSha &&
                    File.Exists(replaceBackup) &&
                    Sha(ReadOwn(replaceBackup)) == r.OriginalSha;
                if (!r.CommittedXmlValid || !r.ExistingBackupUnmodified)
                {
                    Fail(r, "UNCERTAIN", "COMMIT_READBACK_OR_BACKUP_UNVERIFIED");
                    return r;
                }
                r.Status = "PASS_TEMP_FILE_TRANSACTION_PARTIAL";
                return r;
            }
            catch (Exception ex)
            {
                // On any unexpected exception after the transaction may have
                // begun, preserve UNKNOWN instead of claiming 'not written'.
                Fail(r, r.StaleResult == ProfileXmlEvidenceResult.WriteAllowed.ToString() ?
                    "UNCERTAIN" : "REJECTED", "EXCEPTION_" + ex.GetType().Name);
                return r;
            }
            finally
            {
                if (r.SandboxCreated && sandbox != null)
                {
                    try
                    {
                        CheckNoLink(sandbox);
                        Directory.Delete(sandbox, true);
                        r.TempCleaned = !Directory.Exists(sandbox);
                    }
                    catch (Exception) { r.TempCleaned = false; }
                    if (!r.TempCleaned)
                        Fail(r, "UNCERTAIN", "PRIVATE_TEMP_CLEANUP_FAILED");
                }
            }
        }

        internal static void LogFinal(Report r, int attempt, string stageStatus,
            string stageReason, string stageSha, long stageBytes, bool? sourceStable,
            bool? ownedStable, bool scribeIdle, bool outerTempCleaned,
            bool sessionLocked)
        {
            bool stagePass = stageStatus ==
                "PASS_DETACHED_VALUES_LOG_QUEUE_CLEAN_PARTIAL";
            bool pass = r != null && r.Status == "PASS_TEMP_FILE_TRANSACTION_PARTIAL" &&
                stagePass && sourceStable == true && ownedStable == true &&
                scribeIdle && outerTempCleaned && r.TempCleaned &&
                r.FailureClassificationsFailClosed && r.BackupShaMatchesOriginal &&
                r.CommittedXmlValid && r.ExistingBackupUnmodified;
            string status = pass ? "PASS_TEMP_FILE_TRANSACTION_PARTIAL" :
                (stageStatus == "UNCERTAIN" ||
                 (r != null && (r.Status == "UNCERTAIN" ||
                     r.Status == "PASS_TEMP_FILE_TRANSACTION_PARTIAL")) ?
                    "UNCERTAIN" :
                    (stageStatus == "REJECTED" || r == null ? "REJECTED" : r.Status));
            string reason = pass ? "NONE" :
                (r == null ? "STAGE_NOT_APPROVED:" + stageReason :
                    r.Status == "PASS_TEMP_FILE_TRANSACTION_PARTIAL" ?
                        "POST_TRANSACTION_GATE_FAILED" : r.FirstFailure);
            Log.Message(Tag + " status=" + status +
                "; revision=" + Revision + "; reason=" + reason +
                "; build=" + XpCustomizerMod.BuildId +
                "; attempt=" + attempt +
                "; testContext=EXPLICIT_DEBUG_ACTION_OS_TEMP_ONLY" +
                "; fixtureType=INDEPENDENT_XML_BASELINE" +
                "; stageSha256=" + stageSha + "; stageBytes=" + stageBytes +
                "; originalSha=" + (r == null ? "NONE" : r.OriginalSha) +
                "; proposedSha=" + (r == null ? "NONE" : r.ProposedSha) +
                "; retainedBackupSha=" + (r == null ? "NONE" : r.BackupSha) +
                "; commitReadbackSha=" + (r == null ? "NONE" : r.ReadbackSha) +
                "; prepareResult=" + (r == null ? "NOT_RUN" : r.PrepareResult) +
                "; staleCheckResult=" + (r == null ? "NOT_RUN" : r.StaleResult) +
                "; replaceApiResult=" + (r == null ? "NOT_RUN" : r.ReplaceResult) +
                "; readbackResult=" + (r == null ? "NOT_RUN" : r.ReadbackResult) +
                "; policyPhase=" + (r == null ? "NOT_RUN" : r.PolicyPhase) +
                "; firstFailure=" + (r == null ? "NOT_RUN" : r.FirstFailure) +
                "; policyFirstFailure=" + (r == null ? "NOT_RUN" : r.PolicyFirstFailure) +
                "; backupRetainedDuringAudit=" + (r != null && r.BackupRetained) +
                "; backupShaMatchesOriginal=" + (r != null && r.BackupShaMatchesOriginal) +
                "; expectedTargetShaMatches=" + (r != null && r.ExpectedTargetShaMatches) +
                "; staleTargetBlocked=" + (r != null && r.StaleTargetBlocked) +
                "; backupDamageBlocked=" + (r != null && r.BackupDamageBlocked) +
                "; writerReturned=" + (r != null && r.WriterReturned) +
                "; committedShaMatchesProposed=" + (r != null && r.CommittedShaMatchesProposed) +
                "; committedXmlValid=" + (r != null && r.CommittedXmlValid) +
                "; existingBackupUnmodified=" + (r != null && r.ExistingBackupUnmodified) +
                "; allFailureClassificationsFailClosed=" +
                    (r != null && r.FailureClassificationsFailClosed) +
                "; liveSourceStable=" + (sourceStable == true) +
                "; ownedConfigXmlStable=" + (ownedStable == true) +
                "; scribeIdleAfter=" + scribeIdle +
                "; isolatedTempCleaned=" + (r != null && r.TempCleaned && outerTempCleaned) +
                "; actualModXmlWritesByAudit=0; otherModXmlWritesByAudit=0" +
                "; vanillaWriterTouched=False; productionApply=NOT_EXERCISED_BY_THIS_AUDIT" +
                "; realVanillaWriteSettingsAtomicity=NOT_VERIFIED" +
                "; realScribeAtomicity=NOT_VERIFIED; osCrashDurability=NOT_VERIFIED" +
                "; realReplaceFaultInjection=NOT_RUN; powerLossInjection=NOT_RUN" +
                "; actionRequired=" + (status == "UNCERTAIN" ?
                    "EXIT_WITHOUT_SAVING_AND_REPORT" : "NONE"));
        }
    }
}
#endif
