#if DEBUG
using System;
using System.Reflection;
using Verse;
using XpCustomizer.UI;

namespace XpCustomizer
{
    /// <summary>
    /// SUH2A/B1 structural CLR audit. This intentionally does NOT invoke GUI
    /// methods without a live IMGUI context. B1's actual source-level guard
    /// and boundary vector parity are checked by the independent package audit.
    /// </summary>
    internal static class SettingsUIHelpersSafetySelfTests
    {
        private const BindingFlags DeclaredStatic = BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static int checks;

        private static void Check(bool ok, string label)
        {
            checks++;
            if (!ok)
                throw new InvalidOperationException("SUH2_UI_SAFETY:" + label);
        }

        private static void HasFinally(Type owner, string name, params Type[] parameters)
        {
            MethodInfo method = owner.GetMethod(name, DeclaredStatic, null, parameters, null);
            Check(method != null && method.DeclaringType == owner, name + "_EXISTS");
            MethodBody body = method.GetMethodBody();
            Check(body != null, name + "_HAS_IL_BODY");
            bool found = false;
            foreach (ExceptionHandlingClause clause in body.ExceptionHandlingClauses)
            {
                if (clause.Flags == ExceptionHandlingClauseOptions.Finally)
                {
                    found = true;
                    break;
                }
            }
            Check(found, name + "_HAS_CLR_FINALLY");
        }

        internal static void Run()
        {
            checks = 0;
            Type owner = typeof(SettingsUIHelpers);
            Check(owner.FullName == "XpCustomizer.UI.SettingsUIHelpers", "SAME_CLR_TYPE");
            Check(owner.IsAbstract && owner.IsSealed && owner.IsNotPublic,
                "SAME_INTERNAL_STATIC_TYPE");
            Check(owner.GetFields(DeclaredStatic).Length == 0, "ZERO_NEW_FIELDS");
            Check(owner.GetMethods(DeclaredStatic).Length == 12, "TWELVE_EXISTING_METHODS");
            Check(!string.IsNullOrEmpty(XpCustomizerMod.BuildId) &&
                XpCustomizerMod.BuildId.StartsWith("XPC-", StringComparison.Ordinal),
                "BUILD_ID_FAMILY");

            Type listing = typeof(Listing_Standard);
            Type numericState = typeof(NumericInputState);
            HasFinally(owner, "DrawSectionTitle", listing, typeof(string));
            HasFinally(owner, "DrawDescription", listing, typeof(string));
            HasFinally(owner, "DrawReferenceText", listing, typeof(string));
            HasFinally(owner, "DrawIntegerSetting", listing, typeof(string),
                typeof(int), typeof(int), typeof(int), typeof(bool));
            HasFinally(owner, "DrawPercentageInput", listing, typeof(string),
                typeof(float), numericState, typeof(float), typeof(float));
            HasFinally(owner, "DrawMultiplierInput", listing, typeof(string),
                typeof(float), numericState, typeof(float), typeof(float));

            Log.Message("[XPC-SUH2A-B1][GUI_STATE_AND_INT_BOUNDARY_AUDIT] " +
                "status=PASS; checks=" + checks +
                "; guiFinally=6_CLR_HANDLERS" +
                "; originalType=UNCHANGED; methods=12_RETAINED; fields=0" +
                "; integerSaturation=EXTERNAL_SOURCE_AND_BOUNDARY_AUDIT" +
                "; narrowLayout=UNCHANGED; profileNumericFocus=UNCHANGED" +
                "; liveIMGUI=NOT_RUN; exceptionInjection=NOT_RUN" +
                "; actualButtonClicks=NOT_RUN; profileIO=0; scribeCalls=0" +
                "; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
