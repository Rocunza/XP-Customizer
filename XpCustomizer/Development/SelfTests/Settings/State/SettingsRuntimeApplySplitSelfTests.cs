#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// RAC1 structural and detached regression. Uses only cloned settings and
    /// fake ISettingsRuntimeRefreshActions. No live game/Def/cache/Scribe writes.
    /// The existing R6P0D2, R6P0D3 and recovery-gate suites remain authoritative
    /// for the more exhaustive transaction and compensation fault cases.
    /// </summary>
    internal static class SettingsRuntimeApplySplitSelfTests
    {
        private static int checks;
        private const BindingFlags AllStatic = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Static;

        private sealed class DetachedActions : ISettingsRuntimeRefreshActions
        {
            internal readonly List<string> Calls = new List<string>();
            internal int GeneState = 73;
            internal int Captures;
            internal int Restores;
            internal string Fault;
            private bool fired;

            public object CaptureBiotechRollbackState()
            {
                Captures++;
                return GeneState;
            }

            public bool RestoreBiotechRollbackState(object snapshot, out string reason)
            {
                Calls.Add("RESTORE_GENE");
                Restores++;
                GeneState = (int)snapshot;
                reason = "NONE";
                return true;
            }

            public void InvalidateCurveCache()
            {
                Calls.Add("CACHE");
                MaybeFail("CACHE");
            }

            public void ReapplyBiotech()
            {
                Calls.Add("BIOTECH");
                GeneState = 974; // only this detached fake state changes
                MaybeFail("BIOTECH");
            }

            public void NotifyAllEditors()
            {
                Calls.Add("UI");
                MaybeFail("UI");
            }

            private void MaybeFail(string stage)
            {
                if (Fault == stage && !fired)
                {
                    fired = true;
                    throw new InvalidOperationException("RAC1_FAKE_" + stage);
                }
            }
        }

        internal static void Run()
        {
            checks = 0;
            Type facade = typeof(SettingsRuntimeApplyCoordinator);
            Type engine = typeof(SettingsRuntimeTransactionEngine);
            Type detector = typeof(SettingsRuntimeChangeDetector);
            Type resultType = typeof(SettingsRuntimeApplyResult);
            Type statusType = typeof(SettingsRuntimeApplyStatus);

            Check(facade.IsAbstract && facade.IsSealed, "facade remains static");
            Check(engine.IsAbstract && engine.IsSealed, "transaction engine remains static");
            Check(detector.IsAbstract && detector.IsSealed, "change detector remains static");
            Check(SettingsRuntimeApplyCoordinator.Revision == "R6P0D3-RUNTIME-ROLLBACK-20261106",
                "runtime algorithm revision unchanged");
            Check(SettingsRuntimeApplyCoordinator.LiveApplyStillBlockedForAudit,
                "legacy D4 route remains closed");
            Check(Enum.GetNames(statusType).Length == 5, "five status values preserved");
            foreach (string status in new string[] {
                "APPLY_PASS", "APPLY_NO_CHANGE", "APPLY_REJECTED",
                "RUNTIME_REFRESH_ROLLBACK_PASS", "RUNTIME_REFRESH_FAILED_UNRECOVERED" })
                Check(Enum.IsDefined(statusType, status), "missing status " + status);
            foreach (string field in new string[] {
                "Status", "Reason", "CacheInvalidated", "BiotechReapplied",
                "UiNotified", "RecoveryVerified" })
                Check(resultType.GetField(field, BindingFlags.Instance |
                    BindingFlags.Public | BindingFlags.NonPublic) != null,
                    "missing result field " + field);
            Check(typeof(ISettingsRuntimeRefreshActions).GetMethods().Length == 5,
                "five refresh action members preserved");

            foreach (string entry in new string[] {
                "ApplyLiveForGlobalNumericDefaults", "ApplyLiveForProductionProfile",
                "ApplyLiveAfterD4", "CareerCurveChanged", "BiotechChanged",
                "ApplyDetachedForAudit", "ApplyDetachedForAuditWithDataFault",
                "ApplyLiveForManualDiagnostic", "ApplyLiveForDebugProfile" })
                Check(facade.GetMethod(entry, AllStatic) != null, "facade route missing: " + entry);
            Check(facade.GetNestedType("LiveDiagnosticFault",
                BindingFlags.NonPublic | BindingFlags.Public) != null,
                "DEBUG fault enum must retain parent type");
            Check(facade.GetMethod("ApplyCore", AllStatic) == null,
                "core must not be duplicated on facade");
            Check(facade.GetField("applying", AllStatic) == null,
                "no second reentrant state on facade");
            Check(engine.GetMethod("ApplyCore", AllStatic) != null,
                "detached transaction engine entry present");
            Check(engine.GetMethod("Recover", AllStatic) != null,
                "recovery in the same engine");
            Check(engine.GetField("applying", AllStatic) != null &&
                engine.GetField("applying", AllStatic).FieldType == typeof(bool),
                "single bool reentrant flag retained");
            Check(detector.GetMethod("CareerCurveChanged", AllStatic) != null &&
                detector.GetMethod("BiotechChanged", AllStatic) != null,
                "read-only change detector entries present");
            Check(detector.GetMethod("SequenceEqual", AllStatic) != null,
                "list identity/equality helper moved intact");
            Check(facade.GetMethod("Result", AllStatic) != null,
                "result logger still routes through facade");

            XpCustomizerSettings original = new XpCustomizerSettings();
            XpCustomizerSettings same = SettingsSnapshotService.CloneIndependent(original);
            XpCustomizerSettings changed = SettingsSnapshotService.CloneIndependent(original);
            Check(!SettingsRuntimeApplyCoordinator.CareerCurveChanged(original, same),
                "unchanged curve is not dirty");
            Check(!SettingsRuntimeApplyCoordinator.BiotechChanged(original, same),
                "unchanged biotech is not dirty");
            changed.careerXpMultiplier = original.careerXpMultiplier + 1f;
            Check(SettingsRuntimeApplyCoordinator.CareerCurveChanged(original, changed),
                "career multiplier change detected");
            Check(SettingsRuntimeChangeDetector.CareerCurveChanged(original, changed) ==
                SettingsRuntimeApplyCoordinator.CareerCurveChanged(original, changed),
                "career wrapper and detector agree");
            changed = SettingsSnapshotService.CloneIndependent(original);
            changed.biotechAwfulAptitude = original.biotechAwfulAptitude + 1;
            Check(!SettingsRuntimeApplyCoordinator.BiotechChanged(original, changed),
                "OFF-to-OFF must not take GeneDef ownership");
            changed.biotechCustomizationEnabled = true;
            Check(SettingsRuntimeApplyCoordinator.BiotechChanged(original, changed),
                "OFF-to-ON must be seen");
            Check(SettingsRuntimeChangeDetector.BiotechChanged(original, changed) ==
                SettingsRuntimeApplyCoordinator.BiotechChanged(original, changed),
                "biotech wrapper and detector agree");

            XpCustomizerSettings target = SettingsSnapshotService.CloneIndependent(original);
            DetachedActions hooks = new DetachedActions();
            SettingsRuntimeApplyResult result = Apply(target, same, hooks, true);
            Check(result.Status == SettingsRuntimeApplyStatus.APPLY_NO_CHANGE &&
                result.Reason == "NO_CHANGE", "detached no-op result");
            Check(hooks.Calls.Count == 0 && hooks.Captures == 0 && hooks.Restores == 0,
                "no-op does not touch any effects");

            changed = SettingsSnapshotService.CloneIndependent(target);
            changed.biotechAwfulAptitude++;
            hooks = new DetachedActions();
            result = Apply(target, changed, hooks, true);
            Check(result.Status == SettingsRuntimeApplyStatus.APPLY_PASS &&
                !result.CacheInvalidated && !result.BiotechReapplied && result.UiNotified,
                "OFF-to-OFF stored-only change uses UI refresh");
            Sequence(hooks.Calls, "UI");
            Check(hooks.Captures == 0 && hooks.Restores == 0,
                "OFF-to-OFF does not capture GeneDefs");

            changed = SettingsSnapshotService.CloneIndependent(target);
            changed.careerXpMultiplier += 1f;
            hooks = new DetachedActions();
            result = Apply(target, changed, hooks, true);
            Check(result.Status == SettingsRuntimeApplyStatus.APPLY_PASS &&
                result.CacheInvalidated && !result.BiotechReapplied,
                "career-only change invalidates cache");
            Sequence(hooks.Calls, "CACHE", "UI");
            Check(hooks.Captures == 0 && hooks.Restores == 0,
                "career-only never touches GeneDefs");

            changed = SettingsSnapshotService.CloneIndependent(target);
            changed.biotechCustomizationEnabled = true;
            changed.biotechAwfulAptitude = -8;
            hooks = new DetachedActions();
            hooks.Fault = "BIOTECH";
            XpCustomizerSettings previous = SettingsSnapshotService.CloneIndependent(target);
            result = Apply(target, changed, hooks, true);
            string mismatch;
            Check(result.Status == SettingsRuntimeApplyStatus.RUNTIME_REFRESH_ROLLBACK_PASS &&
                result.RecoveryVerified, "biotech injected failure is recovered");
            Check(result.Reason.StartsWith("BIOTECH:", StringComparison.Ordinal),
                "reason failure stage prefix unchanged");
            Check(SettingsApplyCoordinator.Equivalent(target, previous, out mismatch),
                "original settings data restored");
            Check(hooks.Captures == 1 && hooks.Restores == 1 && hooks.GeneState == 73,
                "GeneDef fake state restored exactly once");
            Sequence(hooks.Calls, "BIOTECH", "RESTORE_GENE", "UI");
            Check(engine.GetField("applying", AllStatic).GetValue(null).Equals(false),
                "engine reentrancy state cleared in finally");

            Log.Message("[XPC-RAC1][RUNTIME_APPLY_SPLIT_AUDIT] status=PASS; checks=" + checks +
                "; facade=COMPATIBLE; liveAndDebug=SEPARATE; coreAndRecovery=ONE_ENGINE" +
                "; reentrancy=ONE_STATIC_FLAG; changeDetection=DELEGATED" +
                "; reasonAndStatuses=PRESERVED; noChange=NO_EXTERNAL_EFFECTS" +
                "; offToOff=GENEDEFS_NOT_TOUCHED; cacheOnly=PASS" +
                "; syntheticBiotechFailure=ROLLED_BACK; source=DETACHED_FAKE_ONLY" +
                "; actualLiveGeneDefs=NOT_RUN; realIMGUI=NOT_RUN" +
                "; productionXmlWrite=NOT_RUN; actualLiveApply=NOT_RUN" +
                "; realSettingsWrites=0; realGeneDefWrites=0; scribeCalls=0" +
                "; build=" + XpCustomizerMod.BuildId);
        }

        private static SettingsRuntimeApplyResult Apply(XpCustomizerSettings target,
            XpCustomizerSettings candidate, DetachedActions hooks, bool biotechAvailable)
        {
            return SettingsRuntimeApplyCoordinator.ApplyDetachedForAudit(target,
                SettingsSnapshotService.Capture(candidate), hooks, biotechAvailable);
        }

        private static void Sequence(IList<string> actual, params string[] expected)
        {
            Check(actual.Count == expected.Length,
                "effect call count expected=" + expected.Length + " actual=" + actual.Count);
            for (int i = 0; i < expected.Length; i++)
                Check(actual[i] == expected[i], "effect sequence at index=" + i);
        }

        private static void Check(bool passed, string description)
        {
            checks++;
            if (!passed)
                throw new InvalidOperationException("[XPC-RAC1] " + description);
        }
    }
}
#endif
