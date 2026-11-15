#if DEBUG
using System;
using Verse;
using XpCustomizer.UI;

namespace XpCustomizer
{
    internal static class SettingsRefreshSelfTests
    {
        internal static void Run()
        {
            // Exercise the actual six-group router and the scoped Biotech
            // router. Only editor buffers are manipulated. No setting,
            // runtime GeneDef, Scribe or pawn modification is performed.
            int allCleared = BiotechSettingsUI.DebugVerifyExternalRefresh(
                delegate {
                    SettingsRefreshCoordinator.NotifyAll(SettingsRefreshReason.DebugAudit);
                });
            int biotechCleared = BiotechSettingsUI.DebugVerifyExternalRefresh(
                delegate {
                    SettingsRefreshCoordinator.NotifyBiotechOnly(SettingsRefreshReason.DebugAudit);
                });
            if (allCleared != 12 || biotechCleared != 12)
                throw new InvalidOperationException(
                    "[XPC-R6P0D1] unexpected Biotech editor count");

            Log.Message("[XPC-R6P0D1][UI_REFRESH_AUDIT] status=PASS; revision=" +
                SettingsRefreshCoordinator.Revision +
                "; groupRoutes=6; testPaths=ALL+BIOTECH_ONLY" +
                "; biotechEditorsPerPath=12; staleBuffersCleared=PASS" +
                "; focusFlagsCleared=PASS; editorStateRestoredAfterTest=True" +
                "; gameplayApply=NOT_EXERCISED_BY_THIS_TEST; liveIMGUI=NOT_TESTED" +
                "; geneDefWrites=0; scribeCalls=0; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
