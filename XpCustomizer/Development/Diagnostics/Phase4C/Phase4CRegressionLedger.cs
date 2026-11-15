#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// R5-B1 in-process versioned regression inventory. Records the outcome
    /// of suites only when their OWN original runners actually execute.
    /// It does not replay old 1.6 results, claim unobserved live scenarios
    /// passed, mutate settings, or persist results to a save file.
    /// </summary>
    internal static class Phase4CRegressionLedger
    {
        internal const string Schema = "4C-REGRESSION-MANIFEST-R3-20261106";
        private sealed class Entry
        {
            internal readonly string Id;
            internal readonly string Scope;
            internal readonly string DefaultStatus;
            internal string Status;
            internal string Detail;
            internal int Runs;
            internal Entry(string id, string scope, string defaultStatus)
            {
                Id = id;
                Scope = scope;
                DefaultStatus = defaultStatus;
                Status = defaultStatus;
                Detail = "NOT_EXECUTED_IN_THIS_PROCESS";
            }
        }
        private static readonly Entry[] manifest =
        {
            new Entry("4C-A-SAFE", "PURE_AND_PATCH_REGISTRATION", "NOT_RUN"),
            new Entry("4C-B-DETACHED", "DETACHED_SKILLRECORD_NOT_SAVED_XP", "NOT_RUN"),
            new Entry("4C-M-MULTIPAWN", "DETACHED_MULTI_PAWN", "NOT_RUN"),
            new Entry("4C-RND", "DETERMINISTIC_PURE_CALCULATION", "NOT_RUN"),
            new Entry("4C-C-WORK", "REAL_JOB_CONTEXT_XP_PAYOUT", "NOT_VERIFIED"),
            new Entry("4C-C-READING", "REAL_READING_XP_PAYOUT", "NOT_VERIFIED"),
            new Entry("4C-C-DECAY", "REAL_SKILL_DECAY_INTERVAL", "NOT_VERIFIED"),
            new Entry("4C-C-POSTCAP-WORK", "REAL_POSTCAP_WORK_RATE_AND_PAYOUT", "NOT_VERIFIED"),
            new Entry("4C-C-POSTCAP-READING", "REAL_POSTCAP_READING_RATE_AND_PAYOUT", "NOT_VERIFIED"),
            new Entry("4C-C-TRANSITION", "ENGINE_SATURATION_BOUNDARY_OBSERVATION", "NOT_VERIFIED"),
            new Entry("4C-C-DAILYCAP-PURE", "INDEPENDENT_CAP_SYNTHETIC_BOUNDARIES", "NOT_RUN"),
            new Entry("4C-C-DAILYCAP-LIVE", "READ_ONLY_PAWN_CAP_AND_THRESHOLD_COMPARISON", "NOT_VERIFIED"),
            new Entry("4C-C-FURTHER-PURE", "INDEPENDENT_POSTCAP_FURTHER_MTH_SYNTHETIC", "NOT_RUN"),
            new Entry("4C-C-FURTHER-LIVE", "REAL_POSTCAP_FURTHER_MTH_COMPONENTS", "NOT_VERIFIED"),
            new Entry("REAL-TRAINER-USE", "REAL_ITEM_CONSUMPTION", "NOT_VERIFIED"),
            new Entry("DISK-SCRIBE", "SAVE_LOAD_ROUNDTRIP", "NOT_VERIFIED"),
            new Entry("IMGUI-REAL", "GAME_UI_INTERACTION", "NOT_VERIFIED"),
            new Entry("FOREIGN-MOD", "EXTERNAL_MOD_GAMEPLAY_COMPATIBILITY", "NOT_VERIFIED")
        };
        private static int printSequence;

        internal static void Record(string id, string status, string detail)
        {
            foreach (Entry item in manifest)
            {
                if (item.Id != id) continue;
                if (status != "PASS" && status != "PARTIAL" && status != "FAIL" &&
                    status != "REVIEW" && status != "SKIP" && status != "NOT_VERIFIED")
                    throw new ArgumentException("Unknown regression outcome " + status);
                item.Status = status;
                item.Detail = Clean(detail);
                item.Runs++;
                return;
            }
            throw new ArgumentException("Unregistered regression suite " + id);
        }

        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "NONE";
            s = s.Replace(';', ',').Replace('\r', ' ').Replace('\n', ' ');
            return s.Length > 120 ? s.Substring(0, 120) : s;
        }

        // The RimWorld version API may change between 1.6 and 1.7.
        // Reflection failure is reported as UNKNOWN, never guessed.
        private static string RuntimeGameVersion()
        {
            try
            {
                Type type = typeof(RimWorld.VersionControl);
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                foreach (string name in new[] { "CurrentVersionString", "CurrentVersionStringWithRev" })
                {
                    PropertyInfo property = type.GetProperty(name, flags);
                    if (property != null && property.PropertyType == typeof(string))
                    {
                        object v = property.GetValue(null, null);
                        if (v is string && !string.IsNullOrEmpty((string)v))
                            return Clean((string)v);
                    }
                    FieldInfo field = type.GetField(name, flags);
                    if (field != null && field.FieldType == typeof(string))
                    {
                        object v = field.GetValue(null);
                        if (v is string && !string.IsNullOrEmpty((string)v))
                            return Clean((string)v);
                    }
                }
            }
            catch (Exception) { }
            return "UNKNOWN_VERSION_API";
        }

        private static void Print(string trigger)
        {
            int pass = 0, partial = 0, failed = 0, notVerified = 0, notRun = 0;
            var sb = new StringBuilder(2600);
            sb.Append("[XPC-REGRESSION][RUN] sequence=").Append(++printSequence)
                .Append("; trigger=").Append(trigger)
                .Append("; schema=").Append(Schema)
                .Append("; gameVersion=").Append(RuntimeGameVersion())
                .Append("; build=").Append(XpCustomizerMod.BuildId)
                .Append("; inProcessOnly=True; historical1p6PassImported=False")
                .Append("; xpWrites=0; settingsWrites=0; saveWrites=0");
            foreach (Entry e in manifest)
            {
                switch (e.Status)
                {
                    case "PASS": pass++; break;
                    case "FAIL": case "REVIEW": failed++; break;
                    case "PARTIAL": case "SKIP": partial++; break;
                    case "NOT_VERIFIED": notVerified++; break;
                    default: notRun++; break;
                }
                sb.Append("\n[XPC-REGRESSION][SUITE] id=").Append(e.Id)
                    .Append("; scope=").Append(e.Scope)
                    .Append("; status=").Append(e.Status)
                    .Append("; runs=").Append(e.Runs)
                    .Append("; detail=").Append(e.Detail);
            }
            sb.Append("\n[XPC-REGRESSION][SUMMARY] pass=").Append(pass)
                .Append("; partialOrSkip=").Append(partial)
                .Append("; failOrReview=").Append(failed)
                .Append("; notVerified=").Append(notVerified)
                .Append("; notRun=").Append(notRun)
                .Append("; total=").Append(manifest.Length)
                .Append("; status=").Append(failed > 0 ? "REVIEW" :
                    notVerified + notRun + partial > 0 ? "PARTIAL_COVERAGE" : "PASS")
                .Append("; notRunDoesNotMeanPass=True")
                .Append("; statusIsThisProcessOnly=True");
            if (failed > 0) Log.Error(sb.ToString());
            else Log.Message(sb.ToString());
        }

        internal static void PrintSummaryAfterLive() { Print("LIVE_CAPTURE_STOP"); }
        internal static void PrintManually() { Print("MANUAL"); }

        internal static void RunSafeAndPrint()
        {
            // These two existing runners are isolated/pure; detached and real
            // live tests are intentionally NOT auto-executed by this command.
            if (Current.Game == null || Find.CurrentMap == null)
            {
                Log.Warning("[XPC-REGRESSION] SKIP; reason=NO_LOADED_MAP");
                return;
            }
            Phase4CADiagnosticRunner.RunSafeManually();
            Phase4CRandomizedDiagnostics.RunManually(100, Phase4CRandomizedDiagnostics.DefaultSeed);
            Phase4CCIndependentDailyCapOracle.RunSyntheticAudit();
            Phase4CCLiveXpCapture.RunFurtherPureAudit();
            Print("RUN_SAFE_A_PLUS_RND100_PLUS_R5B");
        }
    }

    public static class Phase4CRegressionDebugActions
    {
        [DebugAction("XP Customizer", "Run safe 4C regression (A + RND100) (R5-A)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void RunSafe() { Phase4CRegressionLedger.RunSafeAndPrint(); }

        [DebugAction("XP Customizer", "Run safe 4C regression + daily cap (R5-B)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void RunSafeWithDailyCap() { Phase4CRegressionLedger.RunSafeAndPrint(); }

        [DebugAction("XP Customizer", "Print versioned 4C regression manifest (R5-A)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Print() { Phase4CRegressionLedger.PrintManually(); }
    }
}
#endif
