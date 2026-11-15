#if DEBUG
using System;
using System.Reflection;
using RimWorld;
using Verse;
using XpCustomizer.LearningCapacity;
using XpCustomizer.SkillTooltip;

namespace XpCustomizer
{
    /// <summary>
    /// TT1-HF1: deterministic tooltip string fixtures, including the Korean
    /// daily-saturation decoy that previously hid the original warning.
    /// No real IMGUI, Pawn XP, Scribe or profile IO is exercised.
    /// </summary>
    internal static class SkillTooltipSplitSelfTests
    {
        private static int checks;

        private static void Check(bool condition, string label)
        {
            checks++;
            if (!condition)
                throw new InvalidOperationException("TT1_TOOLTIP_SPLIT:" + label);
        }

        private static bool Find(
            string text,
            string label,
            out int start,
            out int end,
            out int sectionEnd)
        {
            return SkillTooltipTextEditor.TryFindLearningSpeedSection(
                text, label, out start, out end, out sectionEnd);
        }

        private static string Edit(
            string original,
            string heading,
            string modifiers,
            string localizedLabel = "Learning speed")
        {
            int start;
            int end;
            int sectionEnd;
            if (!Find(original, localizedLabel, out start, out end, out sectionEnd))
                return original;
            string edited = original;
            SkillTooltipTextEditor.ReplaceHeadingRange(
                ref edited, start, end, heading);
            // Same as production: always locate the heading again after its
            // length changes, instead of reusing stale section offsets.
            if (Find(edited, localizedLabel, out start, out end, out sectionEnd))
                edited = SkillTooltipTextEditor.InsertModifierLines(
                    edited, sectionEnd, modifiers);
            return edited;
        }

        internal static void Run()
        {
            checks = 0;
            Check(!String.IsNullOrEmpty(XpCustomizerMod.BuildId) &&
                XpCustomizerMod.BuildId.StartsWith("XPC-", StringComparison.Ordinal),
                "BUILD_ID_PRESENT");
            Check(SkillTooltipComposer.HeadingFixVersion ==
                "XPC-L3F2-HF1-LINE-ANCHORED-HEADING-20261112-R1",
                "HF1_VERSION_STAMP");
            Check(typeof(Patch_SkillUI_GetSkillDescription).FullName ==
                "XpCustomizer.LearningCapacity.Patch_SkillUI_GetSkillDescription",
                "HARMONY_CLASS_IDENTITY");
            MethodInfo postfix = typeof(Patch_SkillUI_GetSkillDescription)
                .GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static);
            Check(postfix != null && postfix.ReturnType == typeof(void),
                "HARMONY_POSTFIX_PRESERVED");
            ParameterInfo[] args = postfix.GetParameters();
            Check(args.Length == 2 &&
                args[0].ParameterType == typeof(SkillRecord) &&
                args[1].ParameterType == typeof(string).MakeByRefType(),
                "HARMONY_POSTFIX_SIGNATURE");
            Check(typeof(SkillTooltipComposer).IsAbstract &&
                typeof(SkillTooltipTextEditor).IsAbstract &&
                typeof(SkillTooltipPresentationValues).IsAbstract,
                "STATIC_HELPERS");

            // Early guard: method remains no-op on null skill independently
            // of the original result, including odd whitespace and null.
            string sentinel = "  Original\r\n";
            Patch_SkillUI_GetSkillDescription.Postfix(null, ref sentinel);
            Check(sentinel == "  Original\r\n", "NULL_SKILL_PASSTHROUGH");
            sentinel = null;
            Patch_SkillUI_GetSkillDescription.Postfix(null, ref sentinel);
            Check(sentinel == null, "NULL_RESULT_PASSTHROUGH");
            sentinel = "";
            Patch_SkillUI_GetSkillDescription.Postfix(null, ref sentinel);
            Check(sentinel == "", "EMPTY_RESULT_PASSTHROUGH");

            int start;
            int end;
            int sectionEnd;
            Check(!Find(null, "Learning speed", out start, out end, out sectionEnd) &&
                start == -1 && end == -1 && sectionEnd == -1,
                "FIND_NULL_NO_MATCH");
            Check(!Find("", "Learning speed", out start, out end, out sectionEnd) &&
                start == -1 && end == -1 && sectionEnd == -1,
                "FIND_EMPTY_NO_MATCH");
            Check(!Find("No related heading\n", "Learning speed", out start,
                out end, out sectionEnd), "FIND_MISSING_NO_MATCH");

            const string simple = "Learning speed: 100%\n  - Base: 1\n  - Passion: 2\nNext";
            Check(Find(simple, "Learning speed", out start, out end, out sectionEnd),
                "FIND_CONSECUTIVE_BULLETS");
            Check(start == 0 && end == 20 && sectionEnd == 47,
                "FIND_CONSECUTIVE_BOUNDARIES");
            Check(Edit(simple, "Learning speed: 140%", "  - GW speed: x110%\n  - Cons: x105%") ==
                "Learning speed: 140%\n  - Base: 1\n  - Passion: 2" +
                "\n  - GW speed: x110%\n  - Cons: x105%\nNext",
                "GOLDEN_BULLETS_AND_RESCAN");
            Check(Edit("Head\nLearning speed: old\nTail", "Learning speed: 200%",
                "  - Cons: x120%") ==
                "Head\nLearning speed: 200%\n  - Cons: x120%\nTail",
                "GOLDEN_ZERO_BULLETS");
            Check(Edit("Learning speed: old", "Learning speed: 200%",
                "  - Cons: x120%") ==
                "Learning speed: 200%\n  - Cons: x120%",
                "GOLDEN_HEADLINE_AT_END");
            Check(Edit("Learning speed: old\n  - Base: 1", "Learning speed: 250%",
                "  - GW speed: x150%") ==
                "Learning speed: 250%\n  - Base: 1\n  - GW speed: x150%",
                "GOLDEN_BULLETS_AT_END");
            Check(Edit("Learning speed: old\n", "Learning speed: new",
                "  - Cons: x101%") ==
                "Learning speed: new\n\n  - Cons: x101%",
                "GOLDEN_TRAILING_NEWLINE");
            Check(Edit("  Learning speed: old\n  - Base: 1\n\nTail",
                "Learning speed: x2", "  - Cons: x1.1") ==
                "Learning speed: x2\n  - Base: 1\n  - Cons: x1.1\n\nTail",
                "GOLDEN_LEADING_SPACE_AND_BLANK_LINE");
            Check(Edit("Learning speed: old\r\n  - Base: 1\r\nTail",
                "Learning speed: new", "  - Cons: x1.2") ==
                "Learning speed: new\n  - Base: 1\r\n  - Cons: x1.2\nTail",
                "GOLDEN_CRLF_MIXED_PRESERVED");

            // HF1: descriptive text and preceding bullets must not steal
            // the actual heading. Preserve the exact untouched decoy line.
            Check(Edit("Note: Learning speed affects XP\nLearning speed: 1\n  - Base: 1\nTail",
                "Learning speed: 2", "  - Cons: x1.1") ==
                "Note: Learning speed affects XP\nLearning speed: 2\n  - Base: 1\n  - Cons: x1.1\nTail",
                "HF1_DESCRIPTION_DECOY_PRESERVED");
            Check(Find("  - Learning speed: old\nLearning speed: actual",
                "Learning speed", out start, out end, out sectionEnd) &&
                start == 24 && end == 46,
                "HF1_BULLET_IGNORED_REAL_HEADING_FOUND");
            Check(Edit("Learning speed: old\n• Unicode bullet\nTail",
                "Learning speed: new", "  - GW speed: x1.3") ==
                "Learning speed: new\n  - GW speed: x1.3\n• Unicode bullet\nTail",
                "LEGACY_UNICODE_BULLET_NOT_CONSECUTIVE");
            const string multipleHeadings =
                "Learning speed: old\n  - Base: 1\nLearning speed: again";
            Check(Edit(multipleHeadings, "Learning speed: new", "  - Cons: x1.1") ==
                multipleHeadings, "HF1_TWO_HEADINGS_NO_MUTATION");
            Check(!Find(multipleHeadings, "Learning speed", out start, out end,
                out sectionEnd) && start == -1 && end == -1 && sectionEnd == -1,
                "HF1_AMBIGUOUS_FAIL_CLOSED");
            Check(Edit("Other label\n", "Learning speed: 1", "  - Cons: 1") ==
                "Other label\n", "MISSING_HEADING_NO_TEXT_EDIT");
            Check(Find("learning SPEED: 20%", "Learning speed", out start,
                out end, out sectionEnd) && start == 0 && end == 19,
                "CASE_INSENSITIVE_LABEL_MATCH");

            // Korean Core's LearnedMaxToday has "이후 학습 속도 x15%"
            // before the true headline. It must survive unchanged.
            const string koreanSaturated =
                "원예\n오늘 최대로 학습함 (6149/5200, 이후 학습 속도 x15%)\n" +
                "<color=#FFFF00>학습 속도: </color>375%\n" +
                "  - 기본값: 100%\n  - 열정: x150%\n" +
                "  - 전역 학습 속도: x250%\n" +
                "전역 학습 속도: 250%\n" +
                "일일 학습 한도: 130%";
            const string koreanEdited =
                "원예\n오늘 최대로 학습함 (6149/5200, 이후 학습 속도 x15%)\n" +
                "<color=#FFFF00>학습 속도: </color>431%\n" +
                "  - 기본값: 100%\n  - 열정: x150%\n" +
                "  - 전역 학습 속도: x250%\n" +
                "  - 전역 작업 속도 1.8: x115%\n" +
                "전역 학습 속도: 250%\n" +
                "일일 학습 한도: 130%";
            // The replacement fixture uses a colored vanilla-style title.
            string koreanActual = Edit(koreanSaturated,
                "<color=#FFFF00>학습 속도: </color>431%",
                "  - 전역 작업 속도 1.8: x115%", "학습 속도");
            Check(koreanActual == koreanEdited,
                "HF1_KOREAN_SATURATED_WARNING_AND_HEADING");
            Check(koreanActual.Contains("이후 학습 속도 x15%") &&
                koreanActual.IndexOf("375%", StringComparison.Ordinal) < 0 &&
                koreanActual.Contains("학습 속도: </color>431%"),
                "HF1_SATURATED_NO_DOUBLE_SPEED");

            const string koreanUnsaturated =
                "조련\n학습 속도: 375%\n  - 기본값: 100%\n" +
                "  - 전역 학습 속도: x250%\n일일 학습 한도: 130%";
            Check(Edit(koreanUnsaturated, "학습 속도: 431%",
                "  - 전역 작업 속도 1.8: x115%", "학습 속도") ==
                "조련\n학습 속도: 431%\n  - 기본값: 100%\n" +
                "  - 전역 학습 속도: x250%\n" +
                "  - 전역 작업 속도 1.8: x115%\n일일 학습 한도: 130%",
                "HF1_KOREAN_UNSATURATED_SINGLE_SPEED");

            const string englishSaturated =
                "Plants\nLearned max today (6149/5200, further learning x15%)\n" +
                "<color=#FFFF00>Learning speed: </color>375%\n" +
                "  - Base value: 100%\n  - Passion: x150%\n" +
                "Daily learning cap: 130%";
            Check(Edit(englishSaturated,
                "<color=#FFFF00>Learning speed: </color>431%",
                "  - GW speed 1.8: x115%") ==
                "Plants\nLearned max today (6149/5200, further learning x15%)\n" +
                "<color=#FFFF00>Learning speed: </color>431%\n" +
                "  - Base value: 100%\n  - Passion: x150%\n" +
                "  - GW speed 1.8: x115%\nDaily learning cap: 130%",
                "HF1_ENGLISH_SATURATED_PARITY");

            Check(!Find("오늘 최대로 학습함 (이후 학습 속도 x15%)\n전역 학습 속도: 250%",
                "학습 속도", out start, out end, out sectionEnd),
                "HF1_NO_HEADING_NO_MUTATION");
            Check(!Find("학습 속도 증가: x115%", "학습 속도",
                out start, out end, out sectionEnd),
                "HF1_LABEL_PREFIX_NOT_HEADING");
            Check(!Find("<custom=tag>학습 속도: 100%", "학습 속도",
                out start, out end, out sectionEnd),
                "HF1_FOREIGN_MARKUP_FAIL_CLOSED");
            Check(!Find("<color=#FFF학습 속도: 100%", "학습 속도",
                out start, out end, out sectionEnd),
                "HF1_BROKEN_MARKUP_FAIL_CLOSED");
            Check(!Find("학습 속도: 100%\n학습 속도: 200%", "학습 속도",
                out start, out end, out sectionEnd),
                "HF1_KOREAN_DUPLICATE_FAIL_CLOSED");
            Check(Find("  <b><color=#FFFF00>학습 속도: </color></b>431%",
                "학습 속도", out start, out end, out sectionEnd) &&
                start == 0 && end == 43,
                "HF1_ALLOWED_RICHTEXT_OPENING_TAGS");
            Check(!Find("학습 속도: 100%", "", out start, out end,
                out sectionEnd), "HF1_EMPTY_LABEL_NO_MATCH");

            Check(SkillTooltipTextEditor.ReplaceAllExact(
                "Saturated / Saturated", "Saturated", "Custom") ==
                "Custom / Custom", "SATURATION_ALL_OCCURRENCES");
            Check(SkillTooltipTextEditor.ReplaceAllExact(
                "Saturated", "saturated", "Custom") ==
                "Saturated", "SATURATION_CASE_SENSITIVE");
            Check(SkillTooltipTextEditor.ReplaceAllExact(
                "Already custom", "Vanilla", "Custom") ==
                "Already custom", "SATURATION_OTHER_MOD_UNCHANGED");
            Check(SkillTooltipTextEditor.ReplaceAllExact(
                "a Vanilla b", "Vanilla", "Custom") ==
                "a Custom b", "SATURATION_SUBSTRING_NOT_LINE_MATCH");
            Check(SkillTooltipTextEditor.ReplaceAllExact(
                "Vanilla", "Vanilla", "Vanilla") ==
                "Vanilla", "SATURATION_IDENTICAL_REPLACEMENT");
            string rangeResult = "prefix\nLearning speed: 1\nsuffix";
            SkillTooltipTextEditor.ReplaceHeadingRange(
                ref rangeResult, 7, 24, "Learning speed: longer");
            Check(rangeResult == "prefix\nLearning speed: longer\nsuffix",
                "RANGE_REPLACE_LONGER_HEADLINE");
            rangeResult = "Learning speed: long";
            SkillTooltipTextEditor.ReplaceHeadingRange(
                ref rangeResult, 0, 20, "Learning speed: x1");
            Check(rangeResult == "Learning speed: x1",
                "RANGE_REPLACE_SHORTER_HEADLINE");
            // Original Postfix used two ref assignments: if Insert throws,
            // the successful Remove remains visible to Harmony's ref result.
            string partialOnFault = "Learning speed: old";
            bool sawInsertFault = false;
            try
            {
                SkillTooltipTextEditor.ReplaceHeadingRange(
                    ref partialOnFault, 0, partialOnFault.Length, null);
            }
            catch (ArgumentNullException)
            {
                sawInsertFault = true;
            }
            Check(sawInsertFault && partialOnFault == "",
                "LEGACY_PARTIAL_REF_RESULT_ON_INSERT_FAULT");

            Check(SkillTooltipTextEditor.InsertModifierLines(
                "abc", 3, "  - GWS: x1.1") ==
                "abc\n  - GWS: x1.1", "INSERT_END");
            Check(SkillTooltipTextEditor.InsertModifierLines(
                "abc", 0, "  - GWS: x1.1") ==
                "\n  - GWS: x1.1abc", "INSERT_START_LEGACY");
            Check(SkillTooltipTextEditor.InsertModifierLines(
                "abc\nTail", 3, "  - Cons: x1.2") ==
                "abc\n  - Cons: x1.2\nTail", "INSERT_MIDDLE");

            Log.Message("[XPC-TT1][TOOLTIP_SPLIT_AUDIT] status=PASS; checks=" + checks +
                "; harmonyClass=PRESERVED; signature=PRESERVED; " +
                "guards=NULL_ONLY_LIVE_DISABLED_NEUTRAL_NOT_RUN; " +
                "fixtures=GOLDEN_VALID_PLUS_KO_EN_SATURATION; headingRescan=VERIFIED; " +
                "saturationReplaceAll=VERIFIED; headingPolicy=LINE_ANCHORED_UNIQUE_HF1; " +
                "scope=PURE_STRING_FIXTURES_AND_REFLECTION; " +
                "realIMGUI=NOT_RUN; realHarmonyRegistration=NOT_RUN; " +
                "pawnRates=NOT_RUN; gameplayWrites=0; scribeCalls=0; " +
                "build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
