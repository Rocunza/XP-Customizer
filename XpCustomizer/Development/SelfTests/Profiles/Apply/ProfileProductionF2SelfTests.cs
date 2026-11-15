#if DEBUG
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer
{
    /// <summary>
    /// F2: isolated state-machine and real repository tests under OS temp.
    /// Never writes the user's Config, XML, saves or actual profile folder.
    /// A test-owned recovery predicate does not mutate the live XML gate.
    /// </summary>
    internal static class ProfileProductionF2SelfTests
    {
        // No test logs are forwarded to Verse.Log. Severity and payload are
        // inspected directly so expected failures cannot open the red error UI.
        private sealed class CapturingOverwriteLogSink : IProfileOverwriteLogSink
        {
            internal int Errors;
            internal int Warnings;
            internal int Messages;
            internal readonly List<string> Entries = new List<string>();
            public ProfileOverwriteLogScope Scope
            { get { return ProfileOverwriteLogScope.IsolatedSelfTest; } }
            public void Write(ProfileOverwriteLogLevel level, string message)
            {
                if (level == ProfileOverwriteLogLevel.Error) Errors++;
                else if (level == ProfileOverwriteLogLevel.Warning) Warnings++;
                else Messages++;
                Entries.Add(message);
            }
        }

        private static int checks;
        private static void Check(bool pass, string label)
        {
            checks++;
            if (!pass) throw new InvalidOperationException("R6P1-F2:" + label);
        }

        internal static void Run()
        {
            checks = 0;
            TestWriterGate();
            TestReviewMessages();
            TestOwnedXmlEvidence();
            TestOverwriteIsolation();
            TestP0ConditionalBiotechPolicy();
            Check(OwnedXmlSemanticContract.ScalarCount == 131 &&
                  OwnedXmlSemanticContract.CollectionCount == 10,
                  "semantic contract centralized");
            Log.Message("[XPC-R6P1F2][PROFILE_PRODUCTION_F2_SELFTEST] status=PASS; checks=" +
                checks + "; scope=ISOLATED_SELFTEST; expectedRejection=PASS" +
                "; testLogSink=CAPTURED_NOT_GLOBAL; productionErrorPolicy=PRESERVED" +
                "; isolatedOwnXmlGate=True; isolatedJsonRoot=True" +
                "; liveGateUnchanged=" + (!ProfileProductionWriteGate.IsBusy &&
                   !ProfileProductionWriteGate.IsUncertain) +
                "; liveSettingsWrites=0; realOwnedXmlWrites=0; foreignWrites=0" +
                "; build=" + XpCustomizerMod.BuildId);
        }

        private static void TestP0ConditionalBiotechPolicy()
        {
            // Pure configuration-only evidence, without touching actual
            // GeneDefs. The production path must use this SAME detector.
            XpCustomizerSettings before = new XpCustomizerSettings();
            before.biotechCustomizationEnabled = true;
            XpCustomizerSettings after = SettingsSnapshotService.CloneIndependent(before);
            after.careerXpMultiplier = 6f;
            Check(!SettingsRuntimeChangeDetector.BiotechChanged(before, after),
                "P0: career-only apply is not a Biotech mutation");
            after = SettingsSnapshotService.CloneIndependent(before);
            after.level20ReserveMultiplier = 3f;
            Check(!SettingsRuntimeChangeDetector.BiotechChanged(before, after),
                "P0: reserve-only apply is not a Biotech mutation");
            after = SettingsSnapshotService.CloneIndependent(before);
            after.biotechStrongAptitude += 1;
            Check(SettingsRuntimeChangeDetector.BiotechChanged(before, after),
                "P0: changed active gene configuration requires verification");
            before.biotechCustomizationEnabled = false;
            after = SettingsSnapshotService.CloneIndependent(before);
            after.biotechStrongAptitude += 1;
            Check(!SettingsRuntimeChangeDetector.BiotechChanged(before, after),
                "P0: inactive-to-inactive stored gene change is no-touch");
            Log.Message("[XPC-P0][APPLY_POLICY_SELFTEST] status=PASS; checks=4; " +
                "mode=DETACHED_SETTINGS_ONLY; liveGenes=NOT_TOUCHED; build=" + XpCustomizerMod.BuildId);
        }

        private static void TestWriterGate()
        {
            var a = new ProfileProductionWriteState();
            Check(!a.OwnXmlWriteBlocked && !a.TryClaim() && !a.Reserve(),
                "unapproved writer has no permit");
            Check(a.Begin() && a.IsBusy && a.OwnXmlWriteBlocked,
                "begin blocks unapproved autosaves");
            Check(!a.Begin() && a.Reserve() && !a.Reserve(),
                "one scoped permit only");
            Check(a.TryClaim() && !a.TryClaim() && a.WriterClaimed,
                "exactly one writer claim");
            Check(!a.CompleteVerified(), "cannot complete before returned");
            a.FinishWrite(true);
            Check(a.WriterReturned && a.CompleteVerified() && !a.OwnXmlWriteBlocked,
                "verified writer restores normal autosave");
            Check(!a.CompleteVerified() && !a.TryClaim(),
                "completed permit is not reusable");

            var b = new ProfileProductionWriteState();
            Check(b.Begin() && b.Reserve() && b.AbortKnownUnchanged(),
                "known no-write abort clears in-flight latch");
            Check(!b.OwnXmlWriteBlocked && !b.WriterClaimed,
                "abort leaves normal settings writes untouched");
            Check(b.Begin() && b.Reserve() && b.TryClaim(),
                "new operation after verified abort");
            Check(!b.AbortKnownUnchanged(),
                "claimed writer can never be treated as no-write");
            b.FinishWrite(false);
            Check(b.IsUncertain && b.OwnXmlWriteBlocked && !b.CompleteVerified(),
                "throwing writer locks own XML");
            b.CancelUnclaimed();
            Check(!b.Begin() && !b.Reserve() && !b.TryClaim(),
                "uncertainty sticky; no in-session retry");

            var c = new ProfileProductionWriteState();
            Check(c.Begin() && c.Reserve(), "third independent gate");
            c.CancelUnclaimed();
            Check(!c.TryClaim() && c.AbortKnownUnchanged(),
                "cancelled reservation does not leak to window-close save");
            var d = new ProfileProductionWriteState();
            Check(d.Begin(), "fourth independent gate");
            d.LatchUncertain();
            Check(d.IsUncertain && !d.Begin() && !d.AbortKnownUnchanged(),
                "explicit uncertainty cannot be cleared by abort");
            Check(!ProfileProductionWriteGate.IsUncertain &&
                !ProfileProductionWriteGate.IsBusy,
                "isolated tests never touch live XML gate");
        }

        private static void TestReviewMessages()
        {
            Check(ProfileApplyReviewMessage.Explain(ProfileApplyReviewCode.Conflict)
                .IndexOf("changed", StringComparison.OrdinalIgnoreCase) >= 0,
                "stale review shows meaningful message");
            Check(ProfileApplyReviewMessage.Explain(ProfileApplyReviewCode.PendingEdit)
                .IndexOf("editing", StringComparison.OrdinalIgnoreCase) >= 0,
                "incomplete numeric editor message");
            Check(ProfileApplyReviewMessage.Explain(ProfileApplyReviewCode.RecoveryRequired)
                .IndexOf("restart", StringComparison.OrdinalIgnoreCase) >= 0,
                "uncertain runtime restart message");
            Check(ProfileApplyReviewMessage.Explain(ProfileApplyReviewCode.InvalidProfile)
                != ProfileApplyReviewMessage.Explain(ProfileApplyReviewCode.RecoveryRequired),
                "invalid JSON distinct from recovery");
        }

        private static void TestOwnedXmlEvidence()
        {
            XpCustomizerSettings source = new XpCustomizerSettings();
            source.careerXpMultiplier = 1.75f;
            source.gwsHighFactor = 1.11f;
            byte[] xml = Encoding.UTF8.GetBytes(DetachedXmlSemanticSelfTests.Fixture(source));
            int emitted, omitted, collections;
            string reason;
            Check(ProfileOwnedXmlEvidence.MatchesLoaded(xml, source,
                out emitted, out omitted, out collections, out reason) &&
                emitted + omitted == OwnedXmlSemanticContract.ScalarCount &&
                collections == OwnedXmlSemanticContract.CollectionCount && reason == "NONE",
                "owned XML and loaded settings agree by the full semantic contract");
            XpCustomizerSettings modified = SettingsSnapshotService.CloneIndependent(source);
            modified.careerXpMultiplier = 2.5f;
            Check(!ProfileOwnedXmlEvidence.MatchesLoaded(xml, modified,
                out emitted, out omitted, out collections, out reason),
                "changed loaded scalar rejected after restart-style readback");
            Check(!ProfileOwnedXmlEvidence.MatchesLoaded(
                Encoding.UTF8.GetBytes("<broken"), source,
                out emitted, out omitted, out collections, out reason),
                "malformed XML rejected");
            Check(!ProfileOwnedXmlEvidence.MatchesLoaded(null, source,
                out emitted, out omitted, out collections, out reason),
                "missing XML evidence rejected");
            Check(!ProfileOwnedXmlEvidence.MatchesLoaded(
                Encoding.UTF8.GetBytes(DetachedXmlSemanticSelfTests.Fixture(source)
                    .Replace("XpCustomizer.XpCustomizerSettings", "Other.ModSettings")), source,
                out emitted, out omitted, out collections, out reason),
                "foreign settings class rejected");
        }

        private static void TestOverwriteIsolation()
        {
            string root = Path.Combine(Path.GetTempPath(), "XPC-F2-OVERWRITE-" +
                Guid.NewGuid().ToString("N"));
            XpCustomizerSettings live = XpCustomizerMod.Settings;
            Check(live != null && !ProfileWriteSessionGate.IsBlocked &&
                !RuntimeRecoverySessionGate.LiveIsBlocked,
                "live settings are readable and no pre-existing write lock");
            object originalMap = live.skillSpecificXpMultipliers;
            float originalCareer = live.careerXpMultiplier;
            bool xmlLock = false;
            var captured = new CapturingOverwriteLogSink();
            Check(ProfileCurrentOverwriteService.ForCurrentUser().UsesVerseLogSink &&
                ProfileCurrentOverwriteService.LogLevelFor(ProfileCurrentOverwriteCode.RecoveryRequired)
                    == ProfileOverwriteLogLevel.Error &&
                ProfileCurrentOverwriteService.LogLevelFor(ProfileCurrentOverwriteCode.StorageFailure)
                    == ProfileOverwriteLogLevel.Warning &&
                ProfileCurrentOverwriteService.LogLevelFor(ProfileCurrentOverwriteCode.Updated)
                    == ProfileOverwriteLogLevel.Message,
                "production logger still routes genuine failures to Verse.Log.Error");
            try
            {
                var paths = ProfileStoragePaths.ForIsolatedTestRoot(root);
                var repo = new ProfileFileRepository(paths, false);
                var creator = new ProfileCreationService(delegate { return repo; }, false);
                var service = new ProfileCurrentOverwriteService(delegate { return repo; },
                    delegate { return new ProfileEditorReadinessResult(
                        ProfileEditorReadinessCode.Ready, "F2-ISOLATED", 15, 0, 0); },
                    delegate { return xmlLock; }, captured);
                Check(!service.UsesVerseLogSink, "isolated service never uses Verse sink");
                XpCustomizerSettings different = SettingsSnapshotService.CloneIndependent(live);
                different.careerXpMultiplier = originalCareer == 1.25f ? 1.75f : 1.25f;
                DateTime created = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc);
                string id = "6a1e281f-3bd0-45f1-a109-1ae700000001";
                Check(creator.CreateCore(different, "F2 isolated", "1.6.4871", created,
                    id, ProfileStorageFault.None).Success, "created test-only JSON");
                ProfileStorageResult before = repo.Load(id);
                Check(before.Success, "test JSON readable");
                var plan = service.Prepare(before.Document, before.ContentHash, live);
                Check(plan.Success && plan.ContentDiffers,
                    "user-visible preflight identifies changed portable snapshot");
                Check(repo.Load(id).ContentHash == before.ContentHash &&
                    !Directory.Exists(paths.Backups),
                    "opening/cancelling overwrite does not write JSON or backup");
                xmlLock = true;
                Check(service.Prepare(before.Document, before.ContentHash, live).Code ==
                    ProfileOverwritePreflightCode.RecoveryRequired,
                    "XML uncertainty blocks creating overwrite consent");
                Check(service.Confirm(before.Document, plan, live).Code ==
                    ProfileCurrentOverwriteCode.RecoveryRequired,
                    "XML uncertainty after dialog opened blocks actual overwrite");
                Check(captured.Errors == 1 && captured.Warnings == 0 &&
                    captured.Messages == 0 && captured.Entries.Count == 1 &&
                    captured.Entries[0].Contains("scope=ISOLATED_SELFTEST") &&
                    captured.Entries[0].Contains("testResult=EXPECTED_REJECTION") &&
                    captured.Entries[0].Contains("status=RecoveryRequired"),
                    "expected rejection captured as Error, not sent to Verse");
                Check(repo.Load(id).ContentHash == before.ContentHash,
                    "blocked overwrite does not modify test JSON");
                xmlLock = false;
                ProfileCurrentOverwriteResult saved = service.Confirm(before.Document, plan, live);
                ProfileStorageResult after = repo.Load(id);
                Check(saved.Success && after.Success &&
                    after.ContentHash != before.ContentHash,
                    "explicit confirm replaces isolated JSON and verifies SHA");
                Check(captured.Errors == 1 && captured.Warnings == 0 &&
                    captured.Messages == 1 && captured.Entries.Count == 2 &&
                    captured.Entries[1].Contains("scope=ISOLATED_SELFTEST") &&
                    captured.Entries[1].Contains("testResult=ISOLATED_JSON_UPDATED") &&
                    captured.Entries[1].Contains("status=Updated"),
                    "isolated JSON success captured and distinguished from user update");
                Check(after.Document.ProfileId == id &&
                    after.Document.CreatedUtc == before.Document.CreatedUtc &&
                    after.Document.UpdatedUtc > before.Document.UpdatedUtc,
                    "identity retained and UpdatedUtc advanced");
                Check(Directory.GetFiles(paths.Backups, id + ".*.bak").Length == 1,
                    "original JSON backup retained");
                Check(repo.Load(id).ContentHash == after.ContentHash &&
                    service.Prepare(after.Document, after.ContentHash, live).ContentDiffers == false,
                    "same current settings need no new JSON write");
                Check(service.Prepare(before.Document, before.ContentHash, live).Code ==
                    ProfileOverwritePreflightCode.Conflict,
                    "stale original SHA rejected");
                Check(System.Object.ReferenceEquals(live, XpCustomizerMod.Settings) &&
                    System.Object.ReferenceEquals(originalMap, live.skillSpecificXpMultipliers) &&
                    live.careerXpMultiplier == originalCareer,
                    "no live settings or comparer mutation");
                Check(!ProfileWriteSessionGate.IsBlocked &&
                    !ProfileProductionWriteGate.IsUncertain &&
                    captured.Entries.Count == 2,
                    "test does not set live recovery latch or emit extra reports");
            }
            finally
            {
                xmlLock = false;
                try { if (Directory.Exists(root)) Directory.Delete(root, true); }
                catch { /* test cleanup does not touch user Config */ }
            }
        }
    }
}
#endif
