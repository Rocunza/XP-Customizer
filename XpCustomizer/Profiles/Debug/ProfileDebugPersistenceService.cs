#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Xml;
using RimWorld;
using Verse;

namespace XpCustomizer.Profiles
{
    internal enum ProfileDebugPersistenceCode
    {
        RejectedBeforeRuntime,
        RuntimeNotVerifiedNoWrite,
        FailedKnownUnchanged,
        ConflictNoWriter,
        WrittenVerifiedNoRestart,
        PersistenceUncertain
    }

    internal sealed class ProfileDebugPersistenceResult
    {
        internal readonly ProfileDebugPersistenceCode Code;
        internal readonly string Reason;
        internal ProfileDebugPersistenceResult(ProfileDebugPersistenceCode code, string reason)
        { Code = code; Reason = reason ?? "NONE"; }
    }

    /// <summary>
    /// D2-D1B: explicit Debug-only, one-shot owned XML persistence test.
    /// Reuses D1A's actual runtime Apply and the normal Mod.WriteSettings saver.
    /// Never calls Scribe directly, never replaces Mod XML with custom code,
    /// never writes other mods' settings or any profile JSON.
    /// A retained independent pre-write backup is intentionally NOT deleted.
    /// Disk readback is only raw semantic evidence, not a crash/restart proof.
    /// </summary>
    internal sealed class ProfileDebugPersistenceService
    {
        internal const string Revision = "R6P1D2D1B-DEBUG-OWNED-XML-ONE-SHOT-20261108";
        internal const string Tag = "[XPC-R6P1D2D1B][DEBUG_PROFILE_PERSIST_AUDIT]";
        private static bool executing;
        private readonly ProfileDebugLiveApplyService runtime = new ProfileDebugLiveApplyService();

        internal ProfileDebugLiveApplyResult Prepare(ProfileApplyReviewSession review,
            XpCustomizerSettings live)
        {
            // Never arm real runtime or disk operations during this preflight.
            if (!XpCustomizerSelfTests.DebugStartupCompleteAndPassed ||
                ProfileDebugLiveApplySessionGuard.LiveWriteBlocked ||
                ProfileDebugPersistWriteGate.LiveTestAttempted ||
                RuntimeRecoverySessionGate.LiveModSettingsIoBlocked ||
                ProfileWriteSessionGate.IsBlocked || !ScribeIdle())
            {
                Log.Message(Tag + " phase=ARM; status=REJECTED; reason=SESSION_OR_SCRIBE_GATE" +
                    "; writerCalls=0; build=" + XpCustomizerMod.BuildId);
                return new ProfileDebugLiveApplyResult(ProfileDebugLiveApplyCode.PrecheckRejected,
                    "SESSION_OR_SCRIBE_GATE", null);
            }
            try
            {
                if (!CurrentInventoryReady(live))
                    throw new InvalidOperationException("XML_SETTINGS_SCHEMA_DRIFT");
                string path = OwnedPath();
                byte[] bytes = ReadStrict(path);
                if (!OwnXmlShape(bytes))
                    throw new InvalidOperationException("OWN_XML_SHAPE_INVALID");
            }
            catch (Exception ex)
            {
                Log.Warning(Tag + " phase=ARM; status=REJECTED; reason=OWN_XML_PREFLIGHT_" +
                    ex.GetType().Name + "; writerCalls=0; build=" + XpCustomizerMod.BuildId);
                return new ProfileDebugLiveApplyResult(ProfileDebugLiveApplyCode.PrecheckRejected,
                    "OWN_XML_NOT_READY:" + ex.GetType().Name, null);
            }
            return runtime.Prepare(review, live);
        }

        internal ProfileDebugPersistenceResult ConfirmAndPersist(ProfileApplyBridgeSession session,
            XpCustomizerSettings live)
        {
            if (executing || session == null || live == null ||
                ProfileDebugPersistWriteGate.LiveTestAttempted ||
                ProfileDebugLiveApplySessionGuard.LiveWriteBlocked ||
                RuntimeRecoverySessionGate.LiveModSettingsIoBlocked ||
                ProfileWriteSessionGate.IsBlocked || !XpCustomizerSelfTests.DebugStartupCompleteAndPassed)
                return Report("RECHECK", ProfileDebugPersistenceCode.RejectedBeforeRuntime,
                    "ONE_SHOT_OR_GLOBAL_GATE", "NONE", "NONE", "NONE", "NONE", false, false,
                    false, 0, 0, 0);
            // A user clicked RUN, not ARM. From now until process restart,
            // automatic XP XML saves are blocked, including early preflight
            // rejection. Only a later verified one-shot permit can pass.
            if (!ProfileDebugPersistWriteGate.BeginLiveTest())
                return Report("RECHECK", ProfileDebugPersistenceCode.RejectedBeforeRuntime,
                    "PERSIST_TEST_ALREADY_STARTED", "NONE", "NONE", "NONE", "NONE",
                    false, false, false, 0, 0, 0);
            executing = true;
            string originalSha = "NONE";
            string backupSha = "NONE";
            string savedSha = "NONE";
            string backupName = "NONE";
            string targetPath = null;
            bool backupVerified = false;
            bool runtimeVerified = false;
            bool writerCalled = false;
            int scalarCompared = 0, scalarDefaults = 0, collections = 0;
            try
            {
                if (!ScribeIdle() || !CurrentInventoryReady(live) ||
                    !System.Object.ReferenceEquals(live, XpCustomizerMod.Settings))
                    return Report("RECHECK", ProfileDebugPersistenceCode.RejectedBeforeRuntime,
                        "SCRIBE_OR_LIVE_INSTANCE_CHANGED", originalSha, backupSha, savedSha,
                        backupName, backupVerified, runtimeVerified, writerCalled,
                        scalarCompared, scalarDefaults, collections);

                targetPath = OwnedPath();
                byte[] original = ReadStrict(targetPath);
                if (!OwnXmlShape(original))
                    return Report("RECHECK", ProfileDebugPersistenceCode.RejectedBeforeRuntime,
                        "INVALID_ORIGINAL_XML", originalSha, backupSha, savedSha,
                        backupName, backupVerified, runtimeVerified, writerCalled,
                        scalarCompared, scalarDefaults, collections);
                originalSha = Sha(original);

                // Independent CREATE_NEW retained backup (never overwrite any backup).
                string backupPath = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(targetPath), Path.GetFileName(targetPath) +
                    ".D2D1B." + Guid.NewGuid().ToString("N") + ".bak"));
                backupName = Path.GetFileName(backupPath);
                if (!String.Equals(Path.GetDirectoryName(backupPath),
                    Path.GetDirectoryName(targetPath), StringComparison.OrdinalIgnoreCase) ||
                    File.Exists(backupPath) || Directory.Exists(backupPath))
                    throw new InvalidOperationException("BACKUP_OUTSIDE_OWNED_DIRECTORY");
                using (FileStream copy = new FileStream(backupPath, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None))
                {
                    copy.Write(original, 0, original.Length);
                    copy.Flush(true);
                }
                backupSha = Sha(ReadStrict(backupPath));
                backupVerified = String.Equals(originalSha, backupSha, StringComparison.Ordinal);
                if (!backupVerified || !String.Equals(originalSha,
                        Sha(ReadStrict(targetPath)), StringComparison.Ordinal))
                    return Report("BACKUP", ProfileDebugPersistenceCode.RejectedBeforeRuntime,
                        "BACKUP_OR_TARGET_SHA_CONFLICT", originalSha, backupSha, savedSha,
                        backupName, backupVerified, runtimeVerified, writerCalled,
                        scalarCompared, scalarDefaults, collections);
                // Durable audit breadcrumb BEFORE any real settings mutation:
                // should the game terminate during the later vanilla save,
                // the retained backup can still be found by filename.
                Log.Message(Tag + " phase=BACKUP_VERIFIED; status=READY; " +
                    "originalSha256=" + originalSha + "; backupSha256=" + backupSha +
                    "; backupFileName=" + backupName +
                    "; backupRetained=True; writerCalls=0; " +
                    "configWriter=NOT_STARTED; build=" + XpCustomizerMod.BuildId);

                // The same D1A rechecks profile SHA, metadata, live baseline,
                // editor focus and safety gates, then performs real Apply.
                ProfileDebugLiveApplyResult applied = runtime.ConfirmAndApply(session, live);
                if (applied.Code != ProfileDebugLiveApplyCode.RuntimeAppliedVerified)
                    return Report("RUNTIME", ProfileDebugPersistenceCode.RuntimeNotVerifiedNoWrite,
                        "D1A_" + applied.Code + ":" + applied.Reason,
                        originalSha, backupSha, savedSha, backupName,
                        backupVerified, false, writerCalled,
                        scalarCompared, scalarDefaults, collections);
                runtimeVerified = true;

                // Save only if the source file has not changed since backup.
                // After D1A begins, its runtime-only lock stays latched even
                // if we abort WITHOUT ever calling a disk writer.
                bool targetStable = String.Equals(originalSha, Sha(ReadStrict(targetPath)),
                    StringComparison.Ordinal);
                bool independentBackupStable = String.Equals(originalSha,
                    Sha(ReadStrict(Path.Combine(Path.GetDirectoryName(targetPath), backupName))),
                    StringComparison.Ordinal);
                if (!targetStable || !independentBackupStable || !ScribeIdle() ||
                    RuntimeRecoverySessionGate.LiveModSettingsIoBlocked)
                    return Report("BEFORE_WRITE",
                        targetStable && independentBackupStable ?
                            ProfileDebugPersistenceCode.FailedKnownUnchanged :
                            ProfileDebugPersistenceCode.ConflictNoWriter,
                        "NO_WRITER_STARTED_TARGET_OR_BACKUP_CONFLICT_OR_SCRIBE_BUSY",
                        originalSha, backupSha, savedSha, backupName,
                        backupVerified, runtimeVerified, writerCalled,
                        scalarCompared, scalarDefaults, collections);

                // Freeze the expected complete settings content before writer.
                XpCustomizerSettings expected = SettingsSnapshotService.CloneIndependent(live);
                if (!CurrentInventoryReady(expected))
                    return Report("SCHEMA", ProfileDebugPersistenceCode.FailedKnownUnchanged,
                        "XML_FIELD_INVENTORY_DRIFT_NO_WRITER_CALLED", originalSha,
                        backupSha, savedSha, backupName, backupVerified, runtimeVerified,
                        writerCalled, scalarCompared, scalarDefaults, collections);
                XpCustomizerMod mod = LoadedModManager.GetMod<XpCustomizerMod>();
                if (mod == null || !System.Object.ReferenceEquals(XpCustomizerMod.Settings, live))
                    throw new InvalidOperationException("MOD_INSTANCE_CHANGED");
                if (!String.Equals(targetPath, ModXmlRoundTripAudit.GetOwnedPath(mod.Content),
                        StringComparison.OrdinalIgnoreCase) ||
                    !ProfileDebugLiveApplySessionGuard.LiveWriteBlocked ||
                    !ProfileDebugPersistWriteGate.ReserveLiveWrite())
                    throw new InvalidOperationException("WRITER_NOT_AUTHORIZED");

                // Mark the persistence boundary as unverified BEFORE the
                // vanilla writer opens/truncates its target. A missing final
                // readback line after this entry is NEVER evidence of success.
                Log.Warning(Tag + " phase=WRITE_BEGIN; status=IN_FLIGHT_UNCERTAIN; " +
                    "backupFileName=" + backupName +
                    "; originalSha256=" + originalSha +
                    "; writer=VANILLA_MOD_WRITE_SETTINGS_ONCE; " +
                    "profileJsonWrites=0; build=" + XpCustomizerMod.BuildId);
                writerCalled = true;
                try
                {
                    mod.WriteSettings();
                }
                catch (Exception ex)
                {
                    return Report("WRITER_EXCEPTION", ProfileDebugPersistenceCode.PersistenceUncertain,
                        "WRITER_EXCEPTION_" + ex.GetType().Name, originalSha, backupSha,
                        savedSha, backupName, backupVerified, runtimeVerified, writerCalled,
                        scalarCompared, scalarDefaults, collections);
                }
                if (!ProfileDebugPersistWriteGate.LiveWriterClaimed ||
                    !ProfileDebugPersistWriteGate.LiveWriterReturned || !ScribeIdle())
                    return Report("WRITER_RETURN", ProfileDebugPersistenceCode.PersistenceUncertain,
                        "ONE_SHOT_WRITER_OR_SCRIBE_NOT_VERIFIED", originalSha,
                        backupSha, savedSha, backupName, backupVerified, runtimeVerified,
                        writerCalled, scalarCompared, scalarDefaults, collections);

                byte[] written = ReadStrict(targetPath);
                savedSha = Sha(written);
                DetachedXmlStageAudit.Inspection inspect =
                    DetachedXmlStageAudit.InspectXml(written, expected);
                DetachedXmlSemanticContract.Result semantic =
                    DetachedXmlSemanticContract.Inspect(written, expected);
                scalarCompared = inspect.ComparedScalars;
                scalarDefaults = semantic.OmittedVerified;
                collections = inspect.VerifiedCollections;
                string mismatch;
                bool memoryStable = SettingsApplyCoordinator.Equivalent(live, expected, out mismatch);
                bool backupStable = String.Equals(originalSha,
                    Sha(ReadStrict(Path.Combine(Path.GetDirectoryName(targetPath), backupName))),
                    StringComparison.Ordinal);
                if (!inspect.Valid || !semantic.Valid || !memoryStable || !backupStable ||
                    inspect.ComparedScalars + semantic.OmittedVerified !=
                        DetachedXmlSemanticContract.ScalarCount ||
                    inspect.VerifiedCollections != DetachedXmlSemanticContract.CollectionCount)
                    return Report("READBACK", ProfileDebugPersistenceCode.PersistenceUncertain,
                        "RAW_SEMANTIC_OR_MEMORY_OR_BACKUP_FAILED:" +
                        (!inspect.Valid ? inspect.Failure :
                        !semantic.Valid ? semantic.Failure : !memoryStable ? "MEMORY_CHANGED" :
                        !backupStable ? "BACKUP_CHANGED" : "INVENTORY_CHANGED"),
                        originalSha, backupSha, savedSha, backupName,
                        backupVerified, runtimeVerified, writerCalled,
                        scalarCompared, scalarDefaults, collections);

                return Report("READBACK", ProfileDebugPersistenceCode.WrittenVerifiedNoRestart,
                    "OWNED_XML_FULL_RAW_SEMANTIC_MATCH_NO_ENGINE_RELOAD",
                    originalSha, backupSha, savedSha, backupName,
                    backupVerified, runtimeVerified, writerCalled,
                    scalarCompared, scalarDefaults, collections);
            }
            catch (Exception ex)
            {
                // Only a fully pre-writer failure with independently verified
                // old bytes can be classified known unchanged.
                bool knownUnchanged = false;
                if (!ProfileDebugPersistWriteGate.LiveWriterClaimed && !writerCalled &&
                    !String.IsNullOrEmpty(targetPath) && originalSha != "NONE")
                {
                    try { knownUnchanged = String.Equals(originalSha,
                        Sha(ReadStrict(targetPath)), StringComparison.Ordinal); }
                    catch { knownUnchanged = false; }
                }
                return Report(writerCalled ? "EXCEPTION_AFTER_WRITER" : "ABORT",
                    knownUnchanged ? ProfileDebugPersistenceCode.FailedKnownUnchanged :
                    writerCalled || runtimeVerified ? ProfileDebugPersistenceCode.PersistenceUncertain :
                    ProfileDebugPersistenceCode.RejectedBeforeRuntime,
                    "EXCEPTION_" + ex.GetType().Name,
                    originalSha, backupSha, savedSha, backupName,
                    backupVerified, runtimeVerified, writerCalled,
                    scalarCompared, scalarDefaults, collections);
            }
            finally
            {
                // Critical: an exception must never leave an unclaimed permit
                // for the vanilla ModSettings dialog to consume on close.
                ProfileDebugPersistWriteGate.CancelUnclaimedLiveWrite();
                executing = false;
            }
        }

        // Detect newly added XP fields before any real live mutation or
        // disk write. Future feature additions must extend the Scribe/default
        // contract together with the snapshot/profile mapper; never silently
        // declare success while ignoring an unregistered serialized setting.
        private static bool CurrentInventoryReady(XpCustomizerSettings settings)
        {
            if (settings == null) return false;
            try
            {
                Dictionary<string, object> actual =
                    DetachedXmlStageAudit.BuildSerializedFields(settings);
                Dictionary<string, object> defaults = DetachedXmlSemanticContract.Defaults();
                if (actual.Count != DetachedXmlSemanticContract.ScalarCount +
                        DetachedXmlSemanticContract.CollectionCount ||
                    defaults.Count != DetachedXmlSemanticContract.ScalarCount) return false;
                int scalars = 0, collections = 0;
                foreach (KeyValuePair<string, object> entry in actual)
                {
                    if (entry.Value is IList || entry.Value is IDictionary)
                    {
                        if (defaults.ContainsKey(entry.Key)) return false;
                        collections++;
                    }
                    else
                    {
                        object declaredDefault;
                        if (!defaults.TryGetValue(entry.Key, out declaredDefault) ||
                            entry.Value == null || entry.Value.GetType() != declaredDefault.GetType())
                            return false;
                        scalars++;
                    }
                }
                return scalars == DetachedXmlSemanticContract.ScalarCount &&
                    collections == DetachedXmlSemanticContract.CollectionCount;
            }
            catch { return false; }
        }

        private static string OwnedPath()
        {
            XpCustomizerMod mod = LoadedModManager.GetMod<XpCustomizerMod>();
            if (mod == null || mod.GetType() != typeof(XpCustomizerMod) || mod.Content == null)
                throw new InvalidOperationException("XP_MOD_NOT_AVAILABLE");
            return ModXmlRoundTripAudit.GetOwnedPath(mod.Content);
        }

        private static bool ScribeIdle()
        {
            return Scribe.mode == LoadSaveMode.Inactive && Scribe.saver != null &&
                Scribe.saver.CurPath == null && !Scribe.saver.savingForDebug;
        }

        private static byte[] ReadStrict(string path)
        {
            if (!File.Exists(path) ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("XML_NOT_REGULAR_FILE");
            using (FileStream stream = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.Read))
            {
                if (stream.Length <= 0 || stream.Length > DetachedXmlStageAudit.MaximumBytes)
                    throw new InvalidOperationException("XML_SIZE_INVALID");
                byte[] bytes = new byte[(int)stream.Length];
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int count = stream.Read(bytes, offset, bytes.Length - offset);
                    if (count <= 0) throw new IOException("XML_READ_SHORT");
                    offset += count;
                }
                return bytes;
            }
        }

        private static string Sha(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "")
                    .ToLowerInvariant();
        }

        internal static bool OwnXmlShape(byte[] bytes)
        {
            try
            {
                XmlReaderSettings opts = new XmlReaderSettings();
                opts.DtdProcessing = DtdProcessing.Prohibit;
                opts.XmlResolver = null;
                opts.MaxCharactersInDocument = DetachedXmlStageAudit.MaximumBytes;
                XmlDocument doc = new XmlDocument();
                doc.XmlResolver = null;
                using (MemoryStream source = new MemoryStream(bytes, false))
                using (XmlReader reader = XmlReader.Create(source, opts)) doc.Load(reader);
                XmlElement root = doc.DocumentElement;
                if (root == null || root.Name != "SettingsBlock") return false;
                XmlElement settings = null;
                foreach (XmlNode child in root.ChildNodes)
                {
                    if (child.NodeType != XmlNodeType.Element) continue;
                    if (settings != null) return false;
                    settings = child as XmlElement;
                }
                return settings != null && settings.Name == "ModSettings" &&
                    settings.GetAttribute("Class") == "XpCustomizer.XpCustomizerSettings";
            }
            catch { return false; }
        }

        private static ProfileDebugPersistenceResult Report(string phase,
            ProfileDebugPersistenceCode code, string reason, string originalSha,
            string backupSha, string readbackSha, string backupName,
            bool backupVerified, bool runtimeVerified, bool writerCalled,
            int comparedScalars, int verifiedOmitted, int collections)
        {
            string msg = Tag + " revision=" + Revision + "; phase=" + phase +
                "; status=" + code + "; reason=" + (reason ?? "NONE") +
                "; originalSha256=" + originalSha + "; backupSha256=" + backupSha +
                "; readbackSha256=" + readbackSha +
                "; backupFileName=" + backupName + "; backupVerified=" + backupVerified +
                "; runtimeVerified=" + runtimeVerified +
                "; writerCalledByService=" + writerCalled +
                "; oneShotWriterClaimed=" + ProfileDebugPersistWriteGate.LiveWriterClaimed +
                "; vanillaWriterReturned=" + ProfileDebugPersistWriteGate.LiveWriterReturned +
                "; comparedXmlScalars=" + comparedScalars +
                "; verifiedOmittedDefaults=" + verifiedOmitted +
                "; verifiedCollections=" + collections +
                "; liveWriteLock=" + ProfileDebugLiveApplySessionGuard.LiveWriteBlocked +
                "; persistenceTestLock=" + ProfileDebugPersistWriteGate.LiveTestAttempted +
                "; profileJsonWrites=0; ownXmlOnly=True; otherModWritesByService=0" +
                "; fullEngineReload=NOT_RUN; crashDurability=NOT_VERIFIED" +
                "; restartDurability=NOT_VERIFIED; releaseRoute=NOT_EXERCISED_BY_DEBUG_ONLY_SERVICE" +
                "; build=" + XpCustomizerMod.BuildId;
            if (code == ProfileDebugPersistenceCode.PersistenceUncertain)
                Log.Error(msg);
            else if (code == ProfileDebugPersistenceCode.WrittenVerifiedNoRestart)
                Log.Message(msg);
            else Log.Warning(msg);
            return new ProfileDebugPersistenceResult(code, reason);
        }
    }
}
#endif
