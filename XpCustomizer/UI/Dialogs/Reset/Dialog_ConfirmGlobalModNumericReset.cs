using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace XpCustomizer.UI
{
    /// <summary>R3-A confirmation for numeric-only reset, not Vanilla or Hard.</summary>
    internal sealed class Dialog_ConfirmGlobalModNumericReset : Window
    {
        private readonly Action confirmedReset;
        private readonly Action closed;
        private readonly GlobalVanillaResetConfirmationGate gate =
            new GlobalVanillaResetConfirmationGate();
        private string note = "";

        internal Dialog_ConfirmGlobalModNumericReset(Action onConfirmed, Action onClosed)
        {
            if (onConfirmed == null) throw new ArgumentNullException("onConfirmed");
            confirmedReset = onConfirmed;
            closed = onClosed;
            doCloseX = true;
            closeOnCancel = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            draggable = true;
        }

        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(
                    Mathf.Min(570f, Mathf.Max(340f, Verse.UI.screenWidth - 48f)),
                    Mathf.Min(300f, Mathf.Max(250f, Verse.UI.screenHeight - 48f)));
            }
        }

        public override void DoWindowContents(Rect rect)
        {
            GameFont previous = Text.Font;
            try
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0f, 0f, rect.width, 36f),
                    "Restore Mod Default Values?");
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(0f, 46f, rect.width, 80f),
                    "Reset numeric values and curve parameters across XP Customizer. " +
                    "Keep current ON/OFF, Auto/Manual choices, skill switches, " +
                    "curve modes and stage boundaries/locks.");
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(0f, 136f, rect.width, 54f),
                    "Yes discards any unfinished numeric input; Cancel does not reset it. " +
                    "Owned Biotech GeneDefs may update if ON. Saved profile JSON stays unchanged. " +
                    "Pause an active game before confirming.");
                if (note.Length > 0)
                    Widgets.Label(new Rect(0f, 198f, rect.width, 37f), note);
            }
            finally { Text.Font = previous; }

            float width = Mathf.Min(172f, (rect.width - 12f) * 0.49f);
            float y = rect.height - 37f;
            Rect left = new Rect(rect.width - 2f * width - 8f, y, width, 32f);
            Rect right = new Rect(rect.width - width, y, width, 32f);
            if (Widgets.ButtonText(right, "Cancel"))
            {
                Close();
                return;
            }
            bool wasEnabled = GUI.enabled;
            try
            {
                GUI.enabled = wasEnabled && gate.IsPending;
                if (Widgets.ButtonText(left, "Yes, Restore"))
                {
                    Log.Message("[XPC-RESET][GLOBAL_RESET_UI] kind=MOD_NUMERIC_VALUES; " +
                        "action=CONFIRM; oneShot=True; build=" + XpCustomizerMod.BuildId);
                    bool applied = false;
                    try { applied = gate.TryConfirm(confirmedReset); }
                    catch (GlobalNumericDefaultsException ex)
                    {
                        note = ex.RequiresRestart ?
                            "Reset recovery failed. Exit without saving and restart." :
                            "Reset not applied: " + ex.Reason;
                        Log.Warning("[XPC-RESET][GLOBAL_RESET_UI] kind=MOD_NUMERIC_VALUES; " +
                            "action=FAILED; reason=" + ex.Reason +
                            "; restartRequired=" + ex.RequiresRestart +
                            "; build=" + XpCustomizerMod.BuildId);
                        Messages.Message(note, MessageTypeDefOf.RejectInput, false);
                    }
                    catch (Exception ex)
                    {
                        RuntimeRecoverySessionGate.ReportLiveFinalVerificationFailure(
                            RuntimeRecoveryFailureStage.Unknown,
                            RuntimeRecoveryGateSource.LiveTransaction);
                        note = "Unexpected reset failure. Exit without saving and restart.";
                        Log.Error("[XPC-RESET][GLOBAL_RESET_UI] kind=MOD_NUMERIC_VALUES; " +
                            "action=FAILED_UNEXPECTED; reason=" + ex +
                            "; build=" + XpCustomizerMod.BuildId);
                        Messages.Message(note, MessageTypeDefOf.RejectInput, false);
                    }
                    if (applied)
                    {
                        Log.Message("[XPC-RESET][GLOBAL_RESET_UI] kind=MOD_NUMERIC_VALUES; " +
                            "action=CONFIRMED_APPLIED; applyAttempts=1; " +
                            "build=" + XpCustomizerMod.BuildId);
                        Close();
                    }
                }
            }
            finally { GUI.enabled = wasEnabled; }
        }

        public override void PostClose()
        {
            base.PostClose();
            if (gate.TryCancel())
                Log.Message("[XPC-RESET][GLOBAL_RESET_UI] kind=MOD_NUMERIC_VALUES; " +
                    "action=CANCEL; resetCalls=0; profileJsonWrites=0; " +
                    "ownXmlWritesByDialog=0; build=" + XpCustomizerMod.BuildId);
            if (closed != null)
            {
                try { closed(); }
                catch (Exception ex)
                { Log.Warning("[XPC-RESET] kind=MOD_NUMERIC_VALUES; " +
                    "action=CLOSE_CALLBACK_FAILURE; exception=" +
                    ex.GetType().Name + "; build=" + XpCustomizerMod.BuildId); }
            }
        }
    }
}
