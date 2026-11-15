using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.Biotech;

namespace XpCustomizer.Profiles
{
    internal enum ProfileProductionApplyCode
    {
        Prepared, NoChange, Rejected, Conflict, RuntimeRecoveredNoWrite,
        WrittenVerifiedNoRestart, RuntimeRecoveryRequired, PersistenceUncertain
    }

    internal sealed class ProfileProductionApplySession
    {
        internal readonly ProfileApplyGateSession GateSession;
        internal readonly string Id, Hash, Name, GameVersion;
        internal readonly DateTime CreatedUtc, UpdatedUtc;
        private readonly XpCustomizerSettings baseline;
        private bool attempted;
        internal ProfileProductionApplySession(ProfileApplyGateSession gate, ProfileDocument selected,
            string hash, XpCustomizerSettings current)
        {
            GateSession = gate;
            Id = selected.ProfileId;
            Hash = hash;
            Name = selected.DisplayName;
            GameVersion = selected.SourceGameVersion;
            CreatedUtc = selected.CreatedUtc;
            UpdatedUtc = selected.UpdatedUtc;
            baseline = SettingsSnapshotService.CloneIndependent(current);
        }
        internal bool Consume() { if (attempted) return false; attempted = true; return true; }
        internal bool SameBaseline(XpCustomizerSettings live, out string field)
        { return SettingsApplyCoordinator.Equivalent(baseline, live, out field); }
        internal bool SameProfile(ProfileStorageResult fresh)
        {
            return fresh != null && fresh.Success && fresh.Document != null &&
                String.Equals(fresh.ContentHash, Hash, StringComparison.Ordinal) &&
                fresh.Document.ProfileId == Id && fresh.Document.DisplayName == Name &&
                fresh.Document.CreatedUtc == CreatedUtc && fresh.Document.UpdatedUtc == UpdatedUtc &&
                fresh.Document.SourceGameVersion == GameVersion;
        }
    }

    internal sealed class ProfileProductionApplyResult
    {
        internal readonly ProfileProductionApplyCode Code;
        internal readonly string Reason;
        internal readonly ProfileProductionApplySession Session;
        internal bool Prepared { get { return Code == ProfileProductionApplyCode.Prepared && Session != null; } }
        internal ProfileProductionApplyResult(ProfileProductionApplyCode code, string reason,
            ProfileProductionApplySession session = null)
        { Code = code; Reason = reason ?? "NONE"; Session = session; }
    }

    /// <summary>
    /// R6-P1-D2-D3: separate Release-capable, explicit UI action. The original
    /// Mod.WriteSettings is called ONCE under a scoped permit. No custom XML
    /// commit, no profile JSON writes, no game save, no global Scribe patches.
    /// On uncertain disk/runtime outcomes lock own XML writes until restart.
    /// </summary>
    internal sealed class ProfileProductionApplyService
    {
        internal const string Revision = "R6P1D2D3-RELEASE-OWN-XML-APPLY-20261108";
        private const string Tag = "[XPC-R6P1D2D3][PROFILE_APPLY_COMMIT]";
        private static bool executing;
        private readonly ProfileFileRepository repository;
        private readonly ProfileApplyGateService gate;

        internal ProfileProductionApplyService()
        {
            repository = ProfileFileRepository.ForCurrentUser();
            gate = new ProfileApplyGateService(ProfileApplyPreflightService.ForCurrentUser(), false);
        }

        internal ProfileProductionApplyResult Prepare(ProfileApplyReviewSession selected,
            XpCustomizerSettings live)
        {
            if (selected == null || live == null ||
                !System.Object.ReferenceEquals(live, XpCustomizerMod.Settings))
                return Report("PREPARE", ProfileProductionApplyCode.Rejected,
                    "INVALID_REVIEW_OR_LIVE_SETTINGS");
            if (!selected.ContentDiffers)
                return Report("PREPARE", ProfileProductionApplyCode.NoChange, "NO_CHANGED_REVIEW");
            if (!ReadyForApply())
                return Report("PREPARE", ProfileProductionApplyCode.Rejected,
                    "PAUSED_GAME_OR_SCRIBE_OR_RECOVERY_GATE");
            try
            {
                string mismatch;
                if (!selected.BaselineMatches(live, out mismatch))
                    return Report("PREPARE", ProfileProductionApplyCode.Conflict,
                        "SETTINGS_CHANGED:" + mismatch);
                ProfileStorageResult fresh = repository.Load(selected.ProfileId);
                if (!MatchReview(fresh, selected))
                    return Report("PREPARE", ProfileProductionApplyCode.Conflict,
                        "PROFILE_FILE_OR_METADATA_CHANGED");
                ProfileApplyGateResult eligible = gate.Prepare(fresh.Document, fresh.ContentHash, live);
                if (!eligible.Prepared)
                    return Report("PREPARE", eligible.Code == ProfileApplyGateCode.NoChange ?
                        ProfileProductionApplyCode.NoChange : ProfileProductionApplyCode.Rejected,
                        "READONLY_PREFLIGHT_" + eligible.Code);
                return Report("PREPARE", ProfileProductionApplyCode.Prepared,
                    "AWAIT_EXPLICIT_CONFIRM", new ProfileProductionApplySession(
                        eligible.Session, fresh.Document, fresh.ContentHash, live));
            }
            catch (Exception ex)
            {
                return Report("PREPARE", ProfileProductionApplyCode.Rejected,
                    "EXCEPTION_" + ex.GetType().Name);
            }
        }

        internal ProfileProductionApplyResult ConfirmAndApply(
            ProfileProductionApplySession session, XpCustomizerSettings live)
        {
            if (session == null || !session.Consume() || executing)
                return Report("CONFIRM", ProfileProductionApplyCode.Rejected, "DUPLICATE_OR_BUSY");
            executing = true;
            bool started = false, runtimePass = false, writerStarted = false;
            string originalHash = "NONE", backupHash = "NONE", finalHash = "NONE", backupName = "NONE";
            string xmlPath = null;
            try
            {
                string mismatch;
                if (!ReadyForApply() || live == null ||
                    !System.Object.ReferenceEquals(live, XpCustomizerMod.Settings) ||
                    !session.SameBaseline(live, out mismatch))
                    return Report("RECHECK", ProfileProductionApplyCode.Conflict,
                        "SETTINGS_OR_GAME_CHANGED");
                ProfileApplyGateResult approved = gate.ConfirmForReadOnlyAudit(
                    session.GateSession, live, true);
                if (approved.Code != ProfileApplyGateCode.EligibleReadOnly)
                    return Report("CONSENT", ProfileProductionApplyCode.Rejected,
                        "RECHECK_" + approved.Code + ":" + approved.Reason);
                ProfileStorageResult profile = repository.Load(session.Id);
                if (!session.SameProfile(profile))
                    return Report("RECHECK", ProfileProductionApplyCode.Conflict,
                        "PROFILE_FILE_CHANGED");
                XpCustomizerSettings before = SettingsSnapshotService.CloneIndependent(live);
                XpCustomizerSettings candidate = ProfileMapper.ImportDetached(profile.Document, before);
                if (!AlignMaps(before, candidate, out mismatch) ||
                    !SettingsCandidateValidator.Validate(candidate).Accepted)
                    return Report("CANDIDATE", ProfileProductionApplyCode.Rejected,
                        "CANDIDATE_INVALID_OR_COMPARER:" + mismatch);
                if (ProfileApplySettingsComparison.PortableEquivalent(before, candidate, out mismatch))
                    return Report("CANDIDATE", ProfileProductionApplyCode.NoChange, "ALREADY_APPLIED");
                if (!ReadyForApply() || !session.SameProfile(repository.Load(session.Id)) ||
                    !session.SameBaseline(live, out mismatch))
                    return Report("LAST_RECHECK", ProfileProductionApplyCode.Conflict,
                        "LATE_SETTINGS_OR_FILE_CHANGE");

                XpCustomizerMod mod = LoadedModManager.GetMod<XpCustomizerMod>();
                if (mod == null || mod.GetType() != typeof(XpCustomizerMod))
                    return Report("XML", ProfileProductionApplyCode.Rejected, "XP_MOD_NOT_AVAILABLE");
                xmlPath = ProfileOwnedXmlEvidence.GetPath(mod.Content);
                byte[] originalBytes = ProfileOwnedXmlEvidence.ReadStrict(xmlPath);
                originalHash = OwnedXmlScanner.Sha(originalBytes);
                if (!OwnXmlShape(originalBytes))
                    return Report("XML", ProfileProductionApplyCode.Rejected,
                        "OWNED_XML_SHAPE_INVALID");

                // Independent retained backup is fully verified BEFORE any
                // runtime mutation. FileMode.CreateNew never overwrites backups.
                string backupPath = Path.Combine(Path.GetDirectoryName(xmlPath),
                    Path.GetFileName(xmlPath) + ".D2D3." + Guid.NewGuid().ToString("N") + ".bak");
                backupName = Path.GetFileName(backupPath);
                if (File.Exists(backupPath) || Directory.Exists(backupPath))
                    return Report("BACKUP", ProfileProductionApplyCode.Rejected, "BACKUP_PATH_ALREADY_USED");
                using (FileStream file = new FileStream(backupPath, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None))
                {
                    file.Write(originalBytes, 0, originalBytes.Length);
                    file.Flush(true);
                }
                backupHash = OwnedXmlScanner.Sha(ProfileOwnedXmlEvidence.ReadStrict(backupPath));
                if (backupHash != originalHash ||
                    OwnedXmlScanner.Sha(ProfileOwnedXmlEvidence.ReadStrict(xmlPath)) != originalHash)
                    return Report("BACKUP", ProfileProductionApplyCode.Conflict,
                        "BACKUP_OR_XML_CHANGED_NO_RUNTIME_APPLY");
                Log.Message(Tag + " phase=BACKUP_VERIFIED; status=READY; originalSha256=" +
                    originalHash + "; backupSha256=" + backupHash +
                    "; backupFile=" + backupName + "; retained=True; build=" + XpCustomizerMod.BuildId);

                // P0: Profile Apply overwrites selected settings. The existing
                // GeneDef values are NOT a precondition: a different mod may
                // have changed them without affecting this profile transaction.
                // Only a real Biotech transition needs a GeneDef rollback copy.
                bool curveChanged = SettingsRuntimeApplyCoordinator.CareerCurveChanged(before, candidate);
                bool biotechChanged = ModsConfig.BiotechActive &&
                    SettingsRuntimeApplyCoordinator.BiotechChanged(before, candidate);
                SettingsRuntimeRecovery.Backup runtimeBefore =
                    SettingsRuntimeRecovery.Capture(live);
                BiotechGeneService.RollbackSnapshot geneBefore = biotechChanged ?
                    BiotechGeneService.CaptureRollbackSnapshot() : null;
                Log.Message("[XPC-P0][APPLY_POLICY] biotechChanged=" + biotechChanged +
                    "; genePreAudit=SKIPPED_NON_BLOCKING; geneSnapshot=" +
                    (geneBefore == null ? "NOT_NEEDED" : "CAPTURED") +
                    "; genePostAudit=" + (biotechChanged ? "IN_RUNTIME_TRANSACTION" : "NOT_NEEDED") +
                    "; decision=PROCEED; build=" + XpCustomizerMod.BuildId);

                // A synchronous UI operation owns this latch. It never blocks
                // the normal settings writer after a VERIFIED completion.
                if (!ProfileProductionWriteGate.Begin())
                    return Report("BEGIN", ProfileProductionApplyCode.Rejected, "WRITE_GATE_BUSY");
                started = true;
                SettingsRuntimeApplyResult applied =
                    SettingsRuntimeApplyCoordinator.ApplyLiveForProductionProfile(
                        SettingsSnapshotService.Capture(candidate));
                if (applied.Status != SettingsRuntimeApplyStatus.APPLY_PASS)
                {
                    if (RuntimeRecoverySessionGate.LiveIsBlocked ||
                        applied.Status == SettingsRuntimeApplyStatus.RUNTIME_REFRESH_FAILED_UNRECOVERED)
                        return Uncertain("RUNTIME", "UNRECOVERED:" + applied.Reason,
                            originalHash, backupHash, finalHash, backupName, false);
                    if (!RollbackIntact(live, runtimeBefore, geneBefore, out mismatch) ||
                        OwnedXmlScanner.Sha(ProfileOwnedXmlEvidence.ReadStrict(xmlPath)) != originalHash ||
                        OwnedXmlScanner.Sha(ProfileOwnedXmlEvidence.ReadStrict(backupPath)) != backupHash)
                        return Uncertain("RUNTIME", "ROLLBACK_OR_XML_CHANGED:" + mismatch,
                            originalHash, backupHash, finalHash, backupName, false);
                    if (!ProfileProductionWriteGate.AbortKnownUnchanged())
                        return Uncertain("RUNTIME", "ABORT_GATE_STATE_UNCERTAIN",
                            originalHash, backupHash, finalHash, backupName, false);
                    started = false;
                    return Report("RUNTIME", ProfileProductionApplyCode.RuntimeRecoveredNoWrite,
                        "NO_XML_WRITER:" + applied.Status + ":" + applied.Reason);
                }
                runtimePass = true;
                if (!applied.UiNotified ||
                    (curveChanged && !applied.CacheInvalidated) ||
                    (biotechChanged && !applied.BiotechReapplied) ||
                    !SettingsApplyCoordinator.Equivalent(live, candidate, out mismatch))
                {
                    RuntimeRecoverySessionGate.ReportLiveFinalVerificationFailure(
                        RuntimeRecoveryFailureStage.Verify, RuntimeRecoveryGateSource.LiveTransaction);
                    return Uncertain("RUNTIME_VERIFY", "LIVE_SETTINGS_OR_UI:" + mismatch,
                        originalHash, backupHash, finalHash, backupName, false);
                }
                // P0: Never re-audit unrelated GeneDefs after the transaction.
                // When Biotech actually changed, LiveRefreshActions verifies
                // the new values BEFORE ApplyCore commits, so verification
                // failures flow through its existing compensation/rollback.
                if (!ProfileOwnedXmlEvidence.IsScribeIdle() ||
                    originalHash != OwnedXmlScanner.Sha(ProfileOwnedXmlEvidence.ReadStrict(xmlPath)) ||
                    backupHash != OwnedXmlScanner.Sha(ProfileOwnedXmlEvidence.ReadStrict(backupPath)) ||
                    !System.Object.ReferenceEquals(XpCustomizerMod.Settings, live) ||
                    !ProfileProductionWriteGate.Reserve())
                    return Uncertain("BEFORE_WRITE", "OWN_XML_CHANGED_OR_WRITER_NOT_AUTHORIZED",
                        originalHash, backupHash, finalHash, backupName, false);

                // This is not success. A crash after this point is uncertain;
                // the independent backup and original SHA remain in the log.
                Log.Warning(Tag + " phase=WRITE_BEGIN; status=IN_FLIGHT_UNCERTAIN; " +
                    "backupFile=" + backupName + "; originalSha256=" + originalHash +
                    "; writer=VANILLA_MOD_WRITE_SETTINGS_ONCE; build=" + XpCustomizerMod.BuildId);
                writerStarted = true;
                try { mod.WriteSettings(); }
                catch (Exception ex)
                {
                    return Uncertain("WRITER", "WRITER_EXCEPTION_" + ex.GetType().Name,
                        originalHash, backupHash, finalHash, backupName, true);
                }
                if (!ProfileProductionWriteGate.WriterReturned ||
                    !ProfileOwnedXmlEvidence.IsScribeIdle())
                    return Uncertain("WRITER_RETURN", "WRITER_NOT_CONFIRMED",
                        originalHash, backupHash, finalHash, backupName, true);
                byte[] saved = ProfileOwnedXmlEvidence.ReadStrict(xmlPath);
                finalHash = OwnedXmlScanner.Sha(saved);
                if (!FullRawXmlMatch(saved, candidate) ||
                    backupHash != OwnedXmlScanner.Sha(ProfileOwnedXmlEvidence.ReadStrict(backupPath)) ||
                    !SettingsApplyCoordinator.Equivalent(live, candidate, out mismatch))
                    return Uncertain("READBACK", "XML_SEMANTIC_OR_MEMORY_OR_BACKUP_MISMATCH",
                        originalHash, backupHash, finalHash, backupName, true);
                if (!ProfileProductionWriteGate.CompleteVerified())
                    return Uncertain("FINISH", "ONE_SHOT_WRITER_STATE_CHANGED",
                        originalHash, backupHash, finalHash, backupName, true);
                started = false;
                return Report("READBACK", ProfileProductionApplyCode.WrittenVerifiedNoRestart,
                    "OWN_XML_FULL_SEMANTIC_VERIFIED; xmlSha256=" + finalHash +
                    "; backupSha256=" + backupHash + "; backupFile=" + backupName +
                    "; profileIdPrefix=" + session.Id.Substring(0, 8) +
                    "; profileJsonShaPrefix=" + session.Hash.Substring(0, 12) +
                    "; restartDurability=NOT_VERIFIED");
            }
            catch (Exception ex)
            {
                return started || runtimePass || writerStarted ?
                    Uncertain("EXCEPTION", ex.GetType().Name, originalHash,
                        backupHash, finalHash, backupName, writerStarted) :
                    Report("ABORT", ProfileProductionApplyCode.Rejected,
                        "EXCEPTION_BEFORE_MUTATION_" + ex.GetType().Name);
            }
            finally
            {
                if (started) ProfileProductionWriteGate.LatchUncertain();
                ProfileProductionWriteGate.CancelUnclaimed();
                executing = false;
            }
        }

        private static ProfileProductionApplyResult Uncertain(string phase, string reason,
            string original, string backup, string written, string backupName, bool writerStarted)
        {
            ProfileProductionWriteGate.LatchUncertain();
            return Report(phase, writerStarted ? ProfileProductionApplyCode.PersistenceUncertain :
                ProfileProductionApplyCode.RuntimeRecoveryRequired,
                reason + "; originalSha256=" + original + "; backupSha256=" + backup +
                "; readbackSha256=" + written + "; backupFile=" + backupName +
                "; ACTION=EXIT_WITHOUT_SAVING_RESTART");
        }

        private static bool RollbackIntact(XpCustomizerSettings live,
            SettingsRuntimeRecovery.Backup previous,
            BiotechGeneService.RollbackSnapshot genes, out string mismatch)
        {
            mismatch = "NONE";
            if (!SettingsApplyCoordinator.Equivalent(live, previous.Values, out mismatch))
                return false;
            for (int i = 0; i < previous.Fields.Length; i++)
            {
                if (!previous.Fields[i].FieldType.IsValueType &&
                    !System.Object.ReferenceEquals(previous.Fields[i].GetValue(live), previous.References[i]))
                { mismatch = "REFERENCE:" + previous.Fields[i].Name; return false; }
            }
            return genes == null || genes.MatchesCurrent(out mismatch);
        }

        private static bool OwnXmlShape(byte[] bytes)
        {
            try
            {
                var settings = new System.Xml.XmlReaderSettings();
                settings.DtdProcessing = System.Xml.DtdProcessing.Prohibit;
                settings.XmlResolver = null;
                settings.MaxCharactersInDocument = OwnedXmlScanner.MaximumBytes;
                var document = new System.Xml.XmlDocument();
                document.XmlResolver = null;
                using (var data = new MemoryStream(bytes, false))
                using (var reader = System.Xml.XmlReader.Create(data, settings))
                    document.Load(reader);
                System.Xml.XmlElement root = document.DocumentElement;
                if (root == null || root.Name != "SettingsBlock" ||
                    root.HasAttributes) return false;
                int childCount = 0;
                foreach (System.Xml.XmlNode child in root.ChildNodes)
                {
                    System.Xml.XmlElement element = child as System.Xml.XmlElement;
                    if (element != null)
                    {
                        childCount++;
                        if (element.Name != "ModSettings" || element.Attributes.Count != 1 ||
                            element.GetAttribute("Class") != "XpCustomizer.XpCustomizerSettings")
                            return false;
                    }
                    else if (child.NodeType != System.Xml.XmlNodeType.Whitespace &&
                        child.NodeType != System.Xml.XmlNodeType.SignificantWhitespace &&
                        child.NodeType != System.Xml.XmlNodeType.Comment)
                        return false;
                }
                return childCount == 1;
            }
            catch { return false; }
        }

        private static bool FullRawXmlMatch(byte[] xml, XpCustomizerSettings expected)
        {
            int emitted, omitted, collections;
            string failure;
            return ProfileOwnedXmlEvidence.MatchesLoaded(xml, expected,
                out emitted, out omitted, out collections, out failure);
        }

        private static bool ReadyForApply()
        {
            if (ProfileWriteSessionGate.IsBlocked || ProfileProductionWriteGate.IsUncertain ||
                ProfileProductionWriteGate.IsBusy || RuntimeRecoverySessionGate.LiveIsBlocked ||
                !ProfileOwnedXmlEvidence.IsScribeIdle() || Event.current == null ||
                Current.Game == null || Find.TickManager == null || !Find.TickManager.Paused ||
                (ModsConfig.BiotechActive && DefDatabase<GeneDef>.AllDefsListForReading == null))
                return false;
#if DEBUG
            if (ProfileDebugLiveApplySessionGuard.LiveWriteBlocked ||
                ProfileDebugPersistWriteGate.LiveTestAttempted) return false;
#endif
            return true;
        }

        private static bool MatchReview(ProfileStorageResult file, ProfileApplyReviewSession review)
        {
            return file != null && file.Success && file.Document != null &&
                file.ContentHash == review.ExpectedHash &&
                file.Document.ProfileId == review.ProfileId &&
                file.Document.DisplayName == review.DisplayName &&
                file.Document.UpdatedUtc == review.UpdatedUtc &&
                file.Document.SourceGameVersion == review.SourceGameVersion;
        }

        private static bool AlignMaps(XpCustomizerSettings baseline,
            XpCustomizerSettings candidate, out string unsafeField)
        {
            unsafeField = "NONE";
            foreach (ProfileFieldSpec spec in ProfileSchemaV1.GetFields())
            {
                if (spec.FieldType == typeof(Dictionary<string, float>))
                {
                    Dictionary<string, float> src = spec.SettingField.GetValue(baseline) as Dictionary<string, float>;
                    Dictionary<string, float> dst = spec.SettingField.GetValue(candidate) as Dictionary<string, float>;
                    if (src == null || dst == null || !Ordinal(src.Comparer) || !Ordinal(dst.Comparer))
                    { unsafeField = spec.Section + "." + spec.Name; return false; }
                    spec.SettingField.SetValue(candidate, new Dictionary<string, float>(dst, src.Comparer));
                }
                else if (spec.FieldType == typeof(Dictionary<string, bool>))
                {
                    Dictionary<string, bool> src = spec.SettingField.GetValue(baseline) as Dictionary<string, bool>;
                    Dictionary<string, bool> dst = spec.SettingField.GetValue(candidate) as Dictionary<string, bool>;
                    if (src == null || dst == null || !Ordinal(src.Comparer) || !Ordinal(dst.Comparer))
                    { unsafeField = spec.Section + "." + spec.Name; return false; }
                    spec.SettingField.SetValue(candidate, new Dictionary<string, bool>(dst, src.Comparer));
                }
            }
            return true;
        }
        private static bool Ordinal(IEqualityComparer<string> comparer)
        {
            return System.Object.ReferenceEquals(comparer, StringComparer.Ordinal) ||
                System.Object.ReferenceEquals(comparer, EqualityComparer<string>.Default);
        }

        private static ProfileProductionApplyResult Report(string phase,
            ProfileProductionApplyCode code, string reason,
            ProfileProductionApplySession session = null)
        {
            string message = Tag + " revision=" + Revision + "; phase=" + phase +
                "; status=" + code + "; reason=" + reason +
                "; source=USER_EXPLICIT; ownXmlOnly=True; profileJsonWrites=0" +
                "; otherModWrites=0; settingsXmlBlocked=" +
                ProfileProductionWriteGate.IsUncertain + "; build=" + XpCustomizerMod.BuildId;
            if (code == ProfileProductionApplyCode.PersistenceUncertain ||
                code == ProfileProductionApplyCode.RuntimeRecoveryRequired) Log.Error(message);
            else if (code == ProfileProductionApplyCode.Rejected ||
                code == ProfileProductionApplyCode.Conflict ||
                code == ProfileProductionApplyCode.RuntimeRecoveredNoWrite) Log.Warning(message);
            else Log.Message(message);
            return new ProfileProductionApplyResult(code, reason, session);
        }
    }
}
