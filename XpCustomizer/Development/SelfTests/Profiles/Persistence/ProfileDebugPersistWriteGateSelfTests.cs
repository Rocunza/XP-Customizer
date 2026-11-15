#if DEBUG
using System;
using System.Text;
using Verse;

namespace XpCustomizer
{
    /// <summary>Pure isolated tests; never touch the actual game/XML/Scribe.</summary>
    internal static class ProfileDebugPersistWriteGateSelfTests
    {
        internal static void Run()
        {
            int checks = 0;
            ProfileDebugPersistWriteGate a = ProfileDebugPersistWriteGate.CreateIsolatedForAudit();
            Check(!a.RequestedIsolated && !a.PendingIsolated, "initial", ref checks);
            Check(!a.ClaimIsolated(), "unapproved claim", ref checks);
            Check(!a.ReserveIsolated(), "reserve without test intent", ref checks);
            Check(a.BeginTestIsolated(), "test begins", ref checks);
            Check(a.TestAttemptedIsolated, "early auto-save blocked", ref checks);
            Check(!a.BeginTestIsolated(), "single test intent", ref checks);
            Check(a.ReserveIsolated(), "reserve", ref checks);
            Check(a.RequestedIsolated && a.PendingIsolated, "pending", ref checks);
            Check(!a.ReserveIsolated(), "duplicate reserve", ref checks);
            Check(a.ClaimIsolated(), "one claim", ref checks);
            Check(a.ClaimedIsolated && !a.PendingIsolated, "permit consumed", ref checks);
            Check(!a.ClaimIsolated(), "second writer blocked", ref checks);
            a.FinishIsolated(true);
            Check(a.FinishedIsolated && a.ReturnedIsolated, "returned", ref checks);
            Check(!a.ReserveIsolated() && !a.ClaimIsolated(), "terminal", ref checks);
            a.FinishIsolated(false);
            Check(a.ReturnedIsolated, "first return immutable", ref checks);
            ProfileDebugPersistWriteGate b = ProfileDebugPersistWriteGate.CreateIsolatedForAudit();
            Check(b.BeginTestIsolated() && b.ReserveIsolated(), "second instance", ref checks);
            b.CancelIsolated();
            Check(!b.PendingIsolated && !b.ClaimIsolated(), "cancelled no writer", ref checks);
            Check(!b.ReserveIsolated(), "cancelled no retry", ref checks);
            ProfileDebugPersistWriteGate c = ProfileDebugPersistWriteGate.CreateIsolatedForAudit();
            Check(c.BeginTestIsolated() && c.ReserveIsolated() && c.ClaimIsolated(),
                "exception scenario", ref checks);
            c.FinishIsolated(false);
            Check(c.FinishedIsolated && !c.ReturnedIsolated, "exception sticky", ref checks);
            Check(!c.ClaimIsolated(), "exception no retry", ref checks);
            Check(!ProfileDebugPersistWriteGate.LiveTestAttempted &&
                  !ProfileDebugPersistWriteGate.LiveAttempted &&
                  !ProfileDebugPersistWriteGate.LiveWriterClaimed &&
                  !ProfileDebugPersistWriteGate.LiveWriterReturned,
                  "live guard unaffected", ref checks);
            byte[] good = Encoding.UTF8.GetBytes(
                "<SettingsBlock><ModSettings Class=\"XpCustomizer.XpCustomizerSettings\"/>" +
                "</SettingsBlock>");
            Check(Profiles.ProfileDebugPersistenceService.OwnXmlShape(good),
                "owned XML shape accepted", ref checks);
            Check(!Profiles.ProfileDebugPersistenceService.OwnXmlShape(
                Encoding.UTF8.GetBytes("<SettingsBlock><ModSettings Class=\"Other.ModSettings\"/>" +
                    "</SettingsBlock>")), "foreign settings rejected", ref checks);
            Check(!Profiles.ProfileDebugPersistenceService.OwnXmlShape(
                Encoding.UTF8.GetBytes("<SettingsBlock><ModSettings Class=\"XpCustomizer.XpCustomizerSettings\"/>" +
                    "<ModSettings/></SettingsBlock>")), "duplicate node rejected", ref checks);
            Check(!Profiles.ProfileDebugPersistenceService.OwnXmlShape(
                Encoding.UTF8.GetBytes("<Other><ModSettings Class=\"XpCustomizer.XpCustomizerSettings\"/>" +
                    "</Other>")), "wrong root rejected", ref checks);
            Check(!Profiles.ProfileDebugPersistenceService.OwnXmlShape(
                Encoding.UTF8.GetBytes("<!DOCTYPE SettingsBlock [<!ENTITY bad \"x\">]>" +
                    "<SettingsBlock>&bad;</SettingsBlock>")), "DTD rejected", ref checks);
            Check(!Profiles.ProfileDebugPersistenceService.OwnXmlShape(new byte[] { 1, 2, 3 }),
                "malformed rejected", ref checks);
            Log.Message("[XPC-R6P1D2D1B][DEBUG_PERSIST_SELFTEST] status=PASS; checks=" +
                checks + "; scope=ISOLATED_PURE; realXmlWrites=0; " +
                "liveGuardUnaffected=True; productionApply=NOT_EXERCISED_BY_THIS_TEST; build=" + XpCustomizerMod.BuildId);
        }
        private static void Check(bool pass, string name, ref int checks)
        {
            checks++;
            if (!pass) throw new InvalidOperationException("D2D1B WRITER_GATE_SELFTEST:" + name);
        }
    }
}
#endif
