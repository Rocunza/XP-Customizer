#if DEBUG
using System;
using System.Reflection;
using UnityEngine;
using Verse;
using XpCustomizer.UI;

namespace XpCustomizer
{
    /// <summary>
    /// SUH1 move-only source split: CLR surface and pure UI geometry only.
    /// No actual IMGUI events, settings mutation, profile IO or Scribe calls.
    /// Source SHA/call-site parity are checked independently at packaging time.
    /// </summary>
    internal static class SettingsUIHelpersSplitSelfTests
    {
        private const BindingFlags StaticDeclared = BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static int checks;

        private static void Check(bool passed, string label)
        {
            checks++;
            if (!passed)
                throw new InvalidOperationException("SUH1_SETTINGS_UI_HELPERS_SPLIT:" + label);
        }

        private static MethodInfo Method(Type owner, string name, Type returns,
            int visibility, params Type[] args)
        {
            MethodInfo info = owner.GetMethod(name, StaticDeclared, null, args, null);
            Check(info != null && info.DeclaringType == owner && info.IsStatic &&
                info.ReturnType == returns &&
                (visibility == 0 ? info.IsPrivate :
                 visibility == 1 ? info.IsAssembly : info.IsPublic),
                "METHOD_" + name);
            return info;
        }

        internal static void Run()
        {
            checks = 0;
            Type owner = typeof(SettingsUIHelpers);
            Check(owner.FullName == "XpCustomizer.UI.SettingsUIHelpers",
                "CLR_FULL_NAME");
            Check(owner.IsAbstract && owner.IsSealed && owner.IsNotPublic,
                "ONE_INTERNAL_STATIC_CLR_TYPE");
            Check(owner.Assembly == typeof(XpCustomizerMod).Assembly,
                "ASSEMBLY_OWNERSHIP");
            Check(!String.IsNullOrEmpty(XpCustomizerMod.BuildId) &&
                XpCustomizerMod.BuildId.StartsWith("XPC-", StringComparison.Ordinal),
                "VERSION_ID_PRESENT");
            Check(owner.GetFields(StaticDeclared).Length == 0,
                "ZERO_ORIGINAL_STATIC_FIELDS");

            Type rectOut = typeof(Rect).MakeByRefType();
            Method(owner, "DrawRightAdjacentToggle", typeof(void), 1,
                typeof(Listing_Standard), typeof(string), typeof(bool).MakeByRefType());
            MethodInfo layout = Method(owner, "ComputeRightAdjacentToggleRects",
                typeof(void), 1, typeof(Rect), typeof(float), rectOut, rectOut);
            ParameterInfo[] p = layout.GetParameters();
            Check(p.Length == 4 && p[2].IsOut && p[3].IsOut,
                "TOGGLE_RECT_OUT_PARAMETERS");

            Method(owner, "DrawSectionTitle", typeof(void), 2,
                typeof(Listing_Standard), typeof(string));
            Method(owner, "DrawDescription", typeof(void), 2,
                typeof(Listing_Standard), typeof(string));
            Method(owner, "DrawReferenceText", typeof(void), 2,
                typeof(Listing_Standard), typeof(string));
            Method(owner, "DrawConsciousnessExampleStrip", typeof(void), 2,
                typeof(Listing_Standard), typeof(float), typeof(float),
                typeof(float), typeof(float), typeof(string), typeof(bool),
                typeof(string));
            Method(owner, "DrawExampleTitle", typeof(void), 0,
                typeof(Rect), typeof(bool));
            Method(owner, "DrawExampleCell", typeof(void), 0,
                typeof(Rect), typeof(string), typeof(string), typeof(Color));
            MethodInfo format = Method(owner, "FormatExample", typeof(string), 0,
                typeof(float), typeof(bool));
            Method(owner, "DrawIntegerSetting", typeof(int), 2,
                typeof(Listing_Standard), typeof(string), typeof(int),
                typeof(int), typeof(int), typeof(bool));
            Method(owner, "DrawPercentageInput", typeof(float), 2,
                typeof(Listing_Standard), typeof(string), typeof(float),
                typeof(NumericInputState), typeof(float), typeof(float));
            Method(owner, "DrawMultiplierInput", typeof(float), 2,
                typeof(Listing_Standard), typeof(string), typeof(float),
                typeof(NumericInputState), typeof(float), typeof(float));

            MethodInfo[] declared = owner.GetMethods(StaticDeclared);
            int publics = 0, internals = 0, privates = 0;
            foreach (MethodInfo m in declared)
            {
                if (m.IsPublic) publics++;
                else if (m.IsAssembly) internals++;
                else if (m.IsPrivate) privates++;
            }
            Check(declared.Length == 12, "TWELVE_ORIGINAL_METHODS");
            Check(publics == 7 && internals == 2 && privates == 3,
                "VISIBILITY_7_PUBLIC_2_INTERNAL_3_PRIVATE");

            // The only production helper executed here is the existing pure
            // geometric allocator. U1's independent 60-case test remains active.
            foreach (float width in new float[] { 500f, 32f, 24f, 0f })
            {
                Rect r = new Rect(17f, 9f, width, 30f);
                Rect label, checkbox;
                SettingsUIHelpers.ComputeRightAdjacentToggleRects(r, 300f,
                    out label, out checkbox);
                Check(label.width >= 0f && checkbox.width >= 0f,
                    "NONNEGATIVE_RECT_" + width);
                Check(checkbox.x >= label.xMax &&
                    checkbox.xMax <= r.xMax + 0.001f,
                    "DISJOINT_IN_BOUNDS_" + width);
            }
            Check((string)format.Invoke(null, new object[] { 1f, false }) ==
                "x" + 1f.ToString("0.00"), "EXAMPLE_MULTIPLIER_FORMAT");
            Check((string)format.Invoke(null, new object[] { 6f, true }) ==
                "+" + 6f.ToString("0.##"), "EXAMPLE_PP_FORMAT");

            Log.Message("[XPC-SUH1][SETTINGS_UI_HELPERS_SPLIT_AUDIT] " +
                "status=PASS; checks=" + checks +
                "; type=ONE_INTERNAL_STATIC_PARTIAL_CLR_TYPE" +
                "; methods=12_RETAINED; visibility=7_PUBLIC_2_INTERNAL_3_PRIVATE" +
                "; fields=0; toggleRectOut=PRESERVED; pureGeometry=PASS" +
                "; examples=PURE_FORMATTING; externalCallSites=SOURCE_AUDIT_ONLY" +
                "; sourceMoveParity=EXTERNAL_SHA_AUDIT" +
                "; profileNumericFocus=UNCHANGED_SOURCE" +
                "; liveIMGUI=NOT_RUN; actualClicks=NOT_RUN; vanillaReset=NOT_RUN" +
                "; settingsWrites=0; profileIO=0; xmlWrites=0; scribeCalls=0" +
                "; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
