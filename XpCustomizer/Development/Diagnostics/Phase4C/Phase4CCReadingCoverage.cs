#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// R5-E2: passive, bounded, book-aware reading telemetry.
    /// The book hook observes the source Book BEFORE vanilla dispatches its
    /// outcomes; the existing SkillRecord.Learn observer counts every reading
    /// attempt BEFORE first-seen sample quotas. Neither hook alters gameplay.
    /// Book outcome layouts are read conservatively. If they cannot be resolved,
    /// the report says UNRESOLVED rather than inferring a missing SkillDef call.
    /// </summary>
    internal static partial class Phase4CCLiveXpCapture
    {
        internal const string ReadingRevision = "4C-C-READING-SOURCE-COVERAGE-R5E2-20261106";
        private const int MaxReadingBookGroups = 16;
        private const int MaxReadingSkillGroups = 64;
        private const int MaxInspectedDoers = 64;
        private const int MaxDisplayedReadingSkillGroups = 32;
        private static MethodBase bookTickTarget;
        private static MethodBase textbookTickTarget;
        private static bool textbookTickHookInstalled;
        private static string textbookTickHookStatus = "NOT_STARTED";
        private static bool bookTickHookInstalled;
        private static string bookTickHookStatus = "NOT_STARTED";
        private static long readingBookTickCalls;
        private static long readingTextbookDoerCalls;
        private static long readingLearnAttempts;
        private static long readingLearnPositive;
        private static long readingLearnDisabled;
        private static long readingLearnNonPositive;
        private static long readingLearnBeyondRegularQuota;
        private static long readingLearnBeyondGroupQuota;
        private static long readingLearnBeyondReadingQuota;
        private static long readingLearnUnattributed;
        private static long readingDoerUnattributed;
        private static long readingTrackedOverflow;
        private static int readingMetadataErrors;
        private sealed class ReferenceIdentity : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) { return ReferenceEquals(a, b); }
            public int GetHashCode(object value) { return RuntimeHelpers.GetHashCode(value); }
        }
        private static readonly HashSet<object> inspectedDoers =
            new HashSet<object>(new ReferenceIdentity());
        private static readonly Dictionary<string, ReadingBookSnapshot> readingBooks =
            new Dictionary<string, ReadingBookSnapshot>(StringComparer.Ordinal);
        private static readonly Dictionary<string, ReadingBookSnapshot> lastBookByPawn =
            new Dictionary<string, ReadingBookSnapshot>(StringComparer.Ordinal);
        private static readonly Dictionary<string, ReadingSkillCounter> readingSkillCounters =
            new Dictionary<string, ReadingSkillCounter>(StringComparer.Ordinal);

        private sealed class ReadingBookSnapshot
        {
            internal string Id;
            internal string DefName;
            internal string Label;
            internal string Quality;
            internal string MetadataStatus;
            internal int TextbookDoersInBook;
            internal int TextbookDoersWithoutSkillMetadata;
            internal readonly HashSet<string> TextbookSkills =
                new HashSet<string>(StringComparer.Ordinal);
            internal readonly HashSet<string> ExecutedDoerSkills =
                new HashSet<string>(StringComparer.Ordinal);
            internal string MetadataPath = "UNRESOLVED";
            internal string ExecutedValuesStatus = "NOT_OBSERVED";
            internal int InspectedExecutedDoers;
            internal long TickCalls;
            internal long TextbookDoerCalls;
            internal long LastTick;
            internal long LearnCalls;
        }

        private sealed class ReadingSkillCounter
        {
            internal string BookId;
            internal string PawnId;
            internal string PawnName;
            internal string Skill;
            internal long Calls;
            internal int FirstRawLevel;
            internal int FirstShownLevel;
            internal long PositiveCalls;
            internal long DisabledCalls;
            internal long NonPositiveCalls;
            internal long StoredRegular;
            internal long StoredPostCap;
            internal long DroppedRegular;
            internal long DroppedByGroup;
        }

        private static void ResetReadingCoverage()
        {
            readingBooks.Clear();
            inspectedDoers.Clear();
            lastBookByPawn.Clear();
            readingSkillCounters.Clear();
            bookTickHookInstalled = false;
            textbookTickHookInstalled = false;
            bookTickHookStatus = "NOT_INSTALLED";
            textbookTickHookStatus = "NOT_INSTALLED";
            readingBookTickCalls = 0;
            readingTextbookDoerCalls = 0;
            readingLearnAttempts = 0;
            readingLearnPositive = 0;
            readingLearnDisabled = 0;
            readingLearnNonPositive = 0;
            readingLearnBeyondRegularQuota = 0;
            readingLearnBeyondGroupQuota = 0;
            readingLearnBeyondReadingQuota = 0;
            readingLearnUnattributed = 0;
            readingDoerUnattributed = 0;
            readingTrackedOverflow = 0;
            readingMetadataErrors = 0;
        }

        private static void InstallReadingBookHook()
        {
            // This patch is diagnostic-only and optional. It never replaces
            // Book.OnBookReadTick, JobDriver_Reading, or any outcome doer.
            bookTickTarget = AccessTools.Method(typeof(Verse.Book), "OnBookReadTick",
                new[] { typeof(Pawn), typeof(int), typeof(float) });
            if (bookTickTarget == null)
            {
                bookTickHookStatus = "METHOD_NOT_FOUND_FALLBACK_LEARN_ONLY";
            }
            else
            {
                MethodInfo prefix = AccessTools.Method(typeof(Phase4CCLiveXpCapture),
                    "ObserveBookReadTick");
                try
                {
                    if (prefix == null) throw new MissingMethodException("ObserveBookReadTick");
                    observerHarmony.Patch(bookTickTarget, prefix: new HarmonyMethod(prefix));
                    bookTickHookInstalled = HasPatchOwner(bookTickTarget, DiagnosticHarmonyId);
                    bookTickHookStatus = bookTickHookInstalled ? "INSTALLED" : "PATCH_NOT_CONFIRMED";
                }
                catch (Exception ex)
                {
                    bookTickHookStatus = "PATCH_ERROR_FALLBACK_LEARN_ONLY";
                    readingMetadataErrors++;
                    Log.Warning("[XPC-4CC][READING_R5E] book hook unavailable; fallback=Learn_only; " +
                        ex.GetType().Name);
                }
            }
            textbookTickTarget = AccessTools.Method(typeof(BookOutcomeDoerGainSkillExp),
                "OnReadingTick", new[] { typeof(Pawn), typeof(float) });
            if (textbookTickTarget == null)
            {
                textbookTickHookStatus = "METHOD_NOT_FOUND";
                return;
            }
            try
            {
                MethodInfo prefix = AccessTools.Method(typeof(Phase4CCLiveXpCapture),
                    "ObserveTextbookReadingTick");
                if (prefix == null) throw new MissingMethodException("ObserveTextbookReadingTick");
                observerHarmony.Patch(textbookTickTarget, prefix: new HarmonyMethod(prefix));
                textbookTickHookInstalled = HasPatchOwner(textbookTickTarget, DiagnosticHarmonyId);
                textbookTickHookStatus = textbookTickHookInstalled ? "INSTALLED" : "PATCH_NOT_CONFIRMED";
            }
            catch (Exception ex)
            {
                textbookTickHookStatus = "PATCH_ERROR";
                readingMetadataErrors++;
                Log.Warning("[XPC-4CC][READING_R5E] textbook doer hook unavailable; " +
                    ex.GetType().Name);
            }
        }

        // The doer may run without calling Learn due to vanilla progress gates.
        // This observes the doer invocation, not a claim that XP was granted.
        private static void ObserveTextbookReadingTick(BookOutcomeDoerGainSkillExp __instance,
            Pawn __0)
        {
            if (!recording || __instance == null || __0 == null) return;
            if (DateTime.UtcNow - startedUtc > MaxWallTime)
            {
                recording = false;
                limitReached = true;
                return;
            }
            try
            {
                if (__0.Dead || __0.Map != sessionMap || __0.Faction != Faction.OfPlayer ||
                    (sessionPawn != null && __0 != sessionPawn)) return;
                readingTextbookDoerCalls++;
                ReadingBookSnapshot book = ResolveReadingBook(__0);
                if (book == null) { readingDoerUnattributed++; return; }
                book.TextbookDoerCalls++;
                if (inspectedDoers.Contains(__instance)) return;
                if (inspectedDoers.Count >= MaxInspectedDoers)
                {
                    readingTrackedOverflow++;
                    return;
                }
                inspectedDoers.Add(__instance);
                string evidence;
                bool resolved = Phase4CCBookMetadataReader.ReadGeneratedSkills(
                    __instance, book.ExecutedDoerSkills, out evidence);
                book.InspectedExecutedDoers++;
                book.ExecutedValuesStatus = evidence;
                if (book.MetadataStatus != "RESOLVED_TEXTBOOK_SKILLS")
                    book.MetadataStatus = resolved ?
                        "RESOLVED_EXECUTED_DOER_VALUES_ONLY" :
                        "UNRESOLVED_EXECUTED_DOER_VALUES";
            }
            catch (Exception)
            {
                readingMetadataErrors++;
            }
        }

        // Harmony prefix; no ref/out parameters, no changes to __instance or arguments.
        private static void ObserveBookReadTick(Verse.Book __instance, Pawn __0)
        {
            if (!recording || __instance == null || __0 == null) return;
            if (DateTime.UtcNow - startedUtc > MaxWallTime)
            {
                recording = false;
                limitReached = true;
                return;
            }
            try
            {
                if (__0.Dead || __0.Map != sessionMap || __0.Faction != Faction.OfPlayer ||
                    (sessionPawn != null && __0 != sessionPawn)) return;
                readingBookTickCalls++;
                string id = Clean(__instance.ThingID);
                ReadingBookSnapshot book;
                if (!readingBooks.TryGetValue(id, out book))
                {
                    if (readingBooks.Count >= MaxReadingBookGroups)
                    {
                        readingTrackedOverflow++;
                        lastBookByPawn.Remove(__0.ThingID);
                        return;
                    }
                    book = SnapshotReadingBook(__instance, id);
                    readingBooks.Add(id, book);
                }
                book.TickCalls++;
                book.LastTick = Find.TickManager == null ? -1 : Find.TickManager.TicksGame;
                lastBookByPawn[__0.ThingID] = book;
            }
            catch (Exception)
            {
                readingMetadataErrors++;
            }
        }

        private static ReadingBookSnapshot SnapshotReadingBook(Verse.Book source, string id)
        {
            var snapshot = new ReadingBookSnapshot
            {
                Id = id,
                DefName = Clean(source.def == null ? "UNKNOWN" : source.def.defName),
                Label = Clean(source.Label),
                Quality = "UNKNOWN",
                MetadataStatus = "UNRESOLVED"
            };
            try
            {
                CompQuality quality = source.TryGetComp<CompQuality>();
                if (quality != null) snapshot.Quality = quality.Quality.ToString();
                IEnumerable doers;
                string path;
                if (!Phase4CCBookMetadataReader.TryGetDeclaredDoers(source,
                    out doers, out path))
                {
                    snapshot.MetadataPath = path;
                    snapshot.MetadataStatus = "UNRESOLVED_OUTCOME_COLLECTION";
                    return snapshot;
                }
                snapshot.MetadataPath = path;
                int count, unresolved;
                string result;
                bool complete = Phase4CCBookMetadataReader.TryCollectBookSkills(doers,
                    snapshot.TextbookSkills, out count, out unresolved, out result);
                snapshot.TextbookDoersInBook = count;
                snapshot.TextbookDoersWithoutSkillMetadata = unresolved;
                snapshot.MetadataPath += ":" + result;
                snapshot.MetadataStatus = !complete ? "PARTIAL_TEXTBOOK_SKILL_MEMBERS" :
                    count == 0 ? "RESOLVED_NO_TEXTBOOK_OUTCOME" :
                    snapshot.TextbookSkills.Count == 0 ?
                        "UNRESOLVED_TEXTBOOK_SKILL_MEMBERS" : "RESOLVED_TEXTBOOK_SKILLS";
            }
            catch (Exception)
            {
                snapshot.MetadataStatus = "UNRESOLVED_REFLECTION_ERROR";
                readingMetadataErrors++;
            }
            return snapshot;
        }

        private static ReadingSkillCounter ObserveReadingLearn(Pawn pawn, string skill,
            float input, bool disabled, int rawLevel, int displayedLevel,
            bool regularSlot, bool groupFull)
        {
            readingLearnAttempts++;
            if (input > 0f) readingLearnPositive++;
            else readingLearnNonPositive++;
            if (disabled) readingLearnDisabled++;
            if (!regularSlot)
            {
                readingLearnBeyondRegularQuota++;
                if (groupFull) readingLearnBeyondGroupQuota++;
                else readingLearnBeyondReadingQuota++;
            }
            ReadingBookSnapshot book = null;
            try { book = ResolveReadingBook(pawn); }
            catch (Exception) { readingMetadataErrors++; }
            if (book == null) readingLearnUnattributed++;
            else book.LearnCalls++;
            string bookId = book == null ? "UNRESOLVED_BOOK" : book.Id;
            string key = bookId + "|" + pawn.ThingID + "|" + skill;
            ReadingSkillCounter entry;
            if (!readingSkillCounters.TryGetValue(key, out entry))
            {
                if (readingSkillCounters.Count >= MaxReadingSkillGroups)
                {
                    readingTrackedOverflow++;
                    return null;
                }
                entry = new ReadingSkillCounter
                {
                    BookId = bookId,
                    PawnId = Clean(pawn.ThingID),
                    PawnName = Clean(pawn.LabelShort),
                    Skill = skill,
                    FirstRawLevel = rawLevel,
                    FirstShownLevel = displayedLevel
                };
                readingSkillCounters.Add(key, entry);
            }
            entry.Calls++;
            if (input > 0f) entry.PositiveCalls++;
            else entry.NonPositiveCalls++;
            if (disabled) entry.DisabledCalls++;
            if (!regularSlot)
            {
                entry.DroppedRegular++;
                if (groupFull) entry.DroppedByGroup++;
            }
            return entry;
        }

        private static ReadingBookSnapshot ResolveReadingBook(Pawn pawn)
        {
            ReadingBookSnapshot entry;
            // Prefer the actual current job target, not a stale book-tick event.
            Verse.Book currentBook = pawn.CurJob == null ? null :
                pawn.CurJob.targetA.Thing as Verse.Book;
            if (currentBook != null)
            {
                string id = Clean(currentBook.ThingID);
                if (readingBooks.TryGetValue(id, out entry)) return entry;
                if (readingBooks.Count < MaxReadingBookGroups)
                {
                    entry = SnapshotReadingBook(currentBook, id);
                    readingBooks.Add(id, entry);
                    lastBookByPawn[pawn.ThingID] = entry;
                    return entry;
                }
            }
            // Only trust the previous event when the source matches a reading
            // tick in this same tick; never infer a different book after a job swap.
            if (bookTickHookInstalled && lastBookByPawn.TryGetValue(pawn.ThingID, out entry) &&
                Find.TickManager != null && entry.LastTick == Find.TickManager.TicksGame)
                return entry;
            return null;
        }

        private static void NoteStoredReadingRegular(ReadingSkillCounter entry)
        {
            if (entry != null) entry.StoredRegular++;
        }

        private static void NoteStoredReadingPostCap(ReadingSkillCounter entry)
        {
            if (entry != null) entry.StoredPostCap++;
        }

        // Pure regression for the 1/2/3-target bookkeeping and zero-call case.
        // This never touches an actual Book, Pawn, SkillRecord or mod setting.
        private static void VerifyReadingCoverageSelfAudit()
        {
            var one = new[] { new ReadingSkillCounter { BookId = "A", Skill = "Plants", Calls = 6 },
                new ReadingSkillCounter { BookId = "A", Skill = "Intellectual", Calls = 2 },
                new ReadingSkillCounter { BookId = "B", Skill = "Artistic", Calls = 3 },
                new ReadingSkillCounter { BookId = "C", Skill = "Mining", Calls = 9 } };
            if (CountReadingSkillCalls("A", "Plants", one) != 6 ||
                CountReadingSkillCalls("A", "Mining", one) != 0 ||
                CountReadingSkillCalls("A", "Intellectual", one) != 2 ||
                CountReadingSkillCalls("B", "Artistic", one) != 3 ||
                CountReadingSkillCalls("C", "Mining", one) != 9)
                throw new InvalidOperationException("R5-E reading coverage book/skill isolation mismatch");
            // Detached metadata probe: the real doer uses a generated
            // SkillDef->float dictionary; ensure keys, not fields, are read.
            var syntheticPlants = new SkillDef { defName = "SyntheticPlants" };
            var syntheticMelee = new SkillDef { defName = "SyntheticMelee" };
            var syntheticValues = new SyntheticSkillValues();
            syntheticValues.Values.Add(syntheticPlants, 0.12f);
            syntheticValues.Values.Add(syntheticMelee, 0.08f);
            var decoded = new HashSet<string>(StringComparer.Ordinal);
            string metadataEvidence;
            if (!Phase4CCBookMetadataReader.ReadGeneratedSkills(
                syntheticValues, decoded, out metadataEvidence) ||
                decoded.Count != 2 || !decoded.Contains("SyntheticPlants") ||
                !decoded.Contains("SyntheticMelee") ||
                metadataEvidence != "VALUES_SKILL_KEYS")
                throw new InvalidOperationException("R5-E2 dictionary-key metadata decoder mismatch");
            Log.Message("[XPC-4CC][READING_R5E_AUDIT] status=PASS; cases=6; " +
                "source=PURE_SYNTHETIC; pawnWrites=0; settingsWrites=0; build=" +
                XpCustomizerMod.BuildId + "; schema=" + ReadingRevision);
        }

        private sealed class SyntheticSkillValues
        {
            internal readonly Dictionary<SkillDef, float> Values =
                new Dictionary<SkillDef, float>();
        }

        private static long CountReadingSkillCalls(string bookId, string skill,
            IEnumerable<ReadingSkillCounter> entries)
        {
            long count = 0;
            foreach (ReadingSkillCounter entry in entries)
                if (entry.BookId == bookId && entry.Skill == skill) count += entry.Calls;
            return count;
        }

        private static bool AllObservedBooksFullyResolved()
        {
            if (readingBooks.Count == 0) return false;
            foreach (ReadingBookSnapshot book in readingBooks.Values)
                if (book.MetadataStatus != "RESOLVED_TEXTBOOK_SKILLS" &&
                    !(book.MetadataStatus == "RESOLVED_NO_TEXTBOOK_OUTCOME" &&
                      book.TextbookDoerCalls == 0)) return false;
            return true;
        }

        private static void AppendReadingCoverage(StringBuilder sb)
        {
            int declaredResolved = 0;
            int executedValueResolved = 0;
            foreach (ReadingBookSnapshot book in readingBooks.Values)
            {
                if (book.MetadataStatus == "RESOLVED_TEXTBOOK_SKILLS") declaredResolved++;
                if (book.ExecutedDoerSkills.Count > 0) executedValueResolved++;
            }
            sb.Append("\n[XPC-4CC][READING_R5E_SUMMARY] build=").Append(XpCustomizerMod.BuildId)
                .Append("; schema=").Append(ReadingRevision)
                .Append("; sourceHook=").Append(bookTickHookStatus)
                .Append("; textbookDoerHook=").Append(textbookTickHookStatus)
                .Append("; bookTicksObserved=").Append(readingBookTickCalls)
                .Append("; textbookDoerInvocations=").Append(readingTextbookDoerCalls)
                .Append("; learnAttemptsAllBeforeQuota=").Append(readingLearnAttempts)
                .Append("; learnPositiveInput=").Append(readingLearnPositive)
                .Append("; learnNonPositiveInput=").Append(readingLearnNonPositive)
                .Append("; learnDisabled=").Append(readingLearnDisabled)
                .Append("; regularStored=").Append(capturedReading)
                .Append("; regularNotStored=").Append(readingLearnBeyondRegularQuota)
                .Append("; groupLimitDropped=").Append(readingLearnBeyondGroupQuota)
                .Append("; readingQuotaOrGlobalDropped=").Append(readingLearnBeyondReadingQuota)
                .Append("; unattributedLearn=").Append(readingLearnUnattributed)
                .Append("; unattributedDoer=").Append(readingDoerUnattributed)
                .Append("; trackedGroupOverflow=").Append(readingTrackedOverflow)
                .Append("; metadataErrors=").Append(readingMetadataErrors)
                .Append("; declaredBooksResolved=").Append(declaredResolved)
                .Append("; executedDoerValuesResolvedBooks=").Append(executedValueResolved)
                .Append("; note=CALLS_NOT_BOOK_SKILL_COUNT; INTELLECTUAL_READING_SEPARATE");
            foreach (ReadingBookSnapshot book in readingBooks.Values)
            {
                var names = new List<string>(book.TextbookSkills);
                names.Sort(StringComparer.Ordinal);
                var executedNames = new List<string>(book.ExecutedDoerSkills);
                executedNames.Sort(StringComparer.Ordinal);
                sb.Append("\n[XPC-4CC][READING_R5E_BOOK] bookId=").Append(book.Id)
                    .Append("; def=").Append(book.DefName)
                    .Append("; label=").Append(book.Label)
                    .Append("; quality=").Append(book.Quality)
                    .Append("; textbookSkills=").Append(names.Count == 0 ? "NONE_OR_UNRESOLVED" :
                        string.Join(",", names.ToArray()))
                    .Append("; skillsStatus=").Append(book.MetadataStatus)
                    .Append("; metadataPath=").Append(book.MetadataPath)
                    .Append("; executedDoerSkills=").Append(executedNames.Count == 0 ?
                        "NONE_OR_UNRESOLVED" : string.Join(",", executedNames.ToArray()))
                    .Append("; executedValuesStatus=").Append(book.ExecutedValuesStatus)
                    .Append("; inspectedExecutedDoers=").Append(book.InspectedExecutedDoers)
                    .Append("; outcomeDoerCount=").Append(book.TextbookDoersInBook)
                    .Append("; outcomeDoersUnresolved=").Append(book.TextbookDoersWithoutSkillMetadata)
                    .Append("; bookTicks=").Append(book.TickCalls)
                    .Append("; textbookDoerInvocations=").Append(book.TextbookDoerCalls)
                    .Append("; attributedLearn=").Append(book.LearnCalls);
                // Observed doer Values is not proof that all configured book
                // doers were enumerated. Keep this evidence separate.
                foreach (string skill in executedNames)
                {
                    long counted = CountReadingSkillCalls(book.Id, skill, readingSkillCounters.Values);
                    sb.Append("\n[XPC-4CC][READING_R5E_EXECUTED_SKILL] bookId=")
                        .Append(book.Id).Append("; skill=").Append(skill)
                        .Append("; learnCalls=").Append(counted)
                        .Append("; source=EXECUTED_DOER_VALUES")
                        .Append("; status=").Append(counted > 0 ? "CALL_OBSERVED" :
                            "NO_CALL_OBSERVED_CAUSE_NOT_PROVEN");
                }
                // Absence of calls is a factual observation, NOT proof of a
                // broken textbook: CanProgressSkill/level caps can gate them.
                if (book.MetadataStatus == "RESOLVED_TEXTBOOK_SKILLS")
                {
                    foreach (string skill in names)
                    {
                        long counted = CountReadingSkillCalls(book.Id, skill, readingSkillCounters.Values);
                        sb.Append("\n[XPC-4CC][READING_R5E_EXPECTED_SKILL] bookId=").Append(book.Id)
                            .Append("; skill=").Append(skill)
                            .Append("; learnCalls=").Append(counted)
                            .Append("; status=").Append(counted > 0 ? "CALL_OBSERVED" :
                                "NO_CALL_OBSERVED_CAUSE_NOT_PROVEN");
                    }
                }
            }
            int printed = 0;
            var ordered = new List<ReadingSkillCounter>(readingSkillCounters.Values);
            ordered.Sort((a, b) => string.CompareOrdinal(a.BookId + "|" + a.PawnId + "|" + a.Skill,
                b.BookId + "|" + b.PawnId + "|" + b.Skill));
            foreach (ReadingSkillCounter x in ordered)
            {
                if (printed++ >= MaxDisplayedReadingSkillGroups) break;
                sb.Append("\n[XPC-4CC][READING_R5E_SKILL] bookId=").Append(x.BookId)
                    .Append("; pawn=").Append(x.PawnName)
                    .Append("; skill=").Append(x.Skill)
                    .Append("; learnCalls=").Append(x.Calls)
                    .Append("; firstRawLevel=").Append(x.FirstRawLevel)
                    .Append("; firstShownLevel=").Append(x.FirstShownLevel)
                    .Append("; positiveInputCalls=").Append(x.PositiveCalls)
                    .Append("; disabledCalls=").Append(x.DisabledCalls)
                    .Append("; nonPositiveCalls=").Append(x.NonPositiveCalls)
                    .Append("; regularStored=").Append(x.StoredRegular)
                    .Append("; regularDropped=").Append(x.DroppedRegular)
                    .Append("; groupLimitDropped=").Append(x.DroppedByGroup)
                    .Append("; postCapStored=").Append(x.StoredPostCap);
            }
            if (ordered.Count > MaxDisplayedReadingSkillGroups)
                sb.Append("\n[XPC-4CC][READING_R5E_DETAIL_TRUNCATED] printed=")
                    .Append(MaxDisplayedReadingSkillGroups).Append("; tracked=").Append(ordered.Count);
            sb.Append("\n[XPC-4CC][READING_R5E_VERDICT] status=")
                .Append(readingLearnAttempts == 0 ? "NO_READING_LEARN_OBSERVED" :
                    readingTrackedOverflow > 0 || readingMetadataErrors > 0 ||
                    readingLearnUnattributed > 0 || readingDoerUnattributed > 0 ||
                    readingBooks.Count == 0 ? "PARTIAL_ATTRIBUTION" :
                    !AllObservedBooksFullyResolved() ? "PARTIAL_BOOK_METADATA" :
                        "PASS_CALL_ACCOUNTING_BOOK_SKILLS")
                .Append("; configurationProof=BOOK_DECLARED_ONLY_IF_RESOLVED_TEXTBOOK_SKILLS")
                .Append("; executedDoerValueProof=NOT_EQUIVALENT_TO_ALL_BOOK_DOERS")
                .Append("; zeroCallsForExpectedSkill=NOT_PROOF_OF_FAILURE")
                .Append("; gameplayMutation=NONE; outcomeCapChecks=NOT_INSTRUMENTED");
        }
    }
}
#endif
