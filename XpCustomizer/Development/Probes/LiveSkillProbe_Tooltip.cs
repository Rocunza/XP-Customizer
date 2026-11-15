#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.FurtherLearning;
using XpCustomizer.SkillTooltip;
using Verse;

namespace XpCustomizer
{
    // R1-B: split DEBUG-only probe code; original method bodies unchanged.
    internal static partial class XpCustomizerLiveSkillProbe
    {
        // ================================================================
        // S5B-A: read-only, real SkillUI tooltip integration audit
        // ================================================================
        // Purpose: verify our presentation sections appear ONCE in the
        // actual Harmony-patched tooltip. Compare the headline's computed
        // learning factor against the game's live (unsaturated) learning
        // rate, but REPORT possible external-mod disagreements as REVIEW,
        // never silently claim total learning-speed accuracy.
        // Does NOT change the tooltip or write to a Pawn/SkillRecord.
        private static void RunSkillTooltipReadOnlyAudit(Pawn pawn, SkillRecord skill)
        {
            try
            {
                MethodInfo method = AccessTools.Method(
                    typeof(SkillUI), "GetSkillDescription");
                if (method == null || !method.IsStatic ||
                    method.ReturnType != typeof(string) ||
                    method.GetParameters().Length != 1 ||
                    method.GetParameters()[0].ParameterType != typeof(SkillRecord))
                {
                    Log.Warning("[XP Customizer] DEBUG tooltip audit SKIPPED " +
                        "(S5B-A): unexpected GetSkillDescription signature.");
                    return;
                }

                float beforeLevelXp = skill.xpSinceLastLevel;
                float beforeDailyXp = skill.xpSinceMidnight;
                int beforeLevel = skill.Level;
                int beforeSkillCount = pawn.skills.skills.Count;

                // Reflection invokes the REAL game method and its Harmony
                // Postfixes. This is a read-only description query.
                string tooltip = method.Invoke(null, new object[] { skill }) as string;
                if (string.IsNullOrEmpty(tooltip))
                {
                    throw new InvalidOperationException("Empty skill tooltip.");
                }

                bool expectCapacity =
                    XpCustomizer.SkillTooltip.SkillTooltipService
                        .IsDailyCapacityPresentationActive();
                bool expectFurther =
                    XpCustomizer.SkillTooltip.SkillTooltipService
                        .IsFurtherLearningPresentationActive();
                string consciousnessLine =
                    XpCustomizer.SkillTooltip.SkillTooltipService
                        .BuildConsciousnessLearningLine(skill).Trim();
                bool expectConsciousness = consciousnessLine.Length > 0;
                string workFactorLine = SkillTooltipService
                    .BuildWorkFactorLearningLine(skill).Trim();
                bool expectWorkFactor = workFactorLine.Length > 0;

                // These headings are authored by OUR tooltip service and
                // are not translations of the vanilla headings.
                if (expectCapacity &&
                    !tooltip.Contains(SkillTooltipLocalizedPresentation.DailyCapHeading))
                {
                    throw new InvalidOperationException(
                        "Configured Daily Learning Cap section missing.");
                }
                if (expectFurther &&
                    !tooltip.Contains(SkillTooltipLocalizedPresentation.FurtherHeading))
                {
                    throw new InvalidOperationException(
                        "Configured Further Learning section missing.");
                }
                // Tooltip R2 regression: retain the REAL computed numeric cap,
                // without appending the redundant XP letters to the amount.
                if (expectCapacity)
                {
                    XpCustomizer.LearningCapacity.LearningCapacityBreakdown capBreakdown =
                        XpCustomizer.LearningCapacity.LearningCapacityService.GetBreakdown(skill);
                    if (capBreakdown == null)
                    {
                        throw new InvalidOperationException(
                            "Expected a valid Daily Learning Cap breakdown.");
                    }
                    int actualFinalCap = UnityEngine.Mathf.Max(
                        0, UnityEngine.Mathf.RoundToInt(capBreakdown.FinalCapacity));
                    int actualBaseCap = UnityEngine.Mathf.Max(
                        0, UnityEngine.Mathf.RoundToInt(capBreakdown.BaseCapacity));
                    int titleStart = tooltip.IndexOf(
                        SkillTooltipLocalizedPresentation.DailyCapHeading, StringComparison.Ordinal);
                    int titleEnd = tooltip.IndexOf('\n', titleStart);
                    if (titleEnd < 0) titleEnd = tooltip.Length;
                    string capTitleLine = tooltip.Substring(
                        titleStart, titleEnd - titleStart);
                    if (!capTitleLine.Contains(" (" + actualFinalCap + ")") ||
                        capTitleLine.Contains(" XP)"))
                    {
                        throw new InvalidOperationException(
                            "Daily Learning Cap must show the computed number without XP units.");
                    }
                    int baseStart = tooltip.IndexOf(
                        SkillTooltipLocalizedPresentation.BaseCapacityLinePrefix, titleEnd, StringComparison.Ordinal);
                    if (baseStart < 0)
                    {
                        throw new InvalidOperationException(
                            "Daily Learning Cap Base Capacity line missing.");
                    }
                    int baseEnd = tooltip.IndexOf('\n', baseStart);
                    if (baseEnd < 0) baseEnd = tooltip.Length;
                    string baseLine = tooltip.Substring(baseStart, baseEnd - baseStart);
                    if (!baseLine.Contains(" (" + actualBaseCap + ")") ||
                        baseLine.Contains(" XP)"))
                    {
                        throw new InvalidOperationException(
                            "Base Capacity must show the computed number without XP units.");
                    }
                }
                // S5B-B: the Learning Speed and Daily Capacity sections
                // legitimately print the SAME Consciousness line when their
                // configured multipliers happen to match (e.g. 69% / x69%).
                // Check duplicates INSIDE the learning-speed section only,
                // rather than treating every occurrence in the entire
                // tooltip as a duplicate.
                AssertTooltipSectionCounterRegression();
                int capacityHeading = tooltip.IndexOf(
                    SkillTooltipLocalizedPresentation.DailyCapHeading, StringComparison.Ordinal);
                int furtherHeading = tooltip.IndexOf(
                    SkillTooltipLocalizedPresentation.FurtherHeading, StringComparison.Ordinal);
                int speedSectionEnd = tooltip.Length;
                if (capacityHeading >= 0 && capacityHeading < speedSectionEnd)
                {
                    speedSectionEnd = capacityHeading;
                }
                if (furtherHeading >= 0 && furtherHeading < speedSectionEnd)
                {
                    speedSectionEnd = furtherHeading;
                }

                int speedLineCount = 0;
                int workFactorLineCount = 0;
                int capacityMatchingLineCount = 0;
                if (expectWorkFactor)
                {
                    workFactorLineCount = CountExactTooltipLines(
                        tooltip, workFactorLine, 0, speedSectionEnd);
                    if (workFactorLineCount != 1)
                    {
                        throw new InvalidOperationException(
                            "Expected exactly one Work Factor in Learning Speed " +
                            "(or fallback), found " + workFactorLineCount + ".");
                    }
                }
                else if (CountTooltipLinesWithPrefix(
                    tooltip, SkillTooltipLocalizedPresentation.WorkFactorLinePrefix, 0, speedSectionEnd) != 0)
                {
                    throw new InvalidOperationException(
                        "Work Factor leaked into a disabled or excluded skill.");
                }
                if (expectWorkFactor && expectConsciousness)
                {
                    int workIndex = tooltip.IndexOf(workFactorLine,
                        StringComparison.Ordinal);
                    int conscIndex = tooltip.IndexOf(consciousnessLine,
                        StringComparison.Ordinal);
                    string adjacentLines = SkillTooltipService
                        .BuildLearningSpeedModifierLines(skill);
                    if (workIndex < 0 || conscIndex < 0 || workIndex >= conscIndex ||
                        tooltip.IndexOf(adjacentLines, StringComparison.Ordinal) < 0)
                    {
                        throw new InvalidOperationException(
                            "Work Factor must appear immediately above Consc.");
                    }
                }
                if (expectConsciousness)
                {
                    speedLineCount = CountExactTooltipLines(
                        tooltip, consciousnessLine, 0, speedSectionEnd);
                    if (speedLineCount != 1)
                    {
                        throw new InvalidOperationException(
                            "Expected one Consciousness line in Learning Speed " +
                            "(or its fallback), found " + speedLineCount + ".");
                    }

                    // Diagnostic only: this can correctly be 1 when the
                    // capacity factor is identical to the speed factor.
                    if (capacityHeading >= 0)
                    {
                        int capacityEnd = furtherHeading > capacityHeading
                            ? furtherHeading : tooltip.Length;
                        capacityMatchingLineCount = CountExactTooltipLines(
                            tooltip, consciousnessLine,
                            capacityHeading, capacityEnd);
                    }
                }

                // Cons follow-up: Further Learning formats its own
                // contribution labels, separately from SkillTooltipService.
                // Verify the actual tooltip uses the same abbreviated label
                // whenever there is a nonzero Consciousness contribution.
                if (expectFurther && furtherHeading >= 0)
                {
                    FurtherLearningBreakdown furtherBreakdown =
                        FurtherLearningService.GetBreakdown(skill);
                    foreach (FurtherLearningContribution contribution in
                             furtherBreakdown.Contributions)
                    {
                        if (contribution == null ||
                            string.IsNullOrEmpty(contribution.Label))
                        {
                            continue;
                        }
                        if (contribution.Label.StartsWith(
                                "Mental threshold ", StringComparison.Ordinal))
                        {
                            if (tooltip.IndexOf(SkillTooltipLocalizedPresentation.WillpowerLinePrefix,
                                    furtherHeading, StringComparison.Ordinal) < 0 ||
                                tooltip.IndexOf("\n  - Mental threshold ",
                                    furtherHeading, StringComparison.Ordinal) >= 0)
                            {
                                throw new InvalidOperationException(
                                    "Further Learning Willpower tooltip label mismatch.");
                            }
                        }
                        if (contribution.Label.StartsWith(
                                "Consciousness ", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                "Further Learning still uses the long " +
                                "Consciousness tooltip label.");
                        }
                        if (contribution.Label.StartsWith(
                                "Cons ", StringComparison.Ordinal) &&
                            tooltip.IndexOf(
                                "\n  - " + SkillTooltipLocalizedPresentation.FurtherSourceLabel(contribution.Label) + ":",
                                furtherHeading, StringComparison.Ordinal) < 0)
                        {
                            throw new InvalidOperationException(
                                "Further Learning Cons label missing " +
                                "from actual SkillUI tooltip.");
                        }
                    }
                    string expectedDetails = SkillTooltipLocalizedPresentation
                        .BuildFurtherDetailLines(furtherBreakdown);
                    if (!tooltip.Contains(expectedDetails))
                        throw new InvalidOperationException(
                            "R5F1 Further Learning additive-first percentage detail mismatch.");
                }

                float liveRate = skill.LearnRateFactor(false);
                float displayedFormula = skill.passion.GetLearningFactor() *
                    pawn.GetStatValue(StatDefOf.GlobalLearningFactor);
                if (skill.def == SkillDefOf.Animals)
                {
                    displayedFormula *= pawn.GetStatValue(
                        StatDefOf.AnimalsLearningFactor);
                }
                displayedFormula *=
                    SkillTooltipService.GetWorkFactorLearningMultiplier(skill);
                displayedFormula *=
                    ConsciousnessLearningService.GetFactor(pawn);
                AssertFinite(liveRate, "S5B-A live learning rate");
                AssertFinite(displayedFormula,
                    "S5B-A displayed headline calculation");

                // Saturated learning deliberately includes Further Learning;
                // it must NOT be compared with the full-rate headline.
                bool comparable = !skill.LearningSaturatedToday &&
                    !DebugSettings.fastLearning;
                bool rateMatch = !comparable ||
                    Math.Abs(liveRate - displayedFormula) <=
                        Math.Max(0.005f,
                            Math.Abs(liveRate) * 0.005f);

                if (skill.xpSinceLastLevel != beforeLevelXp ||
                    skill.xpSinceMidnight != beforeDailyXp ||
                    skill.Level != beforeLevel ||
                    pawn.skills.skills.Count != beforeSkillCount)
                {
                    throw new InvalidOperationException(
                        "Tooltip audit altered saved skill state.");
                }

                string detail = "(S5B-A): skill=" + skill.def.defName +
                    ", WorkFactor=" + expectWorkFactor +
                    ", workFactorLines=" + workFactorLineCount +
                    ", Consciousness=" + expectConsciousness +
                    ", Capacity=" + expectCapacity +
                    ", FurtherLearning=" + expectFurther +
                    ", speedConsciousnessLines=" + speedLineCount +
                    ", capacitySameTextLines=" + capacityMatchingLineCount +
                    ", runtimeRate=" + liveRate.ToString("0.####") +
                    ", headlineFormula=" + displayedFormula.ToString("0.####") +
                    ", rateComparable=" + comparable +
                    ", savedSkillUntouched=True.";

                if (comparable && !rateMatch)
                {
                    // A discrepancy can arise from other mods changing the
                    // final rate. Do not treat a re-created vanilla formula
                    // as a perfect oracle for every mod configuration.
                    Log.Warning("[XP Customizer] DEBUG tooltip audit REVIEW " +
                        detail + " The displayed headline formula may " +
                        "omit a learning-rate modifier; inspect other mods.");
                }
                else
                {
                    Log.Message("[XP Customizer] DEBUG tooltip audit passed " +
                        detail + " Actual SkillUI queried; XP NOT changed.");
                }
            }
            catch (Exception error)
            {
                // This is a DEBUG probe, never block game loading.
                Log.Error("[XP Customizer] DEBUG tooltip audit FAILED " +
                    "(S5B-A): " + error);
            }
        }

        // Detect an unexpected Work Factor line even when its numeric
        // value differs from the expected factor.
        private static int CountTooltipLinesWithPrefix(
            string source, string prefix, int start, int end)
        {
            if (string.IsNullOrEmpty(source)) return 0;
            int count = 0;
            int cursor = start;
            while (cursor < end)
            {
                int next = source.IndexOf('\n', cursor);
                if (next < 0 || next > end) next = end;
                if (source.Substring(cursor, next - cursor).TrimStart()
                    .StartsWith(prefix, StringComparison.Ordinal)) count++;
                cursor = next + 1;
            }
            return count;
        }

        // Count whole, trimmed lines inside ONE section. Text matching
        // across unrelated tooltip sections would produce false positives.
        private static int CountExactTooltipLines(
            string text, string trimmedExpectedLine, int start, int end)
        {
            if (string.IsNullOrEmpty(text) ||
                string.IsNullOrEmpty(trimmedExpectedLine) ||
                start < 0 || end < start || end > text.Length)
            {
                return 0;
            }

            int count = 0;
            string expected = trimmedExpectedLine.Trim();
            int cursor = start;
            while (cursor < end)
            {
                int lineEnd = text.IndexOf('\n', cursor);
                if (lineEnd < 0 || lineEnd > end)
                {
                    lineEnd = end;
                }
                if (string.Equals(
                    text.Substring(cursor, lineEnd - cursor).Trim(),
                    expected, StringComparison.Ordinal))
                {
                    count++;
                }
                cursor = lineEnd + 1;
            }
            return count;
        }

        // Pure regression fixture. Two matching lines in DIFFERENT sections
        // are valid; two matching lines in Learning Speed are not.
        private static void AssertTooltipSectionCounterRegression()
        {
            const string line = "- Cons 0.69: x69%";
            const string expectedTooltip =
                "Learning speed: 24%\n  - Cons 0.69: x69%" +
                "\n\nDaily learning cap: 69% (2760)" +
                "\n  - Cons 0.69: x69%";
            int capacityStart = expectedTooltip.IndexOf(
                "Daily learning cap:", StringComparison.Ordinal);
            if (CountExactTooltipLines(
                    expectedTooltip, line, 0, capacityStart) != 1 ||
                CountExactTooltipLines(
                    expectedTooltip, line,
                    capacityStart, expectedTooltip.Length) != 1)
            {
                throw new InvalidOperationException(
                    "S5B-B section counter rejected a valid Speed+Capacity tooltip.");
            }

            const string duplicatedSpeed =
                "Learning speed: 24%\n  - Cons 0.69: x69%" +
                "\n  - Cons 0.69: x69%" +
                "\n\nDaily learning cap: 69% (2760)";
            int duplicateEnd = duplicatedSpeed.IndexOf(
                "Daily learning cap:", StringComparison.Ordinal);
            if (CountExactTooltipLines(
                    duplicatedSpeed, line, 0, duplicateEnd) != 2)
            {
                throw new InvalidOperationException(
                    "S5B-B section counter missed duplicate Learning Speed lines.");
            }
        }

    }
}
#endif
