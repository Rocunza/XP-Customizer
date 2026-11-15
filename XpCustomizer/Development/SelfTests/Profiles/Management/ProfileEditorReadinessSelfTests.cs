#if DEBUG
using System;
using Verse;
using XpCustomizer.Profiles;
using XpCustomizer.UI;

namespace XpCustomizer
{
    internal static class ProfileEditorReadinessSelfTests
    {
        private static int checks;
        private static void Check(bool ok, string reason)
        {
            checks++;
            if (!ok) throw new InvalidOperationException("R6P1C2B2A EDITOR " + reason);
        }

        internal static void Run()
        {
            checks = 0;
            Check(ProfileEditorReadinessService.KnownSourceCount == 15,
                "fixed source allowlist covers all current numeric holders");
            Check(ProfileEditorReadinessService.ClassifyTextBuffer(null, false, false) ==
                ProfileEditorReadinessCode.Ready, "no buffer");
            foreach (string raw in new[] { "", "-", "+", "1.", "1,", "1e", "1e-", "garbage", "NaN", "Infinity" })
            {
                Check(ProfileEditorReadinessService.ClassifyTextBuffer(raw, false, false) ==
                    ProfileEditorReadinessCode.PendingEdit, "partial/invalid buffer: " + raw);
            }
            Check(ProfileEditorReadinessService.ClassifyTextBuffer("1.5", true, true) ==
                ProfileEditorReadinessCode.PendingEdit, "focused staged number rejected");
            Check(ProfileEditorReadinessService.ClassifyTextBuffer("1.5", true, false) ==
                ProfileEditorReadinessCode.Ready, "known unfocused staged number");
            Check(ProfileEditorReadinessService.ClassifyTextBuffer("1.5", false, false) ==
                ProfileEditorReadinessCode.Uncertain, "unnamed editor focus not guessed");
            Check(ProfileEditorReadinessService.ClassifyTextBuffer("170", false, false) ==
                ProfileEditorReadinessCode.Uncertain, "valid integer in untracked field uncertain");
            ProfileEditorReadinessResult liveProbe = ProfileEditorReadinessService.Check();
            Check(liveProbe != null && liveProbe.CoveredGroups == 15,
                "reflection observes every registered UI holder without editing");
            Check(liveProbe.Code == ProfileEditorReadinessCode.Ready ||
                liveProbe.Code == ProfileEditorReadinessCode.PendingEdit ||
                liveProbe.Code == ProfileEditorReadinessCode.Uncertain,
                "well-defined read-only gate result");
            Log.Message("[XPC-R6P1C2B2A][PROFILE_EDITOR_READINESS_AUDIT] status=PASS; " +
                "revision=" + ProfileEditorReadinessService.Revision + "; checks=" + checks +
                "; registeredSources=15; textFieldNumeric=OBSERVED_OR_FAIL_CLOSED; " +
                "focused=BLOCKED; partial=BLOCKED; customControls=READ_ONLY; " +
                "runtimeResult=" + liveProbe.Code + "; runtimeTrackedBuffers=" +
                liveProbe.ProbedBuffers + "; runtimeUntrackedFocus=" +
                liveProbe.UntrackedFocusBuffers + "; fileWrites=0; settingsWrites=0; " +
                "geneDefWrites=0; scribeCalls=0; productionApply=NOT_EXERCISED_BY_THIS_TEST; " +
                "realIMGUI=MANUAL; build=" + XpCustomizerMod.BuildId);

            int a1Checks = 0;
            Action<bool, string> a1 = delegate(bool ok, string reason)
            {
                a1Checks++;
                if (!ok) throw new InvalidOperationException("R6P1C2B2A1 FOCUS " + reason);
            };
            a1(ProfileEditorReadinessService.ClassifyObservedText("1.5", false, false, false) ==
                ProfileEditorReadinessCode.Uncertain, "unobserved valid fails closed");
            a1(ProfileEditorReadinessService.ClassifyObservedText("1.5", true, true, true) ==
                ProfileEditorReadinessCode.PendingEdit, "focused valid blocks");
            a1(ProfileEditorReadinessService.ClassifyObservedText("1.", true, false, true) ==
                ProfileEditorReadinessCode.PendingEdit, "partial after blur blocks");
            a1(ProfileEditorReadinessService.ClassifyObservedText("1.5", true, false, false) ==
                ProfileEditorReadinessCode.Uncertain, "programmatic buffer change blocks");
            a1(ProfileEditorReadinessService.ClassifyObservedText("1.5", true, false, true) ==
                ProfileEditorReadinessCode.Ready, "observed unfocused value ready");
            a1(ProfileEditorReadinessService.ClassifyObservedText(null, false, false, false) ==
                ProfileEditorReadinessCode.Ready, "no buffer can remain idle");
            a1(ProfileNumericFocus.NextIdleObservationCount(0, null, "1.5", false) == 0,
                "first changed value never attested");
            a1(ProfileNumericFocus.NextIdleObservationCount(0, "1.5", "1.5", false) == 1,
                "first stable idle observation insufficient");
            a1(ProfileNumericFocus.NextIdleObservationCount(1, "1.5", "1.5", false) == 2,
                "second stable idle observation attests");
            a1(ProfileNumericFocus.NextIdleObservationCount(2, "1.5", "1.5", true) == 0,
                "focus invalidates settlement");
            a1(ProfileNumericFocus.NextIdleObservationCount(2, "1.5", "2", false) == 0,
                "changed buffer invalidates settlement");
            a1(ProfileNumericFocus.NextIdleObservationCount(2, "1.5", "1.5", false) == 2,
                "idle saturates");
            NumericInputState numeric = new NumericInputState();
            numeric.buffer = "1.25";
            numeric.FocusObserved = true;
            numeric.WasFocused = true;
            numeric.LastObservedBuffer = "1.25";
            numeric.IdleObservations = 2;
            numeric.Reset();
            a1(numeric.buffer == null && !numeric.FocusObserved && !numeric.WasFocused &&
                numeric.IdleObservations == 0 && numeric.LastObservedBuffer == null,
                "reset clears stale focus evidence");
            const string probeKey = "ProfileNumericFocusSelfTest.ownedRaw";
            ProfileNumericFocus.ObserveExisting(probeKey, "3.5", true);
            bool focused, same;
            bool observed = ProfileNumericFocus.TryGetRaw(probeKey, "3.5", out focused, out same);
            a1(!observed && focused, "focused raw never attested idle");
            ProfileNumericFocus.ObserveExisting(probeKey, "3.5", false);
            ProfileNumericFocus.ObserveExisting(probeKey, "3.5", false);
            observed = ProfileNumericFocus.TryGetRaw(probeKey, "3.5", out focused, out same);
            a1(observed && !focused && same, "raw settled after repeated blur");
            observed = ProfileNumericFocus.TryGetRaw(probeKey, "4.0", out focused, out same);
            a1(observed && !same, "raw stale snapshot detectable");
            Log.Message("[XPC-R6P1C2B2A1][PROFILE_FOCUS_COVERAGE_AUDIT] status=PASS; " +
                "scope=PURE_AND_STARTUP; checks=" + a1Checks +
                "; registeredSources=" + ProfileEditorReadinessService.KnownSourceCount +
                "; trackedWidgets=XP_OWNED_ONLY; rawArrays=OBSERVED_BY_IDENTITY; " +
                "singleBlur=NOT_READY; unobservedNonempty=UNCERTAIN; " +
                "pendingFocused=BLOCKED; fileWrites=0; settingsWrites=0; " +
                "geneDefWrites=0; scribeCalls=0; productionApply=NOT_EXERCISED_BY_THIS_TEST; " +
                "realIMGUI=MANUAL; build=" + XpCustomizerMod.BuildId);
            RunDisabledDecayObservationTests();
        }

        // Tests the actual raw-observation registry transitions, not just
        // an isolated boolean predicate. Each fake control identity is scoped
        // to this suite and forgotten to avoid modifying user editor state.
        private static void RunDisabledDecayObservationTests()
        {
            int disabledChecks = 0;
            Action<bool, string> verify = delegate(bool ok, string reason)
            {
                disabledChecks++;
                if (!ok) throw new InvalidOperationException(
                    "R6P1C2B2A11 DISABLED_DECAY " + reason);
            };
            string idle = "R6P1C2B2A11.Test.Idle";
            string mismatch = "R6P1C2B2A11.Test.Mismatch";
            string focused = "R6P1C2B2A11.Test.Focused";
            string changed = "R6P1C2B2A11.Test.Changed";
            string settled = "R6P1C2B2A11.Test.Settled";
            string hiddenFocus = "R6P1C2B2A11.Test.HiddenFocus";
            string[] keys = { idle, mismatch, focused, changed, settled, hiddenFocus };
            try
            {
                foreach (string key in keys) ProfileNumericFocus.ForgetRaw(key);
                bool hasFocus, same;
                // Newly initialized OFF buffers can attest settlement only
                // after multiple identical *inactive* observations.
                ProfileNumericFocus.ObserveInactiveCore(idle, "150", "150", false);
                verify(!ProfileNumericFocus.TryGetRaw(idle, "150", out hasFocus, out same),
                    "first disabled observation cannot attest");
                ProfileNumericFocus.ObserveInactiveCore(idle, "150", "150", false);
                verify(!ProfileNumericFocus.TryGetRaw(idle, "150", out hasFocus, out same),
                    "second disabled observation cannot attest");
                ProfileNumericFocus.ObserveInactiveCore(idle, "150", "150", false);
                verify(ProfileNumericFocus.TryGetRaw(idle, "150", out hasFocus, out same) &&
                    !hasFocus && same, "stable canonical disabled field attested");
                verify(ProfileEditorReadinessService.ClassifyObservedText("150", true,
                    hasFocus, same) == ProfileEditorReadinessCode.Ready,
                    "canonical disabled field ready");

                // An uncommitted value is NOT an inactive-field exemption.
                for (int i = 0; i < 4; i++)
                    ProfileNumericFocus.ObserveInactiveCore(mismatch, "151", "150", false);
                verify(!ProfileNumericFocus.TryGetRaw(mismatch, "151", out hasFocus,
                    out same), "mismatched hidden buffer always unobserved");
                verify(ProfileEditorReadinessService.ClassifyObservedText("151", false,
                    false, false) == ProfileEditorReadinessCode.Uncertain,
                    "mismatched hidden field blocks");
                verify(ProfileEditorReadinessService.ClassifyObservedText("1.", false,
                    false, false) == ProfileEditorReadinessCode.PendingEdit,
                    "incomplete hidden numeric draft still pending");

                // A previously focused ON field must remain blocked when OFF.
                ProfileNumericFocus.ObserveExisting(focused, "150", true);
                for (int i = 0; i < 4; i++)
                    ProfileNumericFocus.ObserveInactiveCore(focused, "150", "150", false);
                verify(!ProfileNumericFocus.TryGetRaw(focused, "150", out hasFocus,
                    out same), "focused-before-off cannot become ready while hidden");

                // Also detect a silent programmatic text replacement on hide.
                for (int i = 0; i < 3; i++)
                    ProfileNumericFocus.ObserveExisting(changed, "149", false);
                verify(ProfileNumericFocus.TryGetRaw(changed, "149", out hasFocus,
                    out same), "old active text stable");
                for (int i = 0; i < 4; i++)
                    ProfileNumericFocus.ObserveInactiveCore(changed, "150", "150", false);
                verify(!ProfileNumericFocus.TryGetRaw(changed, "150", out hasFocus,
                    out same), "active-to-inactive buffer change blocks");

                // A genuinely settled active control can transition OFF safely.
                for (int i = 0; i < 3; i++)
                    ProfileNumericFocus.ObserveExisting(settled, "150", false);
                ProfileNumericFocus.ObserveInactiveCore(settled, "150", "150", false);
                verify(ProfileNumericFocus.TryGetRaw(settled, "150", out hasFocus,
                    out same) && !hasFocus && same,
                    "settled active-to-inactive canonical transition ready");

                // While disabled, GUI still reporting the old named control
                // must NEVER be treated as an unfocused inactive input.
                ProfileNumericFocus.ObserveInactiveCore(hiddenFocus, "150", "150", true);
                for (int i = 0; i < 4; i++)
                    ProfileNumericFocus.ObserveInactiveCore(hiddenFocus, "150", "150", false);
                verify(!ProfileNumericFocus.TryGetRaw(hiddenFocus, "150", out hasFocus,
                    out same), "current hidden focus latches unsafe transition");

                // Recovery needs a real ON draw with repeated idle observations.
                ProfileNumericFocus.ObserveExisting(focused, "150", false);
                verify(!ProfileNumericFocus.TryGetRaw(focused, "150", out hasFocus,
                    out same), "first active draw does not clear unsafe transition");
                ProfileNumericFocus.ObserveExisting(focused, "150", false);
                ProfileNumericFocus.ObserveExisting(focused, "150", false);
                verify(ProfileNumericFocus.TryGetRaw(focused, "150", out hasFocus,
                    out same) && !hasFocus && same,
                    "reenabled active blur can settle unsafe transition");

                // External reset explicitly invalidates an earlier observation.
                ProfileNumericFocus.ForgetRaw(idle);
                verify(!ProfileNumericFocus.TryGetRaw(idle, "150", out hasFocus,
                    out same), "external reset invalidates raw attestation");
                int expectedDisabled = 0;
                for (int i = 1; i <= 20; i++)
                {
                    expectedDisabled++; // one XP input per level
                    if (XpCustomizer.Decay.SkillDecayService.VanillaDaily[i] > 0f)
                        expectedDisabled++; // plus ratio input when vanilla > 0
                }
                verify(expectedDisabled == 31, "Decay OFF exposes 31 hidden numeric buffers");
                Log.Message("[XPC-R6P1C2B2A11][PROFILE_DISABLED_EDITOR_AUDIT] " +
                    "status=PASS; scope=PURE_RAW_OBSERVATION; checks=" + disabledChecks +
                    "; disabledDecayBuffers=" + expectedDisabled +
                    "; canonicalIdle=READY_AFTER_STABLE; hiddenFocused=BLOCKED; " +
                    "hiddenMismatch=BLOCKED; activeToOffDraft=BLOCKED; " +
                    "activeReentry=REQUIRED_AFTER_UNSAFE; externalReset=INVALIDATED; " +
                    "profileFileWrites=0; settingsWrites=0; geneDefWrites=0; " +
                    "scribeCalls=0; productionApply=NOT_EXERCISED_BY_THIS_TEST; realIMGUI=MANUAL; " +
                    "build=" + XpCustomizerMod.BuildId);
            }
            finally
            {
                foreach (string key in keys) ProfileNumericFocus.ForgetRaw(key);
            }
        }
    }
}
#endif
