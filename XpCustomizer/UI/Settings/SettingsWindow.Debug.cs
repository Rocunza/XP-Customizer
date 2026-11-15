#if DEBUG
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace XpCustomizer.UI
{
    internal sealed partial class SettingsWindow
    {
        private Vector2 inspScrollPosition = Vector2.zero;
        private Vector2 combatScrollPosition = Vector2.zero;
        private float inspViewHeight = 200f;
        private float combatViewHeight = 200f;
        private bool d4LiveAuditArmed;
        private int d4ArmFrame;

        private void DisarmD4LiveAudit()
        {
            d4LiveAuditArmed = false;
        }

        // The exact 600/601 boundary is shared with the detached SW1 probe.
        internal static bool DebugIsD4Expired(int currentFrame, int armedAtFrame)
        {
            return currentFrame - armedAtFrame > 600;
        }

        private void DrawD4LiveAuditRow(Rect innerRect, Rect resetRect)
        {
            if (currentTab == SettingsTab.Biotech)
            {
                // An armed probe expires automatically: it is not a saved
                // setting and must not survive a long window-session gap.
                if (d4LiveAuditArmed && DebugIsD4Expired(Time.frameCount, d4ArmFrame))
                    d4LiveAuditArmed = false;
                Rect auditRect = new Rect(innerRect.x,
                    resetRect.y - 38f, innerRect.width, 30f);
                TooltipHandler.TipRegion(auditRect,
                    "DEBUG ONLY - live GeneDef verification. Pause an active game and back up its save. " +
                    "Two clicks are required. Temporarily updates real gene definitions, " +
                    "XP curve cache and UI, then verifies restoration. Does not create a profile. " +
                    "Do not save if the log reports UNRECOVERED.");
                if (Widgets.ButtonText(auditRect,
                    d4LiveAuditArmed ? "RUN D4 live audit" : "Arm D4 live audit"))
                {
                    if (!d4LiveAuditArmed)
                    {
                        d4LiveAuditArmed = true;
                        d4ArmFrame = Time.frameCount;
                        Log.Warning("[XPC-R6P0D4][LIVE_ROLLBACK_AUDIT] status=ARMED; " +
                            "warning=DEBUG_ONLY_PAUSED_GAME_REQUIRED_TEMPORARY_REAL_GENEDEF_WRITES; " +
                            "saveBackupRecommended=True; build=" + XpCustomizerMod.BuildId);
                    }
                    else
                    {
                        d4LiveAuditArmed = false;
                        SettingsLiveRecoveryProbe.RunManual();
                    }
                }
            }
            else
            {
                d4LiveAuditArmed = false;
            }
        }

        // Planned features only. GWS is implemented separately.
        private static void DrawInspComingSoon(Listing_Standard listing)
        {
            DrawComingSoon(listing, "Inspiration XP");
        }

        private static void DrawCombatComingSoon(Listing_Standard listing)
        {
            DrawComingSoon(listing, "Combat XP");
        }

        private static void DrawComingSoon(
            Listing_Standard listing, string title)
        {
            SettingsUIHelpers.DrawSectionTitle(listing, title);
            SettingsUIHelpers.DrawDescription(
                listing, "Coming in a future update. No settings or gameplay effects are active in this tab.");
        }
    }
}
#endif
