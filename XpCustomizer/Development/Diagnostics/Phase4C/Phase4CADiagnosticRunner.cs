#if DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using LudeonTK;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// Phase 4C-A: manually runnable, non-destructive regression orchestrator.
    /// Reuses existing tests on detached settings/values; does not spawn pawns,
    /// call Learn() on saved skills, alter game settings, or write to disk.
    /// A PASS here is NOT a live XP, UI, or Scribe-disk PASS.
    /// </summary>
    internal static class Phase4CADiagnosticRunner
    {
        internal const string Revision = "4C-A-DIAG-R1-HF1-20261104";

        private enum Outcome { Pass, Fail, Skip }

        private sealed class TestCase
        {
            internal readonly string Id;
            internal readonly Action Action;
            internal readonly string SkipReason;

            internal TestCase(string id, Action action, string skipReason = null)
            {
                Id = id;
                Action = action;
                SkipReason = skipReason;
            }
        }

        private sealed class TestResult
        {
            internal string Id;
            internal Outcome Status;
            internal long Milliseconds;
            internal string Detail;
        }

        private static readonly List<TestResult> lastResults = new List<TestResult>();
        private static string lastRunLabel;
        private static int lastRunId;
        private static bool running;
        private static int nextRunId;

        // Explicit and stable list: no reflection-based test discovery.
        // All entries are existing isolated tests, NOT live-Pawn scenarios.
        private static TestCase[] SafeCases()
        {
            return new[]
            {
                new TestCase("RUNNER-001", VerifyRunnerInfrastructure),
                new TestCase("CONS-FURTHER-V2", XpCustomizerSelfTests.DiagnosticFurtherV2),
                new TestCase("CAREER-XP", CareerXpSelfTests.Run),
                new TestCase("LEARNING-CAPACITY", LearningCapacitySelfTests.Run),
                new TestCase("BOOK-XP", BookXpSelfTests.Run),
                new TestCase("GWS-V2", GwsSelfTests.Run),
                new TestCase("BIOTECH-INPUT", BiotechUiSelfTests.Run),
                new TestCase("FURTHER-BASE", XpCustomizerSelfTests.DiagnosticFurtherBase),
                new TestCase("MENTAL-THRESHOLD", XpCustomizerSelfTests.DiagnosticMentalThreshold),
                new TestCase("CONS-ANCHOR-V2", ConsciousnessAnchorCurveSelfTests.Run),
                new TestCase("CONS-PHASE4B", ConsciousnessPhase4BR2SelfTests.Run),
                new TestCase("TRAINER-PROPS", XpCustomizerSelfTests.DiagnosticTrainerNoSharedMutation),
                new TestCase("TRAINER-SKILLS", XpCustomizerSelfTests.DiagnosticTrainerSpecific),
                new TestCase("SETTINGS-IN-MEMORY", SettingsPersistenceSelfTests.Run),
                new TestCase("DECAY-PURE", DecaySelfTests.Run),
                new TestCase("SKILL-XP", SkillSpecificXpSelfTests.Run),
                new TestCase("HARMONY-REGISTRATION", VerifyHarmonyRegistration),
                // Explicit coverage debt: these are NOT implicitly passing just
                // because the safe/regression suites above pass.
                new TestCase("LIVE-PAWN-XP", null, "DEFERRED_TO_4C_LIVE"),
                new TestCase("REAL-WORK-AND-READING", null, "DEFERRED_TO_4C_SCENARIOS"),
                new TestCase("REAL-TRAINER-USE", null, "DEFERRED_TO_4C_SCENARIOS"),
                new TestCase("DISK-SCRIBE-ROUNDTRIP", null, "DEFERRED_TO_4C_PERSISTENCE"),
                new TestCase("IMGUI-FOCUS", null, "DEFERRED_TO_4C_UI"),
                new TestCase("FOREIGN-MOD-COMPAT", null, "DEFERRED_TO_4C_COMPAT")
            };
        }

        internal static void RunSafeManually()
        {
            // The DebugAction is map-only. This additional guard makes direct
            // future calls safe as well and reports SKIP instead of crashing.
            if (Current.Game == null || Find.CurrentMap == null)
            {
                Log.Warning("[XP Customizer][4C-A] SKIP; reason=NO_LOADED_MAP; " +
                    "build=" + XpCustomizerMod.BuildId);
                return;
            }

            if (running)
            {
                Log.Warning("[XP Customizer][4C-A] SKIP; reason=RUN_ALREADY_ACTIVE");
                return;
            }

            running = true;
            try
            {
                RunCases("MANUAL_SAFE", SafeCases());
            }
            finally
            {
                running = false;
            }
        }

        private static void RunCases(string label, TestCase[] cases)
        {
            var results = new List<TestResult>(cases.Length);
            var report = new StringBuilder(2300);
            int runId = ++nextRunId;
            int pass = 0, fail = 0, skip = 0;
            report.Append("[XP Customizer][4C-A] RUN; id=").Append(runId)
                .Append("; mode=").Append(label)
                .Append("; runner=").Append(Revision)
                .Append("; build=").Append(XpCustomizerMod.BuildId)
                .Append("; scope=PURE_PLUS_PATCH_REGISTRATION")
                .Append("; gameplayMutation=NONE")
                .Append("; startedUtc=").Append(DateTime.UtcNow.ToString("o"));

            foreach (TestCase test in cases)
            {
                TestResult result = Execute(test);
                results.Add(result);
                if (result.Status == Outcome.Pass) pass++;
                else if (result.Status == Outcome.Fail) fail++;
                else skip++;

                report.Append("\n[XPC-TEST][")
                    .Append(result.Status.ToString().ToUpperInvariant())
                    .Append("] ").Append(result.Id)
                    .Append("; ms=").Append(result.Milliseconds);
                if (!string.IsNullOrEmpty(result.Detail))
                    report.Append("; detail=").Append(result.Detail);
            }

            // Publish a coherent snapshot only AFTER all cases are handled.
            lastResults.Clear();
            lastResults.AddRange(results);
            lastRunId = runId;
            lastRunLabel = label;
            report.Append("\n[XP Customizer][4C-A] SUMMARY; run=")
                .Append(runId).Append("; pass=").Append(pass)
                .Append("; fail=").Append(fail)
                .Append("; skip=").Append(skip)
                .Append("; total=").Append(results.Count)
                .Append("; status=")
                .Append(fail > 0 ? "FAIL" : skip > 0 ? "PARTIAL" : "PASS")
                .Append("; livePawnXP=NOT_TESTED; IMGUI=NOT_TESTED; diskScribe=NOT_TESTED")
                .Append("; foreignModGameplay=NOT_TESTED");

            Phase4CRegressionLedger.Record("4C-A-SAFE", fail > 0 ? "FAIL" : skip > 0 ? "PARTIAL" : "PASS",
                "pass=" + pass + ",fail=" + fail + ",deferred=" + skip);
            if (fail > 0)
                Log.Error(report.ToString());
            else
                Log.Message(report.ToString());
        }

        private static TestResult Execute(TestCase test)
        {
            var result = new TestResult
            {
                Id = test.Id,
                Status = Outcome.Skip,
                Detail = test.SkipReason ?? "NO_ACTION"
            };
            if (test.Action == null) return result;

            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                test.Action();
                result.Status = Outcome.Pass;
                result.Detail = null;
            }
            catch (Exception ex)
            {
                result.Status = Outcome.Fail;
                result.Detail = ex.GetType().Name + ": " + SingleLine(ex.Message);
            }
            finally
            {
                watch.Stop();
                result.Milliseconds = watch.ElapsedMilliseconds;
            }
            return result;
        }

        private static string SingleLine(string message)
        {
            if (string.IsNullOrEmpty(message)) return "(no message)";
            message = message.Replace('\r', ' ').Replace('\n', ' ');
            return message.Length <= 240 ? message : message.Substring(0, 240) + "...";
        }

        // Unlike the legacy audit, this fails if an installed reading
        // transpiler matched zero supported IL sites (an inert patch).
        private static void VerifyHarmonyRegistration()
        {
            XpCustomizerSelfTests.RunHarmonyPatchAudit();
            if (XpCustomizer.BookXp.Patch_ReadingIntellectualXp.MatchedSites != 1 ||
                XpCustomizer.BookXp.Patch_TextbookXp.MatchedSites != 1)
            {
                throw new InvalidOperationException("BOOKXP transpiler IL mismatch; " +
                    "intellectual=" + XpCustomizer.BookXp.Patch_ReadingIntellectualXp.MatchedSites +
                    "; textbook=" + XpCustomizer.BookXp.Patch_TextbookXp.MatchedSites);
            }
        }

        // Tests the runner's own PASS/FAIL/SKIP aggregation and the fact that
        // a failed case cannot prevent the following case from running.
        private static void VerifyRunnerInfrastructure()
        {
            int executedAfterFailure = 0;
            TestResult yes = Execute(new TestCase("SYNTHETIC-PASS", delegate { }));
            TestResult no = Execute(new TestCase("SYNTHETIC-FAIL", delegate
            {
                throw new InvalidOperationException("EXPECTED_TEST_EXCEPTION");
            }));
            TestResult skipped = Execute(new TestCase("SYNTHETIC-SKIP", null));
            TestResult continued = Execute(new TestCase("SYNTHETIC-CONTINUE", delegate
            {
                executedAfterFailure++;
            }));
            if (yes.Status != Outcome.Pass || no.Status != Outcome.Fail ||
                skipped.Status != Outcome.Skip || continued.Status != Outcome.Pass ||
                executedAfterFailure != 1 ||
                no.Detail == null || !no.Detail.Contains("EXPECTED_TEST_EXCEPTION"))
            {
                throw new InvalidOperationException("RUNNER-001 accounting/isolation failed");
            }
        }

        internal static void PrintLastReport()
        {
            if (lastRunId == 0)
            {
                Log.Message("[XP Customizer][4C-A] No manual report yet. " +
                    "Use 'Run safe diagnostics (4C-A)' first.");
                return;
            }

            int pass = 0, fail = 0, skip = 0;
            var sb = new StringBuilder(1400);
            sb.Append("[XP Customizer][4C-A] LAST_REPORT; id=")
                .Append(lastRunId).Append("; mode=").Append(lastRunLabel)
                .Append("; build=").Append(XpCustomizerMod.BuildId);
            foreach (TestResult result in lastResults)
            {
                if (result.Status == Outcome.Pass) pass++;
                else if (result.Status == Outcome.Fail) fail++;
                else skip++;
                sb.Append("\n  ").Append(result.Id).Append("=")
                    .Append(result.Status.ToString().ToUpperInvariant());
                if (result.Status != Outcome.Pass)
                    sb.Append("; ").Append(result.Detail);
            }
            sb.Append("\n[XP Customizer][4C-A] LAST_SUMMARY; pass=").Append(pass)
                .Append("; fail=").Append(fail).Append("; skip=").Append(skip)
                .Append("; scope=PURE_PLUS_PATCH_REGISTRATION")
                .Append("; liveXP=NOT_TESTED; gameplaySettingsWrite=NONE");
            Log.Message(sb.ToString());
        }
    }

    // Calls private regressions through the existing partial test class.
    // No tests are copied or rewritten to create this diagnostic runner.
    internal static partial class XpCustomizerSelfTests
    {
        internal static void DiagnosticFurtherV2() { TestConsciousnessFurtherLearningV2(); }
        internal static void DiagnosticFurtherBase() { TestFurtherLearningBaseFactorPrecision(); }
        internal static void DiagnosticMentalThreshold() { TestFurtherLearningMentalThresholdCurve(); }
        internal static void DiagnosticTrainerNoSharedMutation() { TestSkilltrainerXpNoSharedMutation(); }
        internal static void DiagnosticTrainerSpecific() { TestSkilltrainerSpecificXpS2(); }
    }

    public static class Phase4CDiagnosticDebugActions
    {
        [DebugAction("XP Customizer", "Run safe diagnostics (4C-A)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void RunSafe()
        {
            Phase4CADiagnosticRunner.RunSafeManually();
        }

        [DebugAction("XP Customizer", "Print last 4C-A diagnostic report",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void PrintLast()
        {
            Phase4CADiagnosticRunner.PrintLastReport();
        }
    }
}
#endif
