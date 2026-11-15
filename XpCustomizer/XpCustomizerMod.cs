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
    /// <summary>
    /// Main mod entry point.
    ///
    /// Responsibilities:
    /// - load settings
    /// - initialize Harmony
    /// - initialize services that require post-def loading
    /// - delegate settings-window drawing to SettingsWindow
    /// </summary>
    public partial class XpCustomizerMod : Mod
    {
        public static XpCustomizerSettings Settings;

#if DEBUG
        private bool l3f1RecoveryWarningLogged;
#endif

        // Increment for each deployed source patch; used by diagnostics.
        public const string BuildId = "XPC-RW16-4CC-T04-B1-B1-PNL1-HF1-RU1-GTL1-FLR1-R1-20261115";

        private readonly SettingsWindow settingsWindow =
            new SettingsWindow();


        public override string SettingsCategory()
        {
            return "XP Customizer";
        }


        public override void DoSettingsWindowContents(
            Rect inRect)
        {
            // While recovery is uncertain, remove all XP Customizer editing
            // controls, including presets and the optional D4 diagnostic.
            // The vanilla settings window may still close; WriteSettings will
            // suppress this mod's XML serialization until process restart.
            if (RuntimeRecoverySessionGate.LiveModSettingsIoBlocked ||
                ProfileProductionWriteGate.IsUncertain)
            {
#if DEBUG
                if (!l3f1RecoveryWarningLogged)
                {
                    l3f1RecoveryWarningLogged = true;
                    Log.Message("[XPC-L10N-L3F1][KEYED_SAFETY_UI] " +
                        "version=XPC-L10N-L3F1-SAFETY-WARNINGS-20261112-R1; " +
                        "screen=RECOVERY_LOCK; keys=11; sampleKey=XPC_L3F1_RecoveryLock; sample=" +
                        "XPC_L3F1_RecoveryLock".Translate().ToString().Replace('\n', ' ') +
                        "; build=" + BuildId);
                }
#endif
                Widgets.Label(inRect, "XPC_L3F1_RecoveryLock".Translate().ToString());
                return;
            }
            settingsWindow.Draw(inRect);
        }
    }
}
