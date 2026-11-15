#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.FurtherLearning;
using XpCustomizer.SkillTooltip;
using XpCustomizer.UI;
using Verse;

namespace XpCustomizer
{
    internal static partial class XpCustomizerSelfTests
    {
        // ================================================================
        // Further Learning: direct-input precision regression
        // ================================================================

        private static void TestFurtherLearningBaseFactorPrecision()
        {
            // These values are stored as 0..1 factors. In particular,
            // 37% and 12.5% must not snap to old 5% slider steps.
            AssertNear(
                FurtherLearningService.NormalizeBaseFactor(-0.5f),
                0f, 0.000001f, "Further base lower clamp");
            AssertNear(
                FurtherLearningService.NormalizeBaseFactor(0f),
                0f, 0.000001f, "Further base zero");
            AssertNear(
                FurtherLearningService.NormalizeBaseFactor(0.20f),
                0.20f, 0.000001f, "Further base vanilla");
            AssertNear(
                FurtherLearningService.NormalizeBaseFactor(0.37f),
                0.37f, 0.000001f, "Further base 37 percent exact");
            AssertNear(
                FurtherLearningService.NormalizeBaseFactor(0.125f),
                0.125f, 0.000001f, "Further base fractional percent");
            AssertNear(
                FurtherLearningService.NormalizeBaseFactor(1f),
                1f, 0.000001f, "Further base 100 percent");
            AssertNear(
                FurtherLearningService.NormalizeBaseFactor(1.5f),
                1f, 0.000001f, "Further base upper clamp");
        }


        // ================================================================
        // R5F3: source stat as ratio; the effective multiplier stays percent.
        // Pure presentation checks -- no live Pawn and no XP writes.
        // ================================================================
        private static void TestTooltipSourceRatioUiR5F3()
        {
            AssertTrue(TooltipSourceRatioFormatter.Format(0.8f) == "0.8" &&
                TooltipSourceRatioFormatter.Format(0.89f) == "0.89" &&
                TooltipSourceRatioFormatter.Format(1f) == "1.0" &&
                TooltipSourceRatioFormatter.Format(1.2f) == "1.2" &&
                TooltipSourceRatioFormatter.Format(1.35f) == "1.35" &&
                TooltipSourceRatioFormatter.Format(1.7f) == "1.7" &&
                TooltipSourceRatioFormatter.Format(float.NaN) == "N/A",
                "R5F3 source ratios show 1 or 2 decimal places with invariant decimal dot");
            AssertTrue(SkillTooltipService.BuildGwsPreviewLine(0.89f, 0.91f) ==
                "  - GW speed: x91%",
                "GTL1 GWS source 0.89 is hidden; x91% effect remains visible");
            AssertTrue(SkillTooltipService.BuildConsPreviewLine(1.35f, 1.06f) ==
                "  - Cons 1.35: x106%" &&
                SkillTooltipService.BuildConsPreviewLine(1.2f, 1.06f) ==
                "  - Cons 1.2: x106%",
                "R5F3 Cons source 1.35/1.2 must remain distinct from x106% effect");
            Log.Message("[XP Customizer][TOOLTIP-RATIOS-R5F3] status=PASS; " +
                "GWspeed=x91%; gwsSourceRatio=HIDDEN_UI_ONLY; Cons=1.35:x106%; neutralVisibility=R5F4; " +
                "ratioFormat=0.0_SHARP_INVARIANT; factorFormat=xROUND_PERCENT; " +
                "gameplay=UNCHANGED; build=" + XpCustomizerMod.BuildId);
        }

        // ================================================================
        // R5F4: hide only truly neutral applied effects in every tooltip
        // section; do not change formulas, source stats or diagnostic data.
        // ================================================================
        private static void TestTooltipNeutralVisibilityR5F4()
        {
            AssertTrue(SkillTooltipService.IsNeutralAppliedMultiplier(1f) &&
                !SkillTooltipService.IsNeutralAppliedMultiplier(1.0001f) &&
                !SkillTooltipService.IsNeutralAppliedMultiplier(0.9999f),
                "R5F4 neutral detection uses effective factor BEFORE percent rounding");

            AssertTrue(SkillTooltipService.BuildGwsPreviewLine(1f, 1f) == string.Empty &&
                SkillTooltipService.BuildConsPreviewLine(1f, 1f) == string.Empty,
                "R5F4 x100% GWS and Cons lines must be hidden");
            AssertTrue(SkillTooltipService.BuildGwsPreviewLine(1.44f, 1f) == string.Empty &&
                SkillTooltipService.BuildConsPreviewLine(1.35f, 1f) == string.Empty,
                "R5F4 source stat alone cannot force a neutral EFFECT to display");
            AssertTrue(SkillTooltipService.BuildGwsPreviewLine(1f, 1.11f) ==
                "  - GW speed: x111%" &&
                SkillTooltipService.BuildConsPreviewLine(1f, 1.06f) ==
                "  - Cons 1.0: x106%",
                "R5F4 non-neutral effect must display even when source stat is 1.0");
            AssertTrue(!string.IsNullOrEmpty(
                SkillTooltipService.BuildConsPreviewLine(1.0001f, 1.004f)),
                "R5F4 genuine sub-1-percent effect must not disappear after display rounding");

            string neutralFurther = SkillTooltipService.BuildFurtherLearningDetailLines(
                new FurtherLearningBreakdown(0.20f,
                    new List<FurtherLearningContribution> {
                        FurtherLearningContribution.CreateMultiplier("Cons 1.0", 1f),
                        new FurtherLearningContribution("Mental threshold 17%", 6f)
                    }, 0.26f, 0.26f));
            AssertTrue(neutralFurther ==
                "\n  - Base value: 20%\n  - Willpower: +6%",
                "R5F4 neutral Cons not shown in Further Learning; Willpower preserved");

            string mixedFurther = SkillTooltipService.BuildFurtherLearningDetailLines(
                new FurtherLearningBreakdown(0.20f,
                    new List<FurtherLearningContribution> {
                        FurtherLearningContribution.CreateMultiplier("Cons 1.0", 1f),
                        FurtherLearningContribution.CreateMultiplier("Cons 1.2", 1.235f),
                        new FurtherLearningContribution("Mental threshold 17%", 6f)
                    }, 0.3211f, 0.3211f));
            AssertTrue(mixedFurther ==
                "\n  - Base value: 20%\n  - Willpower: +6%\n  - Cons 1.2: x124%",
                "R5F4 retain non-neutral Further multiplier and additive-first order");

            Log.Message("[XP Customizer][TOOLTIP-NEUTRAL-R5F4] status=PASS; " +
                "speedGwsNeutral=HIDDEN; speedConsNeutral=HIDDEN; " +
                "dailyCapConsNeutral=HIDDEN_SHARED_FORMATTER; " +
                "furtherConsNeutral=HIDDEN; nonNeutralEvenSourceOne=KEPT; " +
                "comparison=EXACT_EFFECT_X1_PRE_ROUNDING; " +
                "gameplay=UNCHANGED; diagnosticData=UNCHANGED; build=" + XpCustomizerMod.BuildId);
        }

        // ================================================================
        // Further Learning: Mental Break Threshold anchors, slider & migration
        // ================================================================

        private static void TestFurtherLearningMentalThresholdCurve()
        {
            // Center = 100% strength. At base 20%, these are 30/20/10%.
            AssertNear(FurtherLearningService.CalculateThresholdAdjustment(0.17f, 1f),
                10f, 0.0001f, "MTH 17% => +10pp");
            AssertNear(FurtherLearningService.CalculateThresholdAdjustment(0.35f, 1f),
                0f, 0.0001f, "MTH 35% => 0pp");
            AssertNear(FurtherLearningService.CalculateThresholdAdjustment(0.50f, 1f),
                -10f, 0.0001f, "MTH 50% => -10pp");
            AssertNear(FurtherLearningService.CalculateThresholdAdjustment(0.26f, 1f),
                5f, 0.0001f, "MTH 26% => +5pp");
            AssertNear(FurtherLearningService.CalculateThresholdAdjustment(0.43f, 1f),
                -10f * 8f / 15f, 0.0001f, "MTH 43% interpolated");
            AssertNear(FurtherLearningService.CalculateThresholdAdjustment(0.17f, 0f),
                0f, 0.0001f, "MTH slider 0 = neutral");
            AssertNear(FurtherLearningService.CalculateThresholdAdjustment(0.17f, 0.5f),
                5f, 0.0001f, "MTH slider 50% = half strength");
            AssertNear(FurtherLearningService.CalculateThresholdAdjustment(0.17f,
                XpCustomizerDefaults.FurtherLearningMentalStrength),
                6f, 0.0001f, "MTH mod default 60% at 17% = +6pp");
            AssertNear(FurtherLearningService.CalculateThresholdAdjustment(0.50f,
                XpCustomizerDefaults.FurtherLearningMentalStrength),
                -6f, 0.0001f, "MTH mod default 60% at 50% = -6pp");
            AssertNear(FurtherLearningService.CalculateThresholdAdjustment(0.50f, 2f),
                -20f, 0.0001f, "MTH slider 200% = double strength");
            AssertNear(FurtherLearningService.NormalizeThresholdStrength(-2f),
                0f, 0.0001f, "MTH strength min");
            AssertNear(FurtherLearningService.NormalizeThresholdStrength(9f),
                2f, 0.0001f, "MTH strength max");
            AssertNear(FurtherLearningService.CalculateThresholdAdjustment(0.01f, 1f),
                10f, 0.0001f, "MTH below 17% stops at anchor (anti-stacking)");
            AssertNear(FurtherLearningService.CalculateThresholdAdjustment(0.99f, 1f),
                -10f, 0.0001f, "MTH stat upper clamp");

            var vanilla = new XpCustomizerSettings();
            if (vanilla.furtherLearningMentalThresholdEnabled ||
                Math.Abs(vanilla.furtherLearningMentalThresholdStrength -
                    XpCustomizerDefaults.FurtherLearningMentalStrength) > 0.00001f ||
                Math.Abs(XpCustomizerDefaults.FurtherLearningMentalVanillaStrength - 1f) > 0.00001f)
                throw new Exception("[XP Customizer] MTH fresh install must be OFF with 60% mod default; vanilla reset reference must be 100%");

            // Legacy values are kept but must never activate the new feature.
            var legacy = new XpCustomizerSettings();
            legacy.furtherLearningTraitModifiersEnabled = true;
            legacy.furtherLearningIronWilledModifier = 10f;
            legacy.NormalizeFurtherLearningMentalSettings();
            if (legacy.furtherLearningMentalThresholdEnabled ||
                legacy.furtherLearningIronWilledModifier != 10f)
                throw new Exception("[XP Customizer] MTH legacy settings rollback failed");
        }


        // ================================================================
        // Consciousness Further Learning V2: post-cap multiplier regressions
        // ================================================================
        private static void TestConsciousnessFurtherLearningV2()
        {
            var s = new XpCustomizerSettings();
            s.consciousnessAffectsLearning = true;
            s.consciousnessAffectsFurtherLearning = true;
            var p = s.consciousnessFurtherV2;
            AssertNear(p.LowAnchor, 0.80f, 0.0001f, "Further V2 default C80");
            AssertNear(p.LowFactor, 0.80f, 0.0001f, "Further V2 default low x0.80");
            AssertNear(p.HighAnchor, 1.20f, 0.0001f, "Further V2 default C120");
            AssertNear(p.HighFactor, 1.20f, 0.0001f, "Further V2 default high x1.20");
            AssertNear(p.HighDiminishingLimit, 2f, 0.0001f, "Further V2 high limit x2");
            AssertTrue(p.LowCurve == (int)ConsAnchorCurveMode.Linear &&
                p.HighCurve == (int)ConsAnchorCurveMode.Linear,
                "Further V2 default curve modes");
            AssertNear(ConsciousnessFurtherLearningService.CalculateMultiplier(1.35f, null),
                1f, 0.0001f, "Further V2 null settings neutral");
            AssertNear(ConsciousnessFurtherLearningService.CalculateMultiplier(0.80f, s),
                0.80f, 0.0001f, "Further V2 C80 anchor");
            AssertNear(ConsciousnessFurtherLearningService.CalculateMultiplier(1.00f, s),
                1f, 0.0001f, "Further V2 C100 neutral");
            AssertNear(ConsciousnessFurtherLearningService.CalculateMultiplier(1.35f, s),
                1.35f, 0.0001f, "Further V2 C135 linear above-anchor point");
            AssertNear(ConsciousnessFurtherLearningService.CalculateMultiplier(1.70f, s),
                1.70f, 0.001f, "Further V2 C170 Linear high default");
            AssertNear(FurtherLearningService.CalculateRawFactor(0.20f, 0.80f, 0f),
                0.16f, 0.0001f, "Further V2 C80 final 16 percent");
            AssertNear(FurtherLearningService.CalculateRawFactor(0.20f, 1.35f, 0f),
                0.27f, 0.0001f, "Further V2 C135 final 27 percent");
            AssertNear(FurtherLearningService.CalculateRawFactor(0.20f, 1.35f, 6f),
                0.351f, 0.0001f, "R5F1 (base+6pp)*x1.35 =35.1 percent");
            AssertNear(FurtherLearningService.CalculateRawFactor(0.20f, 1.35f, -6f),
                0.189f, 0.0001f, "R5F1 (base-6pp)*x1.35 =18.9 percent");
            AssertNear(FurtherLearningService.CalculateRawFactor(0.20f, 1.235f, 6f),
                0.3211f, 0.0001f, "R5F1 user case (20%+6pp)*1.235 =32.11 percent");
            AssertNear(FurtherLearningService.CalculateRawFactor(0.20f, 1f, 6f),
                0.26f, 0.0001f, "Further V2 mental unaffected by x1");
            AssertNear(FurtherLearningService.CalculateRawFactor(0.20f, 0f, -6f),
                0f, 0.0001f, "R5F1 zero cons multiplier zeros even negative subtotal");
            AssertTrue(FurtherLearningService.CalculateRawFactor(0f, 1.5f, -6f) < 0f,
                "R5F1 negative raw correctly reaches floor in gameplay");

            // Tooltips are pure strings built from typed contribution data.
            // Supply intentionally reversed contribution order to prove that
            // presentation always lists the additive step first.
            var tooltipContributions = new List<FurtherLearningContribution> {
                FurtherLearningContribution.CreateMultiplier("Cons 1.2", 1.235f),
                new FurtherLearningContribution("Mental threshold 17%", 6f)
            };
            var tooltipProof = new FurtherLearningBreakdown(.20f, tooltipContributions,
                .3211f, .3211f);
            string detail = SkillTooltipService.BuildFurtherLearningDetailLines(tooltipProof);
            AssertTrue(detail.Contains("\n  - Base value: 20%") &&
                detail.Contains("\n  - Willpower: +6%") &&
                !detail.Contains("Subtotal:") &&
                detail.Contains("\n  - Cons 1.2: x124%"),
                "R5F2 tooltip omits subtotal while retaining values and rounded multiplier");
            AssertTrue(detail.IndexOf("Base value", StringComparison.Ordinal) <
                detail.IndexOf("Willpower", StringComparison.Ordinal) &&
                detail.IndexOf("Willpower", StringComparison.Ordinal) <
                detail.IndexOf("Cons", StringComparison.Ordinal) &&
                !detail.Contains("x1.235") &&
                detail == "\n  - Base value: 20%\n  - Willpower: +6%\n  - Cons 1.2: x124%",
                "R5F3 tooltip exactly Base > Willpower > Cons with no subtotal");
            string onlyMental = SkillTooltipService.BuildFurtherLearningDetailLines(
                new FurtherLearningBreakdown(.20f,
                    new List<FurtherLearningContribution> {
                        new FurtherLearningContribution("Mental threshold 17%", 6f)
                    }, .26f, .26f));
            AssertTrue(!onlyMental.Contains("Subtotal:") &&
                onlyMental.Contains("Willpower: +6%"),
                "R5F2 no subtotal without multiplier");
            string onlyConsciousness = SkillTooltipService.BuildFurtherLearningDetailLines(
                new FurtherLearningBreakdown(.20f,
                    new List<FurtherLearningContribution> {
                        FurtherLearningContribution.CreateMultiplier("Cons 1.2", 1.235f)
                    }, .247f, .247f));
            AssertTrue(onlyConsciousness ==
                "\n  - Base value: 20%\n  - Cons 1.2: x124%",
                "R5F2 multiplier-only tooltip must not add subtotal");

            var multiplierContribution = FurtherLearningContribution.CreateMultiplier("Cons 1.35", 1.35f);
            AssertTrue(multiplierContribution.IsMultiplier &&
                Math.Abs(multiplierContribution.Multiplier - 1.35f) < 0.0001f,
                "Further V2 multiplier contribution stays typed");
            var mentalContribution = new FurtherLearningContribution("Mental threshold 17%", 6f);
            AssertTrue(!mentalContribution.IsMultiplier &&
                Math.Abs(mentalContribution.PercentagePoints - 6f) < 0.0001f,
                "Further V2 Mental contribution remains pp");

            p.LowEnabled = false;
            AssertNear(ConsciousnessFurtherLearningService.CalculateMultiplier(0.30f, s),
                1f, 0.0001f, "Further V2 low OFF neutral even at C30");
            p.LowEnabled = true;
            p.HighEnabled = false;
            AssertNear(ConsciousnessFurtherLearningService.CalculateMultiplier(2.50f, s),
                1f, 0.0001f, "Further V2 high OFF neutral at C250");
            p.HighEnabled = true;
            p.LowFactor = 1f;
            p.HighFactor = 1f;
            AssertTrue(!ConsciousnessFurtherLearningService.HasConfiguredEffect(s),
                "Further V2 configured-neutral skip");
            p.LowFactor = 0.80f;
            p.HighFactor = 1.20f;
            AssertTrue(ConsciousnessFurtherLearningService.HasConfiguredEffect(s),
                "Further V2 configured non-neutral");
            s.consciousnessAffectsFurtherLearning = false;
            AssertTrue(!ConsciousnessFurtherLearningService.HasConfiguredEffect(s),
                "Further V2 sub OFF skipped");
            s.consciousnessAffectsFurtherLearning = true;
            s.consciousnessAffectsLearning = false;
            AssertTrue(!ConsciousnessFurtherLearningService.HasConfiguredEffect(s),
                "Further V2 master OFF skipped");
            s.consciousnessAffectsLearning = true;

            // Legacy pp settings may still be in the XML. They are inert.
            s.consciousnessFurtherBonusAt170 = 88f;
            s.consciousnessFurtherMaximumPenalty = -100f;
            AssertNear(ConsciousnessFurtherLearningService.CalculateMultiplier(1.35f, s),
                1.35f, 0.0001f, "Further V2 old pp fields inert");
            s.consciousnessAnchorSpeed.HighFactor = 3f;
            s.consciousnessAnchorCapacity.HighFactor = 4f;
            AssertNear(ConsciousnessFurtherLearningService.CalculateMultiplier(1.35f, s),
                1.35f, 0.0001f, "Further V2 isolated from Speed/Capacity");

            p.HighFactor = float.NaN;
            AssertNear(ConsciousnessFurtherLearningService.CalculateMultiplier(1.35f, s),
                1.35f, 0.0001f, "Further V2 invalid factor normalizes on read");
            AssertTrue(float.IsNaN(p.HighFactor), "Further V2 pure read must not mutate settings");
            p.Normalize();
            AssertNear(p.HighFactor, 1.20f, 0.0001f, "Further V2 load normalization");
            // Explicitly saved former defaults must remain legal custom values.
            p.LowFactor = 0.75f;
            p.HighAnchor = 1.35f; // Historic explicitly stored high anchor.
            p.HighFactor = 1.10f;
            AssertNear(ConsciousnessFurtherLearningService.CalculateMultiplier(0.80f, s),
                0.75f, 0.0001f, "Further V2 previously saved low x0.75 still supported");
            AssertNear(ConsciousnessFurtherLearningService.CalculateMultiplier(1.35f, s),
                1.10f, 0.0001f, "Further V2 previously saved high x1.10 still supported");
            p.HighAnchor = 1.70f;
            p.HighFactor = 1.28f;
            AssertNear(ConsciousnessFurtherLearningService.CalculateMultiplier(1.70f, s),
                1.28f, 0.0001f, "Further V2 custom 170 x1.28 supported");

            p.LowEnabled = false;
            p.HighEnabled = false;
            p.HighFactor = 1.6f;
            SettingsPresetService.ApplyConsciousnessModDefaults(s);
            AssertTrue(!s.consciousnessFurtherV2.LowEnabled &&
                !s.consciousnessFurtherV2.HighEnabled,
                "Further V2 Cons ModDefaults preserve Low/High switches");
            AssertNear(s.consciousnessFurtherV2.HighFactor, 1.20f, 0.0001f,
                "Further V2 Cons ModDefaults reset high factor");
            AssertNear(s.consciousnessFurtherV2.LowAnchor, 0.80f, 0.0001f,
                "Further V2 Cons ModDefaults reset low anchor");
            AssertNear(s.consciousnessFurtherV2.LowFactor, 0.80f, 0.0001f,
                "Further V2 Cons ModDefaults reset low factor");
            AssertNear(s.consciousnessFurtherV2.HighDiminishingLimit, 2f, 0.0001f,
                "Further V2 Cons ModDefaults retain limit x2");
            AssertTrue(s.consciousnessFurtherV2.LowCurve == (int)ConsAnchorCurveMode.Linear &&
                s.consciousnessFurtherV2.HighCurve == (int)ConsAnchorCurveMode.Linear,
                "Further V2 Cons ModDefaults restore curve modes");
            AssertNear(s.consciousnessAnchorSpeed.HighFactor, 1.08f, 0.0001f,
                "Further V2 Cons ModDefaults reset Speed separately");
            SettingsPresetService.ApplyConsciousnessVanillaPreset(s);
            AssertTrue(!s.consciousnessAffectsLearning,
                "Further V2 Cons Vanilla master OFF");
            AssertNear(s.consciousnessFurtherV2.LowFactor, 0.80f, 0.0001f,
                "Further V2 Vanilla retains editable defaults");
            TestConsciousnessFurtherPreviewR2();
            TestConsciousnessFurtherPreviewCP1();
            Log.Message("[XP Customizer][FURTHER-ORDER-R5F1] status=PASS; " +
                "formula=(base+MTH_pp/100)*cons; exampleBase=0.20; exampleMthPp=+6; " +
                "exampleCons=1.235; exampleResult=0.3211; " +
                "gameplayFormula=CHANGED_R5F1; settingsWrites=0; build=" + XpCustomizerMod.BuildId);
            Log.Message("[XP Customizer][FURTHER-TOOLTIP-R5F2] status=PASS; " +
                "order=Base>Willpower>Cons; subtotal=REMOVED; multiplier=xROUND_PERCENT; " +
                "exampleResult=32%; exampleCons=x124%; gameplayFormula=UNCHANGED_R5F1; " +
                "build=" + XpCustomizerMod.BuildId);
            Log.Message("[XP Customizer][CONS-FURTHER-V2] pure tests PASS; " +
                "low80=x0.80; high135=x1.35; 100%=x1; limit=2; " +
                "low/high switches; formula=(base+MTH-pp)*cons-R5F1; tooltip-additive-first-and-x-percent; " +
                "legacy-pp-inert; presets; isolation; invalid-input; " +
                "livePawnXP/ScribeDisk/IMGUI=NOT_TESTED");
        }

        // CP1: verify all 12 shared High levels and the Post-cap arithmetic
        // across three curves and three preview bases. This is detached only.
        private static void TestConsciousnessFurtherPreviewCP1()
        {
            var settings = new XpCustomizerSettings();
            var p = settings.consciousnessFurtherV2;
            int[] curves = { (int)ConsAnchorCurveMode.Diminishing,
                (int)ConsAnchorCurveMode.Linear,
                (int)ConsAnchorCurveMode.Accelerating };
            float[] bases = { 17f, 20f, 25f };
            int checks = 0;
            AssertTrue(ConsciousnessPreviewGridLayout.HighSampleCount == 12,
                "CP1 Further 12 samples shared with Speed and Capacity");
            checks++;
            foreach (int curve in curves)
            {
                p.HighCurve = curve;
                for (int i = 0; i < ConsciousnessPreviewGridLayout.HighSampleCount; i++)
                {
                    float level = ConsciousnessPreviewGridLayout.HighSampleAt(i);
                    float factor = ConsciousnessFurtherLearningService.CalculateMultiplier(
                        level, settings);
                    AssertTrue(!float.IsNaN(factor) && !float.IsInfinity(factor) &&
                        factor >= 1f, "CP1 Further valid factor " + curve + "/" + i);
                    checks++;
                    foreach (float basePercent in bases)
                    {
                        float displayed = ConsciousnessFurtherLearningSettingsUI
                            .CalculatePreviewEfficiencyPercent(basePercent, factor);
                        float expected = FurtherLearningService.CalculateRawFactor(
                            basePercent / 100f, factor, 0f) * 100f;
                        AssertNear(displayed, expected, 0.00025f,
                            "CP1 Further Post-cap parity " + curve + "/" + i +
                            " base=" + basePercent);
                        checks++;
                    }
                }
            }
            // The prior High preview included the selected anchor in its four
            // samples. CP1 shows that anchor on its own read-only line instead.
            p.HighAnchor = 2.10f; // Not one of the 12 fixed samples.
            p.HighFactor = 1.35f;
            foreach (int curve in curves)
            {
                p.HighCurve = curve;
                AssertNear(ConsciousnessFurtherLearningService.CalculateMultiplier(
                    p.HighAnchor, settings), p.HighFactor, 0.00025f,
                    "CP1 selected High anchor exact " + curve);
                checks++;
            }
            AssertNear(ConsciousnessFurtherLearningSettingsUI.CalculatePreviewEfficiencyPercent(
                20f, p.HighFactor), 27f, 0.00025f,
                "CP1 selected High anchor Post-cap retained at 20 percent base");
            checks++;
            AssertNear(settings.furtherLearningBaseFactor, 0.20f, 0.0001f,
                "CP1 Further preview cannot mutate real base");
            checks++;
            Log.Message("[XPC-CP1][CONS_FURTHER_PREVIEW_AUDIT] status=PASS; checks=" + checks +
                "; highSamples=12; lowSamples=4_UNCHANGED; curves=3; bases=17/20/25" +
                "; postCap=FORMULA_PARITY; selectedAnchor=EXACT_NON_SAMPLE" +
                "; previewOnly=True; actualIMGUI=NOT_RUN; profileWrites=0; xmlWrites=0" +
                "; build=" + XpCustomizerMod.BuildId);
        }

        // The R2 preview has no save keys and no live gameplay inputs.
        // Compare against the SAME existing raw-factor arithmetic (MTH 0pp).
        private static void TestConsciousnessFurtherPreviewR2()
        {
            var settings = new XpCustomizerSettings();
            float originalBase = settings.furtherLearningBaseFactor;
            float originalHigh = settings.consciousnessFurtherV2.HighFactor;

            AssertNear(ConsciousnessFurtherLearningSettingsUI.GetCurrentPreviewBasePercent(settings),
                20f, 0.0001f, "Preview R2 fresh current 20 percent");
            settings.furtherLearningBaseFactor = 0.17f;
            AssertNear(ConsciousnessFurtherLearningSettingsUI.GetCurrentPreviewBasePercent(settings),
                17f, 0.0001f, "Preview R2 current updates to 17 percent");
            settings.furtherLearningBaseFactor = 0.25f;
            AssertNear(ConsciousnessFurtherLearningSettingsUI.GetCurrentPreviewBasePercent(settings),
                25f, 0.0001f, "Preview R2 current updates to 25 percent");
            settings.furtherLearningBaseFactor = float.NaN;
            AssertNear(ConsciousnessFurtherLearningSettingsUI.GetCurrentPreviewBasePercent(settings),
                20f, 0.0001f, "Preview R2 corrupt base falls back to vanilla 20 percent");
            settings.furtherLearningBaseFactor = 0.25f;

            float low = ConsciousnessFurtherLearningService.CalculateMultiplier(0.80f, settings);
            float high = ConsciousnessFurtherLearningService.CalculateMultiplier(1.35f, settings);
            float c170 = ConsciousnessFurtherLearningService.CalculateMultiplier(1.70f, settings);
            float[] examples = { 17f, 20f, 25f };
            foreach (float b in examples)
            {
                AssertNear(ConsciousnessFurtherLearningSettingsUI.CalculatePreviewEfficiencyPercent(b, low),
                    FurtherLearningService.CalculateRawFactor(b / 100f, low, 0f) * 100f,
                    0.0001f, "Preview R2 low formula parity at base " + b);
                AssertNear(ConsciousnessFurtherLearningSettingsUI.CalculatePreviewEfficiencyPercent(b, high),
                    FurtherLearningService.CalculateRawFactor(b / 100f, high, 0f) * 100f,
                    0.0001f, "Preview R2 high formula parity at base " + b);
                AssertNear(ConsciousnessFurtherLearningSettingsUI.CalculatePreviewEfficiencyPercent(b, c170),
                    FurtherLearningService.CalculateRawFactor(b / 100f, c170, 0f) * 100f,
                    0.0001f, "Preview R2 C170 formula parity at base " + b);
            }
            AssertNear(c170, 1.70f, 0.001f, "Preview R2 C170 Linear default multiplier");
            AssertNear(ConsciousnessFurtherLearningSettingsUI.CalculatePreviewEfficiencyPercent(17f, low),
                13.6f, 0.001f, "Preview R2 17 percent C80 = 13.6 percent");
            AssertNear(ConsciousnessFurtherLearningSettingsUI.CalculatePreviewEfficiencyPercent(20f, low),
                16f, 0.001f, "Preview R2 20 percent C80 = 16 percent");
            AssertNear(ConsciousnessFurtherLearningSettingsUI.CalculatePreviewEfficiencyPercent(25f, low),
                20f, 0.001f, "Preview R2 25 percent C80 = 20 percent");
            AssertNear(ConsciousnessFurtherLearningSettingsUI.CalculatePreviewEfficiencyPercent(17f, high),
                22.95f, 0.001f, "Preview R2 17 percent C135 = 22.95 percent");
            AssertNear(ConsciousnessFurtherLearningSettingsUI.CalculatePreviewEfficiencyPercent(20f, high),
                27f, 0.001f, "Preview R2 20 percent C135 = 27 percent");
            AssertNear(ConsciousnessFurtherLearningSettingsUI.CalculatePreviewEfficiencyPercent(25f, high),
                33.75f, 0.001f, "Preview R2 25 percent C135 = 33.75 percent");

            float percent;
            AssertTrue(ConsciousnessFurtherLearningSettingsUI.TryParsePreviewPercent("17.5", out percent) &&
                Math.Abs(percent - 17.5f) < 0.0001f, "Preview R2 fractional percent parse");
            AssertTrue(!ConsciousnessFurtherLearningSettingsUI.TryParsePreviewPercent("-", out percent) &&
                !ConsciousnessFurtherLearningSettingsUI.TryParsePreviewPercent("", out percent) &&
                !ConsciousnessFurtherLearningSettingsUI.TryParsePreviewPercent("150", out percent) &&
                !ConsciousnessFurtherLearningSettingsUI.TryParsePreviewPercent("NaN", out percent),
                "Preview R2 partial and invalid entries do not calculate");
            AssertNear(ConsciousnessFurtherLearningSettingsUI.ResolvePreviewPercentCommit("150", 17f),
                100f, 0.001f, "Preview R2 clamp upper on commit");
            AssertNear(ConsciousnessFurtherLearningSettingsUI.ResolvePreviewPercentCommit("-5", 17f),
                0f, 0.001f, "Preview R2 clamp lower on commit");
            AssertNear(ConsciousnessFurtherLearningSettingsUI.ResolvePreviewPercentCommit("", 17f),
                17f, 0.001f, "Preview R2 restore prior on empty commit");
            AssertNear(ConsciousnessFurtherLearningSettingsUI.ResolvePreviewPercentCommit("nope", 25f),
                25f, 0.001f, "Preview R2 restore prior on invalid commit");
            AssertNear(ConsciousnessFurtherLearningSettingsUI.CalculatePreviewEfficiencyPercent(100f, 2f),
                200f, 0.001f, "Preview R2 no artificial 100 percent clamp");
            AssertNear(ConsciousnessFurtherLearningSettingsUI.CalculatePreviewEfficiencyPercent(0f, high),
                0f, 0.001f, "Preview R2 zero base remains zero");

            AssertNear(settings.furtherLearningBaseFactor, 0.25f, 0.0001f,
                "Preview R2 computation does not mutate real base");
            AssertNear(settings.consciousnessFurtherV2.HighFactor, originalHigh, 0.0001f,
                "Preview R2 computation does not mutate curve");
            settings.furtherLearningBaseFactor = originalBase;
            Log.Message("[XP Customizer][CONS-FURTHER-PREVIEW-R2] pure tests PASS; " +
                "bases=17,20,25; low/high/current-sync; no-MTH; entry-commit; " +
                "settingsUnchanged=True; liveIMGUIAndDisk=NOT_TESTED");
        }
    }
}
#endif
