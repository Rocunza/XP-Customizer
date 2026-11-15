using UnityEngine;
using Verse;
using XpCustomizer.LearningCapacity;

namespace XpCustomizer.UI
{
    // LCUI1: same CLR type; original method bodies retained without behavioral edits.
    internal static partial class LearningCapacitySettingsUI
    {

        private static void DrawBaseDailyCapacity(
            Listing_Standard listing)
        {
            XpCustomizerSettings settings =
                XpCustomizerMod.Settings;


            float value =
                settings.learningCapacityBaseDailyCap;


            if (!float.IsNaN(
                    baseDailyCapInput.lastValue) &&
                !Mathf.Approximately(
                    value,
                    baseDailyCapInput.lastValue))
            {
                baseDailyCapInput.buffer = null;
            }


            string label = "XPC_Learning_DailyCapBase".Translate().ToString();
            NumericRowLayout.Placement geometry = NumericRowLayout.Allocate(
                listing, label, 120f, 30f, 30f, 30f);
            Rect fieldRect = geometry.Field;
            Rect suffixRect = geometry.Suffix;
            Widgets.Label(geometry.Label, label);


            ProfileNumericFocus.Draw(
                fieldRect,
                ref value,
                baseDailyCapInput,
                XpCustomizerDefaults
                    .LearningCapacityBaseDailyCapMin,
                XpCustomizerDefaults
                    .LearningCapacityBaseDailyCapMax);


            value =
                LearningCapacityService
                    .NormalizeBaseDailyCap(
                        value);


            settings.learningCapacityBaseDailyCap =
                value;

            baseDailyCapInput.lastValue =
                value;


            TextAnchor oldAnchor =
                Text.Anchor;

            Text.Anchor =
                TextAnchor.MiddleLeft;

            Widgets.Label(
                suffixRect,
                "XP");

            Text.Anchor =
                oldAnchor;


            VanillaReferenceAccentUI.Draw(listing,
                "XPC_L3F3_DailyCapRangePart", "XPC_L3F3_DailyCapVanillaPart",
                false, "XPC_Learning_DailyCapRange");

            SettingsUIHelpers.DrawReferenceText(
                listing,
                "XPC_Learning_DailyCapZero".Translate().ToString());
        }
    }
}
