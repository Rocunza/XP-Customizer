using UnityEngine;
using Verse;
using XpCustomizer.LearningCapacity;

namespace XpCustomizer.UI
{
    // LCUI1: same CLR type; original method bodies retained without behavioral edits.
    internal static partial class LearningCapacitySettingsUI
    {

        public static void NotifySettingsExternallyChanged()
        {
            ResetInputBuffers();
        }


        private static void ResetInputBuffers()
        {
            baseDailyCapInput.Reset();

            slowLearnerInput.Reset();
            fastLearnerInput.Reset();
            tooSmartInput.Reset();

            blissLobotomyInput.Reset();

            slowStudyInput.Reset();
            quickStudyInput.Reset();

            neuralSuperchargeInput.Reset();
            learningAssistantInput.Reset();
            ranchingCentralInput.Reset();
        }
    }
}
