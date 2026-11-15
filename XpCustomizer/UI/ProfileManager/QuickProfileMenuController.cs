using System;
using System.Collections.Generic;
using System.Globalization;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    /// <summary>
    /// R6-P1-C2-B1.5: minimal read-only quick profile selector.
    /// Menu contains only saved profiles, except a single disabled diagnostic
    /// when the list is empty or unavailable. Damaged rows remain disabled.
    /// Listing and selection are read-only. A selected profile opens the
    /// separate production review dialog, which requires explicit Apply consent.
    /// A fresh controller is constructed at each explicit menu opening.
    /// </summary>
    internal sealed class QuickProfileMenuController
    {
        internal const string Revision = "R6P1C2B15-MINIMAL-QUICKMENU-20261107";
        private readonly ProfileManagementUiState state;

        internal QuickProfileMenuController()
            : this(new ProfileManagementUiState()) { }

        internal QuickProfileMenuController(ProfileManagementUiState model)
        {
            if (model == null) throw new ArgumentNullException("model");
            state = model;
        }

        internal IList<ProfileStorageEntry> Entries { get { return state.VisibleEntries; } }
        internal ProfileStorageCode ListingCode { get { return state.ListingCode; } }
        internal ProfileDocument Selected { get { return state.Selected; } }
        internal string SelectedHash { get { return state.SelectedHash; } }
        internal int ListReads { get { return state.ActiveListReads; } }
        internal int LoadReads { get { return state.LoadReads; } }

        internal void Refresh()
        {
            // Refresh is an explicit menu-open action, never a per-frame/render action.
            // Dedicated quick selector is always an Active-only model.
            state.Refresh();
        }

        internal ProfileStorageCode Select(int row)
        {
            // Performs Load + SHA-256 match against the listed snapshot.
            // Never passes the returned document to the runtime apply service.
            return state.Select(row);
        }

        internal static string FormatRow(ProfileStorageEntry row)
        {
            if (row == null || row.Result == null) return "! Unknown profile row";
            if (!row.Result.Success)
                return "! " + Clip(row.FileName, 30) + "  (" +
                    ProfileManagementUiState.Explain(row.Result.Code) + ")";
            ProfileDocument doc = row.Result.Document;
            if (doc == null) return "! Invalid profile record";
            return Clip(doc.DisplayName, 32) + "  [" +
                FormatUpdatedAt(doc.UpdatedUtc) + "]";
        }

        // Convert only for display. The persisted profile remains UTC and
        // the existing repository order continues to use its original timestamp.
        internal static string FormatUpdatedAt(DateTime updatedUtc)
        {
            return updatedUtc.ToLocalTime().ToString("yyMMdd HH:mm", CultureInfo.InvariantCulture);
        }

        private static string Clip(string text, int max)
        {
            if (String.IsNullOrEmpty(text)) return "(unnamed)";
            // Restrict menu layout; preserve the full name in the profile JSON.
            string safe = text.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            return safe.Length > max ? safe.Substring(0, max - 3) + "..." : safe;
        }

        internal static void OpenMenu()
        {
            // Explicit user action only. Safe one-time import precedes listing.
            BundledProfileInstaller.EnsureForCurrentUser();
            QuickProfileMenuController controller = new QuickProfileMenuController();
            controller.Refresh();
            controller.Display();
        }

        // This view model is deliberately independent of FloatMenu rendering.
        // SourceIndex maps to the cached Active listing, not to a display order
        // or a user-supplied name. -1 is a disabled informational/invalid row.
        internal sealed class MenuChoice
        {
            internal readonly string Label;
            internal readonly int SourceIndex;

            internal MenuChoice(string label, int sourceIndex)
            {
                Label = label;
                SourceIndex = sourceIndex;
            }

            internal bool Selectable { get { return SourceIndex >= 0; } }
        }

        internal IList<MenuChoice> BuildChoices()
        {
            List<MenuChoice> choices = new List<MenuChoice>();
            // A failed listing is never treated as authoritative, even if it
            // contains partially enumerated entries.
            if (ListingCode != ProfileStorageCode.Ok)
            {
                choices.Add(new MenuChoice("! Cannot read profiles: " +
                    ProfileManagementUiState.Explain(ListingCode), -1));
                return choices;
            }

            IList<ProfileStorageEntry> rows = Entries;
            if (rows.Count == 0)
            {
                // No actions here: Manage Profiles already has its own toolbar button.
                choices.Add(new MenuChoice("No saved profiles. Use Manage Profiles to create one.", -1));
                return choices;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                ProfileStorageEntry entry = rows[i];
                bool valid = entry != null && entry.Result != null &&
                    entry.Result.Success && entry.Result.Document != null;
                choices.Add(new MenuChoice(FormatRow(entry), valid ? i : -1));
            }
            return choices;
        }

        private void Display()
        {
            IList<MenuChoice> choices = BuildChoices();
            List<FloatMenuOption> options = new List<FloatMenuOption>(choices.Count);
            int selectableCount = 0;
            for (int i = 0; i < choices.Count; i++)
            {
                MenuChoice choice = choices[i];
                Action action = null;
                if (choice.Selectable)
                {
                    int selectedIndex = choice.SourceIndex;
                    action = delegate { SelectAndShow(selectedIndex); };
                    selectableCount++;
                }
                // BuildChoices/FormatRow are compatibility contracts checked by self-tests.
                // Only the final FloatMenu presentation is localized; source indexes are untouched.
                string label = choice.Label;
                if (ListingCode != ProfileStorageCode.Ok)
                    label = string.Format("XPC_Profile_QuickListingFailure".Translate().ToString(), ProfileManagementUiState.LocalizedExplain(ListingCode));
                else if (Entries.Count == 0)
                    label = "XPC_Profile_QuickEmpty".Translate().ToString();
                else if (!choice.Selectable && i < Entries.Count)
                {
                    ProfileStorageEntry invalid = Entries[i];
                    if (invalid == null || invalid.Result == null)
                        label = "XPC_Profile_QuickUnknown".Translate().ToString();
                    else if (invalid.Result.Success && invalid.Result.Document == null)
                        label = "XPC_Profile_QuickInvalid".Translate().ToString();
                    else
                        label = string.Format("XPC_Profile_QuickInvalidRow".Translate().ToString(),
                            Clip(invalid.FileName, 30), ProfileManagementUiState.LocalizedExplain(invalid.Result.Code));
                }
                options.Add(new FloatMenuOption(label, action));
            }

            try
            {
                Find.WindowStack.Add(new FloatMenu(options));
                Log.Message("[XPC-R6P1C2B15][QUICK_MENU_ACTION] action=OPEN; status=" + ListingCode +
                    "; listingRows=" + Entries.Count + "; selectable=" + selectableCount +
                    "; menuOptions=" + choices.Count + "; nonProfileActions=0; " +
                    "source=ACTIVE_ONLY; readOnly=True; directSettingsWrites=0; " +
                    "geneDefWrites=0; scribeCalls=0; applyAction=REVIEW_WINDOW_ONLY; revision=" + Revision +
                    "; build=" + XpCustomizerMod.BuildId);
            }
            catch (Exception ex)
            {
                Log.Warning("[XPC-R6P1C2B15][QUICK_MENU_ACTION] action=OPEN; status=FAIL; " +
                    "exception=" + ex.GetType().Name + "; applyAction=REVIEW_WINDOW_ONLY; build=" +
                    XpCustomizerMod.BuildId);
            }
        }

        private void SelectAndShow(int row)
        {
            ProfileStorageCode result = Select(row);
            if (result != ProfileStorageCode.Ok || Selected == null)
            {
                LogAction("SELECT", result);
                Messages.Message(string.Format("XPC_Profile_QuickVerifyFailed".Translate().ToString(), ProfileManagementUiState.LocalizedExplain(result)),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            LogAction("SELECT", ProfileStorageCode.Ok);
            ProfileApplyPreflightService service = ProfileApplyPreflightService.ForCurrentUser();
            ProfileApplyReviewResult reviewed = service.Open(Selected, SelectedHash,
                XpCustomizerMod.Settings);
            if (!reviewed.Success || reviewed.Session == null)
            {
                Log.Message("[XPC-R6P1D1][PROFILE_APPLY_UI] action=REJECT; status=" +
                    reviewed.Code + "; applyByMenuAction=NO; fileWritesByD1=0; " +
                    "build=" + XpCustomizerMod.BuildId);
                Messages.Message(string.Format("XPC_Profile_QuickReviewFailed".Translate().ToString(), ProfileManagementUiState.LocalizedReviewReason(reviewed.Code)),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            try
            {
                Find.WindowStack.Add(new Dialog_ReviewXpProfileApply(reviewed.Session));
                Log.Message("[XPC-R6P1D1][PROFILE_APPLY_UI] action=OPEN; status=" +
                    reviewed.Code + "; applyByMenuAction=NO; fileWritesByD1=0; " +
                    "build=" + XpCustomizerMod.BuildId);
            }
            catch (Exception ex)
            {
                Log.Warning("[XPC-R6P1D1][PROFILE_APPLY_UI] action=OPEN; status=FAIL; " +
                    "exception=" + ex.GetType().Name + "; applyByMenuAction=NO; build=" +
                    XpCustomizerMod.BuildId);
                Messages.Message("XPC_Profile_QuickWindowFailed".Translate().ToString(),
                    MessageTypeDefOf.RejectInput, false);
            }
        }

        private static void LogAction(string action, ProfileStorageCode status)
        {
            // No absolute Config paths, user profile contents or GUIDs in logs.
            Log.Message("[XPC-R6P1C2B15][QUICK_MENU_ACTION] action=" + action +
                "; status=" + status + "; mode=SELECTION_ONLY; " +
                "directSettingsWrites=0; geneDefWrites=0; scribeCalls=0; applyAction=REVIEW_WINDOW_ONLY; build=" +
                XpCustomizerMod.BuildId);
        }
    }
    /// <summary>Pure layout shared by the real toolbar and Debug geometry audit.</summary>
    internal static class QuickProfileToolbarLayout
    {
        internal const string Revision = "R6P1C16-OWNED-UTILITY-TOOLBAR-20261107";

        // Measure actual UI font, avoiding clipping the explicit Profile name
        // on narrower layouts. This is a presentation choice only.
        internal static string ManageButtonLabel(float width)
        {
            string full = "XPC_L3_ManageProfilesFull".Translate().ToString();
            string medium = "XPC_L3_ManageProfilesMedium".Translate().ToString();
            string shortLabel = "XPC_L3_ManageProfilesShort".Translate().ToString();
            if (Text.CalcSize(full).x + 16f <= width)
                return full;
            if (Text.CalcSize(medium).x + 16f <= width)
                return medium;
            return shortLabel;
        }

        internal static void Calculate(Rect utility, out Rect quick, out Rect manage)
        {
            const float gap = 8f;
            float manageWidth = Mathf.Min(152f, utility.width * 0.40f);
            float quickWidth = Mathf.Min(202f,
                Mathf.Max(0f, utility.width - manageWidth - gap));
            manage = new Rect(utility.xMax - manageWidth, utility.y, manageWidth, utility.height);
            quick = new Rect(manage.x - gap - quickWidth, utility.y, quickWidth, utility.height);
        }
    }

}
