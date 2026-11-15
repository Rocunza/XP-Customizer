#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// R6-P1-D2-C4-B2-C2: one explicit DebugAction only, receiving an already
    /// verified *private temp stage* from DetachedXmlStageAudit. No real mod
    /// Config XML enters this loader, and no profile Apply or Harmony hook runs.
    /// CrossRef/PostLoad internals can Log.Error and continue; a bounded public
    /// Verse.Log queue observation must accompany successful values. This
    /// does not prove absence of any suppressed/unrecorded engine error.
    /// </summary>
    internal static class DetachedVerseLoadAudit
    {
        internal const string Revision = "R6P1D2C4B2C2-DETACHED-VERSE-LOG-20261107";
        internal const string Tag = "[XPC-R6P1D2C4B2C2][DETACHED_VERSE_LOAD_AUDIT]";
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags PublicInstance =
            BindingFlags.Instance | BindingFlags.Public;
        private static XpCustomizerSettings observedRoot;
        private static int crossRefCompleted;
        private static int postLoadCompleted;

        internal sealed class Report
        {
            internal string Status = "REJECTED";
            internal string Failure = "NOT_STARTED";
            internal bool ValuesMatch;
            internal bool LoadingVarsCaptured;
            internal bool PostLoadCaptured;
            internal int ComparedFields;
            internal string LoadingVarsDifference = "NOT_CHECKED";
            internal string PostLoadDifference = "NOT_CHECKED";
            internal int CrossRefCompleted;
            internal int PostLoadCompleted;
            internal bool PreflightIdle;
            internal bool FinalIdle;
            internal bool StageFileStable;
            internal bool LoadedExactType;
            internal int InitLoadingCalls;
            internal int DeepLookCalls;
            internal int FinalizeLoadingCalls;
            internal bool FinalizeReturned;
            internal bool LogCaptureReliable;
            internal bool LogAnchorPreserved;
            internal string LogObservationReason = "NOT_OBSERVED";
            internal int LogBeforeCount = -1;
            internal int LogAfterCount = -1;
            internal int LogNewErrorEntries;
            internal int LogNewWarningEntries;
            internal int LogNewMessageEntries;
            internal int LogRepeatDelta;
            internal bool LogRepeatDeltaDetected;
        }

        // This hook is called from the end of XpCustomizerSettings.ExposeData
        // only for the isolated root installed after LoadingVars. No settings
        // mutation and no log interception are performed here.
        internal static void ObservePhaseCompleted(XpCustomizerSettings instance,
            LoadSaveMode phase)
        {
            if (observedRoot == null || !Object.ReferenceEquals(instance, observedRoot))
                return;
            if (phase == LoadSaveMode.ResolvingCrossRefs) crossRefCompleted++;
            else if (phase == LoadSaveMode.PostLoadInit) postLoadCompleted++;
        }

        internal static Report Execute(string stagePath, string expectedHash,
            long expectedBytes, XpCustomizerSettings expected)
        {
            Report result = new Report();
            bool started = false;
            ModSettings loadedBase = null;
            XpCustomizerSettings loadingSnapshot = null;
            DetachedLogQueueObserver.Snapshot logBefore = null;
            try
            {
                if (observedRoot != null)
                    return Reject(result, "AUDIT_ALREADY_ARMED");
                if (expected == null || !StagePathIsPrivate(stagePath) ||
                    expectedBytes <= 0 || expectedBytes > DetachedXmlStageAudit.MaximumBytes ||
                    String.IsNullOrEmpty(expectedHash))
                    return Reject(result, "INVALID_STAGE_HANDOFF");
                if (!StageHashMatches(stagePath, expectedHash, expectedBytes))
                    return Reject(result, "STAGE_CHANGED_BEFORE_LOAD");
                if (!IdleWithEmptyLoader())
                    return Reject(result, "SCRIBE_OR_LOADER_BUSY");
                result.PreflightIdle = true;
                // Second gate immediately before the engine's InitLoading,
                // which would otherwise call *global* ForceStop on reentry.
                if (!IdleWithEmptyLoader())
                    return Reject(result, "SCRIBE_BUSY_BEFORE_INIT");

                XpCustomizerSettings baseline = SettingsSnapshotService.CloneIndependent(expected);
                // Start the read-only global queue window immediately before
                // engine InitLoading, after both idle preflight gates.
                string captureFailure;
                if (!DetachedLogQueueObserver.TryCapture(out logBefore, out captureFailure))
                    return Uncertain(result, "LOG_OBSERVABILITY_UNCERTAIN");
                result.LogBeforeCount = logBefore.Entries.Length;
                if (result.LogBeforeCount == 0)
                    return Uncertain(result, "LOG_OBSERVABILITY_UNCERTAIN");
                result.InitLoadingCalls++;
                // Never call LoadedModManager.ReadModSettings: it uses the real
                // Config path and can silently replace a failed load with new T().
                Scribe.loader.InitLoading(stagePath);
                started = true;
                try
                {
                    if (Scribe.mode != LoadSaveMode.LoadingVars)
                        throw new InvalidOperationException("INIT_NOT_LOADING_VARS");
                    result.DeepLookCalls++;
                    Scribe_Deep.Look(ref loadedBase, "ModSettings");
                    XpCustomizerSettings loaded = loadedBase as XpCustomizerSettings;
                    result.LoadedExactType = loaded != null &&
                        loadedBase.GetType() == typeof(XpCustomizerSettings);
                    if (result.LoadedExactType)
                    {
                        observedRoot = loaded;
                        crossRefCompleted = 0;
                        postLoadCompleted = 0;
                        // CloneIndependent reads settings without invoking Scribe.
                        // Capture before engine FinalizeLoading/PostLoadInit.
                        loadingSnapshot = SettingsSnapshotService.CloneIndependent(loaded);
                        result.LoadingVarsCaptured = true;
                    }
                }
                finally
                {
                    // We must finalize our own successful InitLoading even if
                    // Deep.Look or snapshot capture throws. Never ForceStop or
                    // finalize an operation in another mode.
                    if (started && Scribe.mode == LoadSaveMode.LoadingVars)
                    {
                        result.FinalizeLoadingCalls++;
                        Scribe.loader.FinalizeLoading();
                        result.FinalizeReturned = true;
                    }
                }
                result.CrossRefCompleted = crossRefCompleted;
                result.PostLoadCompleted = postLoadCompleted;
                if (!result.LoadedExactType || loadedBase == null)
                    return Uncertain(result, "LOADED_NULL_OR_WRONG_CLASS");
                if (!result.FinalizeReturned || !result.LoadingVarsCaptured)
                    return Uncertain(result, "LOAD_PHASE_INCOMPLETE");
                if (result.CrossRefCompleted != 1 || result.PostLoadCompleted != 1)
                    return Uncertain(result, "PHASE_OBSERVATION_MISSING_OR_DUPLICATE");
                XpCustomizerSettings finished = (XpCustomizerSettings)loadedBase;
                XpCustomizerSettings postSnapshot = SettingsSnapshotService.CloneIndependent(finished);
                result.PostLoadCaptured = true;

                string mismatch;
                bool earlyMatch = DetachedVerseLoadValueComparer.Equivalent(
                    baseline, loadingSnapshot, out mismatch);
                result.LoadingVarsDifference = earlyMatch ? "NONE" : mismatch;
                bool finalMatch = DetachedVerseLoadValueComparer.Equivalent(
                    baseline, postSnapshot, out mismatch);
                result.PostLoadDifference = finalMatch ? "NONE" : mismatch;
                result.ComparedFields = DetachedVerseLoadValueComparer.RootFieldCount;
                if (!earlyMatch || !finalMatch)
                    return Uncertain(result, earlyMatch ?
                        "POST_LOAD_VALUE_DIFFERENCE" : "LOADING_VARS_VALUE_DIFFERENCE");
                result.StageFileStable = StageHashMatches(stagePath, expectedHash, expectedBytes);
                if (!result.StageFileStable)
                    return Uncertain(result, "STAGE_CHANGED_DURING_LOAD");
                if (!IdleWithEmptyLoader())
                    return Uncertain(result, "SCRIBE_NOT_CLEAN_AFTER_LOAD");
                result.ValuesMatch = true;
                // This provisional status is promoted only if the finally
                // window analysis confirms reliable, clean queued logs.
                result.Status = "PASS_DETACHED_VALUES_LOG_QUEUE_CLEAN_PARTIAL";
                result.Failure = "NONE";
                return result;
            }
            catch (Exception ex)
            {
                // Engine paths may have already logged their own errors. Do
                // not copy exception messages/user values into audit output.
                return Uncertain(result, "EXCEPTION_" + ex.GetType().Name);
            }
            finally
            {
                // Stop observing before the caller emits any final audit log.
                // Even if the loader throws, a failed/uncertain log snapshot
                // must never be converted into success.
                if (logBefore != null)
                {
                    DetachedLogQueueObserver.Snapshot logAfter;
                    string logFailure;
                    if (DetachedLogQueueObserver.TryCapture(out logAfter, out logFailure))
                    {
                        DetachedLogQueueObserver.Result observation =
                            DetachedLogQueueObserver.Analyze(logBefore, logAfter);
                        result.LogBeforeCount = observation.BeforeCount;
                        result.LogAfterCount = observation.AfterCount;
                        result.LogAnchorPreserved = observation.QueueAnchorPreserved;
                        result.LogCaptureReliable = observation.Reliable;
                        result.LogObservationReason = observation.Reason;
                        result.LogNewErrorEntries = observation.NewErrorEntries;
                        result.LogNewWarningEntries = observation.NewWarningEntries;
                        result.LogNewMessageEntries = observation.NewMessageEntries;
                        result.LogRepeatDelta = observation.RepeatDelta;
                        result.LogRepeatDeltaDetected = observation.RepeatDeltaDetected;
                        if (!observation.Reliable)
                            Uncertain(result, "LOG_OBSERVABILITY_UNCERTAIN");
                        else if (observation.Reason != "NONE")
                            Uncertain(result, observation.Reason);
                    }
                    else
                    {
                        result.LogObservationReason = "LOG_OBSERVABILITY_UNCERTAIN";
                        Uncertain(result, "LOG_OBSERVABILITY_UNCERTAIN");
                    }
                }
                observedRoot = null;
                crossRefCompleted = 0;
                postLoadCompleted = 0;
                result.FinalIdle = IdleWithEmptyLoader();
                if (result.InitLoadingCalls != 0 && !result.FinalIdle)
                    Uncertain(result, "SCRIBE_GLOBAL_STATE_UNSAFE");
                if (result.ValuesMatch && !result.FinalIdle)
                    Uncertain(result, "SCRIBE_GLOBAL_STATE_UNSAFE");
            }
        }

        private static Report Reject(Report report, string reason)
        {
            report.Status = "REJECTED";
            report.Failure = reason;
            return report;
        }

        private static Report Uncertain(Report report, string reason)
        {
            report.ValuesMatch = false;
            report.Status = "UNCERTAIN";
            report.Failure = reason;
            return report;
        }

        private static bool StagePathIsPrivate(string path)
        {
            if (String.IsNullOrEmpty(path)) return false;
            try
            {
                string full = Path.GetFullPath(path);
                string directory = Path.GetDirectoryName(full);
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;
                string config = Path.GetFullPath(GenFilePaths.ConfigFolderPath).TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;
                return directory != null &&
                    full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) &&
                    !full.StartsWith(config, StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(directory).StartsWith("XPC_C4B1_", StringComparison.Ordinal) &&
                    Path.GetFileName(full) == "C4B1_ISOLATED_STAGE.xml";
            }
            catch (Exception) { return false; }
        }

        private static bool StageHashMatches(string path, string sha, long count)
        {
            try
            {
                FileInfo info = new FileInfo(path);
                if (!info.Exists || info.Length != count ||
                    count > DetachedXmlStageAudit.MaximumBytes) return false;
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length != count) return false;
                using (SHA256 algorithm = SHA256.Create())
                {
                    byte[] hash = algorithm.ComputeHash(bytes);
                    StringBuilder hex = new StringBuilder(64);
                    foreach (byte b in hash)
                        hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                    return String.Equals(hex.ToString(), sha, StringComparison.Ordinal);
                }
            }
            catch (Exception) { return false; }
        }

        // Fail closed if the 1.6 private engine field changes. Reflection is
        // read-only; never Clear/ForceStop someone else's loader registrations.
        private static bool IdleWithEmptyLoader()
        {
            try
            {
                if (Scribe.mode != LoadSaveMode.Inactive || Scribe.saver == null ||
                    Scribe.loader == null || Scribe.saver.CurPath != null ||
                    Scribe.saver.savingForDebug || Scribe.loader.curXmlParent != null ||
                    Scribe.loader.curParent != null ||
                    Scribe.loader.curPathRelToParent != null ||
                    Scribe.loader.crossRefs == null ||
                    Scribe.loader.crossRefs.crossReferencingExposables == null ||
                    Scribe.loader.crossRefs.crossReferencingExposables.Count != 0 ||
                    Scribe.loader.initer == null) return false;
                FieldInfo pendingField = typeof(PostLoadIniter).GetField(
                    "saveablesToPostLoad", PrivateInstance);
                if (pendingField == null) return false;
                object pending = pendingField.GetValue(Scribe.loader.initer);
                if (pending == null) return false;
                PropertyInfo count = pending.GetType().GetProperty("Count", PublicInstance);
                return count != null && count.PropertyType == typeof(int) &&
                    (int)count.GetValue(pending, null) == 0;
            }
            catch (Exception) { return false; }
        }

        internal static void LogFinal(Report report, string stageStatus,
            string stageReason, string sha, int verifiedScalars, int collections,
            bool? liveStable, bool? ownedStable, bool scribeIdle, bool cleaned)
        {
            string loadStatus = report == null ? "NOT_STARTED" : report.Status;
            string loadReason = report == null ? "STAGE_NOT_APPROVED" : report.Failure;
            string finalStatus = stageStatus == "PASS_DETACHED_VALUES_LOG_QUEUE_CLEAN_PARTIAL" &&
                report != null && report.ValuesMatch && report.LogCaptureReliable &&
                report.LogObservationReason == "NONE" && liveStable == true &&
                ownedStable == true && scribeIdle && cleaned ?
                "PASS_DETACHED_VALUES_LOG_QUEUE_CLEAN_PARTIAL" :
                (stageStatus == "REJECTED" ? "REJECTED" : "UNCERTAIN");
            Log.Message(Tag + " status=" + finalStatus + "; stageStatus=" + stageStatus +
                "; reason=" + (finalStatus == "UNCERTAIN" ? stageReason : loadReason) +
                "; revision=" + Revision + "; stageSha256=" + sha +
                "; loadedType=" + (report != null && report.LoadedExactType ?
                    "XpCustomizer.XpCustomizerSettings" : "NOT_VERIFIED") +
                "; loadingVarsCaptured=" + (report != null && report.LoadingVarsCaptured) +
                "; postLoadCaptured=" + (report != null && report.PostLoadCaptured) +
                "; crossRefCompleted=" + (report == null ? 0 : report.CrossRefCompleted) +
                "; postLoadCompleted=" + (report == null ? 0 : report.PostLoadCompleted) +
                "; comparedFields=" + (report == null ? 0 : report.ComparedFields) +
                "; loadingVarsDifference=" + (report == null ? "NOT_CHECKED" : report.LoadingVarsDifference) +
                "; postLoadDifference=" + (report == null ? "NOT_CHECKED" : report.PostLoadDifference) +
                "; verifiedXmlScalars=" + verifiedScalars +
                "; verifiedCollections=" + collections +
                "; initLoadingCalls=" + (report == null ? 0 : report.InitLoadingCalls) +
                "; deepLookCalls=" + (report == null ? 0 : report.DeepLookCalls) +
                "; finalizeLoadingCalls=" + (report == null ? 0 : report.FinalizeLoadingCalls) +
                "; finalizeReturned=" + (report != null && report.FinalizeReturned) +
                "; scribeIdleAfter=" + scribeIdle +
                "; tempCleaned=" + cleaned +
                "; stageFileStable=" + (report != null && report.StageFileStable) +
                "; logBeforeCount=" + (report == null ? -1 : report.LogBeforeCount) +
                "; logAfterCount=" + (report == null ? -1 : report.LogAfterCount) +
                "; queueAnchorPreserved=" + (report != null && report.LogAnchorPreserved) +
                "; repeatDeltaDetected=" + (report != null && report.LogRepeatDeltaDetected) +
                "; repeatDelta=" + (report == null ? 0 : report.LogRepeatDelta) +
                "; newErrorEntries=" + (report == null ? 0 : report.LogNewErrorEntries) +
                "; newWarningEntries=" + (report == null ? 0 : report.LogNewWarningEntries) +
                "; newMessageEntries=" + (report == null ? 0 : report.LogNewMessageEntries) +
                "; logCaptureReliable=" + (report != null && report.LogCaptureReliable) +
                "; logObservationReason=" + (report == null ? "NOT_OBSERVED" : report.LogObservationReason) +
                "; liveSourceStable=" + (liveStable.HasValue ? liveStable.Value.ToString() : "NOT_CHECKED") +
                "; ownedXmlStable=" + (ownedStable.HasValue ? ownedStable.Value.ToString() : "NOT_CHECKED") +
                "; engineErrorObservability=" +
                (report != null && report.LogCaptureReliable &&
                 report.LogObservationReason == "NONE" ?
                    "OBSERVED_QUEUED_ONLY" : "NOT_VERIFIED") +
                "; fullSemantic=NOT_VERIFIED" +
                "; actualModXmlWritesByAudit=0; otherModXmlWritesByAudit=0" +
                "; productionApply=NOT_EXERCISED_BY_THIS_AUDIT; actionRequired=" +
                (finalStatus == "UNCERTAIN" ? "EXIT_WITHOUT_SAVING_AND_REPORT" : "NONE") +
                "; build=" + XpCustomizerMod.BuildId);
        }
    }

    /// <summary>
    /// Compares 108 owned root fields and all 36 nested profile primitives;
    /// XML does not persist Dictionary.Comparer instances, so compare ordinal
    /// semantics separately from the strict rollback reference comparator.
    /// </summary>
    internal static class DetachedVerseLoadValueComparer
    {
        internal const int RootFieldCount = 108;
        private const BindingFlags ProfileFields = BindingFlags.Instance |
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        internal static bool Equivalent(XpCustomizerSettings expected,
            XpCustomizerSettings actual, out string mismatch)
        {
            mismatch = "NONE";
            if (expected == null || actual == null)
            { mismatch = "NULL_OBJECT"; return false; }
            FieldInfo[] fields = SettingsSnapshotService.GetDeclaredSettingsFieldsForAudit();
            if (fields.Length != RootFieldCount)
            { mismatch = "ROOT_SCHEMA_DRIFT"; return false; }
            foreach (FieldInfo field in fields)
            {
                object a = field.GetValue(expected);
                object b = field.GetValue(actual);
                if (a == null || b == null)
                {
                    if (a != null || b != null)
                    { mismatch = field.Name; return false; }
                    continue;
                }
                if (field.FieldType.IsValueType)
                {
                    if (!a.Equals(b)) { mismatch = field.Name; return false; }
                }
                else if (a is IDictionary && b is IDictionary)
                {
                    if (!EqualDictionaries((IDictionary)a, (IDictionary)b))
                    { mismatch = field.Name + ".dictionary"; return false; }
                }
                else if (a is IList && b is IList)
                {
                    if (!EqualLists((IList)a, (IList)b))
                    { mismatch = field.Name + ".list"; return false; }
                }
                else if (a.GetType() == b.GetType() &&
                    (a is ConsciousnessLearning.ConsAnchorV2ProfileSettings ||
                     a is ConsciousnessLearning.ConsciousnessFurtherV2Settings))
                {
                    FieldInfo[] nested = a.GetType().GetFields(ProfileFields);
                    if (nested.Length != 12) { mismatch = field.Name + ".profileSchema"; return false; }
                    foreach (FieldInfo part in nested)
                    {
                        if (!Object.Equals(part.GetValue(a), part.GetValue(b)))
                        { mismatch = field.Name + "." + part.Name; return false; }
                    }
                }
                else
                {
                    mismatch = field.Name + ".unsupportedType";
                    return false;
                }
            }
            return true;
        }

        private static bool EqualLists(IList left, IList right)
        {
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
                if (!Object.Equals(left[i], right[i])) return false;
            return true;
        }

        private static bool EqualDictionaries(IDictionary left, IDictionary right)
        {
            if (!KnownOrdinalComparer(left) || !KnownOrdinalComparer(right) ||
                left.Count != right.Count) return false;
            foreach (DictionaryEntry item in left)
                if (!right.Contains(item.Key) ||
                    !Object.Equals(item.Value, right[item.Key])) return false;
            return true;
        }

        private static bool KnownOrdinalComparer(IDictionary dictionary)
        {
            PropertyInfo prop = dictionary.GetType().GetProperty("Comparer", PublicComparer);
            if (prop == null) return false;
            object comparer = prop.GetValue(dictionary, null);
            return Object.ReferenceEquals(comparer, EqualityComparer<string>.Default) ||
                Object.ReferenceEquals(comparer, StringComparer.Ordinal);
        }

        private const BindingFlags PublicComparer = BindingFlags.Instance | BindingFlags.Public;
    }
}
#endif
