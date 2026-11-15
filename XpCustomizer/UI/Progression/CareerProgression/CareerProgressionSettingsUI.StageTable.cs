using System.Collections.Generic;
using UnityEngine;
using Verse;
using XpCustomizer.Progression;

namespace XpCustomizer.UI
{
    // CPS1: same CLR type, original method bodies moved without behavioral changes.
    internal static partial class CareerProgressionSettingsUI
    {

        private static void DrawStageTableHeader(
            Listing_Standard listing)
        {
            Rect row = listing.GetRect(26f);

            GetStageColumnRects(
                row,
                out Rect stageRect,
                out Rect weightRect,
                out Rect shareRect,
                out Rect vanillaShareRect,
                out Rect xpRect,
                out Rect vanillaXpRect);

            TextAnchor oldAnchor = Text.Anchor;
            GameFont oldFont = Text.Font;
            Color oldColor = GUI.color;

            try
            {
                // Match the normal-sized headers in DecaySettingsUI.
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;

                Widgets.Label(stageRect, "XPC_L3B_HeaderStage".Translate().ToString());
                Widgets.Label(weightRect, "XPC_L3B_HeaderWeight".Translate().ToString());

                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(shareRect, "XPC_L3B_HeaderShare".Translate().ToString());
                Widgets.Label(xpRect, "XPC_L3B_HeaderCustomXp".Translate().ToString());

                GUI.color = VanillaColor;
                Widgets.Label(vanillaShareRect,
                    "XPC_L3B_HeaderVanillaShare".Translate().ToString());
                Widgets.Label(vanillaXpRect,
                    "XPC_L3B_HeaderVanillaXp".Translate().ToString());
            }
            finally
            {
                GUI.color = oldColor;
                Text.Anchor = oldAnchor;
                Text.Font = oldFont;
            }
        }


        private static float DrawStageDistributionRow(
            Listing_Standard listing,
            string label,
            float value,
            NumericInputState inputState,
            bool inputEnabled,
            float share,
            float vanillaShare,
            float xp,
            float vanillaXp,
            ref float lockedShare,
            NumericInputState lockedInput,
            bool canStartLock,
            float maxLockedSharePct)
        {
            Rect row = listing.GetRect(32f);

            Widgets.DrawHighlightIfMouseover(row);

            GetStageColumnRects(
                row,
                out Rect stageRect,
                out Rect weightRect,
                out Rect shareRect,
                out Rect vanillaShareRect,
                out Rect xpRect,
                out Rect vanillaXpRect);

            TextAnchor oldAnchor = Text.Anchor;
            bool oldGuiEnabled = GUI.enabled;

            try
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                Rect stageLabelRect = new Rect(stageRect.x, stageRect.y,
                    Mathf.Max(0f, stageRect.width - 36f), stageRect.height);
                Widgets.Label(stageLabelRect, label);

                Rect lockRect = new Rect(stageRect.xMax - 32f,
                    stageRect.y + 2f, 30f, stageRect.height - 4f);
                bool isLocked = lockedShare >= 0f;
                GUI.enabled = oldGuiEnabled && inputEnabled &&
                    (isLocked || canStartLock);
                if (Widgets.ButtonText(lockRect, isLocked ? "[L]" : "L"))
                {
                    if (isLocked)
                        lockedShare = -1f;
                    else
                        lockedShare = Mathf.Clamp(share * 100f,
                            0.1f, maxLockedSharePct) / 100f;
                    lockedInput.buffer = null;
                }
                GUI.enabled = oldGuiEnabled;
                TooltipHandler.TipRegion(lockRect,
                    "XPC_L3B_LockTip".Translate().ToString());
                isLocked = lockedShare >= 0f;

                const float suffixWidth = 20f;
                const float suffixGap = 4f;

                Rect fieldRect = new Rect(
                    weightRect.x,
                    weightRect.y,
                    weightRect.width - suffixWidth - suffixGap,
                    weightRect.height);

                Rect suffixRect = new Rect(
                    fieldRect.xMax + suffixGap,
                    weightRect.y,
                    suffixWidth,
                    weightRect.height);

                GUI.enabled = oldGuiEnabled && inputEnabled && !isLocked;
                ProfileNumericFocus.Draw(
                    fieldRect,
                    ref value,
                    inputState,
                    CareerXpDistributionService.MinStageWeight,
                    CareerXpDistributionService.MaxStageWeight);

                GUI.enabled = oldGuiEnabled;
                Widgets.Label(suffixRect, "x");

                Text.Anchor = TextAnchor.MiddleRight;
                if (isLocked && inputEnabled)
                {
                    float sharePct = lockedShare * 100f;
                    float maxPct = Mathf.Max(0.1f, maxLockedSharePct);
                    Rect shareFieldRect = new Rect(shareRect.x, shareRect.y,
                        Mathf.Max(20f, shareRect.width - 18f), shareRect.height);
                    Rect percentRect = new Rect(shareFieldRect.xMax,
                        shareRect.y, 18f, shareRect.height);
                    ProfileNumericFocus.Draw(shareFieldRect,
                        ref sharePct, lockedInput, 0.1f, maxPct);
                    lockedShare = Mathf.Clamp(sharePct,
                        0.1f, maxPct) / 100f;
                    Widgets.Label(percentRect, "%");
                    TooltipHandler.TipRegion(shareRect,
                        string.Format("XPC_L3B_LockedShareTip".Translate().ToString(),
                            (lockedShare * 100f).ToString("0.00"),
                            (share * 100f).ToString("0.00")));
                }
                else
                    Widgets.Label(shareRect,
                        (share * 100f).ToString("0.0") + "%");

                DrawVanillaComparison(vanillaShareRect,
                    (vanillaShare * 100f).ToString("0.0") + "%",
                    " (x" + FormatVanillaRatio(share, vanillaShare) + ")");

                Widgets.Label(
                    xpRect,
                    xp.ToString("N0") + " XP");

                DrawVanillaComparison(vanillaXpRect,
                    vanillaXp.ToString("N0"),
                    " (x" + FormatVanillaRatio(xp, vanillaXp) + ")");
            }
            finally
            {
                GUI.enabled = oldGuiEnabled;
                Text.Anchor = oldAnchor;
            }

            return Mathf.Clamp(
                value,
                CareerXpDistributionService.MinStageWeight,
                CareerXpDistributionService.MaxStageWeight);
        }


        private static void DrawVanillaComparison(
            Rect rect,
            string vanillaValue,
            string comparisonSuffix)
        {
            Color oldColor = GUI.color;
            TextAnchor oldAnchor = Text.Anchor;
            try
            {
                Text.Anchor = TextAnchor.MiddleRight;

                // Text.CalcSize uses the currently active RimWorld font/scale.
                // Reserve exactly the suffix width to keep both labels aligned.
                float suffixWidth = Mathf.Min(rect.width,
                    Text.CalcSize(comparisonSuffix).x);
                Rect valueRect = new Rect(rect.x, rect.y,
                    Mathf.Max(0f, rect.width - suffixWidth), rect.height);
                Rect suffixRect = new Rect(rect.xMax - suffixWidth, rect.y,
                    suffixWidth, rect.height);

                GUI.color = VanillaColor;
                Widgets.Label(valueRect, vanillaValue);
                GUI.color = oldColor;
                Widgets.Label(suffixRect, comparisonSuffix);
            }
            finally
            {
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;
            }
        }


        private static string FormatVanillaRatio(
            float currentValue,
            float vanillaValue)
        {
            // Vanilla requirements for every valid level range are positive.
            // Keep this guard so future data changes cannot display NaN/Inf.
            if (vanillaValue <= 0f ||
                float.IsNaN(currentValue) ||
                float.IsInfinity(currentValue))
            {
                return "--";
            }

            return (currentValue / vanillaValue).ToString("0.00");
        }


        private static void GetStageColumnRects(
            Rect row,
            out Rect stageRect,
            out Rect weightRect,
            out Rect shareRect,
            out Rect vanillaShareRect,
            out Rect xpRect,
            out Rect vanillaXpRect)
        {
            const float columnGap = 10f;

            // Keep the original weight input usable while using the large
            // Stage column's spare space for explicit Vanilla comparisons.
            // Widths contract gently when the game UI is scaled up.
            float widthFactor = Mathf.Clamp01((row.width - 800f) / 400f);
            float weightWidth = Mathf.Lerp(112f, 145f, widthFactor);
            float shareWidth = Mathf.Lerp(67f, 88f, widthFactor);
            float vanillaShareWidth = Mathf.Lerp(142f, 170f, widthFactor);
            float xpWidth = Mathf.Lerp(134f, 170f, widthFactor);
            float vanillaXpWidth = Mathf.Lerp(188f, 225f, widthFactor);

            float stageWidth = Mathf.Max(
                0f,
                row.width - weightWidth - shareWidth -
                vanillaShareWidth - xpWidth - vanillaXpWidth -
                columnGap * 5f);

            stageRect = new Rect(row.x, row.y, stageWidth, row.height);
            weightRect = new Rect(stageRect.xMax + columnGap, row.y,
                weightWidth, row.height);
            shareRect = new Rect(weightRect.xMax + columnGap, row.y,
                shareWidth, row.height);
            vanillaShareRect = new Rect(shareRect.xMax + columnGap, row.y,
                vanillaShareWidth, row.height);
            xpRect = new Rect(vanillaShareRect.xMax + columnGap, row.y,
                xpWidth, row.height);
            vanillaXpRect = new Rect(xpRect.xMax + columnGap, row.y,
                vanillaXpWidth, row.height);
        }


        private static int CountShareLocks(List<float> locks)
        {
            int count = 0;
            if (locks != null)
                for (int i = 0; i < locks.Count; i++)
                    if (locks[i] >= 0f) count++;
            return count;
        }


        private static float GetMaxLockedSharePct(
            List<float> locks, int stageIndex)
        {
            float totalOthersPct = 0f;
            int othersLocked = 0;
            for (int i = 0; i < locks.Count; i++)
            {
                if (i == stageIndex || locks[i] < 0f) continue;
                totalOthersPct += locks[i] * 100f;
                othersLocked++;
            }
            // Leave room for the unlocked remainder and all other stages.
            return Mathf.Max(0.1f, Mathf.Min(99.9f,
                100f - totalOthersPct - (locks.Count - 1 - othersLocked) * 0.1f));
        }
    }
}
