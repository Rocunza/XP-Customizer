#if DEBUG
using System;
using System.Collections.Generic;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// P0-D2: synthetic refresh actions ONLY. No live settings, caches,
    /// GeneDefs, pawn objects, IMGUI, Scribe or Harmony are touched.
    /// </summary>
    internal static class SettingsRuntimeApplySelfTests
    {
        private sealed class Recorder : ISettingsRuntimeRefreshActions
        {
            internal readonly List<string> Calls = new List<string>();
            internal XpCustomizerSettings Target;
            internal float ExpectedCareerMultiplier;
            internal int ExpectedBiotechAptitude;
            internal string FailAt;
            internal Action OnCache;
            internal int FakeGeneValue = 135;
            internal bool RestoreFails;
            private bool failureTriggered;
            private int geneBefore;

            public object CaptureBiotechRollbackState()
            {
                geneBefore = FakeGeneValue;
                return geneBefore;
            }

            public bool RestoreBiotechRollbackState(object state, out string reason)
            {
                if (RestoreFails)
                {
                    reason = "INJECTED_GENE_RESTORE_FAILURE";
                    return false;
                }
                FakeGeneValue = (int)state;
                reason = FakeGeneValue == geneBefore ? "NONE" : "FAKE_GENE_MISMATCH";
                return reason == "NONE";
            }

            private void FailOnce(string stage)
            {
                if (FailAt == stage && !failureTriggered)
                {
                    failureTriggered = true;
                    throw new InvalidOperationException("INJECTED_" + stage + "_FAILURE");
                }
            }

            public void InvalidateCurveCache()
            {
                Check(failureTriggered || Target.careerXpMultiplier == ExpectedCareerMultiplier,
                    "cache invoked before data apply");
                Calls.Add("CACHE");
                if (OnCache != null) OnCache();
                FailOnce("CACHE");
            }
            public void ReapplyBiotech()
            {
                Check(Target.biotechAwfulAptitude == ExpectedBiotechAptitude,
                    "biotech invoked before data apply");
                Calls.Add("BIOTECH");
                FakeGeneValue = 911; // a partial external write before failure
                FailOnce("BIOTECH");
                FailOnce("BIOTECH_VERIFY"); // simulated post-apply validation failure
            }
            public void NotifyAllEditors()
            {
                Calls.Add("UI");
                FailOnce("UI");
            }
        }

        internal static void Run()
        {
            int checks = 0;
            XpCustomizerSettings original = new XpCustomizerSettings();
            XpCustomizerSettings target = SettingsSnapshotService.CloneIndependent(original);
            XpCustomizerSettings targetIdentity = target;
            Recorder hooks = NewRecorder(target);

            SettingsRuntimeApplyResult result = Apply(target, original, hooks, true);
            Check(result.Status == SettingsRuntimeApplyStatus.APPLY_NO_CHANGE &&
                hooks.Calls.Count == 0, "no-op should not refresh anything");
            checks++;

            XpCustomizerSettings changed = SettingsSnapshotService.CloneIndependent(original);
            changed.careerXpMultiplier = 7.5f;
            hooks = NewRecorder(target);
            hooks.ExpectedCareerMultiplier = 7.5f;
            result = Apply(target, changed, hooks, true);
            Check(result.Status == SettingsRuntimeApplyStatus.APPLY_PASS &&
                result.CacheInvalidated && !result.BiotechReapplied && result.UiNotified,
                "curve-only dispatch flags");
            CheckSequence(hooks.Calls, "CACHE", "UI");
            checks++;

            // P0: foreign-modified live gene state must not block a profile
            // changing only a non-Biotech field; no gene snapshot/write occurs.
            XpCustomizerSettings foreignState = SettingsSnapshotService.CloneIndependent(target);
            XpCustomizerSettings reserveOnly = SettingsSnapshotService.CloneIndependent(foreignState);
            reserveOnly.level20ReserveMultiplier = 1.7f;
            hooks = NewRecorder(foreignState);
            hooks.FakeGeneValue = 731; // deliberately diverges from our expected 135
            result = Apply(foreignState, reserveOnly, hooks, true);
            Check(result.Status == SettingsRuntimeApplyStatus.APPLY_PASS &&
                !result.BiotechReapplied && hooks.FakeGeneValue == 731,
                "P0: unrelated profile change must not touch foreign gene values");
            CheckSequence(hooks.Calls, "UI");
            checks++;

            // Current settings include the previous curve multiplier; no
            // additional career change in the biotech-only transition.
            changed.biotechCustomizationEnabled = true;
            changed.biotechAwfulAptitude = -8;
            hooks = NewRecorder(target);
            hooks.ExpectedCareerMultiplier = 7.5f;
            hooks.ExpectedBiotechAptitude = -8;
            result = Apply(target, changed, hooks, true);
            Check(result.Status == SettingsRuntimeApplyStatus.APPLY_PASS &&
                !result.CacheInvalidated && result.BiotechReapplied && result.UiNotified,
                "biotech-only dispatch flags");
            CheckSequence(hooks.Calls, "BIOTECH", "UI");
            checks++;

            changed.careerXpMultiplier = 9f;
            changed.biotechAwfulAptitude = -9;
            hooks = NewRecorder(target);
            hooks.ExpectedCareerMultiplier = 9f;
            hooks.ExpectedBiotechAptitude = -9;
            result = Apply(target, changed, hooks, true);
            Check(result.Status == SettingsRuntimeApplyStatus.APPLY_PASS &&
                result.CacheInvalidated && result.BiotechReapplied && result.UiNotified,
                "combined dispatch flags");
            CheckSequence(hooks.Calls, "CACHE", "BIOTECH", "UI");
            checks++;

            // Def changes are skipped when the Biotech DLC is absent.
            changed.biotechAwfulAptitude = -7;
            hooks = NewRecorder(target);
            hooks.ExpectedCareerMultiplier = 9f;
            result = Apply(target, changed, hooks, false);
            Check(result.Status == SettingsRuntimeApplyStatus.APPLY_PASS &&
                !result.BiotechReapplied, "DLC-off should skip GeneDefs");
            CheckSequence(hooks.Calls, "UI");
            checks++;

            // OFF -> OFF only updates stored values, never touches Defs.
            changed.biotechCustomizationEnabled = false;
            hooks = NewRecorder(target);
            result = Apply(target, changed, hooks, true);
            Check(result.Status == SettingsRuntimeApplyStatus.APPLY_PASS &&
                result.BiotechReapplied, "ON->OFF must restore owned GeneDefs");
            CheckSequence(hooks.Calls, "BIOTECH", "UI");
            checks++;
            changed.biotechAwfulAptitude = -6;
            hooks = NewRecorder(target);
            result = Apply(target, changed, hooks, true);
            Check(!result.BiotechReapplied, "OFF->OFF must not acquire GeneDefs");
            CheckSequence(hooks.Calls, "UI");
            checks++;

            // Invalid candidate gets no writes and no effects.
            changed.careerXpMultiplier = float.NaN;
            XpCustomizerSettings beforeInvalid = SettingsSnapshotService.CloneIndependent(target);
            hooks = NewRecorder(target);
            result = Apply(target, changed, hooks, true);
            string mismatch;
            Check(result.Status == SettingsRuntimeApplyStatus.APPLY_REJECTED &&
                hooks.Calls.Count == 0 &&
                SettingsApplyCoordinator.Equivalent(target, beforeInvalid, out mismatch),
                "invalid candidate changed target");
            checks++;
            changed.careerXpMultiplier = 10f;

            // Simulated failures are distinguishable, not reported as PASS.
            foreach (string failAt in new string[] { "CACHE", "BIOTECH", "BIOTECH_VERIFY", "UI" })
            {
                XpCustomizerSettings detached = SettingsSnapshotService.CloneIndependent(target);
                hooks = NewRecorder(detached);
                hooks.ExpectedCareerMultiplier = 10f;
                hooks.ExpectedBiotechAptitude = -10;
                changed.biotechCustomizationEnabled = true;
                changed.biotechAwfulAptitude = -10;
                hooks.FailAt = failAt;
                XpCustomizerSettings beforeFault = SettingsSnapshotService.CloneIndependent(detached);
                result = Apply(detached, changed, hooks, true);
                string mismatchAfterFault;
                Check(result.Status == SettingsRuntimeApplyStatus.RUNTIME_REFRESH_ROLLBACK_PASS &&
                    result.RecoveryVerified &&
                    result.Reason.StartsWith((failAt == "BIOTECH_VERIFY" ?
                        "BIOTECH" : failAt) + ":", StringComparison.Ordinal) &&
                    SettingsApplyCoordinator.Equivalent(detached, beforeFault, out mismatchAfterFault) &&
                    hooks.FakeGeneValue == 135,
                    "injected failure not recovered: " + failAt);
                checks++;
            }

            // Reentrant call during cache refresh must reject, never mutate.
            changed.biotechAwfulAptitude = -11;
            hooks = NewRecorder(target);
            hooks.ExpectedCareerMultiplier = 10f;
            hooks.ExpectedBiotechAptitude = -11;
            hooks.OnCache = delegate
            {
                XpCustomizerSettings nested = SettingsSnapshotService.CloneIndependent(original);
                Recorder nestedHooks = NewRecorder(nested);
                SettingsRuntimeApplyResult nestedResult = Apply(nested, changed, nestedHooks, true);
                Check(nestedResult.Status == SettingsRuntimeApplyStatus.APPLY_REJECTED &&
                    nestedResult.Reason == "REENTRANT_APPLY" && nestedHooks.Calls.Count == 0,
                    "reentrant execution was not blocked");
            };
            result = Apply(target, changed, hooks, true);
            Check(result.Status == SettingsRuntimeApplyStatus.APPLY_PASS &&
                object.ReferenceEquals(target, targetIdentity), "root identity lost");
            CheckSequence(hooks.Calls, "CACHE", "BIOTECH", "UI");
            checks++;

            Log.Message("[XPC-R6P0D2][RUNTIME_DISPATCH_AUDIT] status=PASS; revision=" +
                SettingsRuntimeApplyCoordinator.Revision + "; cases=" + checks +
                "; noOp=PASS; preflightReject=PASS; cacheOnly=PASS; biotechOnly=PASS" +
                "; dlcOff=PASS; biotechOffNoTouch=PASS; onToOffRestoreRoute=PASS" +
                "; order=CACHE>BIOTECH>UI; injectedFailures=4; recovery=PASS; reentrancy=BLOCKED" +
                "; p0ForeignGeneNoTouch=PASS; p0PostApplyVerificationRollback=PASS" +
                "; testEffects=FAKE_ONLY; liveSettingsWrites=0; geneDefWrites=0" +
                "; liveCacheWrites=0; uiBufferWrites=0; scribeCalls=0" +
                "; legacyTestRoute=DETACHED_FAKE_EFFECTS; productionApply=NOT_EXERCISED_BY_THIS_TEST; build=" + XpCustomizerMod.BuildId);
        }

        private static Recorder NewRecorder(XpCustomizerSettings target)
        {
            return new Recorder { Target = target,
                ExpectedCareerMultiplier = target.careerXpMultiplier,
                ExpectedBiotechAptitude = target.biotechAwfulAptitude };
        }

        private static SettingsRuntimeApplyResult Apply(XpCustomizerSettings target,
            XpCustomizerSettings desired, Recorder hooks, bool biotechAvailable)
        {
            return SettingsRuntimeApplyCoordinator.ApplyDetachedForAudit(target,
                SettingsSnapshotService.Capture(desired), hooks, biotechAvailable);
        }

        private static void CheckSequence(IList<string> observed, params string[] expected)
        {
            Check(observed.Count == expected.Length, "effect count mismatch: " + observed.Count);
            for (int i = 0; i < expected.Length; i++)
                Check(observed[i] == expected[i], "effect order mismatch at " + i);
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[XPC-R6P0D2] " + message);
        }
    }
}
#endif
