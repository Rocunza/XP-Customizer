#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.BookXp;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.Decay;
using XpCustomizer.FurtherLearning;
using XpCustomizer.Gws;
using XpCustomizer.Progression;
using XpCustomizer.Skilltrainer;

namespace XpCustomizer
{
    /// <summary>
    /// 4C-RND1: deterministic, bounded property/metamorphic test suite.
    /// Creates disposable test settings, never exchanges or writes the global
    /// ModSettings instance, Pawn, Def, saved SkillRecord or Unity/Verse RNG.
    /// Pure calculator coverage != live Harmony/job/serialization coverage.
    /// </summary>
    internal static class Phase4CRandomizedDiagnostics
    {
        internal const string Revision = "4C-RND1-PURE-SEED-REPLAY-R1-20261104";
        internal const int DefaultSeed = 20261104;
        private static bool running;
        private static string lastReport;
        private static int runId;

        // Fixed algorithm, unlike UnityEngine.Random/Verse.Rand global state.
        private struct LocalRng
        {
            private uint state;
            internal LocalRng(int seed) { state = (uint)seed == 0 ? 0x9E3779B9U : (uint)seed; }
            private uint Next()
            {
                uint x = state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                state = x;
                return x;
            }
            internal int Int(int count) { return (int)(Next() % (uint)count); }
            internal float Range(float min, float max)
            {
                return min + (max - min) * ((Next() & 0xFFFFFFU) / 16777216f);
            }
            internal bool Bool() { return (Next() & 1U) != 0; }
        }

        internal static void RunManually(int count, int seed)
        {
            if (running) { Log.Warning("[XPC-RND1] SKIP; already running"); return; }
            running = true;
            try { Run(Math.Max(1, Math.Min(1000, count)), seed); }
            catch (Exception ex)
            {
                lastReport = "[XPC-RND1][SUMMARY] status=FAIL; build=" +
                    XpCustomizerMod.BuildId + "; fatal=" + Clean(ex.ToString());
                Phase4CRegressionLedger.Record("4C-RND", "FAIL", "RUNNER_FATAL");
                Log.Error(lastReport);
            }
            finally { running = false; }
        }

        private static void Run(int count, int seed)
        {
            var rng = new LocalRng(seed);
            var baseline = XpCustomizerMod.Settings;
            var tally = new int[7];
            var failures = new int[7];
            int passed = 0, failed = 0;
            var failuresText = new StringBuilder(2200);
            string[] domains = { "GWS", "CONS", "MTH_FURTHER", "DECAY_MEMORY",
                "BOOK_XP", "SKILLTRAINER", "CAREER_XP" };
            for (int i = 0; i < count; i++)
            {
                int domain = i % domains.Length;
                tally[domain]++;
                string input = "case=" + i;
                try
                {
                    switch (domain)
                    {
                        case 0: CheckGws(ref rng, ref input); break;
                        case 1: CheckCons(ref rng, ref input); break;
                        case 2: CheckMental(ref rng, ref input); break;
                        case 3: CheckDecay(ref rng, ref input); break;
                        case 4: CheckBook(ref rng, ref input); break;
                        case 5: CheckTrainer(ref rng, ref input); break;
                        case 6: CheckCareer(ref rng, ref input); break;
                    }
                    if (!System.Object.ReferenceEquals(baseline, XpCustomizerMod.Settings))
                        throw new InvalidOperationException("GLOBAL_SETTINGS_REFERENCE_CHANGED");
                    passed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    failures[domain]++;
                    if (failuresText.Length < 12000)
                        failuresText.Append("\n[XPC-RND1][FAIL] domain=")
                            .Append(domains[domain]).Append("; case=").Append(i)
                            .Append("; seed=").Append(seed)
                            .Append("; input=").Append(input)
                            .Append("; error=").Append(Clean(ex.ToString()));
                }
            }
            var sb = new StringBuilder(2500);
            sb.Append("[XPC-RND1][RUN] id=").Append(++runId)
                .Append("; build=").Append(XpCustomizerMod.BuildId)
                .Append("; revision=").Append(Revision)
                .Append("; seed=").Append(seed)
                .Append("; rng=XORSHIFT32")
                .Append("; cases=").Append(count)
                .Append("; modes=BOUNDARY_PLUS_DETERMINISTIC_RANDOM")
                .Append("; isolation=DISPOSABLE_SETTINGS_AND_IMMUTABLE_PROFILES")
                .Append("; globalSettingsWrites=0; pawnWrites=0; saveWrites=0")
                .Append("; disclaimer=PURE_CALCULATORS_NOT_LIVE_JOB_XP");
            for (int j = 0; j < domains.Length; j++)
                sb.Append("\n[XPC-RND1][DOMAIN] name=").Append(domains[j])
                    .Append("; cases=").Append(tally[j])
                    .Append("; pass=").Append(tally[j] - failures[j])
                    .Append("; fail=").Append(failures[j]);
            sb.Append(failuresText);
            sb.Append("\n[XPC-RND1][SUMMARY] status=")
                .Append(failed == 0 ? "PASS" : "FAIL")
                .Append("; pass=").Append(passed).Append("; fail=").Append(failed)
                .Append("; total=").Append(count)
                .Append("; seed=").Append(seed)
                .Append("; globalSettingsIdentityPreserved=")
                .Append(System.Object.ReferenceEquals(baseline, XpCustomizerMod.Settings))
                .Append("; limits=MAX_1000_SEVEN_DOMAINS_NOT_ALL_MOD_FEATURES");
            lastReport = sb.ToString();
            Phase4CRegressionLedger.Record("4C-RND", failed > 0 ? "FAIL" : "PASS",
                "pass=" + passed + ",fail=" + failed + ",seed=" + seed);
            if (failed > 0) Log.Error(lastReport);
            else Log.Message(lastReport);
        }

        private static void CheckGws(ref LocalRng r, ref string input)
        {
            var s = new XpCustomizerSettings();
            s.gwsEnabled = true;
            s.gwsLowEnabled = true;
            s.gwsHighEnabled = true;
            s.gwsLowAnchor = r.Range(.30f, .95f);
            s.gwsHighAnchor = r.Range(1.10f, 10f);
            s.gwsLowFactor = r.Range(.05f, .99f);
            s.gwsHighFactor = r.Range(1.01f, 3f);
            s.gwsLowCurve = r.Int(3);
            s.gwsHighCurve = r.Int(3);
            s.gwsLowCurvature = r.Range(0f, 2f);
            s.gwsHighCurvature = r.Range(0f, 2f);
            s.gwsHighDiminishingLimit = s.gwsHighFactor + r.Range(.02f, 4f);
            s.gwsHighTechnicalLimit = s.gwsHighFactor + r.Range(.01f, 4f);
            float observedAt = r.Range(0f, 10f);
            input = "low=" + F(s.gwsLowAnchor) + "/" + F(s.gwsLowFactor) +
                "; high=" + F(s.gwsHighAnchor) + "/" + F(s.gwsHighFactor) +
                "; lowMode=" + s.gwsLowCurve + "; highMode=" + s.gwsHighCurve +
                "; observedAt=" + F(observedAt);
            Near(GwsLearningService.Calculate(1f, s), 1f, "GWS_NEUTRAL");
            Near(GwsLearningService.Calculate(s.gwsLowAnchor, s), s.gwsLowFactor, "GWS_LOW_ANCHOR");
            Near(GwsLearningService.Calculate(s.gwsHighAnchor, s), s.gwsHighFactor, "GWS_HIGH_ANCHOR");
            float v = GwsLearningService.Calculate(observedAt, s);
            Finite(v, "GWS_RANDOM");
            if (v < -0.0001f || v > 20.001f) throw new Exception("GWS_OUT_OF_BOUNDS=" + v);
            Near(GwsLearningService.Calculate(0f, s), s.gwsLowFactor, "GWS_LOW_FLOOR");
            s.gwsEnabled = false;
            Near(GwsLearningService.Calculate(observedAt, s), 1f, "GWS_MASTER_OFF");
            if (GwsLearningService.IsSkillEnabled(s, "ForeignModdedSkill"))
                throw new Exception("GWS_FOREIGN_SKILL_ENABLED");
        }

        private static void CheckCons(ref LocalRng r, ref string input)
        {
            float lowPos = r.Range(.05f, .95f), low = r.Range(0.01f, .99f);
            float highPos = r.Range(1.05f, 4f), high = r.Range(1.01f, 3f);
            var profile = new ConsAnchorCurveProfile(lowPos, low,
                (ConsAnchorCurveMode)r.Int(3), r.Range(0f, 2f), highPos, high,
                (ConsAnchorCurveMode)r.Int(3), r.Range(0f, 2f),
                high + r.Range(.05f, 5f), high + r.Range(.05f, 5f));
            float x = r.Range(0f, 5f);
            input = "low=" + F(lowPos) + "/" + F(low) + "; high=" +
                F(highPos) + "/" + F(high) + "; x=" + F(x);
            Near(ConsciousnessAnchorCurveService.Calculate(1f, profile), 1f, "CONS_NEUTRAL");
            Near(ConsciousnessAnchorCurveService.Calculate(lowPos, profile), low, "CONS_LOW_ANCHOR");
            Near(ConsciousnessAnchorCurveService.Calculate(highPos, profile), high, "CONS_HIGH_ANCHOR");
            Near(ConsciousnessAnchorCurveService.Calculate(0f, profile), 0f, "CONS_ZERO");
            float factor = ConsciousnessAnchorCurveService.Calculate(x, profile);
            Finite(factor, "CONS_RANDOM");
            if (factor < -0.0001f || factor > 20.001f) throw new Exception("CONS_OUT_OF_BOUNDS");
        }

        private static void CheckMental(ref LocalRng r, ref string input)
        {
            float strength = r.Range(0f, 2f);
            float baseFactor = r.Range(0f, 1f);
            float cons = r.Range(0f, 2f);
            float threshold = r.Range(.01f, .70f);
            input = "threshold=" + F(threshold) + "; strength=" + F(strength) +
                "; base=" + F(baseFactor) + "; cons=" + F(cons);
            float clamped = Math.Max(.17f, Math.Min(.50f, threshold));
            float expectedPp = clamped <= .35f
                ? 10f * ((.35f - clamped) / .18f) * strength
                : -10f * ((clamped - .35f) / .15f) * strength;
            float actualPp = FurtherLearningService.CalculateThresholdAdjustment(threshold, strength);
            Near(actualPp, expectedPp, "MTH_PIECEWISE_REFERENCE");
            Near(FurtherLearningService.CalculateThresholdAdjustment(.35f, strength), 0f, "MTH_NEUTRAL");
            Near(FurtherLearningService.CalculateThresholdAdjustment(.17f, strength), 10f * strength, "MTH_STABLE");
            Near(FurtherLearningService.CalculateThresholdAdjustment(.50f, strength), -10f * strength, "MTH_UNSTABLE");
            float actual = FurtherLearningService.CalculateRawFactor(baseFactor, cons, actualPp);
            Near(actual, (baseFactor + expectedPp / 100f) * cons, "FURTHER_MTH_COMPOSITION_R5F1");
            Finite(Math.Max(0f, actual), "FURTHER_FLOORED");
        }

        private static void CheckDecay(ref LocalRng r, ref string input)
        {
            var s = new XpCustomizerSettings();
            s.decayByLevelEnabled = true;
            s.decaySkillMultipliersEnabled = true;
            s.decayDailyByLevel = SkillDecayService.DefaultValues();
            int level = r.Int(20) + 1;
            float daily = level == 4 ? 5f : r.Range(0f, 8000f);
            float skillFactor = r.Range(0f, 3f);
            s.decayDailyByLevel[level] = daily;
            s.decaySkillMultipliers["Construction"] = skillFactor;
            bool great = r.Bool(), perfect = r.Bool(), anomaly = r.Bool();
            s.greatMemoryDecayReduction = r.Range(0f, 100f);
            s.perfectMemoryDecayReduction = r.Range(0f, 100f);
            float remaining = anomaly && perfect
                ? (100f - DecayModifiersService.NormalizeReduction(
                    s.perfectMemoryDecayReduction, 100f)) / 100f
                : great ? (100f - DecayModifiersService.NormalizeReduction(
                    s.greatMemoryDecayReduction, 50f)) / 100f : 1f;
            input = "level=" + level + "; daily=" + F(daily) + "; skill=" +
                F(skillFactor) + "; great=" + great + "; perfect=" + perfect +
                "; anomaly=" + anomaly + "; remain=" + F(remaining);
            float expectedDaily = Math.Min(SkillDecayService.MaxDaily, daily * skillFactor * remaining);
            float real = DecayModifiersService.DailyLoss(s, level, "Construction", great, perfect, anomaly);
            Near(real, expectedDaily, "DECAY_DAILY_INDEPENDENT");
            Near(DecayModifiersService.LossPerInterval(s, level, "Construction", great, perfect, anomaly),
                expectedDaily / 300f, "DECAY_INTERVAL_300");
            Near(DecayModifiersService.DailyLoss(s, 0, "Construction", great, perfect, anomaly),
                0f, "DECAY_LEVEL_ZERO");
            if (anomaly && perfect && great)
                Near(DecayModifiersService.MemoryRemainingFactor(s, great, perfect, anomaly),
                    remaining, "PERFECT_PRECEDENCE");
            // Every run covers the reported child's 4-level / 5 XP per day
            // boundary and Perfect Memory's zero loss, not only lucky draws.
            s.decayDailyByLevel[4] = 5f;
            s.decaySkillMultipliersEnabled = false;
            Near(DecayModifiersService.LossPerInterval(s, 4, "Construction",
                false, false, true), 5f / 300f, "DECAY_LEVEL4_FIVE_PER_DAY");
            s.perfectMemoryDecayReduction = 100f;
            Near(DecayModifiersService.DailyLoss(s, 4, "Construction",
                true, true, true), 0f, "DECAY_PERFECT_ZERO");
        }

        private static void CheckBook(ref LocalRng r, ref string input)
        {
            var s = new XpCustomizerSettings();
            QualityCategory quality = (QualityCategory)r.Int(7);
            string skill = BookXpService.VanillaSkillNames[r.Int(BookXpService.VanillaSkillNames.Length)];
            float q = r.Range(0f, 10f), k = r.Range(0f, 10f);
            s.bookXpEnabled = r.Bool(); s.bookQualityEnabled = r.Bool(); s.bookSkillEnabled = r.Bool();
            BookXpService.SetQualityMultiplier(s, quality, q);
            BookXpService.SetSkillMultiplier(s, skill, k);
            input = "quality=" + quality + "; skill=" + skill + "; q=" + F(q) +
                "; k=" + F(k) + "; master=" + s.bookXpEnabled +
                "; qualityOn=" + s.bookQualityEnabled + "; skillOn=" + s.bookSkillEnabled;
            float expected = !s.bookXpEnabled ? 1f :
                (s.bookQualityEnabled ? q : 1f) * (s.bookSkillEnabled ? k : 1f);
            Near(BookXpService.GetEffectiveTextbookMultiplier(s, quality, skill), expected, "BOOK_RATIO");
            Near(BookXpService.GetEffectiveTextbookMultiplier(s, quality, "ForeignSkill"),
                !s.bookXpEnabled ? 1f : (s.bookQualityEnabled ? q : 1f), "BOOK_FOREIGN_SKILL_NEUTRAL");
        }

        private static void CheckTrainer(ref LocalRng r, ref string input)
        {
            var s = new XpCustomizerSettings();
            float factor = r.Range(.01f, 50f);
            float xp = r.Range(.1f, 100000f);
            s.skilltrainerSpecificEnabled = r.Bool();
            SkilltrainerSpecificXpService.SetConfiguredMultiplier(s, "Shooting", factor);
            input = "skill=Shooting; factor=" + F(factor) + "; xp=" + F(xp) +
                "; enabled=" + s.skilltrainerSpecificEnabled;
            float expected = s.skilltrainerSpecificEnabled ? factor : 1f;
            Near(SkilltrainerSpecificXpService.GetEffectiveMultiplier(s, "Shooting"), expected,
                "TRAINER_EFFECTIVE_FACTOR");
            Near(SkilltrainerSpecificXpService.Apply(xp, "Neurotrainer_Shooting", "Shooting", s),
                xp * expected, "TRAINER_PAYOUT");
            Near(SkilltrainerSpecificXpService.Apply(xp, "ForeignTrainer", "Shooting", s),
                xp, "TRAINER_NON_VANILLA_PASSTHROUGH");
        }

        private static void CheckCareer(ref LocalRng r, ref string input)
        {
            float multiplier = r.Range(.05f, 15f);
            input = "multiplier=" + F(multiplier) + "; exact=true; customDistribution=false";
            CareerXpDistribution curve = CareerXpDistributionService.Build(multiplier,
                false, null, null, CareerXpDistributionService.XpRoundingExact);
            double sum = 0d;
            for (int level = 0; level < 20; level++)
            {
                float requirement = curve.GetRequirement(level);
                Finite(requirement, "CAREER_LEVEL_XP");
                if (requirement <= 0f) throw new Exception("CAREER_NONPOSITIVE_LEVEL=" + level);
                sum += requirement;
            }
            float expected = (float)Math.Round(CareerXpDistributionService.VanillaTotalXp *
                CareerXpDistributionService.NormalizeTotalMultiplier(multiplier),
                MidpointRounding.AwayFromZero);
            Near(curve.TotalXp, expected, "CAREER_TOTAL_TARGET");
            Near((float)sum, curve.TotalXp, "CAREER_LEVEL_SUM");
        }

        private static void Near(float actual, float expected, string what)
        {
            Finite(actual, what + "_actual"); Finite(expected, what + "_expected");
            if (Math.Abs(actual - expected) > Math.Max(.0015f, Math.Abs(expected) * .0005f))
                throw new InvalidOperationException(what + "; expected=" + F(expected) +
                    "; actual=" + F(actual));
        }
        private static void Finite(float value, string what)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidOperationException(what + "_NONFINITE=" + value);
        }
        private static string F(float value) { return value.ToString("0.######", CultureInfo.InvariantCulture); }
        private static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value)) return "EMPTY";
            string s = value.Replace('\r', ' ').Replace('\n', ' ').Replace(';', ',');
            return s.Length < 340 ? s : s.Substring(0, 340);
        }
        internal static void PrintLast()
        {
            Log.Message(string.IsNullOrEmpty(lastReport)
                ? "[XPC-RND1] NO_REPORT"
                : "[XPC-RND1] LAST_REPORT_REPRINT\n" + lastReport);
        }
    }

    public static class Phase4CRandomizedDebugActions
    {
        [DebugAction("XP Customizer", "Random pure XP audit 100 cases (4C-RND1)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run100() { Phase4CRandomizedDiagnostics.RunManually(100, Phase4CRandomizedDiagnostics.DefaultSeed); }

        [DebugAction("XP Customizer", "Random pure XP audit 1000 cases (4C-RND1)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Run1000() { Phase4CRandomizedDiagnostics.RunManually(1000, Phase4CRandomizedDiagnostics.DefaultSeed); }

        [DebugAction("XP Customizer", "Print last random XP audit (4C-RND1)",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void Print() { Phase4CRandomizedDiagnostics.PrintLast(); }
    }
}
#endif
