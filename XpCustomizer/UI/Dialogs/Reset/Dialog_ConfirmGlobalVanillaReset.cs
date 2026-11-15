using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace XpCustomizer.UI
{
    /// <summary>
    /// R1: pure per-dialog consent latch. No callback is invoked by creation,
    /// cancellation, Escape or X. The confirm action is consumed BEFORE it runs.
    /// </summary>
    internal sealed class GlobalVanillaResetConfirmationGate
    {
        private bool finished;
        private bool attempted;
        private bool succeeded;

        internal bool IsPending { get { return !finished; } }
        internal bool Attempted { get { return attempted; } }
        internal bool Succeeded { get { return succeeded; } }

        internal bool TryConfirm(Action apply)
        {
            if (finished) return false;
            if (apply == null) throw new ArgumentNullException("apply");
            finished = true;
            attempted = true;
            apply();
            succeeded = true;
            return true;
        }

        internal bool TryCancel()
        {
            if (finished) return false;
            finished = true;
            return true;
        }
    }

    internal sealed class GlobalVanillaResetBlockedException : Exception
    {
        internal GlobalVanillaResetBlockedException()
            : base("A settings recovery or production write lock is active.") { }
    }

    /// <summary>
    /// R4: confirmation UI for the single global install-default reset.
    /// Keeps R1 one-shot consent and recovery-block checks.
    /// </summary>
    internal sealed class Dialog_ConfirmGlobalVanillaReset : Window
    {
        private readonly Action confirmedReset;
        private readonly Action closed;
        private readonly GlobalVanillaResetConfirmationGate gate =
            new GlobalVanillaResetConfirmationGate();
        private string note = "";
        private Vector2 warningScrollPosition = Vector2.zero;

        internal Dialog_ConfirmGlobalVanillaReset(Action onConfirmed, Action onClosed)
        {
            if (onConfirmed == null) throw new ArgumentNullException("onConfirmed");
            confirmedReset = onConfirmed;
            closed = onClosed;
            doCloseX = true;
            closeOnCancel = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            draggable = true;
#if DEBUG
            Log.Message("[XPC-L10N-L3F1][KEYED_SAFETY_UI] " +
                "version=XPC-L10N-L3F1-SAFETY-WARNINGS-20261112-R1; " +
                "screen=GLOBAL_RESET_DIALOG; keys=11; sampleKey=XPC_L3F1_ResetTitle; sample=" +
                "XPC_L3F1_ResetTitle".Translate().ToString() +
                "; build=" + XpCustomizerMod.BuildId);
#endif
        }

        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(
                    Mathf.Min(560f, Mathf.Max(340f, Verse.UI.screenWidth - 48f)),
                    Mathf.Min(290f, Mathf.Max(250f, Verse.UI.screenHeight - 48f)));
            }
        }

        public override void DoWindowContents(Rect rect)
        {
            // L3-F1: scroll ONLY the warning text. Keep confirmation and cancel
            // in their original left/right positions and keep their consent gate.
            string title = "XPC_L3F1_ResetTitle".Translate().ToString();
            string warning = "XPC_L3F1_ResetWarning".Translate().ToString();
            string detail = "XPC_L3F1_ResetDetail".Translate().ToString();
            float buttonY = rect.height - 37f;
            Rect textViewport = new Rect(0f, 45f, rect.width,
                Mathf.Max(1f, buttonY - 53f));
            float textWidth = Mathf.Max(1f, textViewport.width - 20f);

            GameFont previous = Text.Font;
            try
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(0f, 0f, rect.width, 36f), title);
                Text.Font = GameFont.Small;
                float warningHeight = Mathf.Max(69f, Text.CalcHeight(warning, textWidth) + 4f);
                Text.Font = GameFont.Small;
                float detailHeight = Mathf.Max(62f, Text.CalcHeight(detail, textWidth) + 4f);
                float noteHeight = note.Length == 0 ? 0f :
                    Mathf.Max(30f, Text.CalcHeight(note, textWidth) + 4f);
                float contentHeight = warningHeight + 9f + detailHeight +
                    (noteHeight == 0f ? 0f : 9f + noteHeight) + 5f;
                Rect content = new Rect(0f, 0f, textWidth,
                    Mathf.Max(textViewport.height, contentHeight));
                Widgets.BeginScrollView(textViewport, ref warningScrollPosition, content);
                try
                {
                    float textY = 0f;
                    Text.Font = GameFont.Small;
                    Widgets.Label(new Rect(0f, textY, textWidth, warningHeight), warning);
                    textY += warningHeight + 9f;
                    Text.Font = GameFont.Small;
                    Widgets.Label(new Rect(0f, textY, textWidth, detailHeight), detail);
                    textY += detailHeight + 9f;
                    if (note.Length > 0)
                        Widgets.Label(new Rect(0f, textY, textWidth, noteHeight), note);
                }
                finally { Widgets.EndScrollView(); }
            }
            finally { Text.Font = previous; }

            float buttonWidth = Mathf.Min(172f, (rect.width - 12f) * 0.49f);
            float y = rect.height - 37f;
            Rect left = new Rect(rect.width - 2f * buttonWidth - 8f,
                y, buttonWidth, 32f);
            Rect right = new Rect(rect.width - buttonWidth,
                y, buttonWidth, 32f);
            if (Widgets.ButtonText(right, "XPC_L3F1_Cancel".Translate().ToString()))
            {
                Close();
                return;
            }

            bool enabled = GUI.enabled;
            try
            {
                GUI.enabled = enabled && gate.IsPending;
                if (Widgets.ButtonText(left, "XPC_L3F1_Confirm".Translate().ToString()))
                {
                    Log.Message("[XPC-RESET][GLOBAL_RESET_UI] kind=MOD_DEFAULTS; action=CONFIRM; " +
                        "oneShot=True; build=" + XpCustomizerMod.BuildId);
                    bool applied = false;
                    try
                    {
                        applied = gate.TryConfirm(confirmedReset);
                    }
                    catch (GlobalVanillaResetBlockedException)
                    {
                        note = "XPC_L3F1_ResetBlocked".Translate().ToString();
                        Log.Warning("[XPC-RESET][GLOBAL_RESET_UI] kind=MOD_DEFAULTS; " +
                            "action=BLOCKED; reason=RECOVERY_OR_WRITE_LOCK; " +
                            "resetCalls=0; build=" + XpCustomizerMod.BuildId);
                        Messages.Message(note, MessageTypeDefOf.RejectInput, false);
                    }
                    catch (Exception ex)
                    {
                        // The legacy preset may have partially changed memory
                        // or GeneDefs before an exception. Do NOT retry, write
                        // settings XML, or misreport this as a successful reset.
                        note = "XPC_L3F1_ResetFailed".Translate().ToString();
                        Log.Error("[XPC-RESET][GLOBAL_RESET_UI] kind=MOD_DEFAULTS; " +
                            "action=FAILED; stage=RESET_OR_REFRESH; " +
                            "partialRuntimeChangesPossible=True; exception=" + ex +
                            "; build=" + XpCustomizerMod.BuildId);
                        Messages.Message(note, MessageTypeDefOf.RejectInput, false);
                    }
                    if (applied)
                    {
                        Log.Message("[XPC-RESET][GLOBAL_RESET_UI] kind=MOD_DEFAULTS; " +
                            "action=CONFIRMED_APPLIED; resetCalls=1; build=" +
                            XpCustomizerMod.BuildId);
                        Close();
                    }
                }
            }
            finally { GUI.enabled = enabled; }
        }

        public override void PostClose()
        {
            base.PostClose();
            if (gate.TryCancel())
                Log.Message("[XPC-RESET][GLOBAL_RESET_UI] kind=MOD_DEFAULTS; action=CANCEL; " +
                    "resetCalls=0; uiRefresh=0; ownXmlWritesByDialog=0; " +
                    "profileJsonWrites=0; build=" + XpCustomizerMod.BuildId);
            if (closed != null)
            {
                try { closed(); }
                catch (Exception ex)
                {
                    Log.Warning("[XPC-RESET][GLOBAL_RESET_UI] action=CLOSE_CALLBACK_FAILURE; " +
                        "reason=" + ex.GetType().Name + "; build=" + XpCustomizerMod.BuildId);
                }
            }
        }
    }
}
