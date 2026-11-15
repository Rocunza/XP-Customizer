using System;
using System.Collections;
using System.Collections.Generic;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    /// <summary>
    /// R6-P1-C1 view model. Only repository List/ListTrash/Load are reachable.
    /// No Create, Update, SoftDelete, Restore or live settings apply paths.
    /// All disk reads are explicit (initial visit, view switch, Refresh, selection).
    /// </summary>
    internal sealed class ProfileManagementUiState
    {
        internal const string Revision = "R6P1C1-READONLY-PROFILES-20261106";
        private readonly Func<ProfileFileRepository> repositoryFactory;
        private ProfileFileRepository repository;
        private ProfileStorageListing active;
        private ProfileStorageListing trash;
        private static readonly ProfileStorageEntry[] EmptyEntries = new ProfileStorageEntry[0];
        private IList<ProfileStorageEntry> activeRows = EmptyEntries;
        private IList<ProfileStorageEntry> trashRows = EmptyEntries;
        private bool inTrash;
        private ProfileDocument selected;
        private string selectedName;
        private string selectedHash;
        private ProfileStorageCode selectedError;
        private bool hasErrorSelection;
        private string status = "Profiles are read-only in this phase. No game settings will be applied.";
        // Display-only side channel. Existing Status and Explain contracts remain English.
        private enum DisplayStatusKind
        {
            Initial, Refreshed, ListFailed, ListUnavailable, SelectionGone,
            FileUnavailable, TrashPreview, SelectionFailed, ExternalChange,
            ActivePreview, ReadFailed
        }
        private DisplayStatusKind displayStatusKind = DisplayStatusKind.Initial;
        private ProfileStorageCode lastListingCode = ProfileStorageCode.Ok;

        internal int ActiveListReads { get; private set; }
        internal int TrashListReads { get; private set; }
        internal int LoadReads { get; private set; }
        internal bool InTrash { get { return inTrash; } }
        internal string Status { get { return status; } }
        internal string DisplayStatus
        {
            get
            {
                switch (displayStatusKind)
                {
                    case DisplayStatusKind.Refreshed: return "XPC_Profile_StatusRefreshed".Translate().ToString();
                    case DisplayStatusKind.ListFailed: return string.Format("XPC_Profile_StatusListFailed".Translate().ToString(), LocalizedExplain(lastListingCode));
                    case DisplayStatusKind.ListUnavailable: return "XPC_Profile_StatusListUnavailable".Translate().ToString();
                    case DisplayStatusKind.SelectionGone: return "XPC_Profile_StatusSelectionGone".Translate().ToString();
                    case DisplayStatusKind.FileUnavailable: return string.Format("XPC_Profile_StatusFileUnavailable".Translate().ToString(), LocalizedExplain(selectedError));
                    case DisplayStatusKind.TrashPreview: return "XPC_Profile_StatusTrashPreview".Translate().ToString();
                    case DisplayStatusKind.SelectionFailed: return string.Format("XPC_Profile_StatusSelectionFailed".Translate().ToString(), LocalizedExplain(selectedError));
                    case DisplayStatusKind.ExternalChange: return "XPC_Profile_StatusExternalChange".Translate().ToString();
                    case DisplayStatusKind.ActivePreview: return "XPC_Profile_StatusActivePreview".Translate().ToString();
                    case DisplayStatusKind.ReadFailed: return "XPC_Profile_StatusReadFailure".Translate().ToString();
                    default: return "XPC_Profile_InitialStatus".Translate().ToString();
                }
            }
        }
        internal ProfileStorageCode ListingCode { get { return lastListingCode; } }
        internal ProfileDocument Selected { get { return selected; } }
        internal string SelectedName { get { return selectedName; } }
        internal string SelectedHash { get { return selectedHash; } }
        internal bool HasErrorSelection { get { return hasErrorSelection; } }
        internal ProfileStorageCode SelectedError { get { return selectedError; } }

        internal ProfileManagementUiState()
            : this(delegate { return ProfileFileRepository.ForCurrentUser(); }) { }

        internal ProfileManagementUiState(Func<ProfileFileRepository> factory)
        {
            if (factory == null) throw new ArgumentNullException("factory");
            repositoryFactory = factory;
        }

        internal void EnsureLoaded()
        {
            if (inTrash ? trash == null : active == null) Refresh();
        }

        internal IList<ProfileStorageEntry> VisibleEntries
        {
            get
            {
                return inTrash ? trashRows : activeRows;
            }
        }

        internal void SetTrashView(bool showTrash)
        {
            if (inTrash == showTrash) { EnsureLoaded(); return; }
            inTrash = showTrash;
            ClearSelection();
            EnsureLoaded();
        }

        internal void Refresh()
        {
            ClearSelection();
            try
            {
                if (repository == null) repository = repositoryFactory();
                if (inTrash)
                {
                    TrashListReads++;
                    trash = repository.ListTrash();
                    trashRows = trash.Entries.AsReadOnly();
                    lastListingCode = trash.Code;
                }
                else
                {
                    ActiveListReads++;
                    active = repository.List();
                    activeRows = active.Entries.AsReadOnly();
                    lastListingCode = active.Code;
                }
                status = lastListingCode == ProfileStorageCode.Ok
                    ? "List refreshed. Select a row to inspect it; no settings are applied."
                    : "Listing failed: " + Explain(lastListingCode) + ". Refresh after resolving the issue.";
                displayStatusKind = lastListingCode == ProfileStorageCode.Ok
                    ? DisplayStatusKind.Refreshed : DisplayStatusKind.ListFailed;
            }
            catch (Exception)
            {
                lastListingCode = ProfileStorageCode.IoError;
                if (inTrash)
                {
                    trash = new ProfileStorageListing(lastListingCode, new List<ProfileStorageEntry>());
                    trashRows = EmptyEntries;
                }
                else
                {
                    active = new ProfileStorageListing(lastListingCode, new List<ProfileStorageEntry>());
                    activeRows = EmptyEntries;
                }
                status = "Unable to read profile listing. Check configuration-folder access.";
                displayStatusKind = DisplayStatusKind.ListUnavailable;
            }
        }

        // Only accepts a row from the cached listing; avoids constructing paths from UI text.
        internal ProfileStorageCode Select(int index)
        {
            IList<ProfileStorageEntry> entries = VisibleEntries;
            if (index < 0 || index >= entries.Count)
            {
                ClearSelection();
                status = "The selected item is no longer in the displayed list. Refresh.";
                displayStatusKind = DisplayStatusKind.SelectionGone;
                return ProfileStorageCode.NotFound;
            }
            ProfileStorageEntry entry = entries[index];
            ClearSelection();
            selectedName = entry.FileName;
            if (!entry.Result.Success)
            {
                selectedError = entry.Result.Code;
                hasErrorSelection = true;
                status = "File unavailable: " + Explain(selectedError) + ". No file was modified.";
                displayStatusKind = DisplayStatusKind.FileUnavailable;
                return selectedError;
            }
            if (inTrash)
            {
                // There is deliberately no LoadTrash API. The already validated
                // listing snapshot is sufficient for read-only deleted-item details.
                selected = entry.Result.Document;
                selectedHash = entry.Result.ContentHash;
                status = "Deleted profile preview (snapshot). Nothing was restored.";
                displayStatusKind = DisplayStatusKind.TrashPreview;
                return ProfileStorageCode.Ok;
            }
            try
            {
                if (repository == null) repository = repositoryFactory();
                LoadReads++;
                ProfileStorageResult loaded = repository.Load(entry.Result.Document.ProfileId);
                if (!loaded.Success)
                {
                    hasErrorSelection = true;
                    selectedError = loaded.Code;
                    status = "Profile changed or could not be opened: " + Explain(loaded.Code) + ". Refresh.";
                    displayStatusKind = DisplayStatusKind.SelectionFailed;
                    return loaded.Code;
                }
                if (loaded.ContentHash != entry.Result.ContentHash)
                {
                    hasErrorSelection = true;
                    selectedError = ProfileStorageCode.Conflict;
                    status = "Profile was modified outside this window. Refresh before selecting again.";
                    displayStatusKind = DisplayStatusKind.ExternalChange;
                    return ProfileStorageCode.Conflict;
                }
                selected = loaded.Document;
                selectedHash = loaded.ContentHash;
                status = "Read-only profile preview. No game settings were changed.";
                displayStatusKind = DisplayStatusKind.ActivePreview;
                return ProfileStorageCode.Ok;
            }
            catch (Exception)
            {
                hasErrorSelection = true;
                selectedError = ProfileStorageCode.IoError;
                status = "Could not read selected profile. Refresh after checking file access.";
                displayStatusKind = DisplayStatusKind.ReadFailed;
                return selectedError;
            }
        }

        private void ClearSelection()
        {
            selected = null;
            selectedName = null;
            selectedHash = null;
            hasErrorSelection = false;
            selectedError = ProfileStorageCode.Ok;
        }


        internal static string LocalizedExplain(ProfileStorageCode code)
        {
            switch (code)
            {
                case ProfileStorageCode.Ok: return "XPC_Profile_Valid".Translate().ToString();
                case ProfileStorageCode.NotFound: return "XPC_Profile_NotFound".Translate().ToString();
                case ProfileStorageCode.AlreadyExists: return "XPC_Profile_AlreadyExists".Translate().ToString();
                case ProfileStorageCode.Conflict: return "XPC_Profile_Conflict".Translate().ToString();
                case ProfileStorageCode.InvalidArgument: return "XPC_Profile_InvalidArgument".Translate().ToString();
                case ProfileStorageCode.InvalidFormat: return "XPC_Profile_InvalidFormat".Translate().ToString();
                case ProfileStorageCode.UnsupportedVersion: return "XPC_Profile_UnsupportedVersion".Translate().ToString();
                case ProfileStorageCode.IdMismatch: return "XPC_Profile_IdMismatch".Translate().ToString();
                case ProfileStorageCode.FileTooLarge: return "XPC_Profile_FileTooLarge".Translate().ToString();
                case ProfileStorageCode.SecurityRejected: return "XPC_Profile_SecurityRejected".Translate().ToString();
                case ProfileStorageCode.AtomicReplaceUnavailable: return "XPC_Profile_AtomicReplaceUnavailable".Translate().ToString();
                case ProfileStorageCode.IoError: return "XPC_Profile_IoError".Translate().ToString();
                case ProfileStorageCode.RecoveryRequired: return "XPC_Profile_RecoveryRequired".Translate().ToString();
                case ProfileStorageCode.InjectedFailure: return "XPC_Profile_InjectedFailure".Translate().ToString();
                default: return "XPC_Profile_UnknownStorage".Translate().ToString();
            }
        }

        internal static string LocalizedReviewReason(ProfileApplyReviewCode code)
        {
            switch (code)
            {
                case ProfileApplyReviewCode.InvalidSelection: return "XPC_Profile_Review_InvalidSelection".Translate().ToString();
                case ProfileApplyReviewCode.Conflict: return "XPC_Profile_Review_Conflict".Translate().ToString();
                case ProfileApplyReviewCode.PendingEdit: return "XPC_Profile_Review_PendingEdit".Translate().ToString();
                case ProfileApplyReviewCode.Uncertain: return "XPC_Profile_Review_Uncertain".Translate().ToString();
                case ProfileApplyReviewCode.InvalidProfile: return "XPC_Profile_Review_InvalidProfile".Translate().ToString();
                case ProfileApplyReviewCode.InvalidSettings: return "XPC_Profile_Review_InvalidSettings".Translate().ToString();
                case ProfileApplyReviewCode.StorageFailure: return "XPC_Profile_Review_StorageFailure".Translate().ToString();
                case ProfileApplyReviewCode.RecoveryRequired: return "XPC_Profile_Review_RecoveryRequired".Translate().ToString();
                case ProfileApplyReviewCode.InternalFailure: return "XPC_Profile_Review_InternalFailure".Translate().ToString();
                case ProfileApplyReviewCode.Cancelled: return "XPC_Profile_Review_Cancelled".Translate().ToString();
                default: return "XPC_Profile_Review_Default".Translate().ToString();
            }
        }

        internal static string LocalizedPreviewValue(object value, string invariantText)
        {
            // Numeric and stored JSON/DTO keys retain invariant formatting and identity.
            if (value == null) return "XPC_Profile_ValueNull".Translate().ToString();
            if (value is bool) return (bool)value ? "XPC_Profile_ValueTrue".Translate().ToString() : "XPC_Profile_ValueFalse".Translate().ToString();
            IDictionary dict = value as IDictionary;
            if (dict != null) return string.Format("XPC_Profile_ValueMap".Translate().ToString(), dict.Count);
            IList list = value as IList;
            if (list != null) return string.Format("XPC_Profile_ValueList".Translate().ToString(), list.Count);
            return value is int || value is float
                ? invariantText : "XPC_Profile_ValueUnsupported".Translate().ToString();
        }

        internal static string Explain(ProfileStorageCode code)
        {
            switch (code)
            {
                case ProfileStorageCode.Ok: return "Valid profile";
                case ProfileStorageCode.NotFound: return "File not found";
                case ProfileStorageCode.AlreadyExists: return "Profile ID already exists";
                case ProfileStorageCode.Conflict: return "File changed since listing";
                case ProfileStorageCode.InvalidArgument: return "Invalid profile identifier";
                case ProfileStorageCode.InvalidFormat: return "Damaged or invalid JSON/UTF-8";
                case ProfileStorageCode.UnsupportedVersion: return "Unsupported profile version";
                case ProfileStorageCode.IdMismatch: return "Filename and document ID mismatch";
                case ProfileStorageCode.FileTooLarge: return "Profile exceeds file-size limit";
                case ProfileStorageCode.SecurityRejected: return "Unsafe file path rejected";
                case ProfileStorageCode.AtomicReplaceUnavailable: return "Safe file replacement unavailable";
                case ProfileStorageCode.IoError: return "Disk access failed";
                case ProfileStorageCode.RecoveryRequired: return "Manual recovery may be needed";
                case ProfileStorageCode.InjectedFailure: return "Test fault";
                default: return "Unknown storage result";
            }
        }
    }
}
