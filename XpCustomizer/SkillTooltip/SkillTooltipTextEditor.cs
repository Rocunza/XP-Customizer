using System;

namespace XpCustomizer.SkillTooltip
{
    /// <summary>
    /// TT1-HF1: engine-independent string edits. Skill heading lookup is
    /// line-anchored and fail-closed so localized saturation summaries and
    /// other mods' descriptive text cannot be mistaken for the heading.
    /// </summary>
    internal static class SkillTooltipTextEditor
    {
        internal static string ReplaceAllExact(
            string result,
            string originalText,
            string replacementText)
        {
            return result.Replace(originalText, replacementText);
        }

        internal static void ReplaceHeadingRange(
            ref string result,
            int headingStart,
            int headingEnd,
            string replacementLine)
        {
            result =
                result.Remove(
                    headingStart,
                    headingEnd - headingStart);

            result =
                result.Insert(
                    headingStart,
                    replacementLine);
        }

        internal static string InsertModifierLines(
            string result,
            int sectionEnd,
            string learningSpeedLines)
        {
            return result.Insert(sectionEnd, "\n" + learningSpeedLines);
        }

        internal static bool TryFindLearningSpeedSection(
            string result,
            string learningSpeedLabel,
            out int headingStart,
            out int headingEnd,
            out int sectionEnd)
        {
            headingStart = -1;
            headingEnd = -1;
            sectionEnd = -1;

            if (string.IsNullOrEmpty(result) ||
                string.IsNullOrEmpty(learningSpeedLabel))
            {
                return false;
            }

            // HF1: require one complete, line-anchored heading. The Korean
            // "LearnedMaxToday" sentence contains the words "학습 속도"
            // before the real heading; a first-substring search deleted that
            // saturation warning and left two speed headings on screen.
            // Ambiguous headings are deliberately left untouched.
            int cursorLine = 0;
            int matchStart = -1;
            int matchEnd = -1;

            while (cursorLine < result.Length)
            {
                int lineEnd = result.IndexOf('\n', cursorLine);
                if (lineEnd < 0)
                    lineEnd = result.Length;

                if (IsLearningSpeedHeadingLine(
                        result, cursorLine, lineEnd, learningSpeedLabel))
                {
                    if (matchStart >= 0)
                        return false; // multiple candidates: fail closed

                    matchStart = cursorLine;
                    matchEnd = lineEnd;
                }

                if (lineEnd == result.Length)
                    break;

                cursorLine = lineEnd + 1;
            }

            if (matchStart < 0)
                return false;

            headingStart = matchStart;
            headingEnd = matchEnd;

            // ------------------------------------------------------------
            // Find the end of the consecutive vanilla bullet block
            // ------------------------------------------------------------
            //
            // Vanilla currently produces:
            //
            // Learning speed: ...
            //   - Base value: ...
            //   - Passion: ...
            //   - Global learning factor: ...   [optional]
            //   - Animals learning factor: ...  [optional]
            //
            // We do not depend on any particular bullet being present.
            // Consciousness is inserted after the final consecutive bullet.

            int cursor =
                headingEnd;


            while (cursor <
                   result.Length)
            {
                int nextLineStart =
                    cursor;


                if (result[nextLineStart] ==
                    '\n')
                {
                    nextLineStart++;
                }


                if (nextLineStart >=
                    result.Length)
                {
                    sectionEnd =
                        result.Length;

                    return true;
                }


                int nextLineEnd =
                    result.IndexOf(
                        '\n',
                        nextLineStart);


                if (nextLineEnd < 0)
                {
                    nextLineEnd =
                        result.Length;
                }


                string nextLine =
                    result
                        .Substring(
                            nextLineStart,
                            nextLineEnd -
                            nextLineStart)
                        .TrimStart();


                if (!nextLine.StartsWith(
                        "-",
                        StringComparison.Ordinal))
                {
                    sectionEnd =
                        cursor;

                    return true;
                }


                cursor =
                    nextLineEnd;
            }


            sectionEnd =
                result.Length;


            return true;
        }

        private static bool IsLearningSpeedHeadingLine(
            string text,
            int start,
            int end,
            string label)
        {
            int cursor = start;
            while (cursor < end &&
                   (text[cursor] == ' ' || text[cursor] == '\t'))
                cursor++;

            // Only Unity/RimWorld's known heading formatting may precede
            // the title. Do not skip arbitrary markup or other text.
            int openedTags = 0;
            while (cursor < end && text[cursor] == '<')
            {
                if (openedTags >= 4)
                    return false;

                int close = text.IndexOf('>', cursor);
                if (close < 0 || close >= end ||
                    !IsAllowedHeadingTag(text, cursor, close))
                    return false;

                cursor = close + 1;
                openedTags++;
            }

            if (end - cursor <= label.Length ||
                string.Compare(text, cursor, label, 0, label.Length,
                    StringComparison.OrdinalIgnoreCase) != 0)
                return false;

            // A complete heading has a colon immediately after the exact
            // label. This excludes "이후 학습 속도 x15%", "전역 학습 속도",
            // "Learning speed bonus", bullets and narrative sentences.
            char separator = text[cursor + label.Length];
            return separator == ':' || separator == '：';
        }

        private static bool IsAllowedHeadingTag(
            string text,
            int open,
            int close)
        {
            int length = close - open + 1;
            if (length == 3 &&
                (string.Compare(text, open, "<b>", 0, 3,
                     StringComparison.OrdinalIgnoreCase) == 0 ||
                 string.Compare(text, open, "<i>", 0, 3,
                     StringComparison.OrdinalIgnoreCase) == 0))
                return true;

            return (length > 8 &&
                    string.Compare(text, open, "<color=", 0, 7,
                        StringComparison.OrdinalIgnoreCase) == 0) ||
                   (length > 7 &&
                    string.Compare(text, open, "<size=", 0, 6,
                        StringComparison.OrdinalIgnoreCase) == 0) ||
                   (length > 7 &&
                    string.Compare(text, open, "<font=", 0, 6,
                        StringComparison.OrdinalIgnoreCase) == 0);
        }
    }
}
