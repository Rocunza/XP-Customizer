using System;
using UnityEngine;
using Verse;

namespace XpCustomizer.UI
{
    /// <summary>
    /// Dedicated XP Customizer profile manager (read, create, rename, JSON overwrite, trash/restore/purge). Never uses
    /// WriteSettings/Scribe, changes GeneDefs, or applies a profile.
    /// A new state/model is owned per opening; old selections cannot leak.
    /// </summary>
    internal sealed class ProfileManagerWindow : Window
    {
        private static ProfileManagerWindow openWindow;
        private readonly ProfileManagementSettingsUI content =
            new ProfileManagementSettingsUI();

        public override Vector2 InitialSize
        {
            get
            {
                // RimWorld's Verse.UI.screenWidth/Height are UI-scaled logical pixels.
                return new Vector2(Mathf.Max(320f, Mathf.Min(960f, Verse.UI.screenWidth - 64f)),
                    Mathf.Max(300f, Mathf.Min(660f, Verse.UI.screenHeight - 64f)));
            }
        }

        internal ProfileManagerWindow()
        {
            doCloseX = true;
            closeOnCancel = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            draggable = true;
        }

        internal static bool OpenOnce()
        {
            if (openWindow != null)
            {
                Log.Message("[XPC-R6P1C15][PROFILE_WINDOW_ROUTE] action=OPEN; " +
                    "status=ALREADY_OPEN; windowsCreated=0; windowUiWrites=0; seedInvoked=False; build=" + XpCustomizerMod.BuildId);
                return false;
            }
            // Shared installer also serves users who open Manage before Quick Profiles.
            XpCustomizer.Profiles.BundledProfileInstaller.EnsureForCurrentUser();
            ProfileManagerWindow next = new ProfileManagerWindow();
            openWindow = next;
            try
            {
                Find.WindowStack.Add(next);
                Log.Message("[XPC-R6P1C15][PROFILE_WINDOW_ROUTE] action=OPEN; " +
                    "status=OK; windowsCreated=1; mode=READ_CREATE_RENAME_OVERWRITE_TRASH_PURGE_ONLY; windowUiWrites=0; bundleSeed=PREOPEN_SEPARATE; bundleSeedWrites=SEE_BUNDLED_SEED_LOG; " +
                    "profileApplyByManager=NONE; build=" + XpCustomizerMod.BuildId);
                return true;
            }
            catch (Exception ex)
            {
                openWindow = null;
                Log.Error("[XPC-R6P1C15][PROFILE_WINDOW_ROUTE] action=OPEN; " +
                    "status=FAIL; exception=" + ex.GetType().Name + "; build=" +
                    XpCustomizerMod.BuildId);
                return false;
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            content.Draw(inRect, delegate { Close(); });
        }

        public override void PostClose()
        {
            try
            {
                base.PostClose();
            }
            finally
            {
                if (object.ReferenceEquals(openWindow, this)) openWindow = null;
                Log.Message("[XPC-R6P1C15][PROFILE_WINDOW_ROUTE] action=CLOSE; " +
                    "status=OK; state=DISCARDED; closeWrites=0; profileApplyByManager=NONE; build=" +
                    XpCustomizerMod.BuildId);
            }
        }
    }
}
