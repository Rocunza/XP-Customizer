using System;
using System.Globalization;
using UnityEngine;
using Verse;
using XpCustomizer.Biotech;

namespace XpCustomizer.UI
{
    /// <summary>
    /// Biotech skill-gene settings: side-by-side Vanilla / Custom reference
    /// table with staged integer editors. Only UI changes; GeneDef mutation
    /// remains exclusively in BiotechGeneService.
    /// </summary>
    internal static class BiotechSettingsUI
    {
        internal const string Revision = "BIOTECH-COMPARISON-INPUT-R1+EXTERNAL-REFRESH-R6P0D1-20261106";
        private static bool revisionLogged;
#if DEBUG
        private static bool l3DLocalizationLogged;
        private static void LogBiotechLocalizationOnce(string dlcState)
        {
            if (l3DLocalizationLogged || Event.current == null || Event.current.type != EventType.Repaint) return;
            l3DLocalizationLogged = true;
            Log.Message("[XPC-L10N-L3D][KEYED_DECAY_BIOTECH] version=XPC-L10N-L3D-DECAY-BIOTECH-20261112-R1; screen=BIOTECH; dlc=" +
                dlcState + "; keys=50; sampleKey=XPC_Biotech_SkillGenesTitle; sample=" +
                "XPC_Biotech_SkillGenesTitle".Translate().ToString());
        }
#else
        private static void LogBiotechLocalizationOnce(string dlcState) { }
#endif

        private static readonly Color VanillaGold =
            new Color(0.96f, 0.82f, 0.48f, 1f);
        private static readonly Color HeaderBackground =
            new Color(0.21f, 0.21f, 0.21f, 0.9f);

        private sealed class IntegerEditor
        {
            internal readonly string ControlName;
            internal string Buffer;
            internal int LastValue = int.MinValue;
            internal bool WasFocused;

            internal IntegerEditor(string name)
            {
                ControlName = "ZCN.XpCustomizer.Biotech." + name;
            }

            internal void Reset()
            {
                Buffer = null;
                LastValue = int.MinValue;
                WasFocused = false;
            }
        }

        private sealed class GeneEditors
        {
            internal readonly IntegerEditor Aptitude;
            internal readonly IntegerEditor Complexity;
            internal readonly IntegerEditor Metabolism;

            internal GeneEditors(string name)
            {
                Aptitude = new IntegerEditor(name + ".Aptitude");
                Complexity = new IntegerEditor(name + ".Complexity");
                Metabolism = new IntegerEditor(name + ".Metabolism");
            }

            internal void Reset()
            {
                Aptitude.Reset();
                Complexity.Reset();
                Metabolism.Reset();
            }
        }

        private static readonly GeneEditors awful = new GeneEditors("Awful");
        private static readonly GeneEditors poor = new GeneEditors("Poor");
        private static readonly GeneEditors strong = new GeneEditors("Strong");
        private static readonly GeneEditors great = new GeneEditors("Great");

        /// <summary>
        /// Discard all staged editor strings after a change made outside
        /// this Biotech table. Never change settings or GeneDefs here.
        /// Do not call GUI.FocusControl from this notification: it may be
        /// invoked outside an active IMGUI event.
        /// </summary>
        internal static void NotifySettingsExternallyChanged()
        {
            awful.Reset();
            poor.Reset();
            strong.Reset();
            great.Reset();
        }

#if DEBUG
        /// <summary>
        /// Inject pending invalid text and focused-state into all 12 static
        /// editors, run the real refresh path, verify it cleared every one,
        /// and restore the prior UI state even if an assertion fails.
        /// Never reads/writes the live gameplay settings or GeneDefs.
        /// </summary>
        internal static int DebugVerifyExternalRefresh(Action refresh)
        {
            IntegerEditor[] editors = {
                awful.Aptitude, awful.Complexity, awful.Metabolism,
                poor.Aptitude, poor.Complexity, poor.Metabolism,
                strong.Aptitude, strong.Complexity, strong.Metabolism,
                great.Aptitude, great.Complexity, great.Metabolism
            };
            string[] beforeBuffers = new string[editors.Length];
            int[] beforeValues = new int[editors.Length];
            bool[] beforeFocus = new bool[editors.Length];
            for (int i = 0; i < editors.Length; i++)
            {
                beforeBuffers[i] = editors[i].Buffer;
                beforeValues[i] = editors[i].LastValue;
                beforeFocus[i] = editors[i].WasFocused;
            }
            try
            {
                for (int i = 0; i < editors.Length; i++)
                {
                    editors[i].Buffer = "-";
                    editors[i].LastValue = i;
                    editors[i].WasFocused = true;
                }
                refresh();
                for (int i = 0; i < editors.Length; i++)
                {
                    if (editors[i].Buffer != null ||
                        editors[i].LastValue != int.MinValue ||
                        editors[i].WasFocused)
                        throw new InvalidOperationException(
                            "[XPC-R6P0D1] stale Biotech editor: " + editors[i].ControlName);
                }
                return editors.Length;
            }
            finally
            {
                for (int i = 0; i < editors.Length; i++)
                {
                    editors[i].Buffer = beforeBuffers[i];
                    editors[i].LastValue = beforeValues[i];
                    editors[i].WasFocused = beforeFocus[i];
                }
            }
        }
#endif

        // N1: read-only validation before a profile navigation click removes
        // focus. The actual int commit still happens in DrawIntegerInput.
        internal static bool IsBiotechControl(string name)
        {
            return name != null && (
                String.Equals(name, awful.Aptitude.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, awful.Complexity.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, awful.Metabolism.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, poor.Aptitude.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, poor.Complexity.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, poor.Metabolism.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, strong.Aptitude.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, strong.Complexity.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, strong.Metabolism.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, great.Aptitude.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, great.Complexity.ControlName, StringComparison.Ordinal) ||
                String.Equals(name, great.Metabolism.ControlName, StringComparison.Ordinal));
        }

        private static bool ValidateNavigationEditor(IntegerEditor editor,
            string focusedName, int min, int max, out string reason)
        {
            reason = "NONE";
            if (!ProfileNumericNavigation.IsActive(editor.ControlName,
                editor.WasFocused, focusedName)) return true;
            if (ProfileNumericNavigation.ValidInteger(editor.Buffer, min, max, out reason))
                return true;
            ProfileNumericNavigation.RejectControl(editor.ControlName);
            return false;
        }

        internal static bool ValidateNavigationDraft(XpCustomizerSettings settings,
            string focusedName, out string reason)
        {
            reason = "NONE";
            if (settings == null) { reason = "MISSING_SETTINGS"; return false; }
            return ValidateNavigationEditor(awful.Aptitude, focusedName, -20, 0, out reason) &&
                ValidateNavigationEditor(awful.Complexity, focusedName, 0, 10, out reason) &&
                ValidateNavigationEditor(awful.Metabolism, focusedName, -5, 5, out reason) &&
                ValidateNavigationEditor(poor.Aptitude, focusedName, -20, 0, out reason) &&
                ValidateNavigationEditor(poor.Complexity, focusedName, 0, 10, out reason) &&
                ValidateNavigationEditor(poor.Metabolism, focusedName, -5, 5, out reason) &&
                ValidateNavigationEditor(strong.Aptitude, focusedName, 0, 20, out reason) &&
                ValidateNavigationEditor(strong.Complexity, focusedName, 0, 10, out reason) &&
                ValidateNavigationEditor(strong.Metabolism, focusedName, -5, 5, out reason) &&
                ValidateNavigationEditor(great.Aptitude, focusedName, 0, 20, out reason) &&
                ValidateNavigationEditor(great.Complexity, focusedName, 0, 10, out reason) &&
                ValidateNavigationEditor(great.Metabolism, focusedName, -5, 5, out reason);
        }

        public static void Draw(Listing_Standard listing)
        {
            if (!ModsConfig.BiotechActive)
            {
                SettingsUIHelpers.DrawSectionTitle(listing, "XPC_Biotech_Title".Translate().ToString());
                LogBiotechLocalizationOnce("DLC_INACTIVE");
                SettingsUIHelpers.DrawDescription(listing,
                    "XPC_Biotech_DlcInactive".Translate().ToString());
                listing.Gap(20f);
                return;
            }

            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            if (settings == null) return;

            LogBiotechLocalizationOnce("DLC_ACTIVE");
            if (!revisionLogged)
            {
                revisionLogged = true;
                Log.Message("[XP Customizer] BIOTECH_UI_REVISION=" + Revision +
                    "; layout=Attribute|Vanilla|Custom; numeric=staged-integer; " +
                    "fields=12; preset=preserved; externalBufferRefresh=R6P0D1; GeneDefs=existing-service; " +
                    "Harmony=unchanged");
            }

            SettingsUIHelpers.DrawSectionTitle(listing, "XPC_Biotech_SkillGenesTitle".Translate().ToString());
            SettingsUIHelpers.DrawDescription(listing,
                "XPC_Biotech_ComparisonDescription".Translate().ToString());
            SettingsUIHelpers.DrawReferenceText(listing,
                "XPC_Biotech_PassionUntouched".Translate().ToString());
            listing.Gap(8f);

            bool oldEnabled = settings.biotechCustomizationEnabled;
            bool enabled = oldEnabled;
            SettingsUIHelpers.DrawRightAdjacentToggle(listing, "XPC_Biotech_Enable".Translate().ToString(), ref enabled);
            settings.biotechCustomizationEnabled = enabled;
            if (enabled != oldEnabled) BiotechGeneService.ApplySettings();

            SettingsUIHelpers.DrawReferenceText(listing, enabled
                ? "XPC_Biotech_On".Translate().ToString()
                : "XPC_Biotech_Off".Translate().ToString());
            listing.Gap(10f);

            bool changed = false;
            bool guiEnabled = GUI.enabled;
            try
            {
                GUI.enabled = guiEnabled && enabled;
                changed |= DrawGeneBlock(listing, "XPC_Biotech_GeneAwful".Translate().ToString(), awful,
                    ref settings.biotechAwfulAptitude, -20, 0,
                    XpCustomizerDefaults.BiotechAwfulAptitude,
                    ref settings.biotechAwfulComplexity,
                    XpCustomizerDefaults.BiotechAwfulComplexity,
                    ref settings.biotechAwfulMetabolism,
                    XpCustomizerDefaults.BiotechAwfulMetabolism,
                    "XPC_Biotech_PassionRemove".Translate().ToString());
                listing.Gap(10f);
                changed |= DrawGeneBlock(listing, "XPC_Biotech_GenePoor".Translate().ToString(), poor,
                    ref settings.biotechPoorAptitude, -20, 0,
                    XpCustomizerDefaults.BiotechPoorAptitude,
                    ref settings.biotechPoorComplexity,
                    XpCustomizerDefaults.BiotechPoorComplexity,
                    ref settings.biotechPoorMetabolism,
                    XpCustomizerDefaults.BiotechPoorMetabolism,
                    "XPC_Biotech_PassionNone".Translate().ToString());
                listing.Gap(10f);
                changed |= DrawGeneBlock(listing, "XPC_Biotech_GeneStrong".Translate().ToString(), strong,
                    ref settings.biotechStrongAptitude, 0, 20,
                    XpCustomizerDefaults.BiotechStrongAptitude,
                    ref settings.biotechStrongComplexity,
                    XpCustomizerDefaults.BiotechStrongComplexity,
                    ref settings.biotechStrongMetabolism,
                    XpCustomizerDefaults.BiotechStrongMetabolism,
                    "XPC_Biotech_PassionNone".Translate().ToString());
                listing.Gap(10f);
                changed |= DrawGeneBlock(listing, "XPC_Biotech_GeneGreat".Translate().ToString(), great,
                    ref settings.biotechGreatAptitude, 0, 20,
                    XpCustomizerDefaults.BiotechGreatAptitude,
                    ref settings.biotechGreatComplexity,
                    XpCustomizerDefaults.BiotechGreatComplexity,
                    ref settings.biotechGreatMetabolism,
                    XpCustomizerDefaults.BiotechGreatMetabolism,
                    "XPC_Biotech_PassionAdd".Translate().ToString());
            }
            finally
            {
                GUI.enabled = guiEnabled;
            }

            if (changed && enabled) BiotechGeneService.ApplySettings();

            listing.Gap(12f);
            if (listing.ButtonText("XPC_Biotech_Reset".Translate().ToString()))
            {
                // The preset already invokes BiotechGeneService.ApplySettings().
                SettingsPresetService.ResetBiotechToVanilla();
                SettingsRefreshCoordinator.NotifyBiotechOnly(
                    SettingsRefreshReason.BiotechVanillaReset);
            }
            listing.Gap(20f);
        }

        private static bool DrawGeneBlock(Listing_Standard listing,
            string name, GeneEditors editors,
            ref int aptitude, int aptitudeMin, int aptitudeMax, int vanillaAptitude,
            ref int complexity, int vanillaComplexity,
            ref int metabolism, int vanillaMetabolism,
            string passionEffect)
        {
            GameFont formerFont = Text.Font;
            try
            {
                Text.Font = GameFont.Medium;
                listing.Label(name);
            }
            finally { Text.Font = formerFont; }

            DrawTableHeader(listing);
            bool changed = false;
            changed |= DrawTableRow(listing, "XPC_Biotech_Aptitude".Translate().ToString(), vanillaAptitude,
                ref aptitude, aptitudeMin, aptitudeMax, true, editors.Aptitude);
            changed |= DrawTableRow(listing, "XPC_Biotech_Complexity".Translate().ToString(), vanillaComplexity,
                ref complexity, 0, 10, false, editors.Complexity);
            changed |= DrawTableRow(listing, "XPC_Biotech_Metabolism".Translate().ToString(), vanillaMetabolism,
                ref metabolism, -5, 5, true, editors.Metabolism);
            SettingsUIHelpers.DrawReferenceText(listing,
                passionEffect);
            return changed;
        }

        private static void GetColumns(Rect row, out Rect attribute,
            out Rect vanilla, out Rect custom)
        {
            // One shared layout for headers and rows, so values cannot drift.
            float attributeWidth = row.width * 0.52f;
            float vanillaWidth = row.width * 0.21f;
            attribute = new Rect(row.x + 8f, row.y,
                Mathf.Max(0f, attributeWidth - 12f), row.height);
            vanilla = new Rect(row.x + attributeWidth, row.y,
                vanillaWidth, row.height);
            custom = new Rect(row.x + attributeWidth + vanillaWidth, row.y,
                Mathf.Max(0f, row.width - attributeWidth - vanillaWidth), row.height);
        }

        private static void DrawTableHeader(Listing_Standard listing)
        {
            Rect row = listing.GetRect(28f);
            Widgets.DrawBoxSolid(row, HeaderBackground);
            Rect attribute, vanilla, custom;
            GetColumns(row, out attribute, out vanilla, out custom);
            DrawText(attribute, "XPC_Biotech_Attribute".Translate().ToString(), Color.white, TextAnchor.MiddleLeft);
            DrawText(vanilla, "XPC_Biotech_Vanilla".Translate().ToString(), VanillaGold, TextAnchor.MiddleCenter);
            DrawText(custom, "XPC_Biotech_Custom".Translate().ToString(), Color.white, TextAnchor.MiddleCenter);
        }

        private static bool DrawTableRow(Listing_Standard listing, string label,
            int vanillaValue, ref int current, int min, int max,
            bool showPlus, IntegerEditor editor)
        {
            Rect row = listing.GetRect(33f);
            Rect attribute, vanilla, custom;
            GetColumns(row, out attribute, out vanilla, out custom);
            DrawText(attribute, label, Color.white, TextAnchor.MiddleLeft);
            DrawText(vanilla, FormatValue(vanillaValue, showPlus),
                VanillaGold, TextAnchor.MiddleCenter);

            // Keep input within its column at higher RimWorld UI scales.
            float fieldWidth = Mathf.Min(124f, Mathf.Max(60f, custom.width - 12f));
            Rect inputRect = new Rect(custom.x + (custom.width - fieldWidth) * 0.5f,
                row.y + 2f, fieldWidth, 28f);
            return DrawIntegerInput(inputRect, ref current, min, max, showPlus, editor);
        }

        private static void DrawText(Rect rect, string value, Color color,
            TextAnchor align)
        {
            Color previousColor = GUI.color;
            TextAnchor previousAnchor = Text.Anchor;
            try
            {
                GUI.color = color;
                Text.Anchor = align;
                Widgets.Label(rect, value);
            }
            finally
            {
                Text.Anchor = previousAnchor;
                GUI.color = previousColor;
            }
        }

        private static string FormatValue(int value, bool showPlus)
        {
            return showPlus && value > 0 ? "+" + value.ToString(CultureInfo.InvariantCulture)
                : value.ToString(CultureInfo.InvariantCulture);
        }

        internal static bool TryParseInteger(string text, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            return int.TryParse(text, NumberStyles.Integer,
                CultureInfo.CurrentCulture, out value) ||
                int.TryParse(text, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out value);
        }

        internal static int ResolveIntegerCommit(string raw, int previous,
            int min, int max)
        {
            int candidate;
            if (!TryParseInteger(raw, out candidate)) candidate = previous;
            return Mathf.Clamp(candidate, min, max);
        }

        private static bool DrawIntegerInput(Rect rect, ref int stored,
            int min, int max, bool showPlus, IntegerEditor editor)
        {
            if (!editor.WasFocused &&
                (editor.Buffer == null || editor.LastValue != stored))
                editor.Buffer = FormatValue(stored, showPlus);

            Event evt = Event.current;
            if (editor.WasFocused && evt != null && evt.type == EventType.KeyDown &&
                (evt.keyCode == KeyCode.Return ||
                 evt.keyCode == KeyCode.KeypadEnter) &&
                string.Equals(GUI.GetNameOfFocusedControl(),
                    editor.ControlName, StringComparison.Ordinal))
            {
                GUI.FocusControl(null);
                evt.Use();
            }

            GUI.SetNextControlName(editor.ControlName);
            editor.Buffer = Widgets.TextField(rect, editor.Buffer ?? string.Empty);
            bool focused = string.Equals(GUI.GetNameOfFocusedControl(),
                editor.ControlName, StringComparison.Ordinal);

            bool changed = false;
            int parsed;
            if (focused)
            {
                // Partial text ("-", "+", empty), malformed text and values
                // outside bounds remain visible without rewriting game settings.
                if (TryParseInteger(editor.Buffer, out parsed) &&
                    parsed >= min && parsed <= max && parsed != stored)
                {
                    stored = parsed;
                    changed = true;
                }
            }
            else if (editor.WasFocused)
            {
                int committed = ResolveIntegerCommit(editor.Buffer, stored, min, max);
                if (committed != stored)
                {
                    stored = committed;
                    changed = true;
                }
                editor.Buffer = FormatValue(committed, showPlus);
            }

            editor.LastValue = stored;
            editor.WasFocused = focused;
            return changed;
        }
    }
}
