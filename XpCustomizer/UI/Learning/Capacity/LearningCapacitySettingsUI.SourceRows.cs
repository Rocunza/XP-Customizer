using UnityEngine;
using Verse;
using XpCustomizer.LearningCapacity;

namespace XpCustomizer.UI
{
    // LCUI1: same CLR type; original method bodies retained without behavioral edits.
    internal static partial class LearningCapacitySettingsUI
    {

        private static void GetColumns(Rect row,
            out Rect label, out Rect mode, out Rect finalValue,
            out Rect example)
        {
            const float gap = 10f;
            float exampleWidth = row.width >= 670f ? 116f : 95f;
            float finalWidth = row.width >= 670f ? 128f : 108f;
            float modeWidth = 104f;
            float right = row.xMax;
            example = new Rect(right - exampleWidth, row.y, exampleWidth, row.height);
            right = example.x - gap;
            finalValue = new Rect(right - finalWidth, row.y, finalWidth, row.height);
            right = finalValue.x - gap;
            mode = new Rect(right - modeWidth, row.y, modeWidth, row.height);
            label = new Rect(row.x, row.y,
                Mathf.Max(0f, mode.x - gap - row.x), row.height);
        }


        private static void DrawSourceColumnHeaders(Listing_Standard listing)
        {
            Rect row = listing.GetRect(24f);
            Rect label, mode, finalValue, example;
            GetColumns(row, out label, out mode, out finalValue, out example);
            Color oldColor = GUI.color;
            GameFont oldFont = Text.Font;
            try
            {
                GUI.color = new Color(0.76f, 0.76f, 0.76f);
                Text.Font = GameFont.Tiny;
                Widgets.Label(label, "XPC_Learning_SourceHeader".Translate().ToString());
                Widgets.Label(mode, "XPC_Learning_ModeHeader".Translate().ToString());
                Widgets.Label(finalValue, "XPC_Learning_FinalHeader".Translate().ToString());
                Widgets.Label(example, "XPC_Learning_ExampleHeader".Translate().ToString());
            }
            finally
            {
                Text.Font = oldFont;
                GUI.color = oldColor;
            }
        }


        private enum SourceDescriptionKind { GlobalPercent, SlowStudy, RanchingAnimals }

        // Preserve LCUI1's reflection-tested six-argument method contract.
        private static void DrawSourceValue(
            Listing_Standard listing, string labelKey, float vanillaLf,
            ref bool customOverride, ref float customValue, NumericInputState inputState)
        {
            DrawSourceValueWithKind(listing, labelKey, vanillaLf,
                ref customOverride, ref customValue, inputState,
                SourceDescriptionKind.GlobalPercent);
        }

        private static void DrawSourceValueWithKind(
            Listing_Standard listing, string labelKey, float vanillaLf,
            ref bool customOverride, ref float customValue, NumericInputState inputState,
            SourceDescriptionKind descriptionKind)
        {
            Rect row = listing.GetRect(32f);
            Rect labelRect, modeRect, finalRect, exampleRect;
            GetColumns(row, out labelRect, out modeRect, out finalRect, out exampleRect);
            // Display translation must never control source semantics.
            string vanillaDescription = descriptionKind == SourceDescriptionKind.SlowStudy
                ? "XPC_Learning_SourceSlowDescription".Translate().ToString()
                : descriptionKind == SourceDescriptionKind.RanchingAnimals
                    ? "XPC_Learning_SourceRanchDescription".Translate().ToString()
                    : string.Format("XPC_Learning_SourceGlobalDescription".Translate().ToString(),
                        (vanillaLf > 0f ? "+" : "") + vanillaLf.ToString("0.##"));
            Widgets.Label(labelRect, labelKey.Translate().ToString() + " (" + vanillaDescription + ")");

            if (Widgets.ButtonText(modeRect, customOverride ? "XPC_Learning_ModeCustom".Translate().ToString() : "XPC_Learning_ModeAuto".Translate().ToString()))
            {
                if (!customOverride)
                {
                    // Enter manual mode at the current Auto value: no surprise
                    // jump in a Pawn's cap just because the mode was switched.
                    customValue = LearningCapacityService.GetV4SourcePercentagePoints(
                        false, customValue, vanillaLf,
                        XpCustomizerMod.Settings.learningCapacitySourceStrength);
                }
                customOverride = !customOverride;
                inputState.Reset();
            }

            if (!float.IsNaN(inputState.lastValue) &&
                !Mathf.Approximately(customValue, inputState.lastValue))
                inputState.buffer = null;

            float effective = LearningCapacityService.GetV4SourcePercentagePoints(
                customOverride, customValue, vanillaLf,
                XpCustomizerMod.Settings.learningCapacitySourceStrength);

            TextAnchor anchor = Text.Anchor;
            try
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                if (customOverride)
                {
                    Rect numeric = new Rect(finalRect.x, finalRect.y,
                        finalRect.width - 19f, finalRect.height);
                    Rect suffix = new Rect(numeric.xMax + 3f, finalRect.y, 16f, finalRect.height);
                    ProfileNumericFocus.Draw(numeric, ref customValue,
                        inputState,
                        XpCustomizerDefaults.LearningCapacityCustomMin,
                        XpCustomizerDefaults.LearningCapacityCustomMax);
                    customValue = LearningCapacityService.NormalizeSourcePercentagePoints(customValue);
                    effective = customValue;
                    Widgets.Label(suffix, "%");
                }
                else
                {
                    Widgets.Label(finalRect,
                        (effective > 0f ? "+" : "") + effective.ToString("0.##") + "%");
                }
                inputState.lastValue = customValue;

                float raw, intermediate;
                float cap = LearningCapacityService.CalculateFromComponents(
                    LearningCapacityService.NormalizeBaseDailyCap(
                        XpCustomizerMod.Settings.learningCapacityBaseDailyCap),
                    true, effective / 100f, 1f, out raw, out intermediate);
                Widgets.Label(exampleRect, cap.ToString("N0"));
            }
            finally
            {
                Text.Anchor = anchor;
            }
            TooltipHandler.TipRegion(modeRect, customOverride
                ? "XPC_Learning_ModeCustomTooltip".Translate().ToString()
                : "XPC_Learning_ModeAutoTooltip".Translate().ToString());
        }
    }
}
