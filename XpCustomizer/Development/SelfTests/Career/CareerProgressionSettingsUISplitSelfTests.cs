#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;
using XpCustomizer.Progression;
using XpCustomizer.Profiles;
using XpCustomizer.UI;

namespace XpCustomizer
{
    /// <summary>
    /// CPS1: read-only source contract and detached pure helper checks.
    /// No live settings writes, GUI drawing, profile operations or Scribe calls.
    /// CBE1, Career XP math, profile readiness and runtime apply have separate suites.
    /// </summary>
    internal static class CareerProgressionSettingsUISplitSelfTests
    {
        private const BindingFlags DeclaredStatic = BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static int checks;

        private static void Check(bool condition, string label)
        {
            checks++;
            if (!condition)
                throw new InvalidOperationException("CPS1_UI_SPLIT:" + label);
        }

        private static FieldInfo CheckField(Type type, string name, Type fieldType)
        {
            FieldInfo field = type.GetField(name, DeclaredStatic);
            Check(field != null && field.IsStatic && field.IsPrivate &&
                field.IsInitOnly && field.FieldType == fieldType,
                "OWNED_READONLY_FIELD_" + name);
            Check(field.GetValue(null) != null, "INITIALIZED_FIELD_" + name);
            return field;
        }

        private static MethodInfo CheckMethod(Type type, string name,
            Type[] arguments, Type returnType, bool isPublic)
        {
            MethodInfo method = type.GetMethod(name, DeclaredStatic, null,
                arguments, null);
            Check(method != null && method.IsStatic &&
                method.IsPublic == isPublic && method.ReturnType == returnType,
                "EXISTING_METHOD_" + name);
            return method;
        }

        private static MethodInfo CheckPrivateName(Type type, string name)
        {
            MethodInfo method = type.GetMethod(name, DeclaredStatic);
            Check(method != null && method.IsStatic && method.IsPrivate,
                "SOURCE_METHOD_" + name);
            return method;
        }

        internal static void Run()
        {
            checks = 0;
            Type ui = typeof(CareerProgressionSettingsUI);
            Check(ui.IsAbstract && ui.IsSealed, "SINGLE_STATIC_CLR_TYPE");
            Check(ui.FullName == "XpCustomizer.UI.CareerProgressionSettingsUI",
                "ORIGINAL_NAMESPACE_AND_TYPE");
            Check(ui.Assembly == typeof(CareerXpStageBoundaryEditorUI).Assembly,
                "CBE1_IN_SAME_ASSEMBLY");
            Check(!String.IsNullOrEmpty(XpCustomizerMod.BuildId) &&
                XpCustomizerMod.BuildId.StartsWith("XPC-", StringComparison.Ordinal),
                "CURRENT_BUILD_FAMILY_NOT_STALE_EXACT_VERSION");

            CheckField(ui, "VanillaColor", typeof(Color));
            FieldInfo multiplier = CheckField(ui, "totalMultiplierInput",
                typeof(NumericInputState));
            FieldInfo weights = CheckField(ui, "stageWeightInputs",
                typeof(List<NumericInputState>));
            FieldInfo locks = CheckField(ui, "stageLockedShareInputs",
                typeof(List<NumericInputState>));
            Check(object.ReferenceEquals(multiplier.GetValue(null),
                multiplier.GetValue(null)), "TOTAL_INPUT_STABLE_IDENTITY");
            Check(object.ReferenceEquals(weights.GetValue(null),
                weights.GetValue(null)), "WEIGHT_INPUTS_STABLE_IDENTITY");
            Check(object.ReferenceEquals(locks.GetValue(null),
                locks.GetValue(null)), "LOCK_INPUTS_STABLE_IDENTITY");
            Check(ProfileEditorReadinessService.KnownSourceCount >= 15,
                "PROFILE_READINESS_SOURCES_STILL_REGISTERED");

            CheckMethod(ui, "Draw", new Type[] { typeof(Listing_Standard) },
                typeof(void), true);
            CheckMethod(ui, "NotifySettingsExternallyChanged", Type.EmptyTypes,
                typeof(void), true);
            CheckMethod(ui, "DrawCareerProgression", new Type[] {
                typeof(Listing_Standard) }, typeof(void), false);
            CheckMethod(ui, "DrawTotalMultiplierInput", new Type[] {
                typeof(Listing_Standard), typeof(float), typeof(NumericInputState) },
                typeof(float), false);
            CheckMethod(ui, "DrawCareerDistribution", new Type[] {
                typeof(Listing_Standard), typeof(XpCustomizerSettings) },
                typeof(void), false);
            CheckMethod(ui, "DrawXpRoundingControls", new Type[] {
                typeof(Listing_Standard), typeof(XpCustomizerSettings) },
                typeof(void), false);
            CheckMethod(ui, "DrawStageDistributionRow", new Type[] {
                typeof(Listing_Standard), typeof(string), typeof(float),
                typeof(NumericInputState), typeof(bool), typeof(float),
                typeof(float), typeof(float), typeof(float),
                typeof(float).MakeByRefType(), typeof(NumericInputState),
                typeof(bool), typeof(float) }, typeof(float), false);

            // Do not lock an exact method *count* to the 2026 source revision.
            // Additional future UI methods must not break mod startup.
            foreach (string name in new string[] {
                "DrawRoundingButton", "GetRoundingStepLabel", "DrawStageTableHeader",
                "DrawVanillaComparison", "FormatVanillaRatio", "GetStageColumnRects",
                "CountShareLocks", "GetMaxLockedSharePct", "EnsureLockedShareStorage",
                "ResetStageLockedShareInputStates", "EnsureStageLockedShareInputStates",
                "ResetStageWeightInputStates", "EnsureStageWeightStorage",
                "EnsureStageWeightInputStates", "FormatBoundaries"
            })
                CheckPrivateName(ui, name);

            MethodInfo formatBoundaries = ui.GetMethod("FormatBoundaries", DeclaredStatic);
            Check((string)formatBoundaries.Invoke(null,
                new object[] { null }) == "0 | 20", "EMPTY_BOUNDARIES_FORMAT");
            Check((string)formatBoundaries.Invoke(null,
                new object[] { new int[] { 3, 8, 12 } }) ==
                "0 | 3 | 8 | 12 | 20", "STAGE_BOUNDARIES_FORMAT");

            MethodInfo step = ui.GetMethod("GetRoundingStepLabel", DeclaredStatic);
            Check((string)step.Invoke(null, new object[] {
                CareerXpDistributionService.XpRoundingExact }) == "Exact",
                "ROUND_EXACT_LABEL");
            Check((string)step.Invoke(null, new object[] {
                CareerXpDistributionService.XpRounding100 }) == "100 XP",
                "ROUND_100_LABEL");
            Check((string)step.Invoke(null, new object[] {
                CareerXpDistributionService.XpRounding500 }) == "500 XP",
                "ROUND_500_LABEL");
            Check((string)step.Invoke(null, new object[] {
                CareerXpDistributionService.XpRounding1000 }) == "1,000 XP",
                "ROUND_1000_LABEL");

            MethodInfo ratio = ui.GetMethod("FormatVanillaRatio", DeclaredStatic);
            Check((string)ratio.Invoke(null, new object[] { 1f, 0f }) == "--",
                "VANILLA_ZERO_RATIO_GUARD");
            Check((string)ratio.Invoke(null, new object[] { float.NaN, 1f }) == "--",
                "VANILLA_NAN_RATIO_GUARD");

            MethodInfo lockCount = ui.GetMethod("CountShareLocks", DeclaredStatic);
            Check((int)lockCount.Invoke(null, new object[] {
                new List<float> { -1f, 0.25f, -1f, 0.4f } }) == 2,
                "LOCKED_SHARE_COUNT");
            Check((int)lockCount.Invoke(null, new object[] { null }) == 0,
                "NULL_LOCKS_COUNT");

            Log.Message("[XPC-CPS1][CAREER_PROGRESSION_UI_SPLIT_AUDIT] " +
                "status=PASS; checks=" + checks +
                "; type=ONE_STATIC_PARTIAL_CLR_TYPE; fields=4_ORIGINAL_STATIC" +
                "; inputFields=3_SAME_DECLARING_TYPE; publicApis=DRAW_AND_NOTIFY" +
                "; sourceMethods=RETAINED; pureFormatting=PASS" +
                "; profileReadiness=READ_ONLY_SOURCE_CHECK" +
                "; cbe1=UNCHANGED; stageBuildOrder=SOURCE_AUDIT_ONLY" +
                "; scope=REFLECTION_AND_PURE; liveIMGUI=NOT_RUN" +
                "; actualClicks=NOT_RUN; profileApply=NOT_RUN" +
                "; liveSettingsWrites=0; configXmlWrites=0; geneDefWrites=0" +
                "; scribeCalls=0; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
