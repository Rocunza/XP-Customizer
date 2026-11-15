using System.Collections.Generic;
using UnityEngine;
using Verse;
using XpCustomizer.Progression;

namespace XpCustomizer.UI
{
    // CPS1: same CLR type, original method bodies moved without behavioral changes.
    internal static partial class CareerProgressionSettingsUI
    {

        private static void EnsureLockedShareStorage(
            XpCustomizerSettings settings, int stageCount)
        {
            if (settings.careerXpStageLockedShares == null)
                settings.careerXpStageLockedShares = new List<float>();
            var locks = settings.careerXpStageLockedShares;
            while (locks.Count < stageCount) locks.Add(-1f);
            while (locks.Count > stageCount) locks.RemoveAt(locks.Count - 1);
            for (int i = 0; i < locks.Count; i++)
                locks[i] = CareerXpDistributionService.NormalizeLockedStageShare(
                    locks[i]);
            if (stageCount <= 1 || CountShareLocks(locks) >= stageCount)
                locks[stageCount - 1] = -1f;
        }


        private static void ResetStageLockedShareInputStates()
        {
            for (int i = 0; i < stageLockedShareInputs.Count; i++)
                stageLockedShareInputs[i].Reset();
        }


        private static void EnsureStageLockedShareInputStates(int stageCount)
        {
            while (stageLockedShareInputs.Count < stageCount)
                stageLockedShareInputs.Add(new NumericInputState());
            while (stageLockedShareInputs.Count > stageCount)
                stageLockedShareInputs.RemoveAt(stageLockedShareInputs.Count - 1);
        }


        private static void ResetStageWeightInputStates()
        {
            for (int i = 0;
                 i < stageWeightInputs.Count;
                 i++)
            {
                stageWeightInputs[i].Reset();
            }
        }


        private static void EnsureStageWeightStorage(
            XpCustomizerSettings settings,
            int stageCount)
        {
            if (settings.careerXpStageWeights == null)
            {
                settings.careerXpStageWeights =
                    new List<float>();
            }


            while (settings.careerXpStageWeights.Count < stageCount)
            {
                settings.careerXpStageWeights.Add(
                    1f);
            }


            while (settings.careerXpStageWeights.Count > stageCount)
            {
                settings.careerXpStageWeights.RemoveAt(
                    settings.careerXpStageWeights.Count - 1);
            }


            for (int i = 0;
                 i < settings.careerXpStageWeights.Count;
                 i++)
            {
                settings.careerXpStageWeights[i] =
                    CareerXpDistributionService.NormalizeStageWeight(
                        settings.careerXpStageWeights[i]);
            }
        }


        private static void EnsureStageWeightInputStates(
            int stageCount)
        {
            while (stageWeightInputs.Count < stageCount)
            {
                stageWeightInputs.Add(
                    new NumericInputState());
            }


            while (stageWeightInputs.Count > stageCount)
            {
                stageWeightInputs.RemoveAt(
                    stageWeightInputs.Count - 1);
            }
        }


#if DEBUG
        // N1-R0 diagnostic ONLY: identifies the pending XP-owned career editor.
        // Never modifies a buffer, focus, live setting, profile or file.
        // The returned identifier is a field category, never raw input text.
        internal static string DiagnoseBlockedNumericField(string rejectedControl)
        {
            return DiagnoseCareerNumericField(totalMultiplierInput,
                stageWeightInputs, stageLockedShareInputs, rejectedControl);
        }

        // Pure helper also exercised using detached NumericInputState fixtures.
        internal static string DiagnoseCareerNumericField(
            NumericInputState total, IList<NumericInputState> weights,
            IList<NumericInputState> locks, string rejectedControl)
        {
            if (!string.IsNullOrEmpty(rejectedControl) &&
                ProfileNumericFocus.IsNumericControl(rejectedControl))
            {
                if (Matches(total, rejectedControl)) return "TOTAL_MULTIPLIER";
                if (weights != null)
                    for (int i = 0; i < weights.Count; i++)
                        if (Matches(weights[i], rejectedControl)) return "STAGE_WEIGHT_" + i;
                if (locks != null)
                    for (int i = 0; i < locks.Count; i++)
                        if (Matches(locks[i], rejectedControl)) return "STAGE_LOCK_" + i;
                return "REJECTED_CONTROL_NOT_IN_CAREER";
            }

            string candidate = null;
            int pending = 0;
            if (IsPending(total)) { candidate = "TOTAL_MULTIPLIER"; pending++; }
            if (weights != null)
                for (int i = 0; i < weights.Count; i++)
                    if (IsPending(weights[i])) { candidate = "STAGE_WEIGHT_" + i; pending++; }
            if (locks != null)
                for (int i = 0; i < locks.Count; i++)
                    if (IsPending(locks[i])) { candidate = "STAGE_LOCK_" + i; pending++; }
            return pending == 1 ? candidate :
                pending > 1 ? "MULTIPLE_CAREER_PENDING" : "UNRESOLVED";
        }

        private static bool Matches(NumericInputState state, string rejectedControl)
        {
            return state != null && !string.IsNullOrEmpty(state.FocusControlName) &&
                string.Equals(state.FocusControlName, rejectedControl,
                    System.StringComparison.Ordinal);
        }

        private static bool IsPending(NumericInputState state)
        {
            return state != null &&
                XpCustomizer.Profiles.ProfileEditorReadinessService.ClassifyObservedText(
                    state.buffer, state.FocusObserved, state.WasFocused,
                    string.Equals(state.LastObservedBuffer, state.buffer,
                        System.StringComparison.Ordinal)) ==
                XpCustomizer.Profiles.ProfileEditorReadinessCode.PendingEdit;
        }
#endif

        // N1-R1: exactly one known malformed TOTAL_MULTIPLIER draft is eligible.
        // Do NOT call NotifySettingsExternallyChanged: it would also discard
        // weight/lock input and the separate career boundary editor's draft.
        internal static bool TryPrepareTotalMultiplierRecovery(
            out CareerTotalMultiplierRecovery ticket)
        {
            return CareerTotalMultiplierRecovery.TryCapture(totalMultiplierInput,
                stageWeightInputs, stageLockedShareInputs, XpCustomizerMod.Settings,
                out ticket);
        }

        internal static bool TryDiscardTotalMultiplierRecovery(
            CareerTotalMultiplierRecovery ticket)
        {
            return ticket != null && ticket.TryDiscard(totalMultiplierInput,
                stageWeightInputs, stageLockedShareInputs, XpCustomizerMod.Settings);
        }

        public static void NotifySettingsExternallyChanged()
        {
            totalMultiplierInput.Reset();

            ResetStageWeightInputStates();
            ResetStageLockedShareInputStates();

            CareerXpStageBoundaryEditorUI
                .NotifySettingsExternallyChanged();
        }
    }
}
