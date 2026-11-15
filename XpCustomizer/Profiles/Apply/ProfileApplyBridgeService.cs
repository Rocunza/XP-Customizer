#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Verse;

namespace XpCustomizer.Profiles
{
    // R6-P1-D2-B1. A DEBUG-ONLY, DETACHED rehearsal; never a permission to apply live.
    internal enum ProfileApplyBridgeCode
    {
        Prepared, NoChange, Rejected, Conflict, PendingEdit, Uncertain,
        Duplicate, TestApplyPass, RollbackPass, RecoveryRequired
    }

    internal sealed class ProfileApplyBridgeSession
    {
        internal readonly ProfileApplyGateSession GateSession;
        internal readonly string Id;
        internal readonly string Hash;
        internal readonly string Name;
        internal readonly DateTime CreatedUtc;
        internal readonly DateTime UpdatedUtc;
        internal readonly string GameVersion;
        private readonly XpCustomizerSettings baseline;
        private bool attempted;

        internal ProfileApplyBridgeSession(ProfileApplyGateSession gateSession,
            ProfileDocument selected, string hash, XpCustomizerSettings live)
        {
            if (gateSession == null || selected == null || live == null)
                throw new ArgumentNullException("gateSession/selected/live");
            GateSession = gateSession;
            Id = selected.ProfileId;
            Hash = hash;
            Name = selected.DisplayName;
            CreatedUtc = selected.CreatedUtc;
            UpdatedUtc = selected.UpdatedUtc;
            GameVersion = selected.SourceGameVersion;
            baseline = SettingsSnapshotService.CloneIndependent(live);
        }

        internal bool Consume()
        {
            if (attempted) return false;
            attempted = true; // all paths are one-shot, including exceptions and missing consent
            return true;
        }

        internal bool BaselineMatches(XpCustomizerSettings live, out string field)
        { return SettingsApplyCoordinator.Equivalent(baseline, live, out field); }
    }

    internal sealed class ProfileApplyBridgeResult
    {
        internal readonly ProfileApplyBridgeCode Code;
        internal readonly string Reason;
        internal readonly string FirstMismatch;
        internal readonly ProfileApplyBridgeSession Session;
        internal readonly string BaselineFingerprint;
        internal readonly string CandidateFingerprint;
        internal bool Prepared { get { return Code == ProfileApplyBridgeCode.Prepared && Session != null; } }

        internal ProfileApplyBridgeResult(ProfileApplyBridgeCode code, string reason, string mismatch,
            ProfileApplyBridgeSession session, string baselineHash, string candidateHash)
        {
            Code = code;
            Reason = reason ?? "NONE";
            FirstMismatch = mismatch ?? "NOT_CHECKED";
            Session = session;
            BaselineFingerprint = baselineHash ?? "NOT_COMPUTED";
            CandidateFingerprint = candidateHash ?? "NOT_COMPUTED";
        }
    }

    internal enum ProfileApplyBridgeFakeFault { None, Cache, Biotech, Ui }

    /// <summary>
    /// The only refresh adapter accepted by the B1 bridge. It cannot touch
    /// the live game's caches, GeneDefs or UI; these values are in-memory fakes.
    /// </summary>
    internal sealed class ProfileApplyBridgeFakeActions : ISettingsRuntimeRefreshActions
    {
        internal readonly List<string> Calls = new List<string>();
        internal ProfileApplyBridgeFakeFault FailAt;
        internal bool FailGeneRestore;
        internal bool FailUiRecovery;
        internal bool FailCapture;
        internal Action OnCache;
        internal int FakeGeneValue = 135;
        internal int RestoreAttempts;
        internal int CacheCalls;
        internal int UiCalls;
        internal XpCustomizerSettings DetachedTarget;
        internal XpCustomizerSettings BeforeTarget;
        private object[] beforeReferences;
        private bool faulted;

        internal void Attach(XpCustomizerSettings target)
        {
            DetachedTarget = target;
            BeforeTarget = SettingsSnapshotService.CloneIndependent(target);
            FieldInfo[] fields = SettingsSnapshotService.GetDeclaredSettingsFieldsForAudit();
            beforeReferences = new object[fields.Length];
            for (int i = 0; i < fields.Length; i++)
                beforeReferences[i] = fields[i].GetValue(target);
        }

        internal bool OriginalReferencesRestored()
        {
            if (DetachedTarget == null || beforeReferences == null) return false;
            FieldInfo[] fields = SettingsSnapshotService.GetDeclaredSettingsFieldsForAudit();
            if (beforeReferences.Length != fields.Length) return false;
            for (int i = 0; i < fields.Length; i++)
                if (!fields[i].FieldType.IsValueType &&
                    !object.ReferenceEquals(beforeReferences[i], fields[i].GetValue(DetachedTarget)))
                    return false;
            return true;
        }

        public object CaptureBiotechRollbackState()
        {
            Calls.Add("CAPTURE_GENE");
            if (FailCapture) throw new InvalidOperationException("B1_FAKE_CAPTURE");
            return FakeGeneValue;
        }

        public bool RestoreBiotechRollbackState(object value, out string reason)
        {
            Calls.Add("RESTORE_GENE");
            RestoreAttempts++;
            if (FailGeneRestore) { reason = "INJECTED_RESTORE_FAILURE"; return false; }
            FakeGeneValue = (int)value;
            reason = "NONE";
            return true;
        }

        public void InvalidateCurveCache()
        {
            Calls.Add("CACHE");
            CacheCalls++;
            if (OnCache != null) OnCache();
            if (FailAt == ProfileApplyBridgeFakeFault.Cache && !faulted)
            { faulted = true; throw new InvalidOperationException("B1_FAKE_CACHE"); }
        }

        public void ReapplyBiotech()
        {
            Calls.Add("BIOTECH");
            FakeGeneValue = 911; // represents an external mutation in a fake only
            if (FailAt == ProfileApplyBridgeFakeFault.Biotech && !faulted)
            { faulted = true; throw new InvalidOperationException("B1_FAKE_BIOTECH"); }
        }

        public void NotifyAllEditors()
        {
            Calls.Add("UI");
            UiCalls++;
            if ((FailAt == ProfileApplyBridgeFakeFault.Ui && !faulted) ||
                (FailUiRecovery && faulted))
            { faulted = true; throw new InvalidOperationException("B1_FAKE_UI"); }
        }
    }

    /// <summary>
    /// Approval->detached candidate->fake transaction boundary. This class,
    /// including every entry point and fake action, is excluded from Release.
    /// In particular EligibleReadOnly is NOT converted to a live apply token.
    /// </summary>
    internal sealed class ProfileApplyBridgeService
    {
        internal const string Revision = "R6P1D2B1-DETACHED-BRIDGE-20261107";
        private const string FingerprintId = "00000000-0000-4000-8000-000000000001";
        private static readonly DateTime FingerprintDate =
            new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);
        private static bool executing;
        private readonly Func<ProfileFileRepository> repositoryFactory;
        private readonly Func<ProfileEditorReadinessResult> readiness;
        private readonly ProfileApplyGateService gate;
        private readonly bool logging;
        private ProfileApplyBridgeSession auditContext;

        internal ProfileApplyBridgeService(Func<ProfileFileRepository> repo,
            Func<ProfileEditorReadinessResult> editor, bool log = true)
        {
            if (repo == null || editor == null) throw new ArgumentNullException("repo/editor");
            repositoryFactory = repo;
            readiness = editor;
            gate = new ProfileApplyGateService(new ProfileApplyPreflightService(repo, editor, false), false);
            logging = log;
        }

        internal ProfileApplyBridgeResult Prepare(ProfileDocument selected, string hash,
            XpCustomizerSettings live)
        {
            if (executing) return Report("PREPARE", ProfileApplyBridgeCode.Rejected,
                "REENTRANT_BRIDGE", null, null, null, null);
            ProfileApplyGateResult result = gate.Prepare(selected, hash, live);
            if (result.Code == ProfileApplyGateCode.NoChange)
                return Report("PREPARE", ProfileApplyBridgeCode.NoChange, "NO_PORTABLE_CHANGE",
                    null, null, null, null);
            if (!result.Prepared)
                return Report("PREPARE", MapGate(result), result.Reason, null, null, null, null);
            try
            {
                ProfileApplyBridgeSession session = new ProfileApplyBridgeSession(
                    result.Session, selected, hash, live);
                string mismatch;
                if (!session.BaselineMatches(live, out mismatch))
                    return Report("PREPARE", ProfileApplyBridgeCode.Conflict, "BASELINE_CHANGED",
                        null, mismatch, null, null);
                return Report("PREPARE", ProfileApplyBridgeCode.Prepared,
                    "EXPLICIT_USER_CONFIRMATION_REQUIRED", session, null, null, null);
            }
            catch (Exception ex)
            {
                return Report("PREPARE", ProfileApplyBridgeCode.Rejected,
                    "SNAPSHOT:" + ex.GetType().Name, null, null, null, null);
            }
        }

        internal ProfileApplyBridgeResult ConfirmAndSimulate(ProfileApplyBridgeSession session,
            XpCustomizerSettings live, bool explicitUserConfirmation,
            ProfileApplyBridgeFakeActions fake, bool biotechAvailable, int failAfterDataField = -1)
        {
            if (session == null)
                return Report("CONSENT", ProfileApplyBridgeCode.Rejected, "NULL_SESSION",
                    null, null, null, null);
            if (!session.Consume())
                return Report("CONSENT", ProfileApplyBridgeCode.Duplicate, "ONE_SHOT_CONSUMED",
                    null, null, null, null);
            if (executing)
                return Report("CONSENT", ProfileApplyBridgeCode.Rejected, "REENTRANT_BRIDGE",
                    null, null, null, null);
            executing = true;
            auditContext = session;
            try
            {
                // The D2-A1 and D1 tokens are consumed even when consent is denied.
                ProfileApplyGateResult confirmed = gate.ConfirmForReadOnlyAudit(
                    session.GateSession, live, explicitUserConfirmation);
                if (confirmed.Code != ProfileApplyGateCode.EligibleReadOnly)
                    return Report("CONSENT", MapGate(confirmed), confirmed.Reason,
                        null, null, null, null);
                if (fake == null)
                    return Report("RECHECK", ProfileApplyBridgeCode.Rejected, "FAKE_ACTIONS_REQUIRED",
                        null, null, null, null);
                if (ProfileWriteSessionGate.IsBlocked)
                    return Report("RECHECK", ProfileApplyBridgeCode.RecoveryRequired,
                        "PROFILE_RECOVERY_LOCK", null, null, null, null);

                ProfileEditorReadinessResult editor = readiness();
                if (editor == null || editor.Code != ProfileEditorReadinessCode.Ready)
                    return Report("RECHECK", editor != null &&
                        editor.Code == ProfileEditorReadinessCode.PendingEdit ?
                        ProfileApplyBridgeCode.PendingEdit : ProfileApplyBridgeCode.Uncertain,
                        "EDITOR_NOT_READY", null, null, null, null);
                ProfileStorageResult loaded = repositoryFactory().Load(session.Id);
                if (loaded == null || !loaded.Success || loaded.Document == null ||
                    !String.Equals(loaded.ContentHash, session.Hash, StringComparison.Ordinal))
                    return Report("RECHECK", ProfileApplyBridgeCode.Conflict,
                        "PROFILE_FILE_CHANGED", null, null, null, null);
                ProfileDocument doc = loaded.Document;
                ProfileJsonCodec.Validate(doc);
                if (!String.Equals(doc.ProfileId, session.Id, StringComparison.Ordinal) ||
                    !String.Equals(doc.DisplayName, session.Name, StringComparison.Ordinal) ||
                    doc.CreatedUtc != session.CreatedUtc || doc.UpdatedUtc != session.UpdatedUtc ||
                    !String.Equals(doc.SourceGameVersion, session.GameVersion, StringComparison.Ordinal))
                    return Report("RECHECK", ProfileApplyBridgeCode.Conflict,
                        "PROFILE_METADATA_CHANGED", null, null, null, null);
                string mismatch;
                if (!session.BaselineMatches(live, out mismatch))
                    return Report("RECHECK", ProfileApplyBridgeCode.Conflict,
                        "LIVE_BASELINE_CHANGED", null, mismatch, null, null);

                XpCustomizerSettings before = SettingsSnapshotService.CloneIndependent(live);
                XpCustomizerSettings candidate = ProfileMapper.ImportDetached(doc, before);
                // Normalise only known semantically equivalent portable comparer
                // identities. The strict transaction/rollback comparator is untouched.
                string unsupported;
                if (!AlignPortableMapComparers(before, candidate, out unsupported))
                    return Report("CANDIDATE", ProfileApplyBridgeCode.Rejected,
                        "UNSAFE_PORTABLE_MAP_COMPARER", null, unsupported, null, null);
                if (!SettingsCandidateValidator.Validate(candidate).Accepted)
                    return Report("CANDIDATE", ProfileApplyBridgeCode.Rejected,
                        "INVALID_CANDIDATE", null, null, null, null);
                string firstPortable;
                if (ProfileApplySettingsComparison.PortableEquivalent(before, candidate, out firstPortable))
                    return Report("CANDIDATE", ProfileApplyBridgeCode.NoChange,
                        "CHANGES_DISAPPEARED", null, null, null, null);
                string baseHash = Fingerprint(before);
                string candidateHash = Fingerprint(candidate);

                // Last read-only check on the same call stack, immediately
                // before any detached transaction effects are invoked.
                if (ProfileWriteSessionGate.IsBlocked ||
                    !IsReady(readiness()) || !session.BaselineMatches(live, out mismatch))
                    return Report("ABORT", ProfileApplyBridgeCode.Conflict,
                        "LATE_LIVE_CONFLICT_OR_EDITOR", null, mismatch, baseHash, candidateHash);
                ProfileStorageResult last = repositoryFactory().Load(session.Id);
                if (last == null || !last.Success || last.ContentHash != session.Hash)
                    return Report("ABORT", ProfileApplyBridgeCode.Conflict,
                        "LATE_FILE_CONFLICT", null, firstPortable, baseHash, candidateHash);

                // The only target is a new, unshared XpCustomizerSettings object.
                XpCustomizerSettings detachedTarget = SettingsSnapshotService.CloneIndependent(before);
                fake.Attach(detachedTarget);
                SettingsRuntimeApplyResult applied =
                    SettingsRuntimeApplyCoordinator.ApplyDetachedForAuditWithDataFault(
                        detachedTarget, SettingsSnapshotService.Capture(candidate), fake,
                        biotechAvailable, failAfterDataField);
                // The real settings and selected file must still be unchanged.
                if (!session.BaselineMatches(live, out mismatch))
                    return Report("VERIFY", ProfileApplyBridgeCode.Conflict,
                        "LIVE_CHANGED_DURING_FAKE", null, mismatch, baseHash, candidateHash);
                ProfileStorageResult verified = repositoryFactory().Load(session.Id);
                if (verified == null || !verified.Success || verified.ContentHash != session.Hash)
                    return Report("VERIFY", ProfileApplyBridgeCode.Conflict,
                        "FILE_CHANGED_DURING_FAKE", null, firstPortable, baseHash, candidateHash);

                if (applied.Status == SettingsRuntimeApplyStatus.APPLY_PASS)
                {
                    if (!SettingsApplyCoordinator.Equivalent(detachedTarget, candidate, out mismatch))
                        return Report("VERIFY", ProfileApplyBridgeCode.RecoveryRequired,
                            "DETACHED_RESULT_MISMATCH", null, mismatch, baseHash, candidateHash);
                    return Report("VERIFY", ProfileApplyBridgeCode.TestApplyPass,
                        "FAKE_TRANSACTION_ONLY", null, firstPortable, baseHash, candidateHash);
                }
                if (applied.Status == SettingsRuntimeApplyStatus.RUNTIME_REFRESH_ROLLBACK_PASS ||
                    (applied.Status == SettingsRuntimeApplyStatus.APPLY_REJECTED &&
                     applied.Reason.StartsWith("DATA_APPLY_ROLLBACK_PASS", StringComparison.Ordinal)))
                {
                    if (!SettingsApplyCoordinator.Equivalent(detachedTarget, before, out mismatch) ||
                        !fake.OriginalReferencesRestored())
                        return Report("VERIFY", ProfileApplyBridgeCode.RecoveryRequired,
                            "DETACHED_ROLLBACK_MISMATCH_OR_REFERENCES", null, mismatch, baseHash, candidateHash);
                    return Report("VERIFY", ProfileApplyBridgeCode.RollbackPass,
                        applied.Reason, null, firstPortable, baseHash, candidateHash);
                }
                if (applied.Status == SettingsRuntimeApplyStatus.RUNTIME_REFRESH_FAILED_UNRECOVERED)
                    return Report("ABORT", ProfileApplyBridgeCode.RecoveryRequired,
                        applied.Reason, null, firstPortable, baseHash, candidateHash);
                return Report("ABORT", ProfileApplyBridgeCode.Rejected,
                    "FAKE_TRANSACTION:" + applied.Status + ":" + applied.Reason,
                    null, firstPortable, baseHash, candidateHash);
            }
            catch (ProfileContractException ex)
            {
                return Report("ABORT", ProfileApplyBridgeCode.Rejected,
                    "PROFILE:" + ex.Code, null, null, null, null);
            }
            catch (Exception ex)
            {
                return Report("ABORT", ProfileApplyBridgeCode.Rejected,
                    "EXCEPTION:" + ex.GetType().Name, null, null, null, null);
            }
            finally
            {
                auditContext = null;
                executing = false;
            }
        }

        internal static bool AlignPortableMapComparers(XpCustomizerSettings baseline,
            XpCustomizerSettings candidate, out string unsafeField)
        {
            unsafeField = "NONE";
            if (baseline == null || candidate == null) { unsafeField = "NULL"; return false; }
            foreach (ProfileFieldSpec spec in ProfileSchemaV1.GetFields())
            {
                if (spec.FieldType == typeof(Dictionary<string, float>))
                {
                    Dictionary<string, float> src = spec.SettingField.GetValue(baseline) as Dictionary<string, float>;
                    Dictionary<string, float> dst = spec.SettingField.GetValue(candidate) as Dictionary<string, float>;
                    if (src == null || dst == null || !SafeOrdinal(src.Comparer) || !SafeOrdinal(dst.Comparer))
                    { unsafeField = spec.Section + "." + spec.Name; return false; }
                    spec.SettingField.SetValue(candidate,
                        new Dictionary<string, float>(dst, src.Comparer));
                }
                else if (spec.FieldType == typeof(Dictionary<string, bool>))
                {
                    Dictionary<string, bool> src = spec.SettingField.GetValue(baseline) as Dictionary<string, bool>;
                    Dictionary<string, bool> dst = spec.SettingField.GetValue(candidate) as Dictionary<string, bool>;
                    if (src == null || dst == null || !SafeOrdinal(src.Comparer) || !SafeOrdinal(dst.Comparer))
                    { unsafeField = spec.Section + "." + spec.Name; return false; }
                    spec.SettingField.SetValue(candidate,
                        new Dictionary<string, bool>(dst, src.Comparer));
                }
            }
            return true;
        }

        private static bool SafeOrdinal(IEqualityComparer<string> comparer)
        {
            return object.ReferenceEquals(comparer, StringComparer.Ordinal) ||
                object.ReferenceEquals(comparer, EqualityComparer<string>.Default);
        }

        private static bool IsReady(ProfileEditorReadinessResult result)
        { return result != null && result.Code == ProfileEditorReadinessCode.Ready; }

        private static ProfileApplyBridgeCode MapGate(ProfileApplyGateResult gateResult)
        {
            if (gateResult.Code == ProfileApplyGateCode.Duplicate) return ProfileApplyBridgeCode.Duplicate;
            if (gateResult.Code == ProfileApplyGateCode.Conflict) return ProfileApplyBridgeCode.Conflict;
            if (gateResult.Code == ProfileApplyGateCode.NoChange) return ProfileApplyBridgeCode.NoChange;
            if (gateResult.Reason.Contains("Conflict")) return ProfileApplyBridgeCode.Conflict;
            if (gateResult.Reason.Contains("PendingEdit")) return ProfileApplyBridgeCode.PendingEdit;
            if (gateResult.Reason.Contains("Uncertain")) return ProfileApplyBridgeCode.Uncertain;
            if (gateResult.Reason.Contains("RecoveryRequired")) return ProfileApplyBridgeCode.RecoveryRequired;
            return ProfileApplyBridgeCode.Rejected;
        }

        private static string Fingerprint(XpCustomizerSettings settings)
        {
            ProfileDocument doc = ProfileMapper.Export(settings, "B1_FINGERPRINT",
                "1.6", FingerprintDate, FingerprintId);
            byte[] bytes = Encoding.UTF8.GetBytes(ProfileJsonCodec.Serialize(doc));
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private ProfileApplyBridgeResult Report(string phase, ProfileApplyBridgeCode code,
            string reason, ProfileApplyBridgeSession session, string mismatch,
            string baselineHash, string candidateHash)
        {
            ProfileApplyBridgeResult result = new ProfileApplyBridgeResult(code, reason,
                mismatch, session, baselineHash, candidateHash);
            if (logging)
            {
                ProfileApplyBridgeSession logged = session ?? auditContext;
                string profileId = logged == null ? "NONE" :
                    logged.Id.Substring(0, Math.Min(8, logged.Id.Length));
                string profileHash = logged == null || logged.Hash == null ? "NONE" :
                    logged.Hash.Substring(0, Math.Min(8, logged.Hash.Length));
                string prefix = "[XPC-R6P1D2B1][PROFILE_APPLY_BRIDGE] revision=" + Revision +
                    "; phase=" + phase + "; status=" + code + "; reason=" + result.Reason +
                    "; profileIdPrefix=" + profileId + "; selectedHashPrefix=" + profileHash +
                    "; baselineFingerprint=" + result.BaselineFingerprint +
                    "; candidateFingerprint=" + result.CandidateFingerprint +
                    "; firstMismatchField=" + result.FirstMismatch +
                    "; consentIsAuditOnly=True; testEffects=DETACHED_FAKE_ONLY; " +
                    "liveSettingsWrites=0; geneDefWrites=0; pawnWrites=0; liveCacheWrites=0; " +
                    "scribeCalls=0; userProfileWrites=0; productionApply=NOT_EXERCISED_BY_DETACHED_BRIDGE; build=" + XpCustomizerMod.BuildId;
                if (code == ProfileApplyBridgeCode.RecoveryRequired)
                    Log.Error(prefix);
                else
                    Log.Message(prefix);
            }
            return result;
        }
    }
}
#endif
