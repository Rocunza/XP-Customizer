#if DEBUG
using System;
using RimWorld;
using Verse;
using XpCustomizer.ConsciousnessLearning;

namespace XpCustomizer
{
    /// <summary>
    /// R4 independent model of normal SkillRecord.LearnRateFactor(false).
    /// Reads only vanilla stat/passion inputs, the configured V2 profile fields,
    /// and the saturation boolean observed before Learn(). It NEVER invokes
    /// LearnRateFactor, GWS/Consciousness/FurtherLearning GetFactor/Calculate,
    /// or changes settings, skills, jobs, and Harmony owners.
    /// The native stat system and saturation decision are INPUTS, not proven here.
    /// </summary>
    internal static partial class Phase4CCLiveXpCapture
    {
        private static bool foreignRateHookPresent;
        private static string rateHookOwnersAtStart = "NONE";
        private static int lastRateReviewCount;

        private static void SnapshotIndependentRate(StartState b)
        {
            b.OracleReason = "NOT_ELIGIBLE";
            b.OracleWorkSpeed = -1f;
            b.OracleForeignRateHook = foreignRateHookPresent;
            if (b.Direct || b.IgnoreLearnRate || b.FastLearningBefore ||
                b.TotallyDisabled || !b.RateCaptured) return;
            if (foreignRateHookPresent)
            {
                b.OracleReason = "FOREIGN_RATE_HOOK";
                return;
            }
            try
            {
                XpCustomizerSettings s = XpCustomizerMod.Settings;
                if (s == null || b.Pawn == null || b.Record == null || b.Record.def == null)
                {
                    b.OracleReason = "MISSING_INPUT";
                    return;
                }
                Pawn pawn = b.Pawn;
                b.OraclePassion = b.Record.passion.GetLearningFactor();
                b.OracleGlobal = pawn.GetStatValue(StatDefOf.GlobalLearningFactor);
                b.OracleAnimals = b.Record.def == SkillDefOf.Animals
                    ? pawn.GetStatValue(StatDefOf.AnimalsLearningFactor) : 1f;
                b.OracleConsciousness = pawn.health != null && pawn.health.capacities != null
                    ? pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) : 1f;
                b.OracleSpeedFactor = !s.consciousnessAffectsLearning ||
                    !s.consciousnessAffectsLearningSpeed || pawn.health == null ||
                    pawn.health.capacities == null ? 1f : IndependentCons(
                        b.OracleConsciousness, s.consciousnessAnchorSpeed);
                b.OracleWorkSpeed = -1f;
                b.OracleGwsFactor = 1f;
                b.OracleGwsWithoutReading = 1f;
                if (s.gwsEnabled && OracleGwsSkillEnabled(s, b.Skill) &&
                    pawn.RaceProps != null && pawn.RaceProps.Humanlike)
                {
                    b.OracleWorkSpeed = pawn.GetStatValue(StatDefOf.WorkSpeedGlobal);
                    b.OracleGwsWithoutReading = IndependentGws(b.OracleWorkSpeed, s);
                    b.OracleGwsFactor = !s.gwsApplyToReading && b.ReadingScope
                        ? 1f : b.OracleGwsWithoutReading;
                }
                b.OraclePostCap = 1f;
                if (b.SaturatedBefore)
                {
                    if (foreignFurtherHookPresent)
                    {
                        b.OracleReason = "FOREIGN_FURTHER_HOOK";
                        return;
                    }
                    b.OracleFurtherProof = ComposeIndependentFurther(b, s);
                    if (!b.OracleFurtherProof.Ready)
                    {
                        b.OracleReason = "FURTHER_" + b.OracleFurtherProof.SourceStatus;
                        return;
                    }
                    b.OraclePostCap = b.OracleFurtherProof.ExpectedFactor;
                    CompareIndependentFurther(b);
                }
                double expected = (double)b.OraclePassion * b.OracleGlobal *
                    b.OracleAnimals * b.OracleSpeedFactor * b.OracleGwsFactor *
                    b.OraclePostCap;
                b.OracleRate = (float)expected;
                b.OracleReady = IsFinite(b.OracleRate) && b.OracleRate >= 0f &&
                    IsFinite(b.OraclePostCap) && IsFinite(b.OracleSpeedFactor) &&
                    IsFinite(b.OracleGwsFactor) && IsFinite(b.OraclePassion) &&
                    IsFinite(b.OracleGlobal) && IsFinite(b.OracleAnimals);
                b.OracleReason = b.OracleReady ? "OK" : "INVALID_COMPONENT";
            }
            catch (Exception e)
            {
                b.OracleReady = false;
                b.OracleReason = "SOURCE_EXCEPTION_" + Clean(e.GetType().Name);
            }
        }

        // Deliberately separate from GwsLearningService.IsSkillEnabled/Calculate.
        private static bool OracleGwsSkillEnabled(XpCustomizerSettings s, string name)
        {
            bool defaultEnabled;
            switch (name)
            {
                case "Shooting": case "Melee": case "Social": defaultEnabled = false; break;
                case "Construction": case "Mining": case "Cooking": case "Plants":
                case "Animals": case "Crafting": case "Artistic": case "Medicine":
                case "Intellectual": defaultEnabled = true; break;
                default: return false;
            }
            bool value;
            return s.gwsSkillEnabled != null &&
                s.gwsSkillEnabled.TryGetValue(name, out value) ? value : defaultEnabled;
        }

        private static double Bound(double value, double fallback, double low, double high)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) value = fallback;
            return Math.Max(low, Math.Min(high, value));
        }

        // Independently reconstructs the low/high configured GWS curve.
        private static float IndependentGws(float speed, XpCustomizerSettings s)
        {
            if (s == null || !s.gwsEnabled || !IsFinite(speed)) return 1f;
            double g = speed;
            if (g < 1d)
            {
                if (!s.gwsLowEnabled) return 1f;
                double anchor = Bound(s.gwsLowAnchor, .30d, .30d, .95d);
                double t = Bound((1d - Math.Max(g, anchor)) / (1d - anchor), 0d, 0d, 1d);
                double curve = Bound(s.gwsLowCurvature, 1d, 0d, 2d);
                int mode = s.gwsLowCurve >= 0 && s.gwsLowCurve <= 2 ? s.gwsLowCurve : 0;
                double share = mode == 0 ? 1d - Math.Pow(1d - t, 1d + curve) :
                    mode == 2 ? Math.Pow(t, 1d + curve) : t;
                double factor = Bound(s.gwsLowFactor, .70d, 0d, 1d);
                return (float)Bound(1d - (1d - factor) * share, 1d, 0d, 1d);
            }
            if (g <= 1d || !s.gwsHighEnabled) return 1f;
            double high = Bound(s.gwsHighFactor, 1.30d, 1d, 5d);
            double highAnchor = Bound(s.gwsHighAnchor, 5d, 1.10d, 10d);
            double x = Math.Max(0d, (g - 1d) / (highAnchor - 1d));
            double shape = Bound(s.gwsHighCurvature, .40d, 0d, 2d);
            int highMode = s.gwsHighCurve >= 0 && s.gwsHighCurve <= 2 ? s.gwsHighCurve : 0;
            if (highMode == 0)
            {
                double limit = Bound(s.gwsHighDiminishingLimit, 1.50d,
                    high + .01d, 20d);
                double fraction = Bound((high - 1d) / (limit - 1d), 0d, 0d, .9999d);
                double k = -Math.Log(1d - fraction);
                double power = Bound(1d - .30d * shape, 1d, .4d, 1d);
                return (float)Bound(1d + (limit - 1d) *
                    (1d - Math.Exp(-k * Math.Pow(x, power))), 1d, 1d, limit);
            }
            double safety = Bound(s.gwsHighTechnicalLimit, 2d, high, 20d);
            double exponent = highMode == 2 ? 1d + shape : 1d;
            return (float)Bound(1d + (high - 1d) * Math.Pow(x, exponent),
                safety, 1d, safety);
        }

        // Read raw V2 profile fields, not the gameplay ToProfile/Calculate helper.
        internal static float IndependentCons(float c, ConsAnchorV2ProfileSettings p)
        {
            double lowAnchor = .5d, lowFactor = .5d, highAnchor = 1.20d;
            double highFactor = 1.08d, lowCurve = 1d, highCurve = 1d;
            double lowShape = 1d, highShape = 1d, limit = 2d, safety = 2d;
            if (p != null)
            {
                lowAnchor = Bound(p.LowAnchor, .5d, .05d, .95d);
                lowFactor = p.LowEnabled ? Bound(p.LowFactor, .5d, 0d, 1d) : 1d;
                highAnchor = Bound(p.HighAnchor, 1.20d, 1.01d, 10d);
                highFactor = p.HighEnabled ? Bound(p.HighFactor, 1.08d, 1d, 19.99d) : 1d;
                lowCurve = p.LowCurve >= 0 && p.LowCurve <= 2 ? p.LowCurve : 1d;
                highCurve = p.HighCurve >= 0 && p.HighCurve <= 2 ? p.HighCurve : 1d;
                lowShape = Bound(p.LowCurvature, 1d, 0d, 2d);
                highShape = Bound(p.HighCurvature, 1d, 0d, 2d);
                limit = Bound(p.HighDiminishingLimit, 2d, highFactor + .01d, 20d);
                safety = Bound(p.HighSafetyLimit, 2d, highFactor, 20d);
            }
            return IndependentCurve(c, lowAnchor, lowFactor, (int)lowCurve,
                lowShape, highAnchor, highFactor, (int)highCurve, highShape, limit, safety);
        }

        private static float IndependentFurtherCurve(float c,
            ConsciousnessFurtherV2Settings p)
        {
            double lowAnchor = .80d, lowFactor = .80d, highAnchor = 1.20d;
            double highFactor = 1.20d, lowCurve = 1d, highCurve = 1d;
            double lowShape = 1d, highShape = 1d, limit = 2d, safety = 2d;
            if (p != null)
            {
                lowAnchor = Bound(p.LowAnchor, .80d, .05d, .95d);
                lowFactor = p.LowEnabled ? Bound(p.LowFactor, .80d, 0d, 1d) : 1d;
                highAnchor = Bound(p.HighAnchor, 1.20d, 1.01d, 10d);
                highFactor = p.HighEnabled ? Bound(p.HighFactor, 1.20d, 1d, 19.99d) : 1d;
                lowCurve = p.LowCurve >= 0 && p.LowCurve <= 2 ? p.LowCurve : 1d;
                highCurve = p.HighCurve >= 0 && p.HighCurve <= 2 ? p.HighCurve : 1d;
                lowShape = Bound(p.LowCurvature, 1d, 0d, 2d);
                highShape = Bound(p.HighCurvature, 1d, 0d, 2d);
                limit = Bound(p.HighDiminishingLimit, 2d, highFactor + .01d, 20d);
                safety = Bound(p.HighSafetyLimit, 2d, highFactor, 20d);
            }
            return IndependentCurve(c, lowAnchor, lowFactor, (int)lowCurve,
                lowShape, highAnchor, highFactor, (int)highCurve, highShape, limit, safety);
        }

        private static double CurveShape(double t, int mode, double curve)
        {
            double v = Bound(t, 0d, 0d, 1d);
            return mode == 0 ? 1d - Math.Pow(1d - v, 1d + curve) :
                mode == 2 ? Math.Pow(v, 1d + curve) : v;
        }

        private static float IndependentCurve(float c, double lowAnchor, double lowFactor,
            int lowMode, double lowShape, double highAnchor, double highFactor,
            int highMode, double highShape, double limit, double safety)
        {
            if (!IsFinite(c)) return 1f;
            if (c <= 0f) return lowFactor >= 1d ? 1f : 0f;
            if (c == 1f) return 1f;
            if (c < 1f)
            {
                if (lowFactor >= 1d) return 1f;
                if (c == (float)lowAnchor) return (float)lowFactor;
                double y = c > lowAnchor
                    ? 1d - (1d - lowFactor) * CurveShape((1d - c) / (1d - lowAnchor), lowMode, lowShape)
                    : lowFactor * (1d - CurveShape((lowAnchor - c) / lowAnchor, lowMode, lowShape));
                return (float)Bound(y, 1d, 0d, 1d);
            }
            if (c == (float)highAnchor) return (float)highFactor;
            if (highFactor <= 1d) return 1f;
            double d = (double)c - 1d;
            double da = highAnchor - 1d;
            if (highMode == 0)
            {
                double p = Math.Pow(2d, 1d - highShape);
                double fraction = (highFactor - 1d) / (limit - 1d);
                double scale = da / (Math.Pow(1d - fraction, -1d / p) - 1d);
                return (float)Bound(1d + (limit - 1d) *
                    (1d - Math.Pow(1d + d / scale, -p)), 1d, 1d, limit);
            }
            double exponent = highMode == 2 ? 1d + highShape : 1d;
            return (float)Bound(1d + (highFactor - 1d) * Math.Pow(d / da, exponent),
                safety, 1d, safety);
        }

        private static float RateOracleTolerance(float expected)
        {
            // Tight relative comparison for a reconstructed rate; larger
            // payout accumulator precision is handled separately by R3.
            return Math.Max(0.0001f, Math.Abs(expected) * 0.002f);
        }

        // B1-B1: use precisely the same sealed Sample gate as PayoutClass.
        private static string IndependentRateClass(Sample sample)
        {
            return IndependentRateClassCore(sample, CanApproveBillForOfficial(sample));
        }

        private static string IndependentRateClassCore(Sample sample, bool approvedBill)
        {
            StartState b = sample.Before;
            bool work = b.Category == "WORK_CANDIDATE";
            bool reading = b.Category == "READING";
            if (!work && !reading) return "NOT_TARGET";
            if (!IsFinite(b.Input) || b.Input <= 0f) return "SKIP_NONPOSITIVE_INPUT";
            if (work && !ExpectedWorkSkill(b) &&
                !(approvedBill && InBillAuthorizationScope(b))) return "SKIP_JOB_SKILL_UNVERIFIED";
            if (reading && !b.ReadingScope) return "SKIP_READING_SCOPE_UNCERTAIN";
            if (b.Direct || b.IgnoreLearnRate || b.FastLearningBefore) return "SKIP_NONSTANDARD_LEARNING";
            if (b.TotallyDisabled || sample.AfterDisabled) return "SKIP_DISABLED_SKILL";
            if (!sample.Comparable) return "INCONCLUSIVE_LEVEL_TRANSITION";
            if (b.ForeignLearnHookAtStart) return "INCONCLUSIVE_FOREIGN_LEARN_HOOK";
            if (b.OracleForeignRateHook) return "INCONCLUSIVE_FOREIGN_RATE_HOOK";
            if (!b.RateCaptured || !b.OracleReady) return "INCONCLUSIVE_ORACLE_INPUT";
            if (Math.Abs(b.OracleRate - b.RateBefore) > RateOracleTolerance(b.OracleRate))
                return "REVIEW_INDEPENDENT_RATE_MISMATCH";
            return "PASS_INDEPENDENT_RATE";
        }

        // T01: freeze a D01 golden vector *independently* of production defaults.
        // Historic values remain explicit fixtures; they are never inferred from
        // new objects, nor substituted for missing/invalid D01 numeric fields.
        private static void VerifyIndependentRateSelfAudit()
        {
            int cases = 0;
            var freshGws = new XpCustomizerSettings { gwsEnabled = true };
            CheckOracleNumber(freshGws.gwsHighAnchor, 5f,
                "D01_GWS_CONFIG_ANCHOR500", ref cases);
            CheckOracleNumber(freshGws.gwsHighFactor, 1.30f,
                "D01_GWS_CONFIG_FACTOR130", ref cases);
            CheckOracleNumber(freshGws.gwsHighCurvature, .40f,
                "D01_GWS_CONFIG_CURVATURE40", ref cases);
            CheckOracleNumber(freshGws.gwsHighDiminishingLimit, 1.50f,
                "D01_GWS_CONFIG_LIMIT150", ref cases);
            CheckOracleNumber(freshGws.gwsHighCurve, 0f,
                "D01_GWS_CONFIG_DIMINISHING_MODE", ref cases);
            CheckOracleNumber(IndependentGws(.3f, freshGws), .70f,
                "D01_GWS_LOW_ANCHOR", ref cases);
            CheckOracleNumber(IndependentGws(1f, freshGws), 1f,
                "D01_GWS_NEUTRAL", ref cases);
            CheckOracleNumber(IndependentGws(3f, freshGws), 1.1960918f,
                "D01_GWS_DIMINISHING_G300_CURVE40", ref cases);
            CheckOracleNumber(IndependentGws(5f, freshGws), 1.30f,
                "D01_GWS_HIGH_ANCHOR500", ref cases);
            CheckOracleNumber(IndependentGws(10f, freshGws), 1.4229754f,
                "D01_GWS_G1000_LIMIT150", ref cases);
            freshGws.gwsHighEnabled = false;
            CheckOracleNumber(IndependentGws(5f, freshGws), 1f,
                "D01_GWS_HIGH_DISABLED", ref cases);
            freshGws.gwsHighEnabled = true;
            freshGws.gwsHighAnchor = float.NaN;
            freshGws.gwsHighFactor = float.NaN;
            freshGws.gwsHighCurvature = float.NaN;
            freshGws.gwsHighDiminishingLimit = float.NaN;
            freshGws.gwsHighCurve = -99;
            CheckOracleNumber(IndependentGws(5f, freshGws), 1.30f,
                "D01_GWS_INVALID_NUMBERS_USE_FRESH_FALLBACK", ref cases);

            // These old values are intentionally all explicit; never use them
            // as the defaults of freshly constructed settings or null profiles.
            var historicalGws = new XpCustomizerSettings {
                gwsEnabled = true, gwsHighEnabled = true,
                gwsLowEnabled = true, gwsLowAnchor = .30f, gwsLowFactor = .70f,
                gwsLowCurve = 1, gwsLowCurvature = 1f,
                gwsHighAnchor = 1.8f, gwsHighFactor = 1.15f,
                gwsHighCurve = 0, gwsHighCurvature = 1f,
                gwsHighDiminishingLimit = 1.30f, gwsHighTechnicalLimit = 2f
            };
            CheckOracleNumber(IndependentGws(1.8f, historicalGws), 1.15f,
                "HISTORIC_GWS_HIGH_ANCHOR180", ref cases);
            historicalGws.gwsHighAnchor = 10f;
            CheckOracleNumber(IndependentGws(10f, historicalGws), 1.15f,
                "HISTORIC_GWS_EXPLICIT_ANCHOR1000", ref cases);

            var settings = new XpCustomizerSettings();
            var speed = settings.consciousnessAnchorSpeed;
            var capacity = settings.consciousnessAnchorCapacity;
            CheckOracleNumber(speed.HighAnchor, 1.20f,
                "D01_CONS_SPEED_CONFIG_ANCHOR120", ref cases);
            CheckOracleNumber(speed.HighFactor, 1.08f,
                "D01_CONS_SPEED_CONFIG_FACTOR108", ref cases);
            CheckOracleNumber(speed.HighCurve, 1f,
                "D01_CONS_SPEED_CONFIG_LINEAR", ref cases);
            CheckOracleNumber(capacity.HighAnchor, 1.20f,
                "D01_CONS_CAPACITY_CONFIG_ANCHOR120", ref cases);
            CheckOracleNumber(capacity.HighFactor, 1.08f,
                "D01_CONS_CAPACITY_CONFIG_FACTOR108", ref cases);
            CheckOracleNumber(capacity.HighCurve, 1f,
                "D01_CONS_CAPACITY_CONFIG_LINEAR", ref cases);
            CheckOracleNumber(IndependentCons(.5f, speed), .5f,
                "D01_CONS_SPEED_LOW_ANCHOR", ref cases);
            CheckOracleNumber(IndependentCons(1f, speed), 1f,
                "D01_CONS_SPEED_NEUTRAL", ref cases);
            CheckOracleNumber(IndependentCons(1.20f, speed), 1.08f,
                "D01_CONS_SPEED_ANCHOR120", ref cases);
            CheckOracleNumber(IndependentCons(1.35f, speed), 1.14f,
                "D01_CONS_SPEED_135", ref cases);
            CheckOracleNumber(IndependentCons(1.20f, capacity), 1.08f,
                "D01_CONS_CAPACITY_ANCHOR120", ref cases);
            CheckOracleNumber(IndependentCons(1.35f, capacity), 1.14f,
                "D01_CONS_CAPACITY_135", ref cases);
            CheckOracleNumber(IndependentCons(1.20f, null), 1.08f,
                "D01_CONS_NULL_PROFILE_FALLBACK", ref cases);
            var brokenSpeed = new ConsAnchorV2ProfileSettings { HighAnchor = float.NaN,
                HighFactor = float.NaN, HighCurve = -1 };
            CheckOracleNumber(IndependentCons(1.35f, brokenSpeed), 1.14f,
                "D01_CONS_INVALID_HIGH_FIELDS_FALLBACK", ref cases);
            var historicalCons = new ConsAnchorV2ProfileSettings {
                HighAnchor = 1.35f, HighFactor = 1.10f, HighCurve = 1,
                HighCurvature = 1f, HighDiminishingLimit = 2f, HighSafetyLimit = 2f
            };
            CheckOracleNumber(IndependentCons(1.35f, historicalCons), 1.10f,
                "HISTORIC_CONS_EXPLICIT_135_110", ref cases);

            var further = settings.consciousnessFurtherV2;
            CheckOracleNumber(further.HighAnchor, 1.20f,
                "D01_FURTHER_CONFIG_ANCHOR120", ref cases);
            CheckOracleNumber(further.HighFactor, 1.20f,
                "D01_FURTHER_CONFIG_FACTOR120", ref cases);
            CheckOracleNumber(further.HighCurve, 1f,
                "D01_FURTHER_CONFIG_LINEAR", ref cases);
            CheckOracleNumber(IndependentFurtherCurve(.8f, further), .8f,
                "D01_FURTHER_LOW_ANCHOR", ref cases);
            CheckOracleNumber(IndependentFurtherCurve(1.0f, further), 1f,
                "D01_FURTHER_NEUTRAL", ref cases);
            CheckOracleNumber(IndependentFurtherCurve(1.20f, further), 1.20f,
                "D01_FURTHER_HIGH_ANCHOR120", ref cases);
            CheckOracleNumber(IndependentFurtherCurve(1.35f, further), 1.35f,
                "D01_FURTHER_135", ref cases);
            CheckOracleNumber(IndependentFurtherCurve(1.20f, null), 1.20f,
                "D01_FURTHER_NULL_PROFILE_FALLBACK", ref cases);
            var brokenFurther = new ConsciousnessFurtherV2Settings {
                HighAnchor = float.NaN, HighFactor = float.NaN, HighCurve = -1
            };
            CheckOracleNumber(IndependentFurtherCurve(1.35f, brokenFurther), 1.35f,
                "D01_FURTHER_INVALID_HIGH_FIELDS_FALLBACK", ref cases);
            var historicalFurther = new ConsciousnessFurtherV2Settings {
                HighAnchor = 1.35f, HighFactor = 1.35f, HighCurve = 1,
                HighCurvature = 1f, HighDiminishingLimit = 2f, HighSafetyLimit = 2f
            };
            CheckOracleNumber(IndependentFurtherCurve(1.35f, historicalFurther), 1.35f,
                "HISTORIC_FURTHER_EXPLICIT_135_135", ref cases);

            // Classifier regression: no Pawn, no real Learn(), and no mod writes.
            var b = new StartState { Category = "WORK_CANDIDATE", WorkKind = "HARVEST_CANDIDATE",
                Skill = "Plants", Input = 1f, OracleReady = true, RateCaptured = true,
                OracleRate = 1.15f, RateBefore = 1.15f };
            var row = new Sample { Before = b, Comparable = true };
            RequireOracleClass(row, "PASS_INDEPENDENT_RATE", "MATCH", ref cases);
            b.RateBefore = 1.30f;
            RequireOracleClass(row, "REVIEW_INDEPENDENT_RATE_MISMATCH", "MISMATCH", ref cases);
            b.RateBefore = 1.15f; b.OracleForeignRateHook = true;
            RequireOracleClass(row, "INCONCLUSIVE_FOREIGN_RATE_HOOK", "FOREIGN", ref cases);
            b.OracleForeignRateHook = false; b.ForeignLearnHookAtStart = true;
            RequireOracleClass(row, "INCONCLUSIVE_FOREIGN_LEARN_HOOK", "LEARN_OWNER", ref cases);
            b.ForeignLearnHookAtStart = false; b.TotallyDisabled = true;
            RequireOracleClass(row, "SKIP_DISABLED_SKILL", "DISABLED", ref cases);
            b.TotallyDisabled = false; b.Skill = "Social";
            RequireOracleClass(row, "SKIP_JOB_SKILL_UNVERIFIED", "JOB_SKILL", ref cases);
            Log.Message("[XPC-4CC][INDEPENDENT_RATE_AUDIT] status=PASS; cases=" + cases +
                "; fresh=D01_CONS120_108_FURTHER120_120_GWS500_130_DIM40_LIMIT150" +
                "; historical=EXPLICIT_135_110_AND_GWS180_115_AND_FURTHER135_135" +
                "; invalidFallbacks=D01_NOT_HISTORICAL; independentModel=True" +
                "; syntheticOnly=True; noRealPawnLearnCalls=True; noSettingsWrites=True" +
                "; oracle=RAW_SETTINGS_AND_VANILLA_STAT_INPUTS; revision=" + Revision +
                "; build=" + XpCustomizerMod.BuildId);
        }

        private static void CheckOracleNumber(float actual, float expected, string id,
            ref int cases)
        {
            cases++;
            if (!IsFinite(actual) || Math.Abs(actual - expected) > .0002f)
                throw new InvalidOperationException("4C-C T01 anchor " + id +
                    " expected=" + expected + " actual=" + actual);
        }
        // 기존 호출부와 호환
        private static void RequireOracleClass(
            Sample sample, string expected, string id)
        {
            string actual = IndependentRateClass(sample);

            if (actual != expected)
                throw new InvalidOperationException(
                    "4C-C T01 classification " + id +
                    " expected=" + expected +
                    " actual=" + actual);
        }

        // T01 자체검사용 카운트 지원
        private static void RequireOracleClass(
            Sample sample, string expected, string id,
            ref int cases)
        {
            RequireOracleClass(sample, expected, id);
            cases++;
        }

    }
}
#endif
