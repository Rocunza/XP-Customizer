#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using LudeonTK;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// R6-P1-D2-C4-B1-F1: EXPLICIT, DEBUG-ONLY, disposable Scribe staging probe.
    /// This is not a profile Apply, not a full round-trip / semantic proof, and
    /// not a safe XML commit implementation. The only Scribe output path is a
    /// uniquely created child of the operating system's TEMP directory.
    /// No Harmony/global Scribe patches and no ForceStop calls are installed.
    /// </summary>
    internal static class DetachedXmlStageAudit
    {
        internal const string Revision = "R6P1D2C4B2AB1-RAW-XML-SEMANTIC-20261107";
        internal const string Tag = "[XPC-R6P1D2C4B2AB1][XML_SEMANTIC_STAGE_AUDIT]";
        internal const int MaximumBytes = 4 * 1024 * 1024;
        internal const string ExpectedClass = "XpCustomizer.XpCustomizerSettings";
        private const string StageFilename = "C4B1_ISOLATED_STAGE.xml";
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static bool running;
        // Debug-only one-way latch after an uncertain temp commit outcome.
        // Never resets in this game session; does not affect vanilla saves.
        private static bool tempCommitSessionLocked;
        private static int sequence;

        internal sealed class Inspection
        {
            internal string Failure = "NONE";
            internal int ComparedScalars;
            internal int OmittedScalars;
            internal int VerifiedCollections;
            internal int VerifiedItems;
            internal string Sha = "NONE";
            internal long Bytes;
            internal bool Valid;
        }

        private sealed class FileEvidence
        {
            internal bool Exists;
            internal string Hash = "MISSING";
            internal long Bytes;
        }

        /// <summary>Called ONLY by the explicit game developer DebugAction.</summary>
        internal static void RunManual()
        {
            RunManualCore(false);
        }

        /// <summary>Separately named explicit action; never runs on startup.</summary>
        internal static void RunManualWithDetachedLoad()
        {
            RunManualCore(true);
        }

        /// <summary>B2-C3 single representative vector, explicit action only.</summary>
        internal static void RunManualWithRepresentativeVector()
        {
            RunManualCore(true, true);
        }

        /// <summary>B3-A: same proven B2-C3 stage and Verse load; temp transaction only.</summary>
        internal static void RunManualWithTempCommit()
        {
            if (!DetachedTempFileTransactionSelfTests.StartupPassed)
            {
                // Only this new Debug probe is blocked. Never alter global
                // Scribe, vanilla WriteSettings or an unrelated mod's state.
                Log.Warning("[XPC-R6P1D2C4B3AF1][TEMP_COMMIT_AUDIT]" +
                    " status=REJECTED; reason=STARTUP_TEMP_PATH_SELFTEST_NOT_PASSED" +
                    "; transaction=NOT_RUN; configWrites=0; vanillaWriterTouched=False" +
                    "; productionApply=NOT_EXERCISED_BY_THIS_AUDIT; build=" + XpCustomizerMod.BuildId);
                return;
            }
            RunManualCore(true, true, true);
        }

        private static void RunManualCore(bool requestDetachedLoad,
            bool representativeVector = false, bool requestTempTransaction = false)
        {
            int attempt = ++sequence;
            string status = "REJECTED";
            string reason = "NOT_STARTED";
            string stageSha = "NONE";
            long stageBytes = 0;
            int initCalls = 0, deepCalls = 0, finalizeCalls = 0;
            int scalars = 0, omitted = 0, collections = 0, items = 0;
            int defaultVerified = 0, unresolved = DetachedXmlSemanticContract.ScalarCount;
            string rawSemantic = "NOT_CHECKED";
            bool? sourceStable = null, ownedFileStable = null;
            bool scribeRestored = false, cleanupComplete = false;
            bool sandboxCreated = false;
            string sandbox = null;
            bool scribeStarted = false;
            bool scribeEnded = false;
            DetachedVerseLoadAudit.Report loadReport = null;
            DetachedNonDefaultVector.Coverage vectorCoverage = null;
            DetachedTempFileTransactionAudit.Report transactionReport = null;

            if (running)
            {
                Log.Warning(Tag + " status=REJECTED; reason=REENTRANT; attempt=" + attempt +
                    "; actualModXmlWrites=0; productionApply=NOT_EXERCISED_BY_THIS_AUDIT; build=" + XpCustomizerMod.BuildId);
                return;
            }
            running = true;
            try
            {
                if (requestTempTransaction && tempCommitSessionLocked)
                {
                    reason = "TEMP_COMMIT_SESSION_LOCKED_AFTER_UNCERTAIN";
                    return;
                }
                // Explicit action is map-only; disallow external direct calls in menus.
                if (Current.Game == null || Find.CurrentMap == null)
                {
                    reason = "NO_PLAYING_MAP";
                    return;
                }
                if (XpCustomizerMod.Settings == null)
                {
                    reason = "NO_LIVE_SETTINGS";
                    return;
                }
                if (!ScribeIdle())
                {
                    reason = "SCRIBE_BUSY_AT_PREFLIGHT";
                    return;
                }
                // Current B1 float parsing requires invariant-formatted Scribe
                // numeric data. Avoid a false PASS on comma-decimal cultures.
                if (CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator != ".")
                {
                    reason = "SCRIBE_CULTURE_NOT_VERIFIED";
                    return;
                }

                XpCustomizerSettings live = XpCustomizerMod.Settings;
                // Capture the live source, but never pass it to Scribe.
                XpCustomizerSettings before = SettingsSnapshotService.CloneIndependent(live);
                XpCustomizerSettings candidate = SettingsSnapshotService.CloneIndependent(before);
                // Deliberately different DETACHED scalar values: provable staged
                // bytes without modifying live settings, GeneDefs, or UI state.
                candidate.gwsHighFactor = before.gwsHighFactor == 1.11f ? 1.12f : 1.11f;
                candidate.furtherLearningMentalThresholdStrength =
                    before.furtherLearningMentalThresholdStrength == 0.8f ? 0.85f : 0.8f;
                if (representativeVector)
                {
                    // The representative vector NEVER modifies live settings;
                    // reset the two baseline B2-C2 probes inside a fresh clone.
                    candidate = DetachedNonDefaultVector.Build(before);
                    vectorCoverage = DetachedNonDefaultVector.Assess(candidate);
                    if (!vectorCoverage.Accepted)
                    {
                        reason = "REPRESENTATIVE_VECTOR_REJECTED:" + vectorCoverage.Reason;
                        return;
                    }
                }
                SettingsCandidateValidator.Result validated =
                    SettingsCandidateValidator.Validate(candidate);
                if (!validated.Accepted)
                {
                    reason = "CANDIDATE_REJECTED:" + validated.Reason;
                    return;
                }
                XpCustomizerSettings expected = SettingsSnapshotService.CloneIndependent(candidate);

                // Read-only observation of XP-owned XML only. This is NOT a
                // backup, a lock, or a claim about writes by other processes.
                string ownedPath = GetOwnedPath();
                FileEvidence originalFile = InspectOwnedFile(ownedPath);

                string tempRoot = Path.GetFullPath(Path.GetTempPath());
                sandbox = Path.GetFullPath(Path.Combine(tempRoot,
                    "XPC_C4B1_" + Guid.NewGuid().ToString("N")));
                if (!IsDescendant(sandbox, tempRoot) ||
                    IsDescendant(sandbox, Path.GetFullPath(GenFilePaths.ConfigFolderPath)) ||
                    String.Equals(sandbox, Path.GetDirectoryName(ownedPath),
                        StringComparison.OrdinalIgnoreCase) || Directory.Exists(sandbox))
                {
                    reason = "UNSAFE_STAGE_DIRECTORY";
                    return;
                }
                Directory.CreateDirectory(sandbox);
                sandboxCreated = true;
                string stageFile = Path.GetFullPath(Path.Combine(sandbox, StageFilename));
                if (!String.Equals(Path.GetDirectoryName(stageFile), sandbox,
                        StringComparison.OrdinalIgnoreCase) || File.Exists(stageFile))
                {
                    reason = "UNSAFE_STAGE_FILE";
                    return;
                }

                // Global Scribe is not reentrant. The second check prevents an
                // ordinary UI action from starting a nested save; other mods
                // running concurrent background Scribe calls are NOT protected
                // by this check and remain an unverified compatibility risk.
                if (!ScribeIdle())
                {
                    reason = "SCRIBE_BUSY_BEFORE_INIT";
                    return;
                }

                // Exact equivalent of LoadedModManager.WriteModSettings's
                // root/generic shape, but with a disposable private path.
                // Important: type MUST be ModSettings rather than the derived
                // XpCustomizerSettings, or the Class attribute would be absent.
                ModSettings detachedBase = candidate;
                initCalls++;
                Scribe.saver.InitSaving(stageFile, "SettingsBlock");
                scribeStarted = true;
                try
                {
                    deepCalls++;
                    Scribe_Deep.Look(ref detachedBase, "ModSettings");
                }
                finally
                {
                    // Never use Scribe.ForceStop(): it also resets the global
                    // loader and can damage another mod's active operation.
                    // Finalize only our own successfully started save when the
                    // expected Saving state is still present.
                    if (Scribe.mode == LoadSaveMode.Saving)
                    {
                        finalizeCalls++;
                        Scribe.saver.FinalizeSaving();
                        scribeEnded = true;
                    }
                }
                scribeRestored = ScribeIdle();
                if (!scribeRestored || !scribeEnded)
                {
                    status = "UNCERTAIN";
                    reason = "SCRIBE_NOT_RESTORED";
                    return;
                }
                if (!Object.ReferenceEquals(detachedBase, candidate))
                {
                    status = "UNCERTAIN";
                    reason = "DEEP_TARGET_REPLACED";
                    return;
                }
                string mismatch;
                if (!SettingsApplyCoordinator.Equivalent(candidate, expected, out mismatch))
                {
                    status = "UNCERTAIN";
                    reason = "STAGE_MUTATED_CANDIDATE:" + mismatch;
                    return;
                }

                byte[] data = ReadStageBytes(stageFile);
                Inspection scan = InspectXml(data, expected);
                stageSha = scan.Sha;
                stageBytes = scan.Bytes;
                scalars = scan.ComparedScalars;
                omitted = scan.OmittedScalars;
                collections = scan.VerifiedCollections;
                items = scan.VerifiedItems;
                if (!scan.Valid)
                {
                    status = "UNCERTAIN";
                    reason = "STAGE_CONTENT_INVALID:" + scan.Failure;
                    return;
                }
                DetachedXmlSemanticContract.Result semantic =
                    DetachedXmlSemanticContract.Inspect(data, expected);
                defaultVerified = semantic.OmittedVerified;
                unresolved = semantic.Unresolved;
                rawSemantic = semantic.Valid ? "PASS_RAW_SERIALIZED" : "UNCERTAIN";
                // Even on semantic failure, audit source and owned XML before
                // returning. A clean stage is not proof if the original moved.
                sourceStable = SettingsApplyCoordinator.Equivalent(live, before, out mismatch);
                if (sourceStable != true)
                {
                    status = "UNCERTAIN";
                    reason = "LIVE_SOURCE_CHANGED:" + mismatch;
                    return;
                }
                FileEvidence afterFile = InspectOwnedFile(ownedPath);
                ownedFileStable = originalFile.Exists == afterFile.Exists &&
                    originalFile.Bytes == afterFile.Bytes &&
                    String.Equals(originalFile.Hash, afterFile.Hash, StringComparison.Ordinal);
                if (ownedFileStable != true)
                {
                    status = "UNCERTAIN";
                    reason = "OWNED_XML_CHANGED_DURING_AUDIT";
                    return;
                }
                if (!semantic.Valid)
                {
                    status = "UNCERTAIN";
                    reason = "RAW_SEMANTIC_INVALID:" + semantic.Failure;
                    return;
                }

                // B2-C1 uses exactly the same already-validated temp artifact;
                // never copy the entire stage-save implementation into a new
                // action and never pass the live settings object to Scribe.
                if (requestDetachedLoad)
                {
                    loadReport = DetachedVerseLoadAudit.Execute(
                        stageFile, stageSha, stageBytes, expected);
                    // Recheck the *actual* live owner and owned XML after the
                    // loader, not just after the saver from the earlier audit.
                    sourceStable = SettingsApplyCoordinator.Equivalent(live, before, out mismatch);
                    FileEvidence postLoadFile = InspectOwnedFile(ownedPath);
                    ownedFileStable = originalFile.Exists == postLoadFile.Exists &&
                        originalFile.Bytes == postLoadFile.Bytes &&
                        String.Equals(originalFile.Hash, postLoadFile.Hash,
                            StringComparison.Ordinal);
                    if (!sourceStable.Value || !ownedFileStable.Value)
                    {
                        status = "UNCERTAIN";
                        reason = !sourceStable.Value ?
                            "LIVE_SOURCE_CHANGED_AFTER_LOAD:" + mismatch :
                            "OWNED_XML_CHANGED_AFTER_LOAD";
                        return;
                    }
                    if (!loadReport.ValuesMatch)
                    {
                        status = loadReport.Status == "REJECTED" ? "REJECTED" : "UNCERTAIN";
                        reason = "DETACHED_LOAD:" + loadReport.Failure;
                        return;
                    }
                    status = "PASS_DETACHED_VALUES_LOG_QUEUE_CLEAN_PARTIAL";
                    reason = "DETACHED_VALUES_MATCH_AND_QUEUED_LOG_WINDOW_CLEAN";
                    // B3-A consumes bytes only after the existing B2-C3 real
                    // Scribe stage, XML contract, engine round-trip and queue
                    // observation have independently passed. NO extra Scribe.
                    if (requestTempTransaction)
                    {
                        if (!representativeVector || vectorCoverage == null ||
                            !vectorCoverage.Accepted || !loadReport.LogCaptureReliable ||
                            loadReport.LogObservationReason != "NONE" || !ScribeIdle())
                        {
                            status = "UNCERTAIN";
                            reason = "TEMP_COMMIT_PRECONDITIONS_UNMET";
                            return;
                        }
                        transactionReport = DetachedTempFileTransactionAudit.Execute(
                            data, stageSha, expected);
                        // The file experiment is distinct from detached-load
                        // success, but the live owner/XML must still be checked.
                        sourceStable = SettingsApplyCoordinator.Equivalent(
                            live, before, out mismatch);
                        FileEvidence postTransactionFile = InspectOwnedFile(ownedPath);
                        ownedFileStable = originalFile.Exists == postTransactionFile.Exists &&
                            originalFile.Bytes == postTransactionFile.Bytes &&
                            String.Equals(originalFile.Hash, postTransactionFile.Hash,
                                StringComparison.Ordinal);
                        if (sourceStable != true || ownedFileStable != true)
                        {
                            status = "UNCERTAIN";
                            reason = sourceStable != true ?
                                "LIVE_SOURCE_CHANGED_AFTER_TEMP_TRANSACTION" :
                                "OWNED_XML_CHANGED_AFTER_TEMP_TRANSACTION";
                            return;
                        }
                    }
                }
                else
                {
                    // The original action remains raw-only; no loader calls.
                    status = "PASS_RAW_SEMANTIC_STAGE";
                    reason = "RAW_131_SCALARS_AND_10_COLLECTIONS_MATCH";
                }
            }
            catch (Exception ex)
            {
                status = scribeStarted || initCalls > 0 ? "UNCERTAIN" : "REJECTED";
                reason = "EXCEPTION:" + ex.GetType().Name;
                // Do not print file contents, paths, or exception.Message:
                // Scribe may include user-controlled paths or values there.
            }
            finally
            {
                scribeRestored = ScribeIdle();
                // Only this probe's uniquely created sandbox may be deleted.
                // If Scribe is not idle, do not touch a potentially open stream.
                // A failed detached load may leave private loader registrations
                // even if the saver is idle. Never delete its input file while
                // that loader may still own it. If no load was initiated, the
                // probe owns the file and can safely clean it up.
                bool loaderMayOwnStage = requestDetachedLoad && loadReport != null &&
                    loadReport.InitLoadingCalls > 0 && !loadReport.FinalIdle;
                if (sandboxCreated && !loaderMayOwnStage &&
                    (scribeRestored || initCalls == 0))
                {
                    try
                    {
                        Directory.Delete(sandbox, true);
                        cleanupComplete = !Directory.Exists(sandbox);
                    }
                    catch (Exception)
                    {
                        cleanupComplete = false;
                    }
                }
                if ((!scribeRestored && initCalls > 0) ||
                    (sandboxCreated && !cleanupComplete))
                {
                    status = "UNCERTAIN";
                    reason = !scribeRestored && initCalls > 0 ?
                        "SCRIBE_GLOBAL_STATE_NOT_IDLE_AFTER_OWN_INIT" :
                        (loaderMayOwnStage ? "TEMP_RETAINED_FOR_LOADER_SAFETY" :
                        "TEMP_SANDBOX_CLEANUP_FAILED");
                }
                Log.Message(Tag + " status=" + status + "; reason=" + reason +
                    "; revision=" + Revision + "; attempt=" + attempt +
                    "; testContext=EXPLICIT_DEBUG_ACTION_ISOLATED_TEMP" +
                    "; vector=" + (representativeVector ?
                        DetachedNonDefaultVector.VectorId : "LEGACY_B2C2") +
                    "; ownedSource=SINGLE_XP_CUSTOMIZER_ONLY" +
                    "; stageSha256=" + stageSha +
                    "; stageBytes=" + stageBytes.ToString(CultureInfo.InvariantCulture) +
                    "; comparedEmittedScalars=" + scalars +
                    "; omittedScalars=" + omitted +
                    "; verifiedOmittedDefaults=" + defaultVerified +
                    "; unresolvedScalarPaths=" + unresolved +
                    "; verifiedScalarPaths=" + (scalars + defaultVerified) +
                    "; rawSemantic=" + rawSemantic +
                    "; defaultContractFingerprint=" + DetachedXmlSemanticContract.SourceFingerprint +
                    "; verifiedCollections=" + collections +
                    "; verifiedCollectionItems=" + items +
                    "; candidateSourceStable=" + (sourceStable.HasValue ?
                        sourceStable.Value.ToString() : "NOT_CHECKED") +
                    "; ownedXmlReadbackStable=" + (ownedFileStable.HasValue ?
                        ownedFileStable.Value.ToString() : "NOT_CHECKED") +
                    "; scribeIdleAfter=" + scribeRestored +
                    "; sandboxCleaned=" + cleanupComplete +
                    "; initSavingCalls=" + initCalls +
                    "; deepLookCalls=" + deepCalls +
                    "; finalizeSavingCalls=" + finalizeCalls +
                    "; actualModXmlWrites=0; otherModWritesByAudit=0" +
                    "; stageHashUse=STAGING_PROOF_ONLY_NOT_COMMIT" +
                    "; fullSemantic=NOT_VERIFIED; detachedVerseLoad=" +
                    (loadReport == null ? "NOT_RUN" : loadReport.Status) +
                    "; engineErrorLogCapture=" +
                    (requestDetachedLoad && loadReport != null &&
                     loadReport.LogCaptureReliable &&
                     loadReport.LogObservationReason == "NONE" ?
                        "OBSERVED_QUEUED_ONLY" : "NOT_VERIFIED") +
                    "; scribeAtomicity=NOT_VERIFIED; osDurability=NOT_VERIFIED" +
                    "; productionApply=NOT_EXERCISED_BY_THIS_AUDIT; actionRequired=" +
                    (status == "UNCERTAIN" ? "EXIT_WITHOUT_SAVING_AND_REPORT" : "NONE") +
                    "; build=" + XpCustomizerMod.BuildId);
                if (requestDetachedLoad)
                    DetachedVerseLoadAudit.LogFinal(loadReport, status, reason,
                        stageSha, scalars + defaultVerified, collections,
                        sourceStable, ownedFileStable, scribeRestored,
                        cleanupComplete);
                if (representativeVector)
                {
                    bool passed = status == "PASS_DETACHED_VALUES_LOG_QUEUE_CLEAN_PARTIAL" &&
                        vectorCoverage != null && vectorCoverage.Accepted &&
                        loadReport != null && loadReport.ValuesMatch &&
                        loadReport.LogCaptureReliable &&
                        loadReport.LogObservationReason == "NONE" &&
                        sourceStable == true && ownedFileStable == true &&
                        scribeRestored && cleanupComplete;
                    Log.Message("[XPC-R6P1D2C4B2C3][NONDEFAULT_ROUNDTRIP_AUDIT]" +
                        " status=" + (passed ? "PASS_REPRESENTATIVE_PARTIAL" :
                            (status == "REJECTED" ? "REJECTED" : "UNCERTAIN")) +
                        "; revision=" + DetachedNonDefaultVector.Revision +
                        "; reason=" + reason +
                        "; vector=" + DetachedNonDefaultVector.VectorId +
                        "; nonDefaultScalars=" + (vectorCoverage == null ? 0 :
                            vectorCoverage.NonDefaultScalars) +
                        "; nestedNonDefault=" + (vectorCoverage == null ? 0 :
                            vectorCoverage.NestedNonDefaultScalars) +
                        "; nonEmptyDictionaries=" + (vectorCoverage == null ? 0 :
                            vectorCoverage.NonEmptyDictionaries) +
                        "; nonEmptyLists=" + (vectorCoverage == null ? 0 :
                            vectorCoverage.NonEmptyLists) +
                        "; escapedKeys=" + (vectorCoverage == null ? 0 :
                            vectorCoverage.EscapedKeys) +
                        "; verifiedXmlScalars=" + (scalars + defaultVerified) +
                        "; comparedFields=" + (loadReport == null ? 0 :
                            loadReport.ComparedFields) +
                        "; loadingVarsDifference=" + (loadReport == null ?
                            "NOT_CHECKED" : loadReport.LoadingVarsDifference) +
                        "; postLoadDifference=" + (loadReport == null ?
                            "NOT_CHECKED" : loadReport.PostLoadDifference) +
                        "; logQueue=" + (loadReport != null &&
                            loadReport.LogCaptureReliable &&
                            loadReport.LogObservationReason == "NONE" ?
                            "OBSERVED_CLEAN_ONLY" : "NOT_VERIFIED") +
                        "; scribeIdleAfter=" + scribeRestored +
                        "; tempCleaned=" + cleanupComplete +
                        "; liveSourceStable=" + (sourceStable == true) +
                        "; ownedXmlStable=" + (ownedFileStable == true) +
                        "; actualModXmlWritesByAudit=0; productionApply=NOT_EXERCISED_BY_THIS_AUDIT" +
                        "; fullSemantic=NOT_VERIFIED; migrationCases=NOT_VERIFIED" +
                        "; actionRequired=" + (passed ? "NONE" :
                            "EXIT_WITHOUT_SAVING_AND_REPORT") +
                        "; build=" + XpCustomizerMod.BuildId);
                }
                if (requestTempTransaction)
                {
                    if (status == "UNCERTAIN" ||
                        (transactionReport != null && transactionReport.Status == "UNCERTAIN") ||
                        (transactionReport != null &&
                         transactionReport.Status == "PASS_TEMP_FILE_TRANSACTION_PARTIAL" &&
                         (sourceStable != true || ownedFileStable != true ||
                          !scribeRestored || !cleanupComplete)))
                        tempCommitSessionLocked = true;
                    DetachedTempFileTransactionAudit.LogFinal(
                        transactionReport, attempt, status, reason, stageSha, stageBytes,
                        sourceStable, ownedFileStable, scribeRestored,
                        cleanupComplete, tempCommitSessionLocked);
                }
                running = false;
            }
        }

        private static bool ScribeIdle()
        {
            return Scribe.mode == LoadSaveMode.Inactive &&
                Scribe.saver != null && Scribe.saver.CurPath == null &&
                !Scribe.saver.savingForDebug;
        }

        private static bool IsDescendant(string child, string root)
        {
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return child.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetOwnedPath()
        {
            // Exactly the installed mod's content folder and handle name,
            // matching LoadedModManager.GetSettingsFilename's 1.6 rule.
            // Never use an arbitrary mod identifier or caller-provided path.
            XpCustomizerMod mod = LoadedModManager.GetMod<XpCustomizerMod>();
            if (mod == null || mod.Content == null ||
                String.IsNullOrEmpty(mod.Content.FolderName) ||
                mod.GetType() != typeof(XpCustomizerMod))
                throw new InvalidOperationException("OWNED_MOD_UNAVAILABLE");
            string root = Path.GetFullPath(GenFilePaths.ConfigFolderPath);
            string filename = GenText.SanitizeFilename("Mod_" +
                mod.Content.FolderName + "_" + mod.GetType().Name + ".xml");
            if (String.IsNullOrEmpty(filename) || filename == "." || filename == ".." ||
                filename.IndexOf('/') >= 0 || filename.IndexOf('\\') >= 0)
                throw new InvalidOperationException("BAD_OWNED_NAME");
            string path = Path.GetFullPath(Path.Combine(root, filename));
            if (!String.Equals(Path.GetDirectoryName(path),
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("OWNED_PATH_OUTSIDE_CONFIG");
            return path;
        }

        private static FileEvidence InspectOwnedFile(string path)
        {
            FileEvidence result = new FileEvidence();
            if (!File.Exists(path)) return result;
            FileInfo info = new FileInfo(path);
            if (info.Length < 0 || info.Length > MaximumBytes)
                throw new InvalidOperationException("OWNED_XML_SIZE_OUT_OF_RANGE");
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length > MaximumBytes)
                throw new InvalidOperationException("OWNED_XML_OVER_LIMIT");
            result.Exists = true;
            result.Bytes = bytes.Length;
            result.Hash = Sha(bytes);
            return result;
        }

        private static byte[] ReadStageBytes(string path)
        {
            FileInfo info = new FileInfo(path);
            if (!info.Exists || info.Length == 0 || info.Length > MaximumBytes)
                throw new InvalidOperationException("STAGE_MISSING_OR_OVERSIZE");
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length == 0 || bytes.Length > MaximumBytes)
                throw new InvalidOperationException("STAGE_BYTES_INVALID");
            return bytes;
        }

        private static string Sha(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(bytes);
                StringBuilder hex = new StringBuilder(64);
                foreach (byte b in digest)
                    hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return hex.ToString();
            }
        }

        /// <summary>
        /// Pure scanner, independently testable without opening Scribe or disk.
        /// Checks *emitted* scalar values and every settings collection. It
        /// cannot prove the correctness of fields omitted by Scribe_Values,
        /// nor detect all exceptions swallowed by Scribe_Deep.
        /// </summary>
        internal static Inspection InspectXml(byte[] bytes, XpCustomizerSettings expected)
        {
            Inspection result = new Inspection();
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumBytes ||
                expected == null)
            {
                result.Failure = "INVALID_INPUT";
                return result;
            }
            result.Bytes = bytes.Length;
            result.Sha = Sha(bytes);
            try
            {
                XmlReaderSettings settings = new XmlReaderSettings();
                settings.DtdProcessing = DtdProcessing.Prohibit;
                settings.XmlResolver = null;
                settings.MaxCharactersInDocument = MaximumBytes;
                XmlDocument document = new XmlDocument();
                document.XmlResolver = null;
                using (MemoryStream stream = new MemoryStream(bytes, false))
                using (XmlReader reader = XmlReader.Create(stream, settings))
                    document.Load(reader);
                XmlElement root = document.DocumentElement;
                if (root == null || root.Name != "SettingsBlock" || root.HasAttributes ||
                    DirectElements(root).Count != 1)
                    return Fail(result, "BAD_ROOT");
                XmlElement node = DirectElements(root)[0];
                if (node.Name != "ModSettings" || node.Attributes.Count != 1 ||
                    node.GetAttribute("Class") != ExpectedClass)
                    return Fail(result, "BAD_OWNER_CLASS");
                if (node.HasAttribute("IsNull")) return Fail(result, "NULL_SETTINGS");

                Dictionary<string, object> fields = BuildSerializedFields(expected);

                HashSet<string> found = new HashSet<string>(StringComparer.Ordinal);
                foreach (XmlElement entry in DirectElements(node))
                {
                    if (!found.Add(entry.Name)) return Fail(result, "DUPLICATE_FIELD");
                    object exp;
                    if (!fields.TryGetValue(entry.Name, out exp))
                        return Fail(result, "UNKNOWN_OR_UNEXPECTED_FIELD");
                    if (exp is IList)
                    {
                        if (!CheckList(entry, (IList)exp, result))
                            return Fail(result, "LIST_MISMATCH:" + entry.Name);
                        result.VerifiedCollections++;
                    }
                    else if (exp is IDictionary)
                    {
                        if (!CheckDictionary(entry, (IDictionary)exp, result))
                            return Fail(result, "DICTIONARY_MISMATCH:" + entry.Name);
                        result.VerifiedCollections++;
                    }
                    else if (!CheckScalar(entry, exp))
                        return Fail(result, "SCALAR_MISMATCH:" + entry.Name);
                    else result.ComparedScalars++;
                }
                foreach (KeyValuePair<string, object> entry in fields)
                {
                    if (entry.Value is IList || entry.Value is IDictionary)
                    {
                        if (!found.Contains(entry.Key))
                            return Fail(result, "COLLECTION_MISSING:" + entry.Key);
                    }
                    else if (!found.Contains(entry.Key)) result.OmittedScalars++;
                }
                // Expected full collection inventory: 4 lists + 6 dictionaries.
                // Future field inventory drift must fail closed, not silently
                // lower the test coverage.
                if (result.VerifiedCollections != 10 || result.ComparedScalars < 2)
                    return Fail(result, "INSUFFICIENT_EVIDENCE");
                result.Valid = true;
                return result;
            }
            catch (XmlException)
            {
                return Fail(result, "XML_MALFORMED");
            }
            catch (InvalidOperationException)
            {
                // Container mixed-content / invalid node: never treated as a
                // harmless scalar text node or an unspecified parser failure.
                return Fail(result, "STRUCTURE_INVALID");
            }
            catch (Exception ex)
            {
                return Fail(result, "PARSE_EXCEPTION:" + ex.GetType().Name);
            }
        }

        private static Inspection Fail(Inspection result, string why)
        {
            result.Valid = false;
            result.Failure = why;
            return result;
        }

        /// <summary>
        /// Shared inventory of actual C# field values. The B2 Scribe-default
        /// contract independently lists each XML key + the Look fallback.
        /// </summary>
        internal static Dictionary<string, object> BuildSerializedFields(XpCustomizerSettings expected)
        {
            if (expected == null) throw new ArgumentNullException("expected");
            Dictionary<string, object> fields = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (FieldInfo f in SettingsSnapshotService.GetDeclaredSettingsFieldsForAudit())
            {
                object value = f.GetValue(expected);
                if (f.FieldType == typeof(bool) || f.FieldType == typeof(int) ||
                    f.FieldType == typeof(float) || typeof(IList).IsAssignableFrom(f.FieldType) ||
                    typeof(IDictionary).IsAssignableFrom(f.FieldType))
                    fields.Add(SerializedName(f.Name), value);
            }
            AddProfileFields(fields, expected.consciousnessAnchorSpeed, "consAnchorV2Speed");
            AddProfileFields(fields, expected.consciousnessAnchorCapacity, "consAnchorV2Capacity");
            AddProfileFields(fields, expected.consciousnessFurtherV2, "consFurtherV2");
            return fields;
        }

        private static string SerializedName(string fieldName)
        {
            // Two retained legacy fields intentionally use their original
            // XML names instead of the in-memory migration field names.
            if (fieldName == "legacyLearningCapacityUseCustomValues")
                return "learningCapacityUseCustomValues";
            if (fieldName == "legacyLearningCapacityBonusConversion")
                return "learningCapacityBonusConversion";
            return fieldName;
        }

        private static void AddProfileFields(Dictionary<string, object> fields,
            object profile, string prefix)
        {
            if (profile == null) throw new InvalidOperationException("NULL_PROFILE");
            foreach (FieldInfo field in profile.GetType().GetFields(Fields))
                fields.Add(prefix + field.Name, field.GetValue(profile));
        }

        private static List<XmlElement> DirectElements(XmlElement parent)
        {
            List<XmlElement> items = new List<XmlElement>();
            foreach (XmlNode child in parent.ChildNodes)
            {
                XmlElement element = child as XmlElement;
                if (element != null) items.Add(element);
                else if (child.NodeType != XmlNodeType.Whitespace &&
                    child.NodeType != XmlNodeType.SignificantWhitespace &&
                    child.NodeType != XmlNodeType.Comment)
                    throw new InvalidOperationException("UNEXPECTED_XML_NODE");
            }
            return items;
        }

        /// <summary>
        /// Read a Scribe_Values leaf. Unlike DirectElements (strictly for XML
        /// containers), a leaf MUST accept ordinary Text nodes. Do not use
        /// InnerText alone: it would hide nested elements and mixed content.
        /// Reject attributes, CDATA, comments, PI and nested elements; none
        /// is part of Scribe_Values' canonical scalar/list-key output.
        /// </summary>
        private static bool TryReadLeafText(XmlElement element, out string token)
        {
            token = null;
            if (element == null || element.HasAttributes) return false;
            StringBuilder value = new StringBuilder();
            foreach (XmlNode child in element.ChildNodes)
            {
                switch (child.NodeType)
                {
                    case XmlNodeType.Text:
                    case XmlNodeType.Whitespace:
                    case XmlNodeType.SignificantWhitespace:
                        value.Append(child.Value);
                        break;
                    default:
                        return false;
                }
            }
            token = value.ToString();
            return true;
        }

        private static bool CheckScalar(XmlElement element, object expected)
        {
            string raw;
            if (expected == null || !TryReadLeafText(element, out raw))
                return false;
            string token = raw.Trim();
            if (expected is bool)
            {
                bool value;
                return Boolean.TryParse(token, out value) && value == (bool)expected;
            }
            if (expected is int)
            {
                int value;
                return Int32.TryParse(token, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out value) && value == (int)expected;
            }
            if (expected is float)
            {
                float value;
                return Single.TryParse(token, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out value) &&
                    !Single.IsNaN(value) && !Single.IsInfinity(value) &&
                    value.Equals((float)expected);
            }
            return false;
        }

        private static bool CheckList(XmlElement element, IList expected, Inspection result)
        {
            if (element.HasAttributes || expected == null) return false;
            List<XmlElement> values = DirectElements(element);
            if (values.Count != expected.Count) return false;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i].Name != "li" || !CheckScalar(values[i], expected[i]))
                    return false;
                result.VerifiedItems++;
            }
            return true;
        }

        private static bool CheckDictionary(XmlElement element, IDictionary expected, Inspection result)
        {
            if (element.HasAttributes || expected == null) return false;
            List<XmlElement> containers = DirectElements(element);
            if (containers.Count != 2 || containers[0].Name != "keys" ||
                containers[1].Name != "values" || containers[0].HasAttributes ||
                containers[1].HasAttributes) return false;
            List<XmlElement> keys = DirectElements(containers[0]);
            List<XmlElement> vals = DirectElements(containers[1]);
            if (keys.Count != expected.Count || vals.Count != expected.Count) return false;
            HashSet<string> observed = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                string key;
                if (keys[i].Name != "li" || vals[i].Name != "li" ||
                    !TryReadLeafText(keys[i], out key))
                    return false;
                // Dictionary<string, T> keys are exact, untrimmed strings.
                // XML entities are decoded by XmlDocument before comparing.
                if (!observed.Add(key) || !expected.Contains(key) ||
                    !CheckScalar(vals[i], expected[key])) return false;
                result.VerifiedItems++;
            }
            return true;
        }
    }

    public static class DetachedXmlStageDebugActions
    {
        [DebugAction("XP Customizer", "Stage detached ModSettings XML (D2-C4-B2AB1; temp only)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void StageIsolated()
        {
            DetachedXmlStageAudit.RunManual();
        }

        [DebugAction("XP Customizer",
            "Load and observe detached ModSettings XML (D2-C4-B2C2; temp only)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void LoadVerifiedIsolated()
        {
            DetachedXmlStageAudit.RunManualWithDetachedLoad();
        }

        [DebugAction("XP Customizer",
            "Round-trip representative nondefault XML (D2-C4-B2C3; temp only)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void LoadRepresentativeNonDefault()
        {
            DetachedXmlStageAudit.RunManualWithRepresentativeVector();
        }

        [DebugAction("XP Customizer",
            "Commit verified XML in isolated temp (D2-C4-B3A; temp only)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void CommitVerifiedTempOnly()
        {
            DetachedXmlStageAudit.RunManualWithTempCommit();
        }
    }
}
#endif
