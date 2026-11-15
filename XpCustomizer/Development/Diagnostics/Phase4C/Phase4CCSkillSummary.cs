#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// T03: read-only, per-SkillDef.defName summaries of RETAINED regular 4C-C samples.
    /// No Learn calls, Harmony ownership, post-cap reserved samples, or global XP totals.
    /// Payout/Rate/Joint strings are supplied by the existing BuildReport foreach;
    /// this reducer must never recompute a classifier using mutable live settings.
    /// </summary>
    internal static partial class Phase4CCLiveXpCapture
    {
        private const string SkillSummaryRevision = "4C-C-SKILL-SUMMARY-T03-R1";
        private static bool lastSkillSummaryInvariantFailed;
        private static readonly string[] SkillSummaryVanillaKeys = {
            "Shooting", "Melee", "Construction", "Mining", "Cooking", "Plants",
            "Animals", "Crafting", "Artistic", "Medicine", "Social", "Intellectual"
        };

        private sealed class SkillSummaryRow
        {
            internal string Key;
            internal int ObservedCalls, WorkCalls, ReadingCalls, OtherCalls, DecayCalls;
            internal int PositiveInputCandidates;
            internal int JointPass, JointReview, JointSkip, JointInconclusive;
            internal int PayoutReview, RateReview;
            internal int WorkMeasured, ReadingMeasured, LevelTransitions, ZeroDeltaCalls;
            internal int InvalidXpCalls, MissingClassificationCalls;
            internal double RetainedPositiveXp, VerifiedPositiveXp;
            internal double WorkPositiveXp, ReadingPositiveXp;

            internal string Verdict
            {
                get
                {
                    if (InvalidXpCalls > 0 || MissingClassificationCalls > 0 ||
                        JointReview > 0 || PayoutReview > 0 || RateReview > 0)
                        return "REVIEW";
                    if (JointPass > 0 && (JointSkip > 0 || JointInconclusive > 0))
                        return "PARTIAL";
                    if (PositiveInputCandidates > 0 && JointPass == PositiveInputCandidates)
                        return "PASS_CAPTURED";
                    if (JointInconclusive > 0) return "INCONCLUSIVE";
                    if (JointSkip > 0) return "UNVERIFIED";
                    return ObservedCalls > 0 ? "OBSERVED_NONCANDIDATE" : "NO_DATA";
                }
            }
        }

        private sealed class SkillSummaryExpectedTotals
        {
            internal int Samples, Work, Reading, Other, Decay, Candidates;
            internal int JointWorkPass, JointReadingPass, JointReview, JointSkip, JointInconclusive;
            internal int WorkMeasured, ReadingMeasured;
            internal float WorkXp, ReadingXp;
        }

        private sealed class SkillSummaryAccumulator
        {
            private readonly Dictionary<string, SkillSummaryRow> rows =
                new Dictionary<string, SkillSummaryRow>(StringComparer.Ordinal);

            internal SkillSummaryAccumulator()
            {
                foreach (string key in SkillSummaryVanillaKeys)
                    rows.Add(key, new SkillSummaryRow { Key = key });
            }

            internal SkillSummaryRow Row(string skill)
            {
                string key = string.IsNullOrEmpty(skill) ? "UNKNOWN" : skill;
                SkillSummaryRow row;
                if (!rows.TryGetValue(key, out row))
                {
                    row = new SkillSummaryRow { Key = key };
                    rows.Add(key, row);
                }
                return row;
            }

            internal List<SkillSummaryRow> OrderedRows()
            {
                var ordered = new List<SkillSummaryRow>(rows.Count);
                var vanilla = new HashSet<string>(SkillSummaryVanillaKeys, StringComparer.Ordinal);
                foreach (string key in SkillSummaryVanillaKeys) ordered.Add(rows[key]);
                var extras = new List<string>();
                foreach (string key in rows.Keys)
                    if (!vanilla.Contains(key)) extras.Add(key);
                extras.Sort(StringComparer.Ordinal);
                foreach (string key in extras) ordered.Add(rows[key]);
                return ordered;
            }

            // Exactly one Add call per retained REGULAR Sample, never per reserved post-cap sample.
            internal void Add(Sample sample, string payout, string rate, string joint)
            {
                if (sample == null || sample.Before == null)
                    throw new InvalidOperationException("4C-C T03 null retained sample");
                StartState before = sample.Before;
                SkillSummaryRow row = Row(before.Skill);
                row.ObservedCalls++;
                bool work = before.Category == "WORK_CANDIDATE";
                bool reading = before.Category == "READING";
                if (work) row.WorkCalls++;
                else if (reading) row.ReadingCalls++;
                else if (before.Category == "DECAY_INTERVAL") row.DecayCalls++;
                else row.OtherCalls++;

                if (!sample.Comparable) row.LevelTransitions++;
                else if (sample.DeltaXp == 0f) row.ZeroDeltaCalls++;
                if (float.IsNaN(sample.DeltaXp) || float.IsInfinity(sample.DeltaXp))
                    row.InvalidXpCalls++;

                bool measured = sample.Comparable && sample.DeltaXp > 0.0001f &&
                    !float.IsNaN(sample.DeltaXp) && !float.IsInfinity(sample.DeltaXp);
                if (measured && (work || reading))
                {
                    double xp = sample.DeltaXp;
                    row.RetainedPositiveXp += xp;
                    if (work) { row.WorkMeasured++; row.WorkPositiveXp += xp; }
                    else { row.ReadingMeasured++; row.ReadingPositiveXp += xp; }
                }

                bool candidate = (work || reading) && before.Input > 0f;
                if (!candidate) return;
                row.PositiveInputCandidates++;
                if (payout == null || rate == null || joint == null)
                    row.MissingClassificationCalls++;
                if (payout != null && payout.StartsWith("REVIEW_", StringComparison.Ordinal))
                    row.PayoutReview++;
                if (rate != null && rate.StartsWith("REVIEW_", StringComparison.Ordinal))
                    row.RateReview++;
                if (joint == "JOINT_PASS")
                {
                    row.JointPass++;
                    if (measured) row.VerifiedPositiveXp += sample.DeltaXp;
                }
                else if (joint == "JOINT_REVIEW") row.JointReview++;
                else if (joint == "JOINT_SKIP") row.JointSkip++;
                else if (joint == "JOINT_INCONCLUSIVE") row.JointInconclusive++;
                else
                {
                    // Never silently promote a future/unknown JOINT result.
                    row.MissingClassificationCalls++;
                    row.JointInconclusive++;
                }
            }

            internal bool AppendReport(StringBuilder sb, SkillSummaryExpectedTotals expected)
            {
                List<SkillSummaryRow> ordered = OrderedRows();
                int observed = 0, work = 0, reading = 0, other = 0, decay = 0;
                int candidates = 0, passes = 0, reviews = 0, skips = 0, inconclusive = 0;
                int workMeasured = 0, readingMeasured = 0, payoutReviews = 0, rateReviews = 0;
                int invalid = 0, missing = 0;
                double retainedXp = 0d, verifiedXp = 0d, workXp = 0d, readingXp = 0d;
                foreach (SkillSummaryRow row in ordered)
                {
                    observed += row.ObservedCalls;
                    work += row.WorkCalls; reading += row.ReadingCalls;
                    other += row.OtherCalls; decay += row.DecayCalls;
                    candidates += row.PositiveInputCandidates;
                    passes += row.JointPass; reviews += row.JointReview;
                    skips += row.JointSkip; inconclusive += row.JointInconclusive;
                    workMeasured += row.WorkMeasured; readingMeasured += row.ReadingMeasured;
                    payoutReviews += row.PayoutReview; rateReviews += row.RateReview;
                    invalid += row.InvalidXpCalls; missing += row.MissingClassificationCalls;
                    retainedXp += row.RetainedPositiveXp; verifiedXp += row.VerifiedPositiveXp;
                    workXp += row.WorkPositiveXp; readingXp += row.ReadingPositiveXp;
                }
                var failed = new List<string>();
                CheckSummary(observed == expected.Samples, "SAMPLES", failed);
                CheckSummary(work == expected.Work, "WORK", failed);
                CheckSummary(reading == expected.Reading, "READING", failed);
                CheckSummary(other == expected.Other, "OTHER", failed);
                CheckSummary(decay == expected.Decay, "DECAY", failed);
                CheckSummary(candidates == expected.Candidates, "CANDIDATES", failed);
                CheckSummary(passes == expected.JointWorkPass + expected.JointReadingPass,
                    "JOINT_PASS", failed);
                CheckSummary(reviews == expected.JointReview, "JOINT_REVIEW", failed);
                CheckSummary(skips == expected.JointSkip, "JOINT_SKIP", failed);
                CheckSummary(inconclusive == expected.JointInconclusive, "JOINT_INCONCLUSIVE", failed);
                CheckSummary(passes + reviews + skips + inconclusive == candidates,
                    "JOINT_PARTITION", failed);
                CheckSummary(workMeasured == expected.WorkMeasured, "WORK_MEASURED", failed);
                CheckSummary(readingMeasured == expected.ReadingMeasured, "READING_MEASURED", failed);
                CheckSummary(SummaryXpEqual(workXp, expected.WorkXp), "WORK_XP", failed);
                CheckSummary(SummaryXpEqual(readingXp, expected.ReadingXp), "READING_XP", failed);
                CheckSummary(SummaryXpEqual(retainedXp, workXp + readingXp), "RETAINED_XP", failed);
                CheckSummary(verifiedXp <= retainedXp + 0.01d, "VERIFIED_XP_BOUND", failed);
                CheckSummary(invalid == 0, "INVALID_XP", failed);
                CheckSummary(missing == 0, "CLASSIFICATION_MISSING", failed);
                bool valid = failed.Count == 0;

                sb.Append("\n[XPC-4CC][SKILL_SUMMARY] schema=").Append(SkillSummaryRevision)
                    .Append("; build=").Append(XpCustomizerMod.BuildId)
                    .Append("; invariant=").Append(valid ? "PASS" : "FAIL")
                    .Append("; failedChecks=").Append(failed.Count == 0 ? "NONE" : string.Join(",", failed.ToArray()))
                    .Append("; vanillaRows=12; additionalRows=").Append(ordered.Count - 12)
                    .Append("; observedCalls=").Append(observed)
                    .Append("; positiveInputCandidates=").Append(candidates)
                    .Append("; jointPass=").Append(passes)
                    .Append("; jointReview=").Append(reviews)
                    .Append("; jointSkip=").Append(skips)
                    .Append("; jointInconclusive=").Append(inconclusive)
                    .Append("; payoutReview=").Append(payoutReviews)
                    .Append("; independentRateReview=").Append(rateReviews)
                    .Append("; retainedPositiveXp=").Append(SummaryXp(retainedXp))
                    .Append("; verifiedPositiveXp=").Append(SummaryXp(verifiedXp))
                    .Append("; retainedSamplesOnly=True; sampleStorage=REGULAR_ONLY")
                    .Append("; reservedPostCapDuplicates=EXCLUDED; droppedXp=UNKNOWN")
                    .Append("; workMeasured=").Append(workMeasured)
                    .Append("; readingMeasured=").Append(readingMeasured);
                for (int i = 0; i < ordered.Count; i++)
                {
                    SkillSummaryRow row = ordered[i];
                    sb.Append("\n[XPC-4CC][SKILL] skill=").Append(Clean(row.Key))
                        .Append("; group=").Append(i < 12 ? "VANILLA" : "ADDITIONAL_SKILL")
                        .Append("; observedCalls=").Append(row.ObservedCalls)
                        .Append("; workCalls=").Append(row.WorkCalls)
                        .Append("; readingCalls=").Append(row.ReadingCalls)
                        .Append("; otherCalls=").Append(row.OtherCalls)
                        .Append("; decayCalls=").Append(row.DecayCalls)
                        .Append("; positiveInputCandidates=").Append(row.PositiveInputCandidates)
                        .Append("; jointPass=").Append(row.JointPass)
                        .Append("; jointReview=").Append(row.JointReview)
                        .Append("; jointSkip=").Append(row.JointSkip)
                        .Append("; jointInconclusive=").Append(row.JointInconclusive)
                        .Append("; payoutReview=").Append(row.PayoutReview)
                        .Append("; independentRateReview=").Append(row.RateReview)
                        .Append("; workMeasured=").Append(row.WorkMeasured)
                        .Append("; readingMeasured=").Append(row.ReadingMeasured)
                        .Append("; retainedPositiveXp=").Append(SummaryXp(row.RetainedPositiveXp))
                        .Append("; verifiedPositiveXp=").Append(SummaryXp(row.VerifiedPositiveXp))
                        .Append("; levelTransitions=").Append(row.LevelTransitions)
                        .Append("; zeroDeltaCalls=").Append(row.ZeroDeltaCalls)
                        .Append("; invalidXpCalls=").Append(row.InvalidXpCalls)
                        .Append("; verdict=").Append(row.Verdict);
                }
                return valid;
            }

            private static void CheckSummary(bool ok, string code, List<string> failed)
            {
                if (!ok) failed.Add(code);
            }

            private static bool SummaryXpEqual(double actual, double expected)
            {
                return !double.IsNaN(actual) && !double.IsInfinity(actual) &&
                    !double.IsNaN(expected) && !double.IsInfinity(expected) &&
                    Math.Abs(actual - expected) <= Math.Max(0.02d, Math.Abs(expected) * 0.0005d);
            }

            private static string SummaryXp(double amount)
            {
                return double.IsNaN(amount) || double.IsInfinity(amount) ? "INVALID" :
                    amount.ToString("0.####", CultureInfo.InvariantCulture);
            }
        }

        // Detached, synthetic-only fixtures: do not invoke SkillRecord.Learn or write to pawns.
        private static void VerifySkillSummarySelfAudit()
        {
            int checks = 0;
            var a = new SkillSummaryAccumulator();
            List<SkillSummaryRow> empty = a.OrderedRows();
            RequireSkillSummary(empty.Count == 12, "EMPTY_TWELVE", ref checks);
            RequireSkillSummary(empty[0].Key == "Shooting" && empty[9].Key == "Medicine" &&
                empty[11].Key == "Intellectual", "VANILLA_DEFNAME_ORDER", ref checks);
            foreach (SkillSummaryRow row in empty)
                RequireSkillSummary(row.Verdict == "NO_DATA" && row.ObservedCalls == 0,
                    "EMPTY_NO_DATA_" + row.Key, ref checks);

            a.Add(SkillFixture("WORK_CANDIDATE", "Construction", 10f, 7f, true),
                "PASS_PAYOUT", "PASS_INDEPENDENT_RATE", "JOINT_PASS");
            a.Add(SkillFixture("READING", "Construction", 10f, 7f, true),
                "PASS_PAYOUT", "PASS_INDEPENDENT_RATE", "JOINT_PASS");
            SkillSummaryRow construction = a.Row("Construction");
            RequireSkillSummary(construction.JointPass == 2 && construction.WorkCalls == 1 &&
                construction.ReadingCalls == 1, "MIXED_WORK_READING", ref checks);
            RequireSkillSummary(construction.Verdict == "PASS_CAPTURED" &&
                Math.Abs(construction.VerifiedPositiveXp - 14d) < 0.00001,
                "CAPTURED_PASS_XP", ref checks);

            // A skipped DoBill event can still reveal an independent classifier REVIEW.
            a.Add(SkillFixture("WORK_CANDIDATE", "Cooking", 10f, 7f, true),
                "PASS_PAYOUT", "REVIEW_RATE_MISMATCH", "JOINT_SKIP");
            SkillSummaryRow cooking = a.Row("Cooking");
            RequireSkillSummary(cooking.JointSkip == 1 && cooking.JointPass == 0 &&
                cooking.RateReview == 1 && cooking.Verdict == "REVIEW",
                "SKIP_DOES_NOT_HIDE_RATE_REVIEW", ref checks);

            // Separate one-sided successes are not composable into a JOINT PASS.
            a.Add(SkillFixture("WORK_CANDIDATE", "Crafting", 10f, 7f, true),
                "PASS_PAYOUT", "REVIEW_RATE_MISMATCH", "JOINT_REVIEW");
            a.Add(SkillFixture("WORK_CANDIDATE", "Crafting", 10f, 6f, true),
                "REVIEW_PAYOUT_MISMATCH", "PASS_INDEPENDENT_RATE", "JOINT_REVIEW");
            SkillSummaryRow crafting = a.Row("Crafting");
            RequireSkillSummary(crafting.JointPass == 0 && crafting.JointReview == 2 &&
                crafting.Verdict == "REVIEW", "NO_CROSS_SAMPLE_PASS", ref checks);

            a.Add(SkillFixture("READING", "Artistic", 10f, 0f, true),
                "SKIP_DISABLED", "SKIP_DISABLED", "JOINT_SKIP");
            RequireSkillSummary(a.Row("Artistic").Verdict == "UNVERIFIED" &&
                a.Row("Artistic").ZeroDeltaCalls == 1, "SKIP_ZERO_DELTA", ref checks);
            a.Add(SkillFixture("READING", "Intellectual", 10f, 9f, false),
                "INCONCLUSIVE_LEVEL_CHANGE", "PASS_INDEPENDENT_RATE", "JOINT_INCONCLUSIVE");
            RequireSkillSummary(a.Row("Intellectual").Verdict == "INCONCLUSIVE" &&
                a.Row("Intellectual").RetainedPositiveXp == 0d &&
                a.Row("Intellectual").LevelTransitions == 1,
                "LEVEL_TRANSITION_NOT_XP", ref checks);
            a.Add(SkillFixture("WORK_CANDIDATE", "Mining", 10f, 7f, true),
                "PASS_PAYOUT", "PASS_INDEPENDENT_RATE", "JOINT_PASS");
            a.Add(SkillFixture("WORK_CANDIDATE", "Mining", 10f, 7f, true),
                "SKIP_UNKNOWN", "PASS_INDEPENDENT_RATE", "JOINT_SKIP");
            RequireSkillSummary(a.Row("Mining").Verdict == "PARTIAL",
                "MIXED_PASS_SKIP", ref checks);
            a.Add(SkillFixture("DECAY_INTERVAL", "Medicine", -1f, -1f, true), null, null, null);
            RequireSkillSummary(a.Row("Medicine").DecayCalls == 1 &&
                a.Row("Medicine").PositiveInputCandidates == 0 &&
                a.Row("Medicine").RetainedPositiveXp == 0d,
                "DECAY_EXCLUDED_FROM_POSITIVE", ref checks);
            a.Add(SkillFixture("OTHER", "Social", 0f, 0f, true), null, null, null);
            RequireSkillSummary(a.Row("Social").Verdict == "OBSERVED_NONCANDIDATE",
                "OTHER_NO_CANDIDATE", ref checks);
            a.Add(SkillFixture("READING", "ModSkillZ", 10f, 4f, true),
                "PASS_PAYOUT", "PASS_INDEPENDENT_RATE", "JOINT_PASS");
            a.Add(SkillFixture("OTHER", "UNKNOWN", 0f, 0f, true), null, null, null);
            List<SkillSummaryRow> ordered = a.OrderedRows();
            RequireSkillSummary(ordered.Count == 14 && ordered[12].Key == "ModSkillZ" &&
                ordered[13].Key == "UNKNOWN", "EXTRA_ORDER_AND_UNKNOWN", ref checks);

            // Fail-closed aggregate audit, including a deliberately mismatched fixture.
            var exact = new SkillSummaryExpectedTotals {
                Samples = 13, Work = 6, Reading = 4, Other = 2, Decay = 1,
                Candidates = 10, JointWorkPass = 2, JointReadingPass = 2,
                JointReview = 2, JointSkip = 3, JointInconclusive = 1,
                WorkMeasured = 6, ReadingMeasured = 2, WorkXp = 41f, ReadingXp = 11f
            };
            var report = new StringBuilder();
            bool accepted = a.AppendReport(report, exact);
            RequireSkillSummary(accepted, "AGGREGATE_PASS", ref checks);
            RequireSkillSummary(report.ToString().Contains("[XPC-4CC][SKILL] skill=Medicine") &&
                report.ToString().Contains("invariant=PASS"), "FORMAT_AND_MEDICINE", ref checks);
            exact.Candidates++;
            RequireSkillSummary(!a.AppendReport(new StringBuilder(), exact),
                "AGGREGATE_MISMATCH_FAIL", ref checks);
            exact.Candidates--;
            exact.WorkXp += 5f;
            RequireSkillSummary(!a.AppendReport(new StringBuilder(), exact),
                "WORK_XP_MISMATCH_FAIL", ref checks);

            var hiddenReview = new SkillSummaryAccumulator();
            hiddenReview.Add(SkillFixture("WORK_CANDIDATE", "Medicine", 10f, 5f, true),
                "REVIEW_PAYOUT_MISMATCH", "SKIP_SOURCE", "JOINT_SKIP");
            RequireSkillSummary(hiddenReview.Row("Medicine").PayoutReview == 1 &&
                hiddenReview.Row("Medicine").JointSkip == 1 &&
                hiddenReview.Row("Medicine").Verdict == "REVIEW",
                "SKIP_DOES_NOT_HIDE_PAYOUT_REVIEW", ref checks);

            var unknown = new SkillSummaryAccumulator();
            unknown.Add(SkillFixture("OTHER", null, 0f, 0f, true), null, null, null);
            RequireSkillSummary(unknown.Row("UNKNOWN").ObservedCalls == 1 &&
                unknown.OrderedRows().Count == 13,
                "NULL_SKILL_RETAINED_AS_UNKNOWN", ref checks);

            var invalid = new SkillSummaryAccumulator();
            invalid.Add(SkillFixture("WORK_CANDIDATE", "Plants", 1f,
                float.PositiveInfinity, true), "SKIP_INVALID", "SKIP_INVALID", "JOINT_SKIP");
            RequireSkillSummary(invalid.Row("Plants").Verdict == "REVIEW" &&
                invalid.Row("Plants").InvalidXpCalls == 1,
                "INVALID_XP_NOT_PROMOTED", ref checks);
            invalid.Add(SkillFixture("OTHER", "Plants", 0f, float.NaN, true),
                null, null, null);
            RequireSkillSummary(invalid.Row("Plants").InvalidXpCalls == 2,
                "NAN_RETAINED_NOT_ACCUMULATED", ref checks);
            Log.Message("[XPC-4CC][SKILL_SUMMARY_AUDIT] status=PASS; cases=" + checks +
                "; schema=" + SkillSummaryRevision +
                "; syntheticOnly=True; regularSamplesOnly=True; vanillaSkills=12" +
                "; xpWrites=0; harmonyWrites=0; build=" + XpCustomizerMod.BuildId);
        }

        private static Sample SkillFixture(string category, string skill, float input,
            float delta, bool comparable)
        {
            return new Sample {
                Before = new StartState { Category = category, Skill = skill, Input = input },
                Comparable = comparable, DeltaXp = delta
            };
        }

        private static void RequireSkillSummary(bool condition, string caseName, ref int checks)
        {
            if (!condition)
                throw new InvalidOperationException("4C-C T03 skill summary " + caseName);
            checks++;
        }
    }
}
#endif
