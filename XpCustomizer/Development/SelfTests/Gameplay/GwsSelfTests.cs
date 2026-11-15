#if DEBUG
using System;
using RimWorld;
using UnityEngine;
using XpCustomizer.Gws;
using Verse;

namespace XpCustomizer
{
    internal static class GwsSelfTests
    {
        private static void AssertNear(float actual, float expected, string reason)
        {
            if (float.IsNaN(actual) || Math.Abs(actual - expected) > 0.012f)
                throw new InvalidOperationException("[XP Customizer] GWS-V2 test: " + reason +
                    " expected=" + expected + " actual=" + actual);
        }
        private static void Check(bool success, string reason)
        {
            if (!success) throw new InvalidOperationException("[XP Customizer] GWS-V2 test: " + reason);
        }
        // Independent snapshot of GWS V1's fixed 30%/180% math. This is
        // intentionally separate from the V2 dispatcher so identical defaults
        // cannot silently change the user's existing balance.
        private static float CalculateV1Baseline(float gws, XpCustomizerSettings s)
        {
            if (s == null || !s.gwsEnabled || float.IsNaN(gws) || float.IsInfinity(gws)) return 1f;
            if (gws < 1f)
            {
                if (!s.gwsLowEnabled) return 1f;
                float t = Mathf.Clamp01((1f - Mathf.Max(gws, 0.30f)) / 0.70f);
                float curve = GwsLearningService.NormalizeNumber(s.gwsLowCurvature, 1f, 0f, 2f);
                GwsCurveMode mode = GwsLearningService.NormalizeMode(s.gwsLowCurve);
                float share = mode == GwsCurveMode.Diminishing
                    ? 1f - Mathf.Pow(1f - t, 1f + curve)
                    : mode == GwsCurveMode.Accelerating ? Mathf.Pow(t, 1f + curve) : t;
                float factor = GwsLearningService.NormalizeNumber(s.gwsLowFactor, 0.70f, 0f, 1f);
                return Mathf.Clamp(1f - (1f - factor) * share, 0f, 1f);
            }
            if (gws <= 1f || !s.gwsHighEnabled) return 1f;
            float high = GwsLearningService.NormalizeNumber(s.gwsHighFactor, 1.15f, 1f, 5f);
            float tHigh = Mathf.Max(0f, (gws - 1f) / 0.80f);
            float strength = GwsLearningService.NormalizeNumber(s.gwsHighCurvature, 1f, 0f, 2f);
            GwsCurveMode modeHigh = GwsLearningService.NormalizeMode(s.gwsHighCurve);
            if (modeHigh == GwsCurveMode.Diminishing)
            {
                float limit = GwsLearningService.NormalizeNumber(s.gwsHighDiminishingLimit,
                    1.30f, high + 0.01f, 20f);
                float proportion = Mathf.Clamp((high - 1f) / (limit - 1f), 0f, 0.9999f);
                float k = -(float)Math.Log(1f - proportion);
                float exponent = Mathf.Clamp(1f - 0.30f * strength, 0.40f, 1f);
                float saturation = 1f - Mathf.Exp(-k * Mathf.Pow(tHigh, exponent));
                return Mathf.Clamp(1f + (limit - 1f) * saturation, 1f, limit);
            }
            float max = GwsLearningService.NormalizeNumber(s.gwsHighTechnicalLimit, 2f, high, 20f);
            float power = modeHigh == GwsCurveMode.Accelerating ? 1f + strength : 1f;
            float scaled = 1f + (high - 1f) * Mathf.Pow(tHigh, power);
            return float.IsNaN(scaled) || float.IsInfinity(scaled)
                ? max : Mathf.Clamp(scaled, 1f, max);
        }

        private static int VerifyDefaultAnchorParity(XpCustomizerSettings s)
        {
            float[] points = { -2f, 0f, 0.1f, 0.30f, 0.50f, 0.65f, 0.80f,
                0.99f, 1f, 1.01f, 1.20f, 1.50f, 1.80f, 2.50f, 3f, 5f, 20f };
            float[] curvatures = { 0f, 0.5f, 1f, 1.5f, 2f };
            int checks = 0;
            s.gwsLowAnchor = 0.30f;
            s.gwsHighAnchor = 1.80f;
            s.gwsLowFactor = 0.70f;
            s.gwsHighFactor = 1.15f;
            s.gwsHighDiminishingLimit = 1.30f;
            s.gwsHighTechnicalLimit = 2f;
            s.gwsLowEnabled = true;
            s.gwsHighEnabled = true;
            s.gwsEnabled = true;
            foreach (GwsCurveMode low in new[] { GwsCurveMode.Diminishing, GwsCurveMode.Linear, GwsCurveMode.Accelerating })
            foreach (GwsCurveMode high in new[] { GwsCurveMode.Diminishing, GwsCurveMode.Linear, GwsCurveMode.Accelerating })
            foreach (float curvature in curvatures)
            {
                s.gwsLowCurve = (int)low;
                s.gwsHighCurve = (int)high;
                s.gwsLowCurvature = curvature;
                s.gwsHighCurvature = curvature;
                foreach (float point in points)
                {
                    float previous = CalculateV1Baseline(point, s);
                    float current = GwsLearningService.Calculate(point, s);
                    if (float.IsNaN(current) || Math.Abs(previous - current) > 0.00002f)
                        throw new InvalidOperationException("[XP Customizer] GWS-V2 V1 parity: " +
                            "low=" + low + "; high=" + high + "; curvature=" + curvature +
                            "; gws=" + point + "; old=" + previous + "; new=" + current);
                    checks++;
                }
            }
            return checks;
        }

        private static int VerifyEditableAnchors()
        {
            var s = new XpCustomizerSettings();
            s.gwsEnabled = true;
            s.gwsLowAnchor = 0.55f;
            s.gwsHighAnchor = 2.50f;
            foreach (GwsCurveMode mode in new[] { GwsCurveMode.Diminishing, GwsCurveMode.Linear, GwsCurveMode.Accelerating })
            {
                s.gwsLowCurve = (int)mode;
                s.gwsHighCurve = (int)mode;
                AssertNear(GwsLearningService.Calculate(0.55f, s), 0.70f, "editable low anchor " + mode);
                AssertNear(GwsLearningService.Calculate(2.50f, s), 1.30f, "editable high anchor " + mode);
                AssertNear(GwsLearningService.Calculate(1f, s), 1f, "editable neutral " + mode);
                AssertNear(GwsLearningService.Calculate(0.20f, s), 0.70f, "low floor " + mode);
            }
            s.gwsLowAnchor = float.NaN;
            s.gwsHighAnchor = float.PositiveInfinity;
            GwsLearningService.NormalizeSettings(s);
            AssertNear(s.gwsLowAnchor, 0.30f, "NaN low anchor default");
            AssertNear(s.gwsHighAnchor, 5.00f, "infinite high anchor default");
            s.gwsLowAnchor = 0f;
            s.gwsHighAnchor = 100f;
            GwsLearningService.NormalizeSettings(s);
            AssertNear(s.gwsLowAnchor, 0.30f, "low anchor clamped minimum");
            AssertNear(s.gwsHighAnchor, 10f, "high anchor clamped maximum 1000%");
            s.gwsLowAnchor = 0.95f;
            s.gwsHighAnchor = 1.10f;
            AssertNear(GwsLearningService.Calculate(1f, s), 1f, "near-neutral anchors 100% neutral");
            float lowValue = GwsLearningService.Calculate(0.95f, s);
            float highValue = GwsLearningService.Calculate(1.10f, s);
            Check(!float.IsNaN(lowValue) && !float.IsInfinity(lowValue) &&
                  !float.IsNaN(highValue) && !float.IsInfinity(highValue), "near-neutral anchors finite");
            float converted;
            Check(UI.GwsSettingsUI.TryParseNumericValue("250", out converted) &&
                Math.Abs(converted - 250f) < 0.001f, "staged raw 250 parse");
            Check(UI.GwsSettingsUI.TryParseNumericValue("2", out converted), "intermediate raw 2 stays text");
            Check(!UI.GwsSettingsUI.TryParseNumericValue("", out converted), "empty edit invalid");
            Check(!UI.GwsSettingsUI.TryParseNumericValue("NaN", out converted), "NaN edit rejected");
            AssertNear(UI.GwsSettingsUI.ResolveNumericCommit("250", 180f, 110f, 1000f),
                250f, "editable high anchor commit");
            AssertNear(UI.GwsSettingsUI.ResolveNumericCommit("1000", 180f, 110f, 1000f),
                1000f, "high anchor maximum accepted on commit");
            AssertNear(UI.GwsSettingsUI.ResolveNumericCommit("", 180f, 110f, 1000f),
                180f, "invalid anchor restores last value");
            AssertNear(UI.GwsSettingsUI.ResolveNumericCommit("500", 180f, 110f, 1000f),
                500f, "existing 500% accepted after maximum expansion");
            AssertNear(UI.GwsSettingsUI.ResolveNumericCommit("750", 180f, 110f, 1000f),
                750f, "new 750% accepted on commit");
            AssertNear(UI.GwsSettingsUI.ResolveNumericCommit("1200", 180f, 110f, 1000f),
                1000f, "above 1000% bounded only on commit");
            return 12 + 4 + 2 + 4 + 3 + 3; // assertion count (28)
        }

        // GHA1-R1: expanded high GWS anchor must behave identically for all curve
        // modes at its configured point and must not affect disabled or neutral GWS.
        // Uses detached settings only; no game state, profiles or Scribe writes.
        private static int VerifyExpandedHighAnchor()
        {
            int checks = 0;
            Check(GwsLearningService.HighAnchorMaximum == 10f,
                "GHA1 maximum is 1000% GWS");
            checks++;
            foreach (float anchor in new[] { 5f, 7.5f, 10f })
            {
                var s = new XpCustomizerSettings();
                s.gwsEnabled = true;
                s.gwsHighEnabled = true;
                s.gwsHighAnchor = anchor;
                GwsLearningService.NormalizeSettings(s);
                AssertNear(s.gwsHighAnchor, anchor, "GHA1 normalize anchor " + anchor);
                checks++;
                foreach (GwsCurveMode mode in new[] {
                    GwsCurveMode.Diminishing, GwsCurveMode.Linear, GwsCurveMode.Accelerating })
                {
                    s.gwsHighCurve = (int)mode;
                    AssertNear(GwsLearningService.Calculate(anchor, s),
                        GwsLearningService.DefaultHighFactor,
                        "GHA1 factor at configured anchor " + anchor + " " + mode);
                    checks++;
                    AssertNear(GwsLearningService.Calculate(1f, s), 1f,
                        "GHA1 neutral retained " + anchor + " " + mode);
                    checks++;
                    float extreme = GwsLearningService.Calculate(20f, s);
                    Check(!float.IsNaN(extreme) && !float.IsInfinity(extreme) &&
                        extreme >= 1f && extreme <= GwsLearningService.TechnicalMaxFactor,
                        "GHA1 extreme GWS finite bounded " + anchor + " " + mode);
                    checks++;
                    if (anchor == 10f)
                    {
                        Check(GwsLearningService.Calculate(5f, s) <
                            GwsLearningService.DefaultHighFactor,
                            "GHA1 high bonus not reached early at 500% " + mode);
                        checks++;
                    }
                }
            }
            return checks; // 34 assertions
        }

        // GP1: verify the production preview sample lists and pure layout math.
        // No actual IMGUI, settings writes or pawn XP; real window checks remain manual.
        private static int VerifyGp1Preview()
        {
            int checks = 0;
            var flags = System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic;
            var highField = typeof(UI.GwsSettingsUI).GetField("HighPreviewGws", flags);
            var lowField = typeof(UI.GwsSettingsUI).GetField("LowPreviewGws", flags);
            Check(highField != null && lowField != null, "GP1 preview fields retained");
            checks++;
            var high = (float[])highField.GetValue(null);
            var low = (float[])lowField.GetValue(null);
            float[] expectedHigh = { 1.20f, 1.35f, 1.40f, 1.55f, 1.60f, 1.75f,
                1.80f, 2.00f, 3.00f, 5.00f, 7.50f, 10.00f };
            float[] expectedLow = { 0.30f, 0.50f, 0.65f, 0.80f };
            Check(high.Length == 12 && low.Length == 4,
                "GP1 High is 12 samples; Low remains 4 samples");
            checks++;
            for (int i = 0; i < high.Length; i++)
            {
                Check(Math.Abs(high[i] - expectedHigh[i]) < 0.000001f,
                    "GP1 High sample and ordering " + i);
                checks++;
            }
            for (int i = 0; i < low.Length; i++)
            {
                Check(Math.Abs(low[i] - expectedLow[i]) < 0.000001f,
                    "GP1 Low sample unchanged " + i);
                checks++;
            }

            // 6 columns on normal width; 4/3/2 when the settings panel narrows.
            foreach (float width in new[] { 1200f, 840f, 839f, 560f, 559f, 420f, 419f, 280f })
            {
                int expectedColumns = width >= 840f ? 6 :
                    width >= 560f ? 4 : width >= 420f ? 3 : 2;
                int columns = UI.GwsSettingsUI.ResolveHighPreviewColumns(width);
                Check(columns == expectedColumns, "GP1 column fallback " + width);
                checks++;
                float height = UI.GwsSettingsUI.PreviewGridHeight(high.Length, columns);
                int rows = (high.Length + columns - 1) / columns;
                Check(Math.Abs(height - (rows * 28f + (rows - 1) * 5f)) < 0.00001f,
                    "GP1 full row height reserved " + width);
                checks++;
                Rect grid = new Rect(10f, 20f, width, height);
                for (int i = 0; i < high.Length; i++)
                {
                    Rect cell = UI.GwsSettingsUI.PreviewCellRect(grid, i, columns);
                    Check(cell.x >= grid.x - 0.001f &&
                        cell.xMax <= grid.xMax + 0.001f &&
                        cell.y >= grid.y - 0.001f &&
                        cell.yMax <= grid.yMax + 0.001f,
                        "GP1 cell stays inside reserved area " + width + ":" + i);
                    checks++;
                    Check(Math.Abs(cell.y - (grid.y + (i / columns) * 33f)) < 0.00001f &&
                        Math.Abs(cell.x - (grid.x + (i % columns) * (width / columns))) < 0.001f,
                        "GP1 cell row-major order " + width + ":" + i);
                    checks++;
                }
            }
            Check(Math.Abs(UI.GwsSettingsUI.PreviewGridHeight(low.Length, 4) - 28f) < 0.00001f,
                "GP1 Low preview remains one 28px row");
            checks++;
            Check(UI.GwsSettingsUI.ResolveHighPreviewColumns(float.NaN) == 2 &&
                UI.GwsSettingsUI.ResolveHighPreviewColumns(float.PositiveInfinity) == 2,
                "GP1 invalid width uses safe fallback");
            checks++;

            // Values in the preview are calculated by the unchanged live calculator.
            var s = new XpCustomizerSettings();
            s.gwsEnabled = true;
            s.gwsHighEnabled = true;
            s.gwsLowEnabled = false;
            s.gwsHighCurve = (int)GwsCurveMode.Linear;
            s.gwsHighAnchor = 10f;
            s.gwsHighFactor = 1.5f;
            s.gwsHighTechnicalLimit = 2f;
            foreach (float gws in high)
            {
                float expected = 1f + 0.5f * (gws - 1f) / 9f;
                float actual = GwsLearningService.Calculate(gws, s);
                Check(!float.IsNaN(actual) &&
                    Math.Abs(actual - expected) < 0.00005f,
                    "GP1 unchanged Linear XP preview at " + gws);
                checks++;
            }
            Check(s.gwsHighAnchor == 10f && s.gwsHighFactor == 1.5f,
                "GP1 preview is read-only");
            checks++;
            return checks;
        }

        private static int VerifyMedicineKeyMigration()
        {
            int count = 0;
            Check(GwsLearningService.IsKnownSkill("Medicine") &&
                !GwsLearningService.IsKnownSkill("Medical"), "canonical ID, no legacy runtime alias");
            count++;
            Check(GwsLearningService.DefaultSkillEnabled("Medicine") &&
                !GwsLearningService.DefaultSkillEnabled("Medical"), "Medicine defaults ON");
            count++;
            int skillCount = 0;
            int medicineCount = 0;
            foreach (string key in GwsLearningService.Skills)
            {
                skillCount++;
                if (key == "Medicine") medicineCount++;
                Check(key != "Medical", "wrong SkillDef ID remains in the live skill list");
            }
            Check(skillCount == 12 && medicineCount == 1, "12 unique-looking skills with Medicine");
            count++;
            // Test both canonical/legacy-key conflicts in every stored state.
            for (int oldBit = 0; oldBit < 2; oldBit++)
            for (int canonicalKind = 0; canonicalKind < 3; canonicalKind++)
            for (int reverseInsertion = 0; reverseInsertion < 2; reverseInsertion++)
            {
                bool oldValue = oldBit == 1;
                var map = new System.Collections.Generic.Dictionary<string, bool>(StringComparer.Ordinal);
                if (canonicalKind != 0 && reverseInsertion == 1)
                    map.Add("Medicine", canonicalKind == 2);
                map.Add("Medical", oldValue);
                if (canonicalKind != 0 && reverseInsertion == 0)
                    map.Add("Medicine", canonicalKind == 2);
                map.Add("ForeignSkill", false);
                bool canonicalWon;
                Check(GwsLearningService.MigrateLegacyMedicalKey(map, out canonicalWon),
                    "migration must see exact old key");
                Check(canonicalWon == (canonicalKind != 0), "canonical conflict flag");
                Check(!map.ContainsKey("Medical") && map.ContainsKey("Medicine") &&
                    map["Medicine"] == (canonicalKind == 0 ? oldValue : canonicalKind == 2),
                    "canonical wins independent of legacy value");
                Check(map.ContainsKey("ForeignSkill") && !map["ForeignSkill"],
                    "unrelated imported map keys untouched");
                bool againWon;
                Check(!GwsLearningService.MigrateLegacyMedicalKey(map, out againWon) &&
                    !againWon, "idempotent migration");
                count++;
            }
            var ignoreCase = new System.Collections.Generic.Dictionary<string, bool>(
                StringComparer.OrdinalIgnoreCase);
            ignoreCase.Add("Medical", false);
            bool flag;
            Check(GwsLearningService.MigrateLegacyMedicalKey(ignoreCase, out flag) &&
                object.ReferenceEquals(ignoreCase.Comparer, StringComparer.OrdinalIgnoreCase) &&
                !ignoreCase["Medicine"], "detached map comparer and old OFF preserved");
            count++;
            var foreignCase = new System.Collections.Generic.Dictionary<string, bool>(
                StringComparer.OrdinalIgnoreCase);
            foreignCase.Add("medical", false);
            Check(!GwsLearningService.MigrateLegacyMedicalKey(foreignCase, out flag) &&
                foreignCase.ContainsKey("medical") && !foreignCase.ContainsKey("Medicine"),
                "foreign mixed-case names not interpreted as legacy");
            count++;
            var oldSettings = new XpCustomizerSettings();
            oldSettings.gwsSkillEnabled["Medical"] = false;
            oldSettings.gwsSkillEnabled["ForeignSkill"] = false;
            oldSettings.gwsHighAnchor = 2.50f;
            GwsLearningService.NormalizeSettings(oldSettings);
            Check(oldSettings.gwsSkillEnabled.ContainsKey("Medicine") &&
                !oldSettings.gwsSkillEnabled["Medicine"] &&
                !oldSettings.gwsSkillEnabled.ContainsKey("Medical") &&
                !oldSettings.gwsSkillEnabled.ContainsKey("ForeignSkill"),
                "PostLoad sanitizer migrates before filtering legacy OFF");
            count++;
            Check(!GwsLearningService.IsSkillEnabled(oldSettings, "Medicine") &&
                !GwsLearningService.IsSkillEnabled(oldSettings, "Medical") &&
                oldSettings.gwsHighAnchor == 2.50f, "OFF meaning preserved and curve unchanged");
            count++;
            GwsLearningService.NormalizeSettings(oldSettings);
            Check(oldSettings.gwsSkillEnabled.ContainsKey("Medicine") &&
                !oldSettings.gwsSkillEnabled["Medicine"], "PostLoad normalization idempotent");
            count++;
            var conflictSettings = new XpCustomizerSettings();
            conflictSettings.gwsSkillEnabled.Add("Medical", false);
            conflictSettings.gwsSkillEnabled.Add("Medicine", true);
            GwsLearningService.NormalizeSettings(conflictSettings);
            Check(GwsLearningService.IsSkillEnabled(conflictSettings, "Medicine") &&
                conflictSettings.gwsSkillEnabled.Count == 0,
                "PostLoad canonical ON wins legacy OFF without redundant key");
            count++;
            GwsLearningService.SetSkillEnabled(oldSettings, "Medicine", true);
            Check(GwsLearningService.IsSkillEnabled(oldSettings, "Medicine") &&
                !oldSettings.gwsSkillEnabled.ContainsKey("Medicine"), "Medicine reset to implicit ON");
            count++;
            return count;
        }

        public static void Run()
        {
            var s = new XpCustomizerSettings();
            int v1ParityChecks = VerifyDefaultAnchorParity(new XpCustomizerSettings());
            int v2AnchorChecks = VerifyEditableAnchors();
            int expandedAnchorChecks = VerifyExpandedHighAnchor();
            int previewChecks = VerifyGp1Preview();
            int medicineChecks = VerifyMedicineKeyMigration();
            Check(!s.gwsEnabled, "new settings must be disabled");
            AssertNear(GwsLearningService.Calculate(0.3f, s), 1f, "master OFF");
            Check(!s.gwsApplyToReading, "reading default OFF");
            Check(s.gwsHighCurve == (int)GwsCurveMode.Diminishing, "high defaults Diminishing");
            AssertNear(s.gwsLowAnchor, 0.30f, "default low GWS position");
            AssertNear(s.gwsHighAnchor, 5.00f, "default high GWS position");
            AssertNear(s.gwsHighFactor, 1.30f, "high default anchor x1.30");
            AssertNear(s.gwsHighDiminishingLimit, 1.50f, "high diminishing limit x1.50");
            AssertNear(s.gwsHighTechnicalLimit, 2f, "high default safety x2");
            AssertNear(s.gwsHighCurvature, 0.40f, "high default curvature 40%");
            s.gwsEnabled = true;
            AssertNear(GwsLearningService.Calculate(1f, s), 1f, "D01 neutral 100%");
            AssertNear(GwsLearningService.Calculate(5f, s), 1.30f, "D01 high anchor 500%");
            Check(GwsLearningService.Calculate(1000f, s) <= 1.50f,
                "D01 diminishing cap x1.50");
            s.gwsEnabled = false;
            // Historical user-edited 180%/x1.15/100%/x1.30 fixture.
            // Keep all later V1-point assertions independent of D01 defaults.
            s.gwsHighAnchor = 1.80f;
            s.gwsHighFactor = 1.15f;
            s.gwsHighCurvature = 1f;
            s.gwsHighDiminishingLimit = 1.30f;
            Check(!GwsLearningService.IsSkillEnabled(s, "Shooting"), "shooting defaults OFF");
            Check(!GwsLearningService.IsSkillEnabled(s, "Melee"), "melee defaults OFF");
            Check(!GwsLearningService.IsSkillEnabled(s, "Social"), "social defaults OFF");
            int active = 0;
            foreach (string skill in GwsLearningService.Skills)
                if (GwsLearningService.IsSkillEnabled(s, skill)) active++;
            Check(active == 9, "default nine enabled skills");
            Check(!GwsLearningService.IsSkillEnabled(s, "ForeignSkill"), "foreign skill bypass");
            GwsLearningService.SetSkillEnabled(s, "Shooting", true);
            Check(GwsLearningService.IsSkillEnabled(s, "Shooting"), "custom enable");
            GwsLearningService.SetSkillEnabled(s, "Shooting", false);
            Check(!GwsLearningService.IsSkillEnabled(s, "Shooting"), "reset to default");
            GwsLearningService.SetSkillEnabled(s, "Social", true);
            Check(GwsLearningService.IsSkillEnabled(s, "Social"), "user Social ON override");
            GwsLearningService.NormalizeSettings(s);
            Check(GwsLearningService.IsSkillEnabled(s, "Social"), "explicit Social ON survives normalization");
            GwsLearningService.SetSkillEnabled(s, "Social", false);
            Check(!GwsLearningService.IsSkillEnabled(s, "Social") &&
                !s.gwsSkillEnabled.ContainsKey("Social"), "Social returns to implicit OFF default");
            GwsLearningService.SetSkillEnabled(s, "Crafting", false);
            GwsLearningService.NormalizeSettings(s);
            Check(!GwsLearningService.IsSkillEnabled(s, "Crafting"), "persist custom off");
            s.gwsEnabled = true;
            foreach (GwsCurveMode mode in new[] { GwsCurveMode.Diminishing, GwsCurveMode.Linear, GwsCurveMode.Accelerating })
            {
                s.gwsLowCurve = (int)mode;
                s.gwsHighCurve = (int)mode;
                AssertNear(GwsLearningService.Calculate(0.3f, s), 0.70f, "low anchor " + mode);
                AssertNear(GwsLearningService.Calculate(1f, s), 1f, "neutral " + mode);
                AssertNear(GwsLearningService.Calculate(1.8f, s), 1.15f, "high anchor " + mode);
            }
            s.gwsLowCurve = (int)GwsCurveMode.Linear;
            s.gwsHighCurve = (int)GwsCurveMode.Linear;
            AssertNear(GwsLearningService.Calculate(0.65f, s), 0.85f, "linear low midpoint");
            AssertNear(GwsLearningService.Calculate(1.4f, s), 1.075f, "linear high midpoint");
            AssertNear(GwsLearningService.Calculate(20f, s), 2f,
                "linear high safety cap x2");
            s.gwsHighCurve = (int)GwsCurveMode.Diminishing;
            AssertNear(GwsLearningService.Calculate(1.2f, s), 1.0693f, "default diminishing 120% preview");
            AssertNear(GwsLearningService.Calculate(1.5f, s), 1.1178f, "default diminishing 150% preview");
            AssertNear(GwsLearningService.Calculate(2.5f, s), 1.1977f, "default diminishing 250% preview");
            AssertNear(GwsLearningService.Calculate(3.0f, s), 1.2196f, "default diminishing 300% preview");
            Check(GwsLearningService.Calculate(50f, s) <= 1.30001f,
                "diminishing asymptote never exceeded");
            Check(GwsLearningService.Calculate(50f, s) >= 1.20f,
                "diminishing grows past 180%");
            s.gwsHighCurve = (int)GwsCurveMode.Accelerating;
            Check(GwsLearningService.Calculate(3f, s) > 1.2f, "acceleration grows past high anchor");
            AssertNear(GwsLearningService.Calculate(1000f, s), 2f,
                "accelerating high safety cap x2");
            s.gwsHighTechnicalLimit = 5f;
            AssertNear(GwsLearningService.Calculate(1000f, s), 5f,
                "user-customized OP high cap still permitted");
            s.gwsLowEnabled = false;
            AssertNear(GwsLearningService.Calculate(0.3f, s), 1f, "independent low switch");
            s.gwsHighEnabled = false;
            AssertNear(GwsLearningService.Calculate(2f, s), 1f, "independent high switch");
            AssertNear(GwsLearningService.Calculate(float.NaN, s), 1f, "nan input neutral");
            Check(!GwsReadingScope.IsReadingTick, "scope initially clear");
            try
            {
                GwsReadingScope.Enter();
                Check(GwsReadingScope.IsReadingTick, "scope detects reading");
                GwsReadingScope.Enter();
                GwsReadingScope.Exit();
                Check(GwsReadingScope.IsReadingTick, "nested scope remains");
            }
            finally { GwsReadingScope.Exit(); }
            Check(!GwsReadingScope.IsReadingTick, "scope restored");
            Log.Message("[XP Customizer] GWS-V2 anchor tests PASS: v1ParityChecks=" + v1ParityChecks +
                "; editableChecks=" + v2AnchorChecks + "; expandedAnchorChecks=" + expandedAnchorChecks +
                "; gp1PreviewChecks=" + previewChecks +
                "; gp1HighPreview=12_SAMPLES_2x6_ADAPTIVE; gp1LowPreview=4_SAMPLES_1x4_UNCHANGED" +
                "; maxHighAnchorGwsPercent=" + (GwsLearningService.HighAnchorMaximum * 100f) +
                "; medfixChecks=" + medicineChecks +
                "; medicineCanonicalAndLegacyOff=PASS; 30%/180% baseline exact within 0.00002; " +
                "invalid bounds/finite; staged numeric commit; expanded 500/750/1000% anchors in 3 modes, disabled vanilla, 30/100/180 anchors in 3 modes, " +
                "low/high independent switches, 12 skill defaults (Shooting/Melee/Social OFF) and overrides, fresh D01 high Diminishing 500%/x1.30/40%/limit1.50 with stored x2 safety, " +
                "linear/accelerating x2 default safety cap, editable OP high cap, NaN safety, scoped reading exemption. " +
                "Actual reading and game XP still require live validation.");
        }
    }
}
#endif
