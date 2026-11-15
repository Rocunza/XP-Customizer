#if DEBUG
using System;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.Gws;
using Verse;

namespace XpCustomizer
{
    internal static class LinearCurveDefaultsL1SelfTests
    {
        private static int checks;
        private static void Check(bool value, string description)
        {
            checks++;
            if (!value) throw new InvalidOperationException("[XPC-L1] " + description);
        }

        private static void Near(float actual, float expected, string description)
        {
            Check(!float.IsNaN(actual) && Math.Abs(actual - expected) < 0.0002f,
                description + " actual=" + actual + " expected=" + expected);
        }

        internal static void Run()
        {
            checks = 0;
            var fresh = new XpCustomizerSettings();
            Check(fresh.gwsLowCurve == (int)GwsCurveMode.Linear, "fresh GWS Low Linear");
            Check(fresh.gwsHighCurve == (int)GwsCurveMode.Diminishing, "fresh GWS High Diminishing");
            Check(fresh.consciousnessAnchorSpeed.LowCurve == (int)ConsAnchorCurveMode.Linear,
                "fresh Cons Speed Low Linear");
            Check(fresh.consciousnessAnchorSpeed.HighCurve == (int)ConsAnchorCurveMode.Linear,
                "fresh Cons Speed High Linear");
            Check(fresh.consciousnessAnchorCapacity.LowCurve == (int)ConsAnchorCurveMode.Linear,
                "fresh Cons Capacity Low Linear");
            Check(fresh.consciousnessAnchorCapacity.HighCurve == (int)ConsAnchorCurveMode.Linear,
                "fresh Cons Capacity High Linear");
            Check(fresh.consciousnessFurtherV2.LowCurve == (int)ConsAnchorCurveMode.Linear,
                "fresh Cons Further Low Linear");
            Check(fresh.consciousnessFurtherV2.HighCurve == (int)ConsAnchorCurveMode.Linear,
                "fresh Cons Further High Linear");
            Check(!fresh.gwsEnabled && !fresh.consciousnessAffectsLearning,
                "fresh activation switches unchanged");

            var nullSpeed = ConsciousnessAnchorV2SettingsStore.GetSpeedProfile(null);
            var nullCapacity = ConsciousnessAnchorV2SettingsStore.GetCapacityProfile(null);
            var nullFurther = ConsciousnessFurtherV2SettingsStore.GetProfile(null);
            Check(nullSpeed.LowMode == ConsAnchorCurveMode.Linear &&
                nullSpeed.HighMode == ConsAnchorCurveMode.Linear, "null Speed fallback Linear");
            Check(nullCapacity.LowMode == ConsAnchorCurveMode.Linear &&
                nullCapacity.HighMode == ConsAnchorCurveMode.Linear, "null Capacity fallback Linear");
            Check(nullFurther.LowMode == ConsAnchorCurveMode.Linear &&
                nullFurther.HighMode == ConsAnchorCurveMode.Linear, "null Further fallback Linear");

            // IMPORTANT: old XML omitted Diminishing=0. Never reinterpret an old missing key.
            Check(ConsAnchorV2ProfileSettings.LegacyXmlHighCurveFallback ==
                (int)ConsAnchorCurveMode.Diminishing, "old Speed/Capacity XML fallback retained");
            Check(ConsciousnessFurtherV2Settings.LegacyXmlHighCurveFallback ==
                (int)ConsAnchorCurveMode.Diminishing, "old Further XML fallback retained");

            // D01: constructor defaults must change while omitted XML keys
            // keep their historical scalar fallbacks. These are detached
            // semantic assertions, not a live Scribe load/restart proof.
            var xmlDefaults = OwnedXmlSemanticContract.Defaults();
            var detachedDefaults = DetachedXmlSemanticContract.Defaults();
            Check(fresh.gwsHighAnchor == 5f && fresh.gwsHighFactor == 1.30f &&
                fresh.gwsHighCurvature == 0.40f &&
                fresh.gwsHighDiminishingLimit == 1.50f &&
                fresh.gwsHighTechnicalLimit == 2f,
                "D01 fresh GWS numeric defaults");
            Check(fresh.consciousnessAnchorSpeed.HighAnchor == 1.20f &&
                fresh.consciousnessAnchorSpeed.HighFactor == 1.08f &&
                fresh.consciousnessAnchorCapacity.HighAnchor == 1.20f &&
                fresh.consciousnessAnchorCapacity.HighFactor == 1.08f &&
                fresh.consciousnessFurtherV2.HighAnchor == 1.20f &&
                fresh.consciousnessFurtherV2.HighFactor == 1.20f,
                "D01 fresh Cons numeric defaults");
            foreach (var defaults in new[] { xmlDefaults, detachedDefaults })
            {
                Check(defaults["gwsHighAnchor"].Equals(1.80f) &&
                    defaults["gwsHighFactor"].Equals(1.20f) &&
                    defaults["gwsHighCurve"].Equals((int)GwsCurveMode.Linear) &&
                    defaults["gwsHighCurvature"].Equals(1f) &&
                    defaults["gwsHighDiminishingLimit"].Equals(1.30f),
                    "D01 historical GWS XML default inventory");
                Check(defaults["consAnchorV2SpeedHighAnchor"].Equals(1.35f) &&
                    defaults["consAnchorV2CapacityHighAnchor"].Equals(1.35f) &&
                    defaults["consAnchorV2SpeedHighFactor"].Equals(1.10f) &&
                    defaults["consAnchorV2CapacityHighFactor"].Equals(1.10f) &&
                    defaults["consFurtherV2HighAnchor"].Equals(1.35f) &&
                    defaults["consFurtherV2HighFactor"].Equals(1.35f),
                    "D01 historical Cons XML default inventory");
            }

            // Existing explicit Diminishing/Accelerating users must not be migrated.
            var existing = new XpCustomizerSettings();
            existing.gwsEnabled = true;
            existing.consciousnessAffectsLearning = true;
            existing.consciousnessAffectsLearningSpeed = true;
            existing.consciousnessAffectsDailyCapacity = true;
            existing.consciousnessAffectsFurtherLearning = true;
            existing.gwsLowCurve = (int)GwsCurveMode.Accelerating;
            existing.gwsHighCurve = (int)GwsCurveMode.Diminishing;
            existing.consciousnessAnchorSpeed.HighCurve = (int)ConsAnchorCurveMode.Diminishing;
            existing.consciousnessAnchorCapacity.HighCurve = (int)ConsAnchorCurveMode.Accelerating;
            existing.consciousnessFurtherV2.HighCurve = (int)ConsAnchorCurveMode.Diminishing;
            GwsLearningService.NormalizeSettings(existing);
            ConsciousnessAnchorV2SettingsStore.NormalizeAfterLoad(existing);
            ConsciousnessFurtherV2SettingsStore.NormalizeAfterLoad(existing);
            Check(existing.gwsLowCurve == (int)GwsCurveMode.Accelerating &&
                existing.gwsHighCurve == (int)GwsCurveMode.Diminishing,
                "old GWS explicit modes preserved");
            Check(existing.consciousnessAnchorSpeed.HighCurve == (int)ConsAnchorCurveMode.Diminishing &&
                existing.consciousnessAnchorCapacity.HighCurve == (int)ConsAnchorCurveMode.Accelerating &&
                existing.consciousnessFurtherV2.HighCurve == (int)ConsAnchorCurveMode.Diminishing,
                "old Cons explicit modes preserved");

            var g = new XpCustomizerSettings();
            g.gwsEnabled = true;
            g.gwsLowEnabled = false;
            g.gwsHighCurve = (int)GwsCurveMode.Diminishing;
            SettingsPresetService.ApplyGwsModDefaults(g);
            Check(g.gwsLowCurve == (int)GwsCurveMode.Linear &&
                g.gwsHighCurve == (int)GwsCurveMode.Diminishing,
                "GWS section Mod Defaults Low Linear / High Diminishing");
            Check(g.gwsEnabled && !g.gwsLowEnabled, "GWS section switches preserved");
            Near(GwsLearningService.Calculate(1f, g), 1f, "GWS neutral 100%");
            Near(GwsLearningService.Calculate(5f, g), 1.30f, "GWS Diminishing 500% anchor");
            Check(GwsLearningService.Calculate(1000f, g) <= 1.50f,
                "GWS Diminishing asymptote x1.50");
            Check(g.gwsHighCurvature == 0.40f && g.gwsHighDiminishingLimit == 1.50f &&
                g.gwsHighTechnicalLimit == 2f, "GWS fresh mode parameters and stored safety");

            var c = new XpCustomizerSettings();
            c.consciousnessAffectsLearning = true;
            c.consciousnessAffectsLearningSpeed = true;
            c.consciousnessAffectsDailyCapacity = true;
            c.consciousnessAffectsFurtherLearning = true;
            c.consciousnessFurtherV2.LowEnabled = false;
            c.consciousnessAnchorSpeed.HighCurve = (int)ConsAnchorCurveMode.Diminishing;
            c.consciousnessAnchorCapacity.HighCurve = (int)ConsAnchorCurveMode.Accelerating;
            c.consciousnessFurtherV2.HighCurve = (int)ConsAnchorCurveMode.Diminishing;
            SettingsPresetService.ApplyConsciousnessModDefaults(c);
            Check(c.consciousnessAnchorSpeed.LowCurve == (int)ConsAnchorCurveMode.Linear &&
                c.consciousnessAnchorSpeed.HighCurve == (int)ConsAnchorCurveMode.Linear &&
                c.consciousnessAnchorCapacity.LowCurve == (int)ConsAnchorCurveMode.Linear &&
                c.consciousnessAnchorCapacity.HighCurve == (int)ConsAnchorCurveMode.Linear &&
                c.consciousnessFurtherV2.LowCurve == (int)ConsAnchorCurveMode.Linear &&
                c.consciousnessFurtherV2.HighCurve == (int)ConsAnchorCurveMode.Linear,
                "Cons section Mod Defaults all six Linear");
            Check(c.consciousnessAffectsLearning && c.consciousnessAffectsLearningSpeed &&
                c.consciousnessAffectsDailyCapacity && c.consciousnessAffectsFurtherLearning &&
                !c.consciousnessFurtherV2.LowEnabled,
                "Cons section top-level switches and Further Low preserved");
            Near(ConsciousnessAnchorCurveService.Calculate(1.70f,
                    c.consciousnessAnchorSpeed.ToProfile()), 1.28f,
                "Cons Speed default C170 Linear x1.28");
            Near(ConsciousnessAnchorCurveService.Calculate(1.70f,
                    c.consciousnessAnchorCapacity.ToProfile()), 1.28f,
                "Cons Capacity default C170 Linear x1.28");
            Near(ConsciousnessFurtherLearningService.CalculateMultiplier(1.70f, c), 1.70f,
                "Cons Further default C170 Linear x1.70");

            Log.Message("[XPC-L1][CURVE_DEFAULTS_SELFTEST] status=PASS; checks=" + checks +
                "; defaultModes=GWS_LOW_LINEAR_HIGH_DIMINISHING+CONS6_LINEAR; oldMissingXmlFallback=DIMINISHING_PRESERVED" +
                "; oldExplicitModes=PRESERVED; sectionModDefaults=PASS; globalR3AModes=PRESERVED" +
                "; d01FreshDefaults=PASS; historicalScribeInventory=PASS" +
                "; noWrites=TRUE; actualScribeRoundTrip=MANUAL; liveIMGUI=MANUAL; build=" +
                XpCustomizerMod.BuildId);
        }
    }
}
#endif
