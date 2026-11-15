#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.UI;

namespace XpCustomizer
{
    /// <summary>Phase 4B-R2 V2-only detached and preset regression checks.
    /// These checks do not simulate IMGUI clicks, disk Scribe, or live Pawn Learn().</summary>
    internal static class ConsciousnessPhase4BR2SelfTests
    {
        private static int checks;
        private static void Check(bool value, string reason)
        {
            checks++;
            if (!value) throw new InvalidOperationException(
                "[XP Customizer][CONS-P4BR2] " + reason);
        }
        private static void Near(float got, float expected, string reason)
        {
            Check(!float.IsNaN(got) && !float.IsInfinity(got) &&
                Math.Abs(got - expected) <= 0.00025f,
                reason + " expected=" + expected + " got=" + got);
        }
        internal static void Run()
        {
            checks = 0;
            var s = new XpCustomizerSettings();
            Near(ConsciousnessCurveDispatcher.CalculateSpeed(1.7f, s), 1f,
                "fresh master OFF speed neutral");
            Near(ConsciousnessCurveDispatcher.CalculateCapacity(0.5f, s), 1f,
                "fresh master OFF cap neutral");
            Check(!ConsciousnessCurveDispatcher.HasConfiguredSpeedEffect(s) &&
                !ConsciousnessCurveDispatcher.HasConfiguredCapacityEffect(s),
                "fresh gates OFF");
            s.consciousnessAffectsLearning = true;
            s.consciousnessAffectsLearningSpeed = true;
            s.consciousnessAffectsDailyCapacity = true;
            s.consciousnessAffectsFurtherLearning = false;
            Near(ConsciousnessCurveDispatcher.CalculateSpeed(0.5f, s), 0.5f,
                "speed low default anchor");
            Near(ConsciousnessCurveDispatcher.CalculateCapacity(0.5f, s), 0.5f,
                "capacity low default anchor");
            Near(ConsciousnessCurveDispatcher.CalculateSpeed(1f, s), 1f,
                "speed C100 neutral");
            Near(ConsciousnessCurveDispatcher.CalculateCapacity(1f, s), 1f,
                "capacity C100 neutral");
            Near(s.consciousnessAnchorSpeed.HighAnchor, 1.20f,
                "speed default high anchor position");
            Near(s.consciousnessAnchorCapacity.HighAnchor, 1.20f,
                "capacity default high anchor position");
            Near(ConsciousnessCurveDispatcher.CalculateSpeed(1.20f, s), 1.08f,
                "speed high default anchor factor");
            Near(ConsciousnessCurveDispatcher.CalculateCapacity(1.20f, s), 1.08f,
                "capacity high default anchor factor");
            Check(ConsciousnessCurveDispatcher.HasConfiguredSpeedEffect(s) &&
                ConsciousnessCurveDispatcher.HasConfiguredCapacityEffect(s),
                "default V2 gates ON");

            // Historic explicitly edited 135% anchor remains supported.
            s.consciousnessAnchorSpeed.HighAnchor = 1.35f;
            s.consciousnessAnchorCapacity.HighAnchor = 1.35f;
            s.consciousnessAnchorSpeed.LowFactor = 0.29f;
            s.consciousnessAnchorSpeed.HighFactor = 1.6f;
            s.consciousnessAnchorSpeed.HighDiminishingLimit = 3f;
            s.consciousnessAnchorCapacity.LowFactor = 0.73f;
            s.consciousnessAnchorCapacity.HighFactor = 1.85f;
            s.consciousnessAnchorCapacity.HighCurve = (int)ConsAnchorCurveMode.Linear;
            s.consciousnessAnchorCapacity.HighSafetyLimit = 2.3f;
            Near(ConsciousnessCurveDispatcher.CalculateSpeed(0.5f, s), 0.29f,
                "speed independent low anchor");
            Near(ConsciousnessCurveDispatcher.CalculateCapacity(0.5f, s), 0.73f,
                "capacity independent low anchor");
            Near(ConsciousnessCurveDispatcher.CalculateSpeed(1.35f, s), 1.6f,
                "speed independent high anchor");
            Near(ConsciousnessCurveDispatcher.CalculateCapacity(1.35f, s), 1.85f,
                "capacity independent high anchor");
            Near(ConsciousnessAnchorV2SettingsUI.GetSelectedPreviewFactor(1.35f, s, true),
                ConsciousnessCurveDispatcher.CalculateSpeed(1.35f, s),
                "speed preview uses gameplay route");
            Near(ConsciousnessAnchorV2SettingsUI.GetSelectedPreviewFactor(1.35f, s, false),
                ConsciousnessCurveDispatcher.CalculateCapacity(1.35f, s),
                "capacity preview uses gameplay route");
            s.consciousnessAnchorSpeed.LowEnabled = false;
            Near(ConsciousnessCurveDispatcher.CalculateSpeed(0.5f, s), 1f,
                "speed low OFF neutral");
            Near(ConsciousnessCurveDispatcher.CalculateCapacity(0.5f, s), 0.73f,
                "speed low OFF does not disable capacity low");
            s.consciousnessAnchorSpeed.HighEnabled = false;
            Check(!ConsciousnessCurveDispatcher.HasConfiguredSpeedEffect(s) &&
                ConsciousnessCurveDispatcher.HasConfiguredCapacityEffect(s),
                "speed profile both OFF independent gates");
            s.consciousnessAffectsDailyCapacity = false;
            Check(!ConsciousnessCurveDispatcher.HasConfiguredCapacityEffect(s),
                "capacity switch OFF gate");
            Near(ConsciousnessCurveDispatcher.CalculateCapacity(1.7f, s), 1f,
                "capacity switch OFF factor neutral");
            s.consciousnessAffectsDailyCapacity = true;
            s.consciousnessAffectsLearningSpeed = false;
            Near(ConsciousnessCurveDispatcher.CalculateSpeed(1.7f, s), 1f,
                "speed switch OFF neutral");
            s.consciousnessAffectsLearningSpeed = true;
            s.consciousnessAffectsLearning = false;
            Check(!ConsciousnessCurveDispatcher.HasConfiguredSpeedEffect(s) &&
                !ConsciousnessCurveDispatcher.HasConfiguredCapacityEffect(s),
                "master OFF both gates");
            s.consciousnessAffectsLearning = true;

            // Null settings/data are guarded, without contaminating sibling.
            Near(ConsciousnessCurveDispatcher.CalculateSpeed(1.7f, null), 1f,
                "null settings neutral");
            s.consciousnessAnchorSpeed = null;
            Near(ConsciousnessCurveDispatcher.CalculateSpeed(1.35f, s), 1.14f,
                "null profile uses V2 default");
            s.consciousnessAnchorCapacity.HighFactor = float.NaN;
            Near(ConsciousnessCurveDispatcher.CalculateCapacity(1.35f, s), 1.08f,
                "bad profile read uses safe fallback without mutating Scribe");
            Check(float.IsNaN(s.consciousnessAnchorCapacity.HighFactor),
                "factor read did not rewrite stored invalid profile");
            ConsciousnessAnchorV2SettingsStore.NormalizeAfterLoad(s);
            Near(s.consciousnessAnchorCapacity.HighFactor, 1.08f,
                "invalid factor normalized");
            Check(s.consciousnessAnchorSpeed != null &&
                !object.ReferenceEquals(s.consciousnessAnchorSpeed,
                    s.consciousnessAnchorCapacity),
                "distinct non-null profiles");

            s.consciousnessAnchorSpeed.HighAnchor = 1.70f;
            s.consciousnessAnchorSpeed.HighFactor = 1.28f;
            Near(ConsciousnessCurveDispatcher.CalculateSpeed(1.70f, s), 1.28f,
                "user-selected 170% x1.28 still supported");
            s.gwsHighFactor = 1.77f;
            s.consciousnessFurtherBonusAt170 = 53f;
            s.consciousnessFurtherV2.HighFactor = 1.70f;
            s.consciousnessFurtherV2.LowEnabled = false;
            s.consciousnessAffectsFurtherLearning = false;
            s.consciousnessAnchorCapacity.LowFactor = 0.66f;
            s.consciousnessAnchorSpeed.HighFactor = 1.90f;
            SettingsPresetService.ApplyConsciousnessModDefaults(s);
            Near(s.consciousnessAnchorSpeed.HighFactor, 1.08f,
                "ModDefaults reset speed V2 high");
            Near(s.consciousnessAnchorSpeed.HighAnchor, 1.20f,
                "ModDefaults reset speed V2 high position");
            Near(s.consciousnessAnchorCapacity.HighAnchor, 1.20f,
                "ModDefaults reset capacity V2 high position");
            Near(s.consciousnessAnchorCapacity.HighFactor, 1.08f,
                "ModDefaults reset capacity V2 high factor");
            Near(s.consciousnessAnchorSpeed.HighDiminishingLimit, 2f,
                "ModDefaults reset speed V2 high limit");
            Near(s.consciousnessAnchorCapacity.HighDiminishingLimit, 2f,
                "ModDefaults reset capacity V2 high limit");
            Near(s.consciousnessAnchorCapacity.LowFactor, 0.5f,
                "ModDefaults reset capacity V2 low");
            Check(s.consciousnessAffectsLearning &&
                s.consciousnessAffectsDailyCapacity &&
                s.consciousnessAffectsLearningSpeed &&
                !s.consciousnessAffectsFurtherLearning,
                "ModDefaults preserve switches");
            Near(s.gwsHighFactor, 1.77f, "ModDefaults preserve GWS");
            Near(s.consciousnessFurtherBonusAt170,
                XpCustomizerDefaults.ConsciousnessFurtherBonusAt170,
                "ModDefaults reset Further values");
            Near(s.consciousnessFurtherV2.HighFactor, 1.20f,
                "ModDefaults reset Further V2 high factor");
            Check(!s.consciousnessFurtherV2.LowEnabled,
                "ModDefaults keep Further V2 low switch");
            Check(s.consciousnessAnchorSettingsVersion ==
                ConsciousnessAnchorV2SettingsStore.CurrentSchemaVersion,
                "version ID preserved");

            SettingsPresetService.ApplyConsciousnessVanillaPreset(s);
            Check(!s.consciousnessAffectsLearning,
                "Vanilla preset disables Cons master");
            Near(ConsciousnessCurveDispatcher.CalculateSpeed(1.7f, s), 1f,
                "Vanilla reset speed neutral");
            Near(ConsciousnessCurveDispatcher.CalculateCapacity(1.7f, s), 1f,
                "Vanilla reset capacity neutral");
            Near(s.gwsHighFactor, 1.77f, "Vanilla reset preserves GWS");
            // H1: the retired Progressive global preset is no longer part
            // of the section API. Re-enable the Cons master explicitly and
            // verify that the existing Mod Defaults button keeps it ON.
            s.consciousnessAffectsLearning = true;
            SettingsPresetService.ApplyConsciousnessModDefaults(s);
            Check(s.consciousnessAffectsLearning,
                "Cons Mod Defaults preserves re-enabled master after Vanilla");
            Near(ConsciousnessCurveDispatcher.CalculateSpeed(1.35f, s), 1.14f,
                "Cons Mod Defaults restores active V2 curve after Vanilla");

            // Editor input regression: intermediate digits must never be clamped
            // while editing. Only the explicit completion path imposes bounds.
            float parsed;
            Check(ConsciousnessAnchorV2SettingsUI.TryParseNumericValue("2", out parsed) &&
                Math.Abs(parsed - 2f) < 0.0001f, "intermediate digit remains editable");
            Check(ConsciousnessAnchorV2SettingsUI.TryParseNumericValue("250", out parsed) &&
                Math.Abs(parsed - 250f) < 0.0001f, "completed high anchor parse");
            Near(ConsciousnessAnchorV2SettingsUI.ResolveNumericCommit("250", 170f, 101f, 1000f),
                250f, "new high anchor commits without jumping to 101");
            Near(ConsciousnessAnchorV2SettingsUI.ResolveNumericCommit("2", 170f, 101f, 1000f),
                101f, "out-of-range value clamped only on commit");
            Near(ConsciousnessAnchorV2SettingsUI.ResolveNumericCommit("1200", 170f, 101f, 1000f),
                1000f, "high anchor upper limit on commit");
            Near(ConsciousnessAnchorV2SettingsUI.ResolveNumericCommit("", 170f, 101f, 1000f),
                170f, "empty field restores prior valid high anchor");
            Near(ConsciousnessAnchorV2SettingsUI.ResolveNumericCommit("abc", 170f, 101f, 1000f),
                170f, "bad text restores prior value");
            Check(!ConsciousnessAnchorV2SettingsUI.TryParseNumericValue("NaN", out parsed),
                "NaN input rejected");
            Near(ConsciousnessAnchorV2SettingsUI.ResolveNumericCommit("0.55", 0.5f, 0f, 1f),
                0.55f, "fractional multiplier edit");
            Near(ConsciousnessAnchorV2SettingsUI.ResolveNumericCommit("0.75", 0.5f, 0f, 1f),
                0.75f, "independent capacity multiplier edit");

            ValidateS06UiSplitContract();
            ValidateCp1PreviewContract();

            Log.Message("[XP Customizer][CONS-NUMERIC-P4BR6] pure input tests PASS; " +
                "cases=10; raw editing does not clamp; high anchor=250; bounds only on commit; " +
                "empty/invalid restore; decimal factors; IMGUI focus/click runtime NOT tested");

            Log.Message("[XP Customizer][CONS-P4BR2] regression PASS; checks=" + checks +
                "; curveSource=ANCHOR_V2_ONLY; defaults/anchors; independent speed/capacity" +
                "; master/sub/low/high OFF; neutral gate; preview parity; null/invalid" +
                "; presets/GWS/Further isolation; staged numeric parse/commit=10 checks; live Pawn XP, disk Scribe and IMGUI clicks NOT tested");
        }

        // CP1: pure grid geometry and high sample regression. No live IMGUI,
        // settings writes, profile writes, disk Scribe, or gameplay patch calls.
        private static void ValidateCp1PreviewContract()
        {
            int verified = 0;
            Action<bool, string> demand = (ok, name) =>
            {
                verified++;
                if (!ok) throw new InvalidOperationException(
                    "[XPC-CP1][CONS_PREVIEW_AUDIT] status=FAIL; check=" + name);
            };
            float[] expected = { 1.05f, 1.10f, 1.15f, 1.20f, 1.30f, 1.35f,
                1.50f, 1.70f, 2.50f, 5.00f, 7.50f, 10.00f };
            demand(ConsciousnessPreviewGridLayout.HighSampleCount == expected.Length,
                "HIGH_SAMPLE_COUNT_12");
            for (int i = 0; i < expected.Length; i++)
            {
                float value = ConsciousnessPreviewGridLayout.HighSampleAt(i);
                demand(Math.Abs(value - expected[i]) < 0.00001f,
                    "HIGH_SAMPLE_VALUE_" + i);
                if (i > 0)
                    demand(value > ConsciousnessPreviewGridLayout.HighSampleAt(i - 1),
                        "HIGH_SAMPLE_ASCENDING_" + i);
            }

            float[] widths = { 1000f, 840f, 839f, 560f, 559f, 420f,
                419f, 0f, float.NaN, float.PositiveInfinity };
            int[] columnsExpected = { 6, 6, 4, 4, 3, 3, 2, 2, 2, 2 };
            for (int w = 0; w < widths.Length; w++)
            {
                int cols = ConsciousnessPreviewGridLayout.ResolveColumns(widths[w], true);
                demand(cols == columnsExpected[w], "HIGH_COLUMNS_" + w);
                demand(ConsciousnessPreviewGridLayout.ResolveColumns(widths[w], false) == 4,
                    "LOW_UNCHANGED_1x4_" + w);
            }

            int[] widthsColumns = { 6, 4, 3, 2 };
            foreach (int cols in widthsColumns)
            {
                int rows = (12 + cols - 1) / cols;
                float[] heights = { ConsciousnessPreviewGridLayout.SimpleRowHeight,
                    ConsciousnessPreviewGridLayout.DetailedRowHeight };
                foreach (float rowHeight in heights)
                {
                    float expectedHeight = rows * rowHeight +
                        (rows - 1) * ConsciousnessPreviewGridLayout.RowGap;
                    float actualHeight = ConsciousnessPreviewGridLayout.GridHeight(
                        12, cols, rowHeight);
                    demand(Math.Abs(actualHeight - expectedHeight) < 0.0001f,
                        "RESERVED_FULL_HEIGHT_" + cols + "_" + rowHeight);
                    Rect grid = new Rect(40f, 100f,
                        cols * ConsciousnessPreviewGridLayout.MinCellWidth, actualHeight);
                    for (int i = 0; i < 12; i++)
                    {
                        Rect cell = ConsciousnessPreviewGridLayout.CellRect(grid, i,
                            cols, rowHeight);
                        int row = i / cols;
                        int column = i % cols;
                        demand(Math.Abs(cell.x - (40f + column *
                            ConsciousnessPreviewGridLayout.MinCellWidth)) < 0.0001f &&
                            Math.Abs(cell.y - (100f + row * (rowHeight +
                            ConsciousnessPreviewGridLayout.RowGap))) < 0.0001f &&
                            cell.xMax <= grid.xMax + 0.0001f &&
                            cell.yMax <= grid.yMax + 0.0001f,
                            "CELL_IN_GRID_" + cols + "_" + rowHeight + "_" + i);
                    }
                }
            }
            demand(ConsciousnessPreviewGridLayout.GridHeight(4, 4,
                ConsciousnessPreviewGridLayout.SimpleRowHeight) == 30f,
                "LOW_SPEED_CAPACITY_HEIGHT_30");
            demand(ConsciousnessPreviewGridLayout.GridHeight(4, 4,
                ConsciousnessPreviewGridLayout.DetailedRowHeight) == 55f,
                "LOW_FURTHER_HEIGHT_55");
            demand(ConsciousnessPreviewGridLayout.GridHeight(0, 6, 30f) == 0f,
                "EMPTY_ZERO_HEIGHT");

            var settings = new XpCustomizerSettings();
            settings.consciousnessAffectsLearning = true;
            settings.consciousnessAffectsLearningSpeed = true;
            settings.consciousnessAffectsDailyCapacity = true;
            settings.consciousnessAnchorSpeed.HighEnabled = true;
            settings.consciousnessAnchorCapacity.HighEnabled = true;
            int[] curves = { (int)ConsAnchorCurveMode.Diminishing,
                (int)ConsAnchorCurveMode.Linear,
                (int)ConsAnchorCurveMode.Accelerating };
            foreach (int curve in curves)
            {
                settings.consciousnessAnchorSpeed.HighCurve = curve;
                settings.consciousnessAnchorCapacity.HighCurve = curve;
                foreach (float level in expected)
                {
                    float speed = ConsciousnessAnchorV2SettingsUI.GetSelectedPreviewFactor(
                        level, settings, true);
                    float capacity = ConsciousnessAnchorV2SettingsUI.GetSelectedPreviewFactor(
                        level, settings, false);
                    demand(!float.IsNaN(speed) && !float.IsInfinity(speed) &&
                        Math.Abs(speed - ConsciousnessCurveDispatcher.CalculateSpeed(
                        level, settings)) < 0.00025f,
                        "SPEED_FACTOR_PARITY_" + curve + "_" + level);
                    demand(!float.IsNaN(capacity) && !float.IsInfinity(capacity) &&
                        Math.Abs(capacity - ConsciousnessCurveDispatcher.CalculateCapacity(
                        level, settings)) < 0.00025f,
                        "CAPACITY_FACTOR_PARITY_" + curve + "_" + level);
                }
            }
            demand(settings.consciousnessAnchorSpeed.HighAnchor == 1.20f &&
                settings.consciousnessAnchorCapacity.HighAnchor == 1.20f &&
                settings.consciousnessAnchorSpeed.HighFactor == 1.08f &&
                settings.consciousnessAnchorCapacity.HighFactor == 1.08f,
                "CONFIG_VALUES_UNCHANGED");
            Log.Message("[XPC-CP1][CONS_PREVIEW_AUDIT] status=PASS; checks=" + verified +
                "; speedHigh=12; capacityHigh=12; highLayout=6x2_ADAPTIVE" +
                "; low=1x4_UNCHANGED; rowHeights=30/55; curves=3" +
                "; previewArithmetic=GAMEPLAY_ROUTE_PARITY; actualIMGUI=NOT_RUN" +
                "; settingsWrites=0; profileWrites=0; build=" + XpCustomizerMod.BuildId);
        }

        // S06: Debug-only read-only contract. Checks the compiled CLR type, not
        // physical source filenames (those are verified by the source manifest).
        // It deliberately does not draw UI, focus controls, touch settings or disk.
        private static void ValidateS06UiSplitContract()
        {
            const string stage = "XPC-S06-CONS-ANCHOR-V2-UI-3PART-20261111-R1";
            int verified = 0;
            Action<bool, string> demand = (ok, reason) =>
            {
                verified++;
                if (!ok)
                    throw new InvalidOperationException(
                        "[XPC-S06][CONS_ANCHOR_UI_SPLIT_AUDIT] status=FAIL; version=" +
                        stage + "; check=" + reason);
            };
            const BindingFlags nonPublicStatic = BindingFlags.Static | BindingFlags.NonPublic;
            const BindingFlags nonPublicInstance = BindingFlags.Instance | BindingFlags.NonPublic |
                BindingFlags.Public;
            Type owner = typeof(ConsciousnessAnchorV2SettingsUI);
            demand(owner.FullName == "XpCustomizer.UI.ConsciousnessAnchorV2SettingsUI" &&
                owner.IsAbstract && owner.IsSealed && !owner.IsPublic,
                "ONE_INTERNAL_STATIC_CLR_TYPE");
            demand(ConsciousnessAnchorV2SettingsUI.SplitStage == stage,
                "S06_STAGE_IDENTITY");

            // 17 existing outer methods, including the S07 shared numeric APIs.
            string[] methodNames = {
                "IsConsControl", "ValidateNavigationEditor", "ValidateNavigationProfile",
                "ValidateNavigationDraft", "DrawProfile", "DrawLowHighTitle",
                "TryParseNumericValue", "ResolveNumericCommit", "DrawNumber",
                "DrawCurveModes", "DrawCurveButton", "DrawCurvature",
                "GetSelectedPreviewFactor", "DrawLowPreview", "DrawHighPreview",
                "DrawSamples", "NotifySettingsExternallyChanged"
            };
            MethodInfo[] methods = owner.GetMethods(nonPublicStatic | BindingFlags.DeclaredOnly);
            demand(methods.Length == methodNames.Length, "METHOD_COUNT_17");
            foreach (string name in methodNames)
            {
                MethodInfo found = owner.GetMethod(name, nonPublicStatic | BindingFlags.DeclaredOnly);
                demand(found != null && found.DeclaringType == owner, "METHOD_" + name);
            }
            demand(owner.GetMethod("TryParseNumericValue", nonPublicStatic,
                null, new[] { typeof(string), typeof(float).MakeByRefType() }, null) != null,
                "S07_NUMERIC_PARSE_SIGNATURE");
            demand(owner.GetMethod("ResolveNumericCommit", nonPublicStatic,
                null, new[] { typeof(string), typeof(float), typeof(float), typeof(float) },
                null) != null, "S07_NUMERIC_COMMIT_SIGNATURE");

            Type editor = owner.GetNestedType("ConsNumericEditState", BindingFlags.NonPublic);
            Type holder = owner.GetNestedType("ProfileInputBuffers", BindingFlags.NonPublic);
            demand(editor != null && editor.DeclaringType == owner, "NESTED_EDIT_STATE");
            demand(holder != null && holder.DeclaringType == owner, "NESTED_PROFILE_BUFFERS");
            FieldInfo control = editor.GetField("ControlName", nonPublicInstance);
            FieldInfo raw = editor.GetField("Buffer", nonPublicInstance);
            FieldInfo focused = editor.GetField("WasFocused", nonPublicInstance);
            demand(control != null && control.FieldType == typeof(string) &&
                raw != null && raw.FieldType == typeof(string) &&
                focused != null && focused.FieldType == typeof(bool),
                "EDITOR_READINESS_REFLECTION_FIELDS");
            string[] inputNames = { "LowAnchor", "LowFactor", "HighAnchor", "HighFactor",
                "DiminishingLimit", "SafetyLimit" };
            string[] groupNames = { "speedInputs", "capacityInputs" };
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            int inputStates = 0;
            foreach (string groupName in groupNames)
            {
                FieldInfo group = owner.GetField(groupName, nonPublicStatic);
                demand(group != null && group.FieldType == holder && group.IsInitOnly,
                    "STATIC_INPUT_GROUP_" + groupName);
                object buffers = group.GetValue(null);
                demand(buffers != null, "INPUT_GROUP_PRESENT_" + groupName);
                foreach (string inputName in inputNames)
                {
                    FieldInfo field = holder.GetField(inputName, nonPublicInstance);
                    demand(field != null && field.FieldType == editor,
                        "INPUT_SLOT_" + groupName + "." + inputName);
                    object state = field.GetValue(buffers);
                    demand(state != null && state.GetType() == editor,
                        "INPUT_STATE_" + groupName + "." + inputName);
                    string id = (string)control.GetValue(state);
                    demand(id != null && id.StartsWith("ZCN.XpCustomizer.ConsV2.",
                        StringComparison.Ordinal) && ids.Add(id),
                        "UNIQUE_CONTROL_" + groupName + "." + inputName);
                    inputStates++;
                }
            }
            demand(inputStates == 12 && ids.Count == 12, "12_OF_12_INPUT_STATES");
            FieldInfo sources = typeof(XpCustomizer.Profiles.ProfileEditorReadinessService)
                .GetField("Sources", nonPublicStatic);
            Type[] tracked = sources == null ? null : (Type[])sources.GetValue(null);
            demand(tracked != null && Array.IndexOf(tracked, owner) >= 0,
                "PROFILE_READINESS_SOURCE_REGISTERED");
            Log.Message("[XPC-S06][CONS_ANCHOR_UI_SPLIT_AUDIT] status=PASS; version=" + stage +
                "; checks=" + verified + "; clrType=ONE_INTERNAL_STATIC_PARTIAL" +
                "; methods=17; nestedTypes=2; inputGroups=2; inputStates=12/12" +
                "; uniqueControlIds=12/12; readinessSource=REGISTERED" +
                "; sharedNumericApi=PRESERVED; sourceFileMove=EXTERNAL_SHA_AUDIT" +
                "; realIMGUI=NOT_RUN; profileWrite=NOT_RUN; diskScribe=NOT_RUN" +
                "; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
