#if DEBUG
using System;
using System.Reflection;
using UnityEngine;
using Verse;
using XpCustomizer.UI;
using XpCustomizer.Profiles;

namespace XpCustomizer
{
    /// <summary>
    /// LCUI1: detached, read-only split audit. Does not draw the real IMGUI,
    /// change live settings, exercise profile IO, or modify a Pawn/GeneDef.
    /// Actual numeric control and DLC behavior retain separate live QA.
    /// </summary>
    internal static class LearningCapacitySettingsUISplitSelfTests
    {
        private const BindingFlags StaticDeclared = BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const BindingFlags InstanceFields = BindingFlags.Instance |
            BindingFlags.Public | BindingFlags.NonPublic;

        private static int checks;

        private static void Check(bool condition, string assertion)
        {
            checks++;
            if (!condition)
                throw new InvalidOperationException("LCUI1_SPLIT:" + assertion);
        }

        private static FieldInfo CheckNumericField(Type ui, string name)
        {
            FieldInfo field = ui.GetField(name, StaticDeclared);
            Check(field != null && field.DeclaringType == ui &&
                field.IsPrivate && field.IsStatic && field.IsInitOnly &&
                field.FieldType == typeof(NumericInputState),
                "INPUT_OWNER_" + name);
            Check(field.GetValue(null) != null, "INPUT_INITIALIZED_" + name);
            return field;
        }

        private static MethodInfo CheckMethod(Type ui, string name,
            Type result, bool isPublic, params Type[] parameters)
        {
            MethodInfo method = ui.GetMethod(name, StaticDeclared,
                null, parameters, null);
            Check(method != null && method.IsStatic &&
                method.DeclaringType == ui && method.IsPublic == isPublic &&
                method.ReturnType == result,
                "METHOD_CONTRACT_" + name);
            return method;
        }

        private static void CheckSourceSettingFields(string suffix)
        {
            FieldInfo custom = typeof(XpCustomizerSettings).GetField(
                "learningCapacityCustom" + suffix, InstanceFields);
            FieldInfo overridden = typeof(XpCustomizerSettings).GetField(
                "learningCapacityOverride" + suffix, InstanceFields);
            Check(custom != null && custom.FieldType == typeof(float) &&
                !custom.IsStatic, "SOURCE_CUSTOM_" + suffix);
            Check(overridden != null && overridden.FieldType == typeof(bool) &&
                !overridden.IsStatic, "SOURCE_OVERRIDE_" + suffix);
        }

        private static void CheckColumns(MethodInfo method, float width, bool wide)
        {
            object[] args = {
                new Rect(10f, 12f, width, 32f),
                new Rect(), new Rect(), new Rect(), new Rect()
            };
            method.Invoke(null, args);
            Rect label = (Rect)args[1];
            Rect mode = (Rect)args[2];
            Rect value = (Rect)args[3];
            Rect example = (Rect)args[4];
            Check(label.x == 10f && label.width >= 0f &&
                label.xMax + 10f == mode.x,
                "LABEL_AND_MODE_GAP_" + width);
            Check(mode.width == 104f && mode.xMax + 10f == value.x,
                "MODE_AND_FINAL_GAP_" + width);
            Check(value.width == (wide ? 128f : 108f) &&
                value.xMax + 10f == example.x,
                "FINAL_AND_EXAMPLE_GAP_" + width);
            Check(example.width == (wide ? 116f : 95f) &&
                example.xMax == width + 10f,
                "EXAMPLE_RIGHT_EDGE_" + width);
            Check(label.y == 12f && mode.height == 32f &&
                value.height == 32f && example.height == 32f,
                "ROW_COORDINATES_" + width);
        }

        internal static void Run()
        {
            checks = 0;
            Type ui = typeof(LearningCapacitySettingsUI);
            Check(ui.FullName == "XpCustomizer.UI.LearningCapacitySettingsUI",
                "PRESERVED_NAMESPACE_AND_CLR_TYPE");
            Check(ui.IsAbstract && ui.IsSealed && ui.IsNotPublic,
                "ONE_INTERNAL_STATIC_CLR_TYPE");
            Check(ui.Assembly == typeof(XpCustomizerMod).Assembly,
                "SAME_MOD_ASSEMBLY");
            Check(!String.IsNullOrEmpty(XpCustomizerMod.BuildId) &&
                XpCustomizerMod.BuildId.StartsWith("XPC-", StringComparison.Ordinal),
                "BUILD_FAMILY_NOT_STALE_EXACT_ID");
            Check(ProfileEditorReadinessService.KnownSourceCount >= 15,
                "READINESS_SOURCE_REGISTRY_PRESENT");

            FieldInfo rev = ui.GetField("UiRevision", StaticDeclared);
            FieldInfo logged = ui.GetField("revisionLogged", StaticDeclared);
            Check(rev != null && rev.IsLiteral &&
                rev.FieldType == typeof(string) &&
                !String.IsNullOrEmpty((string)rev.GetRawConstantValue()),
                "PRESERVED_UI_REVISION");
            Check(logged != null && logged.IsPrivate && logged.IsStatic &&
                logged.FieldType == typeof(bool), "PRESERVED_REVISION_LATCH");

            string[] inputs = {
                "baseDailyCapInput", "slowLearnerInput", "fastLearnerInput",
                "tooSmartInput", "blissLobotomyInput", "slowStudyInput",
                "quickStudyInput", "neuralSuperchargeInput",
                "learningAssistantInput", "ranchingCentralInput"
            };
            foreach (string name in inputs)
            {
                FieldInfo field = CheckNumericField(ui, name);
                Check(object.ReferenceEquals(field.GetValue(null),
                    field.GetValue(null)), "INPUT_STABLE_REFERENCE_" + name);
            }
            int editorFields = 0;
            foreach (FieldInfo f in ui.GetFields(StaticDeclared))
                if (f.FieldType == typeof(NumericInputState)) editorFields++;
            Check(editorFields == inputs.Length,
                "ALL_10_PROFILE_READINESS_EDITOR_FIELDS_PRESENT");

            CheckMethod(ui, "Draw", typeof(void), true,
                typeof(Listing_Standard));
            CheckMethod(ui, "NotifySettingsExternallyChanged", typeof(void),
                true);
            CheckMethod(ui, "DrawBaseDailyCapacity", typeof(void), false,
                typeof(Listing_Standard));
            CheckMethod(ui, "DrawSourceModifierToggle", typeof(void), false,
                typeof(Listing_Standard));
            CheckMethod(ui, "DrawSourceStrength", typeof(void), false,
                typeof(Listing_Standard));
            CheckMethod(ui, "DrawSourceModifiers", typeof(void), false,
                typeof(Listing_Standard));
            MethodInfo columns = CheckMethod(ui, "GetColumns", typeof(void),
                false, typeof(Rect), typeof(Rect).MakeByRefType(),
                typeof(Rect).MakeByRefType(), typeof(Rect).MakeByRefType(),
                typeof(Rect).MakeByRefType());
            CheckMethod(ui, "DrawSourceColumnHeaders", typeof(void), false,
                typeof(Listing_Standard));
            CheckMethod(ui, "DrawSourceValue", typeof(void), false,
                typeof(Listing_Standard), typeof(string), typeof(float),
                typeof(bool).MakeByRefType(), typeof(float).MakeByRefType(),
                typeof(NumericInputState));
            CheckMethod(ui, "DrawVanillaResetButton", typeof(void), false,
                typeof(Listing_Standard));
            CheckMethod(ui, "ResetInputBuffers", typeof(void), false);
            CheckMethod(ui, "DrawGroupTitle", typeof(void), false,
                typeof(Listing_Standard), typeof(string));

            // Real original UI column allocator only; no actual GUI events.
            CheckColumns(columns, 700f, true);
            CheckColumns(columns, 590f, false);

            // Field presence only. Actual save/migration and DLC detection
            // remain independently tested and are untouched by this split.
            foreach (string source in new string[] {
                "SlowLearner", "FastLearner", "TooSmart", "BlissLobotomy",
                "SlowStudy", "QuickStudy", "NeuralSupercharge",
                "RanchingCentral", "LearningAssistant"
            })
                CheckSourceSettingFields(source);

            Log.Message("[XPC-LCUI1][LEARNING_CAPACITY_UI_SPLIT_AUDIT] " +
                "status=PASS; checks=" + checks +
                "; type=ONE_STATIC_PARTIAL_CLR_TYPE" +
                "; inputFields=10_SAME_DECLARING_TYPE" +
                "; sources=9_EXISTING_FIELD_PAIRS; api=DRAW_AND_NOTIFY_PRESERVED" +
                "; sourceMethods=12_RETAINED; columns=PURE_TWO_WIDTHS" +
                "; profileReadiness=READ_ONLY_SOURCE_CHECK" +
                "; sourceMoveParity=EXTERNAL_SOURCE_AUDIT" +
                "; liveIMGUI=NOT_RUN; actualClicks=NOT_RUN" +
                "; autoCustomSwitch=NOT_RUN; vanillaReset=NOT_RUN" +
                "; gameplay=UNCHANGED_SOURCE; liveSettingsWrites=0" +
                "; configXmlWrites=0; profileIO=0; scribeCalls=0" +
                "; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
