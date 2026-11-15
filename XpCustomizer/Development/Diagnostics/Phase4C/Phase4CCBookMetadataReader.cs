#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// R5-E2: read-only decoding of generated textbook skill-value dictionaries.
    /// A GainSkillExp doer stores its generated target skills as keys of Values/values,
    /// not as individual SkillDef fields. Never call OnReadingTick, OnBookGenerated,
    /// CanProgressSkill, or any gameplay mutation method from this reader.
    /// All collection walks are bounded; unresolved paths remain explicitly unresolved.
    /// </summary>
    internal static class Phase4CCBookMetadataReader
    {
        private const int MaxValueEntries = 64;
        private const int MaxBookDoers = 64;
        private const BindingFlags MemberFlags = BindingFlags.Instance |
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        internal static object ReadMember(object source, params string[] memberNames)
        {
            if (source == null) return null;
            for (Type t = source.GetType(); t != null; t = t.BaseType)
            {
                foreach (string name in memberNames)
                {
                    FieldInfo f = t.GetField(name, MemberFlags);
                    if (f != null) return f.GetValue(source);
                    PropertyInfo p = t.GetProperty(name, MemberFlags);
                    if (p != null && p.GetIndexParameters().Length == 0 &&
                        p.GetGetMethod(true) != null)
                        return p.GetValue(source, null);
                }
            }
            return null;
        }

        /// <summary>
        /// Only a successfully located collection counts as a BOOK_DECLARED source.
        /// An executed doer is independently observed, and never silently promoted
        /// to proof that ALL doers configured on a book have been enumerated.
        /// </summary>
        internal static bool TryGetDeclaredDoers(Verse.Book source,
            out IEnumerable list, out string path)
        {
            list = null;
            path = "UNRESOLVED_BOOK_OUTCOME_COLLECTION";
            if (source == null) return false;
            try
            {
                object comp = ReadMember(source, "BookComp", "bookComp");
                if (TryExtractDoerList(comp, out list))
                {
                    path = "BOOKCOMP_OUTCOMES";
                    return true;
                }
                if (TryExtractDoerList(source, out list))
                {
                    path = "BOOK_DIRECT_OUTCOMES";
                    return true;
                }
                // Version-specific subclasses may place book outcomes on a
                // book-specific ThingComp. Inspect only existing comp objects.
                IEnumerable comps = ReadMember(source, "AllComps", "allComps", "comps") as IEnumerable;
                if (comps != null)
                {
                    int inspected = 0;
                    foreach (object item in comps)
                    {
                        if (++inspected > MaxBookDoers) break;
                        if (item == null || item.GetType().Name.IndexOf("Book",
                                StringComparison.OrdinalIgnoreCase) < 0) continue;
                        if (TryExtractDoerList(item, out list))
                        {
                            path = "BOOK_THINGCOMP_OUTCOMES";
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                list = null;
                path = "BOOK_OUTCOME_READ_ERROR_" + ex.GetType().Name;
                return false;
            }
        }

        private static bool TryExtractDoerList(object value, out IEnumerable list)
        {
            list = null;
            if (value == null) return false;
            object raw = ReadMember(value, "outcomeDoers", "OutcomeDoers",
                "outcomes", "Outcomes", "bookOutcomeDoers", "BookOutcomeDoers",
                "doers", "Doers");
            // A string is IEnumerable but never a doer collection.
            list = raw is string ? null : raw as IEnumerable;
            return list != null;
        }

        /// <summary>
        /// Reads generated SkillDef keys from the instance's Values/values dictionary.
        /// Values containing no entries are not proof of a configured empty book.
        /// </summary>
        internal static bool ReadGeneratedSkills(object doer,
            HashSet<string> skills, out string evidence)
        {
            evidence = "UNRESOLVED_VALUES";
            if (doer == null || skills == null) return false;
            try
            {
                object raw = ReadMember(doer, "Values", "values");
                IEnumerable entries = raw is string ? null : raw as IEnumerable;
                if (entries == null)
                {
                    evidence = "VALUES_MEMBER_NOT_ENUMERABLE";
                    return false;
                }
                int inspected = 0;
                int resolved = 0;
                int invalid = 0;
                foreach (object element in entries)
                {
                    if (++inspected > MaxValueEntries)
                    {
                        evidence = "VALUES_TRUNCATED";
                        return false;
                    }
                    DictionaryEntry? de = element is DictionaryEntry ?
                        (DictionaryEntry?)((DictionaryEntry)element) : null;
                    object key = de.HasValue ? de.Value.Key : ReadMember(element, "Key", "key");
                    SkillDef skill = key as SkillDef;
                    if (skill == null || string.IsNullOrEmpty(skill.defName))
                    {
                        invalid++;
                        continue;
                    }
                    skills.Add(skill.defName.Replace(';', '_').Replace('\n', ' '));
                    resolved++;
                }
                evidence = inspected == 0 ? "VALUES_EMPTY" :
                    invalid > 0 ? "VALUES_PARTIAL_INVALID_KEYS" : "VALUES_SKILL_KEYS";
                return resolved > 0 && invalid == 0;
            }
            catch (Exception ex)
            {
                evidence = "VALUES_READ_ERROR_" + ex.GetType().Name;
                return false;
            }
        }

        internal static bool TryCollectBookSkills(IEnumerable items,
            HashSet<string> skills, out int doers, out int unresolved,
            out string evidence)
        {
            doers = 0;
            unresolved = 0;
            evidence = "BOOK_DECLARED_VALUES";
            if (items == null) return false;
            try
            {
                int inspected = 0;
                foreach (object item in items)
                {
                    if (++inspected > MaxBookDoers)
                    {
                        evidence = "BOOK_DOERS_TRUNCATED";
                        return false;
                    }
                    if (!(item is BookOutcomeDoerGainSkillExp)) continue;
                    doers++;
                    string detail;
                    if (!ReadGeneratedSkills(item, skills, out detail)) unresolved++;
                }
                evidence = unresolved > 0 ? "BOOK_DECLARED_VALUES_PARTIAL" :
                    "BOOK_DECLARED_VALUES";
                return unresolved == 0;
            }
            catch (Exception ex)
            {
                evidence = "BOOK_DOERS_READ_ERROR_" + ex.GetType().Name;
                return false;
            }
        }
    }
}
#endif
