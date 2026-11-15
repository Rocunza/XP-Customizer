#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// Isolated fault-injection tests: NO live settings, GeneDefs, pawns,
    /// UI, caches, Scribe or disk writes. Live rollback requires D4 testing.
    /// </summary>
    internal static class SettingsRuntimeRecoverySelfTests
    {
        private sealed class InjectedActions : ISettingsRuntimeRefreshActions
        {
            internal readonly List<string> Calls = new List<string>();
            internal int GeneState = 44;
            internal bool CaptureFails;
            internal bool RestoreFails;
            internal bool ThrowAtBiotech;
            internal bool ThrowAtUi;
            internal bool ThrowAtCache;
            internal bool PermanentUiFailure;
            internal int Captures;
            internal int RestoreAttempts;
            private bool stageFailed;

            public object CaptureBiotechRollbackState()
            {
                Captures++;
                if (CaptureFails) throw new InvalidOperationException("FAKE_CAPTURE_FAILED");
                return GeneState;
            }

            public bool RestoreBiotechRollbackState(object snapshot, out string reason)
            {
                Calls.Add("RESTORE_GENE");
                RestoreAttempts++;
                if (RestoreFails)
                {
                    reason = "FAKE_GENE_RESTORE_FAILED";
                    return false;
                }
                GeneState = (int)snapshot;
                reason = GeneState == (int)snapshot ? "NONE" : "MISMATCH";
                return reason == "NONE";
            }

            public void InvalidateCurveCache()
            {
                Calls.Add("CACHE");
                if (ThrowAtCache && !stageFailed)
                {
                    stageFailed = true;
                    throw new InvalidOperationException("FAKE_PARTIAL_CACHE_FAILURE");
                }
            }

            public void ReapplyBiotech()
            {
                Calls.Add("BIOTECH");
                GeneState = 999; // Mimics partial GeneDef mutation.
                if (ThrowAtBiotech && !stageFailed)
                {
                    stageFailed = true;
                    throw new InvalidOperationException("FAKE_PARTIAL_BIOTECH_FAILURE");
                }
            }

            public void NotifyAllEditors()
            {
                Calls.Add("UI");
                if (PermanentUiFailure || (ThrowAtUi && !stageFailed))
                {
                    stageFailed = true;
                    throw new InvalidOperationException("FAKE_UI_FAILURE");
                }
            }
        }

        internal static void Run()
        {
            int tests = 0;
            foreach (string fault in new string[] { "CACHE", "BIOTECH", "UI" })
            {
                XpCustomizerSettings target = new XpCustomizerSettings();
                XpCustomizerSettings before = SettingsSnapshotService.CloneIndependent(target);
                object[] originalReferences = CaptureReferences(target);
                XpCustomizerSettings candidate = SettingsSnapshotService.CloneIndependent(target);
                candidate.careerXpMultiplier = target.careerXpMultiplier + 3f;
                candidate.biotechCustomizationEnabled = true;
                candidate.biotechAwfulAptitude = -7;
                InjectedActions hooks = new InjectedActions
                {
                    ThrowAtCache = fault == "CACHE",
                    ThrowAtBiotech = fault == "BIOTECH",
                    ThrowAtUi = fault == "UI"
                };
                SettingsRuntimeApplyResult result = Apply(target, candidate, hooks);
                string mismatch;
                Check(result.Status == SettingsRuntimeApplyStatus.RUNTIME_REFRESH_ROLLBACK_PASS &&
                    result.RecoveryVerified &&
                    SettingsApplyCoordinator.Equivalent(target, before, out mismatch) &&
                    OriginalReferencesMatch(target, originalReferences) &&
                    hooks.GeneState == 44 && hooks.RestoreAttempts == (fault == "CACHE" ? 0 : 1) &&
                    hooks.Calls.Contains("CACHE") && hooks.Calls.Contains("UI"),
                    "fault not fully rolled back: " + fault + " / " + result.Reason);
                tests++;
            }

            // A preflight failure must not modify even one field or GeneDef.
            {
                XpCustomizerSettings target = new XpCustomizerSettings();
                XpCustomizerSettings before = SettingsSnapshotService.CloneIndependent(target);
                XpCustomizerSettings candidate = SettingsSnapshotService.CloneIndependent(target);
                candidate.biotechCustomizationEnabled = true;
                candidate.biotechAwfulAptitude = -8;
                InjectedActions hooks = new InjectedActions { CaptureFails = true };
                SettingsRuntimeApplyResult result = Apply(target, candidate, hooks);
                string mismatch;
                Check(result.Status == SettingsRuntimeApplyStatus.APPLY_REJECTED &&
                    hooks.Calls.Count == 0 && hooks.GeneState == 44 &&
                    SettingsApplyCoordinator.Equivalent(target, before, out mismatch),
                    "preflight capture mutated data");
                tests++;
            }

            // An external restoration failure must never report success.
            {
                XpCustomizerSettings target = new XpCustomizerSettings();
                XpCustomizerSettings before = SettingsSnapshotService.CloneIndependent(target);
                XpCustomizerSettings candidate = SettingsSnapshotService.CloneIndependent(target);
                candidate.careerXpMultiplier = target.careerXpMultiplier + 2f;
                candidate.biotechCustomizationEnabled = true;
                candidate.biotechAwfulAptitude = -7;
                InjectedActions hooks = new InjectedActions { ThrowAtBiotech = true, RestoreFails = true };
                SettingsRuntimeApplyResult result = Apply(target, candidate, hooks);
                string mismatch;
                Check(result.Status == SettingsRuntimeApplyStatus.RUNTIME_REFRESH_FAILED_UNRECOVERED &&
                    !result.RecoveryVerified && hooks.GeneState == 999 &&
                    SettingsApplyCoordinator.Equivalent(target, before, out mismatch),
                    "failed external restore incorrectly reported success");
                tests++;
            }

            // A persistent UI failure is still an incomplete transaction.
            {
                XpCustomizerSettings target = new XpCustomizerSettings();
                XpCustomizerSettings candidate = SettingsSnapshotService.CloneIndependent(target);
                candidate.careerXpMultiplier += 4f;
                InjectedActions hooks = new InjectedActions { PermanentUiFailure = true };
                SettingsRuntimeApplyResult result = Apply(target, candidate, hooks);
                Check(result.Status == SettingsRuntimeApplyStatus.RUNTIME_REFRESH_FAILED_UNRECOVERED &&
                    !result.RecoveryVerified && hooks.Captures == 0 && hooks.RestoreAttempts == 0,
                    "persistent UI error reported success or unnecessarily touched GeneDefs");
                tests++;
            }

            // D3 is intentionally not a public live-profile apply gate.
            Check(SettingsRuntimeApplyCoordinator.LiveApplyStillBlockedForAudit,
                "live profile apply was accidentally enabled");
            tests++;
            Log.Message("[XPC-R6P0D3][RUNTIME_RECOVERY_AUDIT] status=PASS; revision=" +
                SettingsRuntimeApplyCoordinator.Revision + "; cases=" + tests +
                "; faults=CACHE,BIOTECH,UI; dataReferences=RESTORED" +
                "; fakeGeneState=RESTORED_OR_UNTOUCHED; preflightZeroWrite=PASS" +
                "; forcedGeneRestoreFailure=UNRECOVERED_DETECTED" +
                "; persistentUiFailure=UNRECOVERED_DETECTED" +
                "; testEffects=FAKE_ONLY; liveGeneDefWrites=0" +
                "; liveSettingsWrites=0; scribeCalls=0" +
                "; legacyD4Entry=BLOCKED_BY_DESIGN; productionApply=NOT_EXERCISED_BY_THIS_TEST; build=" + XpCustomizerMod.BuildId);
        }

        private static SettingsRuntimeApplyResult Apply(XpCustomizerSettings target,
            XpCustomizerSettings candidate, InjectedActions actions)
        {
            return SettingsRuntimeApplyCoordinator.ApplyDetachedForAudit(target,
                SettingsSnapshotService.Capture(candidate), actions, true);
        }

        private static object[] CaptureReferences(XpCustomizerSettings s)
        {
            FieldInfo[] fields = SettingsSnapshotService.GetDeclaredSettingsFieldsForAudit();
            object[] references = new object[fields.Length];
            for (int i = 0; i < fields.Length; i++)
                references[i] = fields[i].GetValue(s);
            return references;
        }

        private static bool OriginalReferencesMatch(XpCustomizerSettings s, object[] old)
        {
            FieldInfo[] fields = SettingsSnapshotService.GetDeclaredSettingsFieldsForAudit();
            for (int i = 0; i < fields.Length; i++)
                if (!fields[i].FieldType.IsValueType &&
                    !object.ReferenceEquals(fields[i].GetValue(s), old[i]))
                    return false;
            return true;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[XPC-R6P0D3] " + message);
        }
    }
}
#endif
