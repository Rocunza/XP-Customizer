#if DEBUG
using System;
using System.Reflection;
using UnityEngine;
using Verse;
using XpCustomizer.UI;

namespace XpCustomizer
{
    /// <summary>
    /// SW1: bounded, detached regression for the extracted settings-window
    /// state owners. Does NOT click real IMGUI, create dialogs, open profiles,
    /// reset settings, invoke Scribe or touch real game/pawn/GeneDef state.
    /// </summary>
    internal static class SettingsWindowSplitSelfTests
    {
        private static int checks;

        private static void Check(bool passes, string reason)
        {
            checks++;
            if (!passes) throw new InvalidOperationException("SW1_WINDOW_SPLIT:" + reason);
        }

        private static object ReadField(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Check(field != null, "FIELD_NOT_FOUND:" + fieldName);
            return field.GetValue(target);
        }

        internal static void Run()
        {
            checks = 0;
            // Logging carries the exact patch identifier. Avoid pinning the
            // probe to this one BuildId so later refactors do not cause a
            // false failure solely because their version string advanced.
            Check(!String.IsNullOrEmpty(XpCustomizerMod.BuildId) &&
                XpCustomizerMod.BuildId.StartsWith("XPC-", StringComparison.Ordinal),
                "BUILD_ID_UNSET");
            Check(typeof(SettingsWindow).Namespace == "XpCustomizer.UI" &&
                typeof(SettingsWindow).IsSealed, "WINDOW_IDENTITY");
            Check(typeof(SettingsResetController).Namespace == "XpCustomizer.UI" &&
                typeof(SettingsProfileNavigationController).Namespace == "XpCustomizer.UI",
                "CONTROLLER_NAMESPACE");

            // The Window is created before GetSettings() is assigned in Mod's
            // constructor. Controller construction must be state-only.
            SettingsWindow windowA = new SettingsWindow();
            SettingsWindow windowB = new SettingsWindow();
            object navA = ReadField(windowA, "profileNavigation");
            object resetA = ReadField(windowA, "resetController");
            object navB = ReadField(windowB, "profileNavigation");
            object resetB = ReadField(windowB, "resetController");
            Check(navA != null && resetA != null && navB != null && resetB != null,
                "WINDOW_COMPONENTS_NON_NULL");
            Check(!object.ReferenceEquals(navA, navB) &&
                !object.ReferenceEquals(resetA, resetB), "WINDOW_INSTANCE_ISOLATION");
            Check(object.ReferenceEquals(ReadField(resetA, "profileNavigation"), navA),
                "RESET_NAV_SHARED_OWNER");
            Check(ReadField(resetA, "disarmLiveAudit") != null,
                "DEBUG_DISARM_CALLBACK_CONNECTED");

            // The production state machine's TryBeginPending and AdvancePending
            // are shared with these detached probes. No destination is opened.
            SettingsProfileNavigationController nav =
                new SettingsProfileNavigationController();
            Check(!nav.HasPending, "NAV_INITIAL_IDLE");
            Check(nav.DebugStart(ProfileNavigationTarget.Quick,
                SettingsTab.Progression, "Progression"), "NAV_START_QUICK");
            Check(nav.HasPending && nav.DebugTarget == ProfileNavigationTarget.Quick,
                "NAV_TARGET_QUICK");
            Check(!nav.DebugStart(ProfileNavigationTarget.Manage,
                SettingsTab.Progression, "Progression"), "NAV_DUPLICATE_SUPPRESSED");
            Check(nav.DebugTarget == ProfileNavigationTarget.Quick,
                "NAV_NO_TARGET_REPLACEMENT");
            Check(nav.DebugDrawChecks == 0 && nav.DebugReadyChecks == 0,
                "NAV_COUNTERS_START_ZERO");
            Check(nav.DebugShouldCheckEvent(EventType.Repaint), "REPAINT_ACCEPTED");
            Check(!nav.DebugShouldCheckEvent(EventType.Layout), "LAYOUT_REJECTED");
            Check(!nav.DebugShouldCheckEvent(EventType.MouseUp), "MOUSEUP_REJECTED");
            Check(nav.DebugStep(false) == "Waiting" && nav.DebugReadyChecks == 0,
                "NAV_PENDING_STAYS_PENDING");
            Check(nav.DebugStep(true) == "Waiting" && nav.DebugReadyChecks == 1,
                "NAV_FIRST_READY_WAIT");
            Check(nav.DebugStep(false) == "Waiting" && nav.DebugReadyChecks == 0,
                "NAV_READY_MUST_BE_CONSECUTIVE");
            Check(nav.DebugStep(true) == "Waiting" && nav.DebugReadyChecks == 1,
                "NAV_READY_AFTER_GAP_WAIT");
            Check(nav.DebugStep(true) == "Settled" && nav.DebugReadyChecks == 2,
                "NAV_SECOND_READY_SETTLES");
            nav.DebugClear();
            Check(!nav.HasPending, "NAV_CLEAR_AFTER_SETTLED");

            // Tab change is a rejection in both pre-Repaint and Repaint paths.
            nav = new SettingsProfileNavigationController();
            Check(nav.DebugStart(ProfileNavigationTarget.Manage,
                SettingsTab.Learning, "Learning"), "NAV_MANAGE_START");
            Check(!nav.DebugTabChanged(SettingsTab.Learning) &&
                nav.DebugTabChanged(SettingsTab.Biotech), "NAV_TAB_CHANGE_DETECTION");
            nav.DebugClear();
            Check(!nav.HasPending, "NAV_TAB_CHANGE_CLEARS");

            // 32 Repaint checks max, not 32 UI callbacks/frame. Timeout is
            // only evaluated AFTER giving the 2nd Ready a chance to succeed.
            nav = new SettingsProfileNavigationController();
            Check(nav.DebugStart(ProfileNavigationTarget.Manage,
                SettingsTab.Cons, "Cons"), "NAV_TIMEOUT_START");
            for (int i = 0; i < 31; i++)
                Check(nav.DebugStep(false) == "Waiting", "NAV_PRE_TIMEOUT_" + i);
            Check(nav.DebugDrawChecks == 31 && nav.DebugStep(false) == "TimedOut",
                "NAV_TIMEOUT_ON_32");
            Check(nav.DebugDrawChecks == 32, "NAV_TIMEOUT_COUNTER_32");

            nav = new SettingsProfileNavigationController();
            Check(nav.DebugStart(ProfileNavigationTarget.Quick,
                SettingsTab.Decay, "Decay"), "NAV_BOUNDARY_START");
            for (int i = 0; i < 30; i++)
                nav.DebugStep(false);
            Check(nav.DebugStep(true) == "Waiting" && nav.DebugDrawChecks == 31,
                "NAV_BOUNDARY_FIRST_READY_31");
            Check(nav.DebugStep(true) == "Settled" && nav.DebugDrawChecks == 32,
                "NAV_BOUNDARY_SETTLED_BEATS_TIMEOUT");

            // Two independent modal latches; cancelling closes a latch but
            // never invokes the actual reset callback in this detached suite.
            SettingsResetController.ConfirmationOpenLatch full =
                new SettingsResetController.ConfirmationOpenLatch();
            SettingsResetController.ConfirmationOpenLatch numeric =
                new SettingsResetController.ConfirmationOpenLatch();
            Check(!full.IsOpen && !numeric.IsOpen, "RESET_LATCHES_IDLE");
            Check(full.TryOpen() && full.IsOpen, "RESET_OPEN_FULL");
            Check(!full.TryOpen(), "RESET_DOUBLE_OPEN_SUPPRESSED");
            Check(numeric.TryOpen() && numeric.IsOpen, "RESET_NUMERIC_INDEPENDENT");
            full.Close();
            Check(!full.IsOpen && numeric.IsOpen, "RESET_LATCH_INDEPENDENCE");
            Check(full.TryOpen(), "RESET_REOPEN_AFTER_CANCEL");
            numeric.Close();
            Check(!numeric.IsOpen, "RESET_NUMERIC_CLOSED");
            full.Close();

            // Control ownership is determined by the original N1 predicate:
            // never treat a vanilla or other-mod text field as XP-owned.
            Check(ProfileNumericNavigation.IsOwnedControl("ZCN.XpCustomizer.TestNumeric"),
                "FOCUS_OWNED_PREFIX");
            Check(!ProfileNumericNavigation.IsOwnedControl("AnotherMod.TextInput") &&
                !ProfileNumericNavigation.IsOwnedControl("RimWorld.TextField") &&
                !ProfileNumericNavigation.IsOwnedControl(null),
                "FOCUS_FOREIGN_REJECTED");
            // Debug D4 helper uses the same strict > 600 boundary as production.
            Check(!SettingsWindow.DebugIsD4Expired(700, 100), "D4_FRAME_600_STAYS_ARMED");
            Check(SettingsWindow.DebugIsD4Expired(701, 100), "D4_FRAME_601_EXPIRES");
            Check(!SettingsWindow.DebugIsD4Expired(101, 100), "D4_FRESH_ARM");

            Log.Message("[XPC-SW-SPLIT-R1][SETTINGS_WINDOW_SPLIT_AUDIT] status=PASS; " +
                "checks=" + checks + "; controllers=Navigation+Reset; " +
                "ready=2_CONSECUTIVE; timeout=32_REPAINT_CHECKS; boundaryReadyWins=True; " +
                "duplicateTarget=BLOCKED; tabChange=DETECTED; foreignFocus=UNCHANGED_PREDICATE; " +
                "dialogLatches=INDEPENDENT; D4Expiry=STRICT_GT_600; " +
                "scope=DETACHED_PRODUCTION_TRANSITION_CORE; " +
                "realIMGUI=NOT_RUN; realDialog=NOT_RUN; actualReset=NOT_RUN; " +
                "profileIO=0; scribeCalls=0; geneDefWrites=0; " +
                "build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
