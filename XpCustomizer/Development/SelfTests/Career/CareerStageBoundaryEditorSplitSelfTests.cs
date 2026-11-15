#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using XpCustomizer.Profiles;
using XpCustomizer.UI;

namespace XpCustomizer
{
    /// <summary>
    /// CBE1: detached draft/commands checks + read-only reflection of UI.
    /// The only shared-state intervention is a short-lived editorOpen probe,
    /// restored in finally. No real settings, IMGUI, files or GeneDefs touched.
    /// </summary>
    internal static class CareerStageBoundaryEditorSplitSelfTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.Static |
            BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static int checks;

        private static void Check(bool condition, string label)
        {
            checks++;
            if (!condition)
                throw new InvalidOperationException("CBE1_SPLIT:" + label);
        }

        private static void EqualIntArray(int[] actual, int[] expected, string label)
        {
            Check(actual != null && actual.Length == expected.Length, label + "_LENGTH");
            for (int i = 0; i < expected.Length; i++)
                Check(actual[i] == expected[i], label + "_INDEX_" + i);
        }

        private static void EqualWeights(List<float> values, int count, float expected,
            string label)
        {
            Check(values != null && values.Count == count, label + "_COUNT");
            for (int i = 0; i < count; i++)
                Check(values[i] == expected, label + "_INDEX_" + i);
        }

        private static void CheckSuggestion(int[] source, int suggestion, string label)
        {
            CareerStageBoundaryDraft draft = new CareerStageBoundaryDraft();
            draft.ReplaceFromNormalized(source);
            Check(draft.TryAppendSuggested(), label + "_APPENDED");
            Check(draft.Count == source.Length + 1, label + "_COUNT");
            Check(draft[draft.Count - 1] == suggestion, label + "_EXPECTED");
        }

        internal static void Run()
        {
            checks = 0;
            Type ui = typeof(CareerXpStageBoundaryEditorUI);
            Type draftType = typeof(CareerStageBoundaryDraft);
            Type commandType = typeof(CareerStageBoundarySettingsCommands);
            Check(ui.IsAbstract && ui.IsSealed, "ONE_STATIC_UI_CLASS");
            Check(ui.FullName == "XpCustomizer.UI.CareerXpStageBoundaryEditorUI",
                "UI_FULL_NAME_UNCHANGED");
            Check(draftType.IsSealed && !draftType.IsAbstract,
                "DRAFT_INDEPENDENT_SEALED_CLASS");
            Check(commandType.IsAbstract && commandType.IsSealed,
                "COMMANDS_INDEPENDENT_STATIC_CLASS");
            Check(ui.Assembly == draftType.Assembly &&
                ui.Assembly == commandType.Assembly, "SAME_ASSEMBLY");
            Check(XpCustomizerMod.BuildId != null &&
                XpCustomizerMod.BuildId.StartsWith("XPC-", StringComparison.Ordinal),
                "BUILD_ID_FAMILY_NOT_OLD_EXACT_ID");

            FieldInfo openField = ui.GetField("editorOpen", PrivateStatic);
            FieldInfo inputsField = ui.GetField("stageBoundaryInputs", PrivateStatic);
            FieldInfo draftField = ui.GetField("stageBoundaryDraft", PrivateStatic);
            Check(openField != null && openField.IsPrivate &&
                openField.FieldType == typeof(bool) && !openField.IsInitOnly,
                "READINESS_EDITOR_OPEN_FIELD");
            Check(inputsField != null && inputsField.IsPrivate &&
                inputsField.IsInitOnly &&
                inputsField.FieldType == typeof(List<NumericInputState>),
                "READINESS_INPUTS_HOLDER_FIELD");
            Check(draftField != null && draftField.IsPrivate && draftField.IsInitOnly &&
                draftField.FieldType == draftType,
                "DRAFT_SINGLE_OWNER_UI_FIELD");
            Check(inputsField.GetValue(null) != null &&
                draftField.GetValue(null) != null, "STATIC_FIELDS_INITIALIZED");
            Check(ProfileEditorReadinessService.KnownSourceCount == 15,
                "READINESS_15_SOURCES_PRESERVED");

            MethodInfo draw = ui.GetMethod("Draw", BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic, null,
                new Type[] { typeof(Listing_Standard), typeof(XpCustomizerSettings),
                    typeof(int[]), typeof(bool).MakeByRefType() }, null);
            Check(draw != null && draw.IsPublic && draw.ReturnType == typeof(bool),
                "PUBLIC_DRAW_SIGNATURE");
            MethodInfo notify = ui.GetMethod("NotifySettingsExternallyChanged",
                BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
            Check(notify != null && notify.ReturnType == typeof(void),
                "EXTERNAL_REFRESH_ENTRY");
            foreach (string name in new string[] { "DrawSplitRows", "GetSplitRowRects",
                "DrawActionButtons", "OpenEditor", "CloseEditor", "SetDraft",
                "EnsureInputStates", "ApplyDraft", "ResetStageWeights",
                "FormatBoundaries" })
                Check(ui.GetMethod(name, PrivateStatic) != null,
                    "RETAINED_UI_METHOD_" + name);
            Check(ui.GetMethod("FindSuggestedSplitLevel", PrivateStatic) == null &&
                ui.GetMethod("FindFirstUnusedSplit", PrivateStatic) == null,
                "SUGGESTIONS_MOVED_OUT_OF_UI");

            // D01-D04: independent detached raw edits, normalize only when asked.
            List<int> source = new List<int> { 19, 3, 7, 7, 0, 20, -5, 14, 17, 21 };
            CareerStageBoundaryDraft draft = new CareerStageBoundaryDraft();
            draft.ReplaceFromNormalized(source);
            EqualIntArray(draft.GetNormalizedCopy(), new int[] { 3, 7, 14, 17, 19 },
                "NORMALIZE_INVALID_DUPLICATE_AND_SORT");
            source[0] = 5;
            Check(draft[4] == 19, "DETACHED_FROM_SOURCE_LIST");
            draft[0] = 18;
            Check(draft[0] == 18 && draft[1] == 7 && draft[4] == 19,
                "RAW_ORDER_NOT_SORTED_ON_EDIT");
            int[] snap = draft.GetNormalizedCopy();
            snap[0] = 99;
            Check(draft[0] == 18 && draft[1] == 7,
                "NORMALIZED_COPY_DOES_NOT_EXPOSE_RAW");
            draft.RemoveAt(1);
            Check(draft.Count == 4 && draft[0] == 18 && draft[1] == 14,
                "REMOVE_RAW_PRESERVES_REMAINING_ORDER");
            draft.Clear();
            EqualIntArray(draft.GetNormalizedCopy(), new int[0], "EMPTY_ONE_STAGE_ALLOWED");
            draft.AddRaw(7);
            draft.AddRaw(3);
            draft.AddRaw(7);
            Check(draft[0] == 7 && draft[1] == 3 && draft[2] == 7,
                "DUPLICATES_REMAIN_RAW_UNTIL_APPLY");
            EqualIntArray(draft.GetNormalizedCopy(), new int[] { 3, 7 },
                "APPLY_NORMALIZES_RAW_DUPLICATES");

            // D05-D06: original strict-maximum-gap suggestion oracle and raw count gate.
            CheckSuggestion(new int[0], 10, "SUGGEST_EMPTY");
            CheckSuggestion(new int[] { 10 }, 5, "SUGGEST_MID");
            CheckSuggestion(new int[] { 5, 10, 15 }, 2, "SUGGEST_TIE_FIRST");
            CheckSuggestion(new int[] { 3, 7, 14, 17, 19 }, 10,
                "SUGGEST_RECOMMENDED");
            CheckSuggestion(new int[] { 19 }, 9, "SUGGEST_LATE");
            CareerStageBoundaryDraft full = new CareerStageBoundaryDraft();
            for (int i = 0; i < 19; i++) full.AddRaw(7);
            Check(!full.TryAppendSuggested() && full.Count == 19,
                "RAW_19_ROWS_BLOCK_ADD_EVEN_WITH_DUPLICATES");
            CareerStageBoundaryDraft eighteen = new CareerStageBoundaryDraft();
            for (int level = 1; level <= 18; level++) eighteen.AddRaw(level);
            Check(eighteen.TryAppendSuggested() && eighteen.Count == 19 &&
                eighteen[18] == 19, "EIGHTEEN_ROWS_APPEND_LAST_UNUSED");

            // D07: analogue of row/list paired remove; live IMGUI remains manual.
            List<NumericInputState> detachedInputs = new List<NumericInputState> {
                new NumericInputState(), new NumericInputState(), new NumericInputState()
            };
            NumericInputState last = detachedInputs[2];
            detachedInputs.RemoveAt(1);
            Check(detachedInputs.Count == 2 &&
                object.ReferenceEquals(detachedInputs[1], last),
                "ROW_REMOVE_RETAIN_SURVIVING_INPUT_REFERENCE_POLICY");

            // S01-S02: applies same boundaries, still resets both weights and locks.
            XpCustomizerSettings settings = new XpCustomizerSettings();
            settings.careerXpSplitLevels = new List<int> { 3, 7 };
            settings.careerXpStageWeights = new List<float> { 8f, 6f, 4f };
            settings.careerXpStageLockedShares = new List<float> { 0.3f, -1f, -1f };
            settings.careerXpMultiplier = 2.75f;
            settings.careerXpUseCustomDistribution = false;
            Dictionary<string, float> untouchedDict = settings.skillSpecificXpMultipliers;
            List<int> oldSplits = settings.careerXpSplitLevels;
            List<float> oldWeights = settings.careerXpStageWeights;
            List<float> oldLocks = settings.careerXpStageLockedShares;
            CareerStageBoundarySettingsCommands.ApplyNormalizedBoundaries(settings,
                draft.GetNormalizedCopy());
            EqualIntArray(settings.careerXpSplitLevels.ToArray(), new int[] { 3, 7 },
                "APPLY_NORMALIZED_SPLITS");
            EqualWeights(settings.careerXpStageWeights, 3, 1f, "APPLY_WEIGHTS_ALL_ONE");
            EqualWeights(settings.careerXpStageLockedShares, 3, -1f,
                "APPLY_LOCKS_ALL_UNLOCKED");
            Check(!object.ReferenceEquals(oldSplits, settings.careerXpSplitLevels) &&
                !object.ReferenceEquals(oldWeights, settings.careerXpStageWeights) &&
                !object.ReferenceEquals(oldLocks, settings.careerXpStageLockedShares),
                "SAME_BOUNDARY_APPLY_IS_NOT_NOOP");
            Check(settings.careerXpMultiplier == 2.75f &&
                settings.careerXpUseCustomDistribution == false &&
                object.ReferenceEquals(untouchedDict, settings.skillSpecificXpMultipliers),
                "UNRELATED_SETTINGS_PRESERVED");

            // S03-S04: Reset uses CONFIRMED splits, preserves locks and draft.
            settings.careerXpSplitLevels = new List<int> { 3, 7, 10, 14, 17 };
            settings.careerXpStageWeights = new List<float> { 2f, 2f, 2f, 2f, 2f, 2f };
            List<int> beforeResetSplits = settings.careerXpSplitLevels;
            List<float> beforeResetLocks = settings.careerXpStageLockedShares;
            int rawBeforeReset = draft.Count;
            CareerStageBoundarySettingsCommands.ResetWeights(settings, 6);
            EqualWeights(settings.careerXpStageWeights, 6, 1f, "RESET_USES_CONFIRMED_STAGE_COUNT");
            Check(object.ReferenceEquals(beforeResetSplits, settings.careerXpSplitLevels) &&
                object.ReferenceEquals(beforeResetLocks, settings.careerXpStageLockedShares) &&
                draft.Count == rawBeforeReset,
                "RESET_PRESERVES_SPLITS_LOCKS_AND_DRAFT");
            CareerStageBoundarySettingsCommands.ResetWeights(null, 2);
            Check(true, "RESET_NULL_SETTINGS_NO_THROW");

            // R01-R04: actual readiness path, restore shared flag even on failure.
            // Never modify user's draft/input list, never invoke IMGUI or storage.
            bool previousOpen = (bool)openField.GetValue(null);
            try
            {
                openField.SetValue(null, true);
                ProfileEditorReadinessResult pending = ProfileEditorReadinessService.Check();
                Check(pending.Code == ProfileEditorReadinessCode.PendingEdit &&
                    pending.Source == "CareerStageDraft",
                    "OPEN_EDITOR_PENDING_EVEN_WITHOUT_INPUT");
                Check(pending.CoveredGroups == 15,
                    "OPEN_EDITOR_ALL_SOURCES_STILL_OBSERVED");
            }
            finally
            {
                openField.SetValue(null, previousOpen);
            }
            Check((bool)openField.GetValue(null) == previousOpen,
                "ORIGINAL_EDITOR_OPEN_RESTORED");

            Log.Message("[XPC-CBE1][CAREER_BOUNDARY_SPLIT_AUDIT] status=PASS; checks=" +
                checks + "; splitOracles=5; editorOpenReadiness=REAL_READ_ONLY_PROBE" +
                "; detachedCommands=APPLY_AND_RESET; fieldContract=PRESERVED" +
                "; liveIMGUI=NOT_RUN; actualClicks=NOT_RUN; actualXmlSave=NOT_RUN" +
                "; realSettingsWrites=0; geneDefWrites=0; scribeCalls=0" +
                "; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
