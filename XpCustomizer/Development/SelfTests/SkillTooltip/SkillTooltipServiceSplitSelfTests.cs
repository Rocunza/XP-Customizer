#if DEBUG
using System;
using System.Reflection;
using System.Text;
using RimWorld;
using Verse;
using XpCustomizer.FurtherLearning;
using XpCustomizer.LearningCapacity;
using XpCustomizer.SkillTooltip;

namespace XpCustomizer
{
    /// <summary>
    /// TT2 move-only split audit: real CLR metadata and pure/nullable inputs.
    /// It does not draw SkillUI, create Pawns, write settings, patch Harmony,
    /// change a game value or serialize profile/XML data.
    /// </summary>
    internal static class SkillTooltipServiceSplitSelfTests
    {
        private const BindingFlags DeclaredStatic = BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static int checks;

        private static void Check(bool result, string label)
        {
            checks++;
            if (!result)
                throw new InvalidOperationException("TT2_TOOLTIP_SERVICE_SPLIT:" + label);
        }

        private static MethodInfo CheckMethod(
            Type owner, string name, Type result, bool isPublic,
            params Type[] arguments)
        {
            MethodInfo method = owner.GetMethod(name, DeclaredStatic,
                null, arguments, null);
            Check(method != null && method.DeclaringType == owner &&
                method.IsStatic && method.IsPublic == isPublic &&
                method.ReturnType == result,
                "METHOD_CONTRACT_" + name + "_" + arguments.Length);
            return method;
        }

        private static string InvokeFormat(MethodInfo method, float value)
        {
            return (string)method.Invoke(null, new object[] { value });
        }

        internal static void Run()
        {
            checks = 0;
            Type owner = typeof(SkillTooltipService);
            Check(owner.FullName == "XpCustomizer.SkillTooltip.SkillTooltipService",
                "PRESERVED_CLR_NAME");
            Check(owner.IsNotPublic && owner.IsAbstract && owner.IsSealed,
                "SINGLE_INTERNAL_STATIC_CLR_TYPE");
            Check(owner.Assembly == typeof(XpCustomizerMod).Assembly,
                "SAME_OWNING_ASSEMBLY");
            Check(!String.IsNullOrEmpty(XpCustomizerMod.BuildId) &&
                XpCustomizerMod.BuildId.StartsWith("XPC-", StringComparison.Ordinal),
                "VERSION_ID_PRESENT");
            FieldInfo latch = owner.GetField("tooltipUiRevisionLogged", DeclaredStatic);
            Check(latch != null && latch.DeclaringType == owner &&
                latch.IsPrivate && latch.IsStatic &&
                latch.FieldType == typeof(bool), "UI_REVISION_LATCH_OWNER");
            Check(owner.GetFields(DeclaredStatic).Length == 1,
                "ONLY_ONE_ORIGINAL_STATIC_FIELD");

            Type floatOut = typeof(float).MakeByRefType();
            CheckMethod(owner, "HasAnyPresentationCustomization", typeof(bool),
                true, typeof(SkillRecord));
            CheckMethod(owner, "ShouldReplaceVanillaSaturatedLine", typeof(bool),
                true);
            CheckMethod(owner, "IsDailyCapacityPresentationActive", typeof(bool),
                true);
            CheckMethod(owner, "IsFurtherLearningPresentationActive", typeof(bool),
                true);
            CheckMethod(owner, "IsConsciousnessLearningSpeedPresentationActive",
                typeof(bool), false);
            CheckMethod(owner, "IsConsciousnessLearningSpeedPresentationActive",
                typeof(bool), false, typeof(XpCustomizerSettings));
            CheckMethod(owner, "BuildLearningSpeedModifierLines", typeof(string),
                true, typeof(SkillRecord));
            CheckMethod(owner, "BuildXpCustomizerBlock", typeof(string), true,
                typeof(SkillRecord), typeof(bool));
            MethodInfo spacing = CheckMethod(owner, "AppendSectionSpacing",
                typeof(void), false, typeof(StringBuilder));

            MethodInfo preview = CheckMethod(owner,
                "TryGetWorkFactorLearningPreview", typeof(bool), false,
                typeof(SkillRecord), floatOut, floatOut);
            CheckMethod(owner, "TryGetWorkFactorLearningMultiplier", typeof(bool),
                true, typeof(SkillRecord), floatOut);
            CheckMethod(owner, "GetWorkFactorLearningMultiplier", typeof(float),
                true, typeof(SkillRecord));
            CheckMethod(owner, "IsWorkFactorLearningSpeedPresentationActive",
                typeof(bool), false, typeof(SkillRecord));
            CheckMethod(owner, "BuildWorkFactorLearningLine", typeof(string),
                true, typeof(SkillRecord));
            CheckMethod(owner, "BuildGwsPreviewLine", typeof(string),
                false, typeof(float), typeof(float));
            CheckMethod(owner, "BuildConsPreviewLine", typeof(string),
                false, typeof(float), typeof(float));
            CheckMethod(owner, "BuildConsciousnessLearningLine", typeof(string),
                true, typeof(SkillRecord));
            CheckMethod(owner, "AppendDailyCapacity", typeof(void), false,
                typeof(StringBuilder), typeof(SkillRecord));
            CheckMethod(owner, "AppendFurtherLearning", typeof(void), false,
                typeof(StringBuilder), typeof(SkillRecord));
            CheckMethod(owner, "BuildFurtherLearningDetailLines", typeof(string),
                false, typeof(FurtherLearningBreakdown));
            CheckMethod(owner, "IsNeutralAppliedMultiplier", typeof(bool),
                false, typeof(float));
            MethodInfo multiplier = CheckMethod(owner, "FormatMultiplierPercent",
                typeof(string), false, typeof(float));
            MethodInfo percent = CheckMethod(owner, "FormatPercent",
                typeof(string), false, typeof(float));
            MethodInfo signed = CheckMethod(owner, "FormatSignedPercent",
                typeof(string), false, typeof(float));
            MethodInfo points = CheckMethod(owner, "FormatPercentagePoints",
                typeof(string), false, typeof(float));
            Check(owner.GetMethods(DeclaredStatic).Length == 25,
                "ALL_25_ORIGINAL_METHODS_SINGLE_OWNER");

            Check(typeof(SkillTooltipComposer).Assembly == owner.Assembly &&
                typeof(SkillTooltipTextEditor).Assembly == owner.Assembly &&
                typeof(SkillTooltipPresentationValues).Assembly == owner.Assembly,
                "TT1_HELPERS_RETAINED");
            Check(typeof(Patch_SkillUI_GetSkillDescription).Assembly == owner.Assembly,
                "HARMONY_ENTRY_OWNER_RETAINED");
            Check(typeof(Patch_SkillUI_GetSkillDescription).FullName ==
                "XpCustomizer.LearningCapacity.Patch_SkillUI_GetSkillDescription",
                "HARMONY_ENTRY_NAME_RETAINED");

            // Pure fixture: neutral means exact x1 before formatting, not rounded x100%.
            Check(SkillTooltipService.IsNeutralAppliedMultiplier(1f),
                "NEUTRAL_EXACT_ONE");
            Check(!SkillTooltipService.IsNeutralAppliedMultiplier(0.9999f),
                "NEUTRAL_BELOW_ONE_FALSE");
            Check(!SkillTooltipService.IsNeutralAppliedMultiplier(1.0001f),
                "NEUTRAL_ABOVE_ONE_FALSE");
            Check(!SkillTooltipService.IsNeutralAppliedMultiplier(float.NaN),
                "NEUTRAL_NAN_FALSE");
            Check(SkillTooltipService.BuildGwsPreviewLine(1.44f, 1f) == "",
                "GWS_NEUTRAL_SOURCE_MUST_NOT_SHOW");
            Check(SkillTooltipService.BuildConsPreviewLine(1.35f, 1f) == "",
                "CONS_NEUTRAL_SOURCE_MUST_NOT_SHOW");
            Check(SkillTooltipService.BuildGwsPreviewLine(0.89f, 0.91f) ==
                "  - GW speed: x91%", "GWS_GOLDEN_GTL1_HIDDEN_SOURCE_RATIO");
            Check(SkillTooltipService.BuildConsPreviewLine(1.35f, 1.06f) ==
                "  - Cons 1.35: x106%", "CONS_GOLDEN_SOURCE_RATIO");
            Check(SkillTooltipService.BuildGwsPreviewLine(1f, 1.0001f) != "",
                "GWS_ROUNDED_100_STILL_VISIBLE");
            Check(SkillTooltipService.BuildConsPreviewLine(1f, 1.0001f) != "",
                "CONS_ROUNDED_100_STILL_VISIBLE");

            // Safe null guards; no Pawn, XP, settings, XML or Scribe writes.
            Check(SkillTooltipService.BuildWorkFactorLearningLine(null) == "",
                "NULL_GWS_LINE_EMPTY");
            Check(SkillTooltipService.GetWorkFactorLearningMultiplier(null) == 1f,
                "NULL_GWS_MULTIPLIER_ONE");
            float nullFactor;
            Check(!SkillTooltipService.TryGetWorkFactorLearningMultiplier(
                null, out nullFactor) && nullFactor == 1f,
                "NULL_GWS_TRY_FALSE_ONE");
            object[] previewArgs = { null, 0f, 0f };
            Check((bool)preview.Invoke(null, previewArgs) == false &&
                (float)previewArgs[1] == 1f &&
                (float)previewArgs[2] == 1f,
                "NULL_READONLY_PREVIEW_RETURNS_DEFAULTS");
            Check(SkillTooltipService.BuildConsciousnessLearningLine(null) == "",
                "NULL_CONSCIOUSNESS_LINE_EMPTY");
            Check(SkillTooltipService.BuildLearningSpeedModifierLines(null) == "",
                "NULL_SPEED_LINES_EMPTY");
            Check(SkillTooltipService.BuildXpCustomizerBlock(null, false) == "" &&
                SkillTooltipService.BuildXpCustomizerBlock(null, true) == "",
                "NULL_XP_BLOCK_EMPTY");
            Check(SkillTooltipService.BuildFurtherLearningDetailLines(null) == "",
                "NULL_FURTHER_DETAIL_EMPTY");
            Check(!ConsciousnessLearning.ConsciousnessCurveDispatcher
                .HasConfiguredSpeedEffect(null), "NULL_CONSCIOUSNESS_SETTINGS_OFF");

            Check(InvokeFormat(multiplier, 1.23f) == "x123%",
                "MULTIPLIER_FORMAT");
            Check(InvokeFormat(percent, 0.2f) == "20%",
                "PERCENT_FORMAT");
            Check(InvokeFormat(signed, 0.06f) == "+6%",
                "SIGNED_POSITIVE_FORMAT");
            Check(InvokeFormat(signed, -0.06f) == "-6%",
                "SIGNED_NEGATIVE_FORMAT");
            Check(InvokeFormat(points, 6.4f) == "+6%",
                "PERCENTAGE_POINTS_POSITIVE_FORMAT");
            Check(InvokeFormat(points, -6.4f) == "-6%",
                "PERCENTAGE_POINTS_NEGATIVE_FORMAT");
            var text = new StringBuilder();
            spacing.Invoke(null, new object[] { text });
            Check(text.Length == 0, "EMPTY_SECTION_NO_NEWLINE");
            text.Append("section");
            spacing.Invoke(null, new object[] { text });
            Check(text.ToString() == "section\n\n", "SECTION_DOUBLE_NEWLINE");

            Log.Message("[XPC-TT2][SKILL_TOOLTIP_SERVICE_SPLIT_AUDIT] " +
                "status=PASS; checks=" + checks +
                "; type=ONE_STATIC_PARTIAL_CLR_TYPE; methods=25_RETAINED" +
                "; field=ONE_UI_REVISION_LATCH; harmonyPatch=UNCHANGED_REFLECTION" +
                "; neutral=EXACT_X1; golden=GWS_GTL1_HIDDEN_SOURCE_AND_CONS_FORMATTING" +
                "; tt1=HF1_LINE_ANCHORED_HEADING; sourceMoveParity=EXTERNAL_SHA_AUDIT" +
                "; liveSkillUI=NOT_RUN; liveIMGUI=NOT_RUN; actualPawnXp=NOT_RUN" +
                "; realHarmonyRuntimeOrder=NOT_RUN; settingsWrites=0" +
                "; profileIO=0; xmlWrites=0; scribeCalls=0" +
                "; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
