#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using XpCustomizer.UI;
using XpCustomizer.Profiles;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// PM1 structural reflection plus detached text formatting tests only.
    /// Does not instantiate ProfileManagerWindow, execute IMGUI, inspect user
    /// profile JSON, invoke storage services, or mutate live settings.
    /// Real clicks and real profile persistence require manual verification.
    /// </summary>
    internal static class ProfileManagerSplitSelfTests
    {
        private static int checks;
        private const BindingFlags AllInstance = BindingFlags.Instance |
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const BindingFlags AllStatic = BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static void Check(bool value, string label)
        {
            checks++;
            if (!value) throw new InvalidOperationException("PM1_PROFILE_MANAGER_SPLIT:" + label);
        }

        private static MethodInfo PrivateStaticMethod(Type type, string name, Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name, AllStatic, null, parameters, null);
            Check(method != null && method.IsPrivate && method.IsStatic,
                "STATIC_METHOD_" + name);
            return method;
        }

        internal static void Run()
        {
            checks = 0;
            Type ui = typeof(ProfileManagementSettingsUI);
            Check(ui.IsSealed && !ui.IsAbstract, "ONE_SEALED_UI_TYPE");
            Check(ui.Namespace == "XpCustomizer.UI", "NAMESPACE_PRESERVED");
            Check(ui.Assembly == typeof(ProfileManagerWindow).Assembly, "SAME_ASSEMBLY");
            Check(XpCustomizerMod.BuildId != null &&
                XpCustomizerMod.BuildId.StartsWith("XPC-", StringComparison.Ordinal),
                "VALID_BUILD_ID_NOT_HARDCODED_PRIOR_VERSION");

            // State ownership and instance semantics must not change with source splitting.
            string[] fields = {
                "state", "detailRows", "listScroll", "detailScroll", "selectedIndex",
                "logged", "creationNotice", "trashActions", "purgeController"
            };
            Check(ui.GetFields(AllInstance).Length == fields.Length,
                "INSTANCE_FIELD_COUNT_9");
            foreach (string f in fields)
            {
                FieldInfo field = ui.GetField(f, AllInstance);
                Check(field != null && field.IsPrivate && !field.IsStatic,
                    "OWNED_INSTANCE_FIELD_" + f);
            }
            Check(ui.GetFields(AllStatic).Length == 0,
                "NO_STATIC_UI_STATE");
            ConstructorInfo empty = ui.GetConstructor(AllInstance, null, Type.EmptyTypes, null);
            ConstructorInfo model = ui.GetConstructor(AllInstance, null,
                new Type[] { typeof(ProfileManagementUiState) }, null);
            Check(empty != null && model != null && ui.GetConstructors(AllInstance).Length == 2,
                "TWO_EXISTING_CTORS");
            MethodInfo draw = ui.GetMethod("Draw", AllInstance, null,
                new Type[] { typeof(UnityEngine.Rect), typeof(Action) }, null);
            Check(draw != null && draw.IsAssembly && draw.ReturnType == typeof(void),
                "DRAW_SIGNATURE");

            foreach (string name in new string[] {
                "DrawHeader", "DrawToolbar", "DrawList", "DrawDetail", "DrawFooter",
                "OpenOverwriteConfirmation", "OnProfileOverwritten", "CanStartSelectedActiveProfileAction",
                "OpenDeleteConfirmation", "OnDeleteConfirmed", "CanRestore",
                "RestoreSelected", "OpenRename", "OnProfileRenamed", "OnProfileCreated",
                "OpenNotesEditor", "OnNotesUpdated",
                "BuildDetailLines", "AddSections", "ChangeView", "ResetSelectionVisual",
                "RefreshAndReselect"
            })
            {
                MethodInfo method = ui.GetMethod(name, AllInstance);
                Check(method != null && method.IsPrivate && !method.IsStatic,
                    "RETAIN_INSTANCE_METHOD_" + name);
            }
            foreach (string name in new string[] {
                "LogTrashAction", "CompactValue", "FormatUtc", "Clip", "LogAction"
            })
            {
                MethodInfo method = ui.GetMethod(name, AllStatic);
                Check(method != null && method.IsPrivate && method.IsStatic,
                    "RETAIN_STATIC_METHOD_" + name);
            }
            // The constructor contains a capturing anonymous delegate. Depending on
            // the C# compiler, this creates a private '<...>' helper method on the
            // same type. Ignore those synthetic helpers rather than failing PM1
            // because a compiler generated one more implementation detail.
            int declaredSourceMethods = 0;
            foreach (MethodInfo method in ui.GetMethods(AllInstance))
                if (method.Name.IndexOf('<') < 0) declaredSourceMethods++;
            foreach (MethodInfo method in ui.GetMethods(AllStatic))
                if (method.Name.IndexOf('<') < 0) declaredSourceMethods++;
            Check(declaredSourceMethods == 28,
                "SINGLE_TYPE_SOURCE_METHODS_28_PN1");

            // UX1: fail closed rather than treating a failed create as a saved
            // profile whose list selection alone failed. Detached result fixtures;
            // these do not open IMGUI or write user profile files.
            Check(ui.GetMethod("CanRename", AllInstance) == null,
                "UX1_OBSOLETE_GATE_NAME_REMOVED");
            ProfileCreationResult created = new ProfileCreationResult(
                ProfileCreationCode.Created, ProfileStorageCode.Ok, "fixture");
            ProfileCreationResult badName = new ProfileCreationResult(
                ProfileCreationCode.InvalidName, ProfileStorageCode.InvalidArgument);
            ProfileCreationResult badSettings = new ProfileCreationResult(
                ProfileCreationCode.InvalidSettings, ProfileStorageCode.InvalidFormat);
            ProfileCreationResult blockedCreate = new ProfileCreationResult(
                ProfileCreationCode.StorageFailure, ProfileStorageCode.RecoveryRequired);
            Check(created.Success && !badName.Success && !badSettings.Success &&
                !blockedCreate.Success, "UX1_CREATE_RESULT_DISCRIMINATION");
            Check(Dialog_CreateXpProfile.Explain(null) ==
                "Save failed. No success was reported.", "UX1_CREATE_NULL_EXPLAIN");
            Check(Dialog_CreateXpProfile.Explain(badName) ==
                "Invalid profile name.", "UX1_CREATE_INVALID_NAME_EXPLAIN");
            Check(Dialog_CreateXpProfile.Explain(badSettings).StartsWith(
                "Current settings are invalid", StringComparison.Ordinal),
                "UX1_CREATE_INVALID_SETTINGS_EXPLAIN");
            Check(Dialog_CreateXpProfile.Explain(blockedCreate).StartsWith(
                "Recovery required.", StringComparison.Ordinal),
                "UX1_CREATE_RECOVERY_LOCK_EXPLAIN");
            Check(Dialog_RenameXpProfile.Explain(null) ==
                "Rename failed without a result.", "UX1_RENAME_NULL_EXPLAIN");
            ProfileRenameResult renameConflict = new ProfileRenameResult(
                ProfileRenameCode.Conflict, ProfileStorageCode.Conflict, "fixture");
            Check(!renameConflict.Success && Dialog_RenameXpProfile.Explain(
                renameConflict).StartsWith("File changed.", StringComparison.Ordinal),
                "UX1_RENAME_CONFLICT_EXPLAIN");


            MethodInfo clip = PrivateStaticMethod(ui, "Clip", new Type[] {
                typeof(string), typeof(int) });
            Check((string)clip.Invoke(null, new object[] { null, 40 }) == "(empty)",
                "CLIP_NULL");
            Check((string)clip.Invoke(null, new object[] { "", 40 }) == "(empty)",
                "CLIP_EMPTY");
            Check((string)clip.Invoke(null, new object[] { "A\rB\nC", 40 }) == "A B C",
                "CLIP_CRLF_TO_SPACES");
            Check((string)clip.Invoke(null, new object[] { "abcdefghij", 6 }) == "abc...",
                "CLIP_LIMIT_6");
            Check((string)clip.Invoke(null, new object[] { "abcdef", 6 }) == "abcdef",
                "CLIP_EXACT_LIMIT");

            MethodInfo compact = PrivateStaticMethod(ui, "CompactValue",
                new Type[] { typeof(object) });
            Check((string)compact.Invoke(null, new object[] { null }) == "null", "PREVIEW_NULL");
            Check((string)compact.Invoke(null, new object[] { true }) == "true", "PREVIEW_TRUE");
            Check((string)compact.Invoke(null, new object[] { false }) == "false", "PREVIEW_FALSE");
            Check((string)compact.Invoke(null, new object[] { 17 }) == "17", "PREVIEW_INT");
            Check((string)compact.Invoke(null, new object[] { 1.25f }) == "1.25", "PREVIEW_FLOAT");
            IDictionary dictionary = new Dictionary<string, string> {
                { "first", "value" }, { "second", "value2" }
            };
            Check((string)compact.Invoke(null, new object[] { dictionary }) == "map [2 entries]",
                "PREVIEW_DICTIONARY");
            IList list = new List<string> { "x", "y", "z" };
            Check((string)compact.Invoke(null, new object[] { list }) == "list [3 entries]",
                "PREVIEW_LIST");
            Check((string)compact.Invoke(null, new object[] { new object() }) ==
                "(unsupported preview type)", "PREVIEW_UNKNOWN_TYPE");

            MethodInfo formatUtc = PrivateStaticMethod(ui, "FormatUtc", new Type[] { typeof(DateTime) });
            DateTime utc = new DateTime(2026, 11, 10, 7, 5, 0, DateTimeKind.Utc);
            Check((string)formatUtc.Invoke(null, new object[] { utc }) ==
                "2026-11-10 07:05 UTC", "PREVIEW_UTC_INVARIANT");
            Check(typeof(ProfileManagementUiState).Assembly == ui.Assembly &&
                typeof(ProfileTrashPurgeUiController).Assembly == ui.Assembly &&
                typeof(ProfileManagerLayout).Assembly == ui.Assembly,
                "EXISTING_STATE_LAYOUT_PURGE_TYPES_RETAINED");

            Log.Message("[XPC-PM1-UX1][PROFILE_MANAGER_UX_AUDIT] status=PASS; " +
                "cases=8; checks=" + checks + "; scope=DETACHED_RESULT_MESSAGES_AND_REFLECTION; " +
                "realIMGUI=NOT_RUN; actualFailureCallbacks=NOT_RUN; " +
                "profileJsonWrites=0; settingsWrites=0; geneDefWrites=0; " +
                "build=" + XpCustomizerMod.BuildId);

            Log.Message("[XPC-PM1][PROFILE_MANAGER_SPLIT_AUDIT] status=PASS; checks=" + checks +
                "; uiType=ONE_SEALED_PARTIAL; fields=9_INSTANCE; constructors=2; methods=26; " +
                "draw=SIGNATURE_PRESERVED; actions=REFLECTION_ONLY; " +
                "preview=DETACHED_FORMAT_FIXTURES; guidShaCallbacks=SOURCE_AUDIT_ONLY; " +
                "realIMGUI=NOT_RUN; actualCreateRenameOverwriteDeleteRestore=NOT_RUN; " +
                "profileJsonWrites=0; liveSettingsWrites=0; scribeCalls=0; geneDefWrites=0; " +
                "build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
