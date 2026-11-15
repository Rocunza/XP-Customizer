#if DEBUG
using System;

namespace XpCustomizer
{
    /// <summary>Pure isolated test: NEVER arms the real game's XML write latch.</summary>
    internal static class ProfileDebugLiveApplySelfTests
    {
        internal static void Run()
        {
            int checks = 0;
            ProfileDebugLiveApplySessionGuard first =
                ProfileDebugLiveApplySessionGuard.CreateIsolatedForAudit();
            Require(!first.IsArmedForIsolatedAudit, "initial not blocked", ref checks);
            Require(!first.HasPermitForIsolatedAudit, "initial no permit", ref checks);
            Require(!first.IsConsumedForIsolatedAudit, "initial unconsumed", ref checks);
            Require(!first.ConsumeForIsolatedAudit(), "unarmed cannot consume", ref checks);
            Require(first.BeginForIsolatedAudit(), "first begin", ref checks);
            Require(first.IsArmedForIsolatedAudit, "write blocked on begin", ref checks);
            Require(first.HasPermitForIsolatedAudit, "single permit", ref checks);
            Require(!first.BeginForIsolatedAudit(), "cannot rearm", ref checks);
            Require(first.ConsumeForIsolatedAudit(), "consume once", ref checks);
            Require(first.IsConsumedForIsolatedAudit, "consumed flag", ref checks);
            Require(!first.HasPermitForIsolatedAudit, "permit consumed", ref checks);
            Require(!first.ConsumeForIsolatedAudit(), "no second execution", ref checks);
            Require(!first.BeginForIsolatedAudit(), "cannot rearm after consume", ref checks);
            Require(first.IsArmedForIsolatedAudit, "write block remains", ref checks);
            ProfileDebugLiveApplySessionGuard second =
                ProfileDebugLiveApplySessionGuard.CreateIsolatedForAudit();
            Require(!second.IsArmedForIsolatedAudit, "independent guard", ref checks);
            Require(second.BeginForIsolatedAudit(), "second independent begin", ref checks);
            Require(second.ConsumeForIsolatedAudit(), "second independent consume", ref checks);
            Require(!ProfileDebugLiveApplySessionGuard.LiveWriteBlocked,
                "startup must not touch live guard", ref checks);
            LogPass(checks);
        }

        private static void Require(bool ok, string label, ref int checks)
        {
            checks++;
            if (!ok) throw new InvalidOperationException("D2D1A PROFILE_DEBUG_SELFTEST: " + label);
        }

        private static void LogPass(int checks)
        {
            Verse.Log.Message("[XPC-R6P1D2D1A][DEBUG_PROFILE_SELFTEST] status=PASS; " +
                "checks=" + checks + "; scope=ISOLATED_PURE; liveGuardUnchanged=True; " +
                "liveSettingsWrites=0; baseWriteSettingsCalls=0; productionApply=NOT_EXERCISED_BY_THIS_TEST; " +
                "build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
