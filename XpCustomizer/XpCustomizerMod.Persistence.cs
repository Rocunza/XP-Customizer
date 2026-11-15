using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.Progression;
using XpCustomizer.Biotech;
using XpCustomizer.UI;
using XpCustomizer.BookXp;
using XpCustomizer.Profiles;

namespace XpCustomizer
{
    public partial class XpCustomizerMod
    {

        // ================================================================
        // Settings
        // ================================================================

        public override void WriteSettings()
        {
            // D2-B2-B: Dialog_ModSettings.PreClose calls this automatically,
            // even if a previous live diagnostic failed to restore runtime state.
            // Never persist a potentially damaged settings object in that case.
            // Other mods' ModSettings / game saves are outside our ownership.
            if (RuntimeRecoverySessionGate.LiveModSettingsIoBlocked)
            {
                ConsciousnessFurtherLearningSettingsUI.ResetPreviewToCurrent();
                Log.Error("[XPC-R6P1D2B2B][MOD_SETTINGS_WRITE_GUARD] action=BLOCK; " +
                    "reason=LIVE_RUNTIME_RECOVERY_UNVERIFIED; " +
                    "xpCustomizerModSettingsScribe=SUPPRESSED; " +
                    "actionRequired=EXIT_WITHOUT_SAVING_RESTART; build=" + BuildId);
                return;
            }
            // D2-D3: separate single-use approved production writer. Automatic
            // UI close must never consume the in-flight profile permit.
            bool productionWriterClaimed = false;
            if (ProfileProductionWriteGate.OwnXmlWriteBlocked)
            {
                productionWriterClaimed = ProfileProductionWriteGate.TryClaim();
                if (!productionWriterClaimed)
                {
                    Log.Warning("[XPC-R6P1D2D3][PROFILE_XML_WRITE_GUARD] action=BLOCK; " +
                        "reason=PROFILE_IN_FLIGHT_OR_UNCERTAIN; xpOwnedXmlOnly=True; build=" + BuildId);
                    return;
                }
            }
#if DEBUG
            // D2-D1A: a Debug runtime-only profile experiment must never be
            // silently persisted by Dialog_ModSettings.PreClose. This affects
            // XP Customizer's own XML ONLY and lasts until process restart.
            bool approvedDebugProfileWriter = false;
            if (ProfileDebugLiveApplySessionGuard.LiveWriteBlocked ||
                ProfileDebugPersistWriteGate.LiveTestAttempted)
            {
                // D2-D1B grants at most one *specific* invocation. This does
                // not clear the D1A session latch, and never affects another mod.
                approvedDebugProfileWriter = ProfileDebugPersistWriteGate.TryClaimLiveWrite();
                if (!approvedDebugProfileWriter)
                {
                    if (productionWriterClaimed) ProfileProductionWriteGate.LatchUncertain();
                    ConsciousnessFurtherLearningSettingsUI.ResetPreviewToCurrent();
                    Log.Warning("[XPC-R6P1D2D1A][DEBUG_XML_WRITE_GUARD] action=BLOCK; " +
                        "reason=RUNTIME_ONLY_OR_PERSIST_SESSION_LOCK; baseWriteSettingsCalls=0; " +
                        "scope=XP_CUSTOMIZER_OWN_XML_ONLY; otherModWrites=UNTOUCHED; " +
                        "restartRequiredToRestoreNormalSaving=True; build=" + BuildId);
                    return;
                }
            }
#endif
            // Vanilla Mod.WriteSettings still executes exactly once. The
            // Debug observer can only read the XP Customizer-owned XML.
            // Observe in finally so a writer exception is NEVER misreported
            // as a successful save. The original exception still propagates.
            bool productionWriterReturned = false;
            try
            {
#if DEBUG
            ModXmlRoundTripAudit.WriteAttempt auditAttempt =
                ModXmlRoundTripAudit.BeforeVanillaWrite(Content);
            bool vanillaWriterReturned = false;
            try
            {
#endif
                base.WriteSettings();
                productionWriterReturned = true;
#if DEBUG
                vanillaWriterReturned = true;
            }
            finally
            {
                // Complete the one-shot permit even if the vanilla writer
                // throws; do not permit an automatic second write on close.
                if (approvedDebugProfileWriter)
                    ProfileDebugPersistWriteGate.FinishLiveWrite(vanillaWriterReturned);
                ModXmlRoundTripAudit.AfterVanillaWrite(
                    auditAttempt, Settings, vanillaWriterReturned);
            }
#endif
            }
            finally
            {
                if (productionWriterClaimed)
                    ProfileProductionWriteGate.FinishWrite(productionWriterReturned);
            }
            ConsciousnessFurtherLearningSettingsUI.ResetPreviewToCurrent();
#if DEBUG
            Log.Message("[XP Customizer][CONS-FURTHER-PREVIEW-R2] settingsWrite=complete; " +
                "transientPreview=resetToCurrent; previewScribeKeys=NONE");
#endif
        }

    }
}
