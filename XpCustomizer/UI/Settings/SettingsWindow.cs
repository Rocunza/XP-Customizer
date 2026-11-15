using System;
using System.Collections.Generic;
using RimWorld;
using XpCustomizer.Profiles;
using UnityEngine;
using Verse;


namespace XpCustomizer.UI
{
    /// <summary>
    /// Draws the XP Customizer settings window.
    ///
    /// This class owns UI state only:
    /// - selected tab
    /// - scroll positions
    /// - scroll heights
    ///
    /// Gameplay calculations do not belong here.
    /// </summary>
    internal sealed partial class SettingsWindow
    {
        private SettingsTab currentTab = SettingsTab.Progression;

        private Vector2 progressionScrollPosition = Vector2.zero;
        private Vector2 learningScrollPosition = Vector2.zero;
        private Vector2 consScrollPosition = Vector2.zero;
        private Vector2 gwsScrollPosition = Vector2.zero;
        private Vector2 decayScrollPosition = Vector2.zero;
        private Vector2 biotechScrollPosition = Vector2.zero;

        private float progressionViewHeight = 700f;
        private float learningViewHeight = 300f;
        private float consViewHeight = 700f;
        private float gwsViewHeight = 1000f;
        private float decayViewHeight = 1100f;
        private float biotechViewHeight = 1200f;

        private readonly List<TabRecord> tabs =
            new List<TabRecord>();

        // UI revision is logged once when the settings window is drawn.
        private bool consTabRevisionLogged;

        // Constructed before XpCustomizerMod.GetSettings; these constructors
        // store state/callbacks only and must not inspect game or mod settings.
        private readonly SettingsProfileNavigationController profileNavigation =
            new SettingsProfileNavigationController();
        private readonly SettingsResetController resetController;

        internal SettingsWindow()
        {
#if DEBUG
            resetController = new SettingsResetController(profileNavigation, DisarmD4LiveAudit);
#else
            resetController = new SettingsResetController(profileNavigation, null);
#endif
        }


        // ================================================================
        // Main window
        // ================================================================

        public void Draw(Rect inRect)
        {
            if (!consTabRevisionLogged)
            {
                consTabRevisionLogged = true;
                // S1 version beacon is present in both Debug and Release.
                // Purely diagnostic: never resets editors or changes navigation.
                Log.Message("[XPC-N1-R2R3] version=" +
                    SettingsNumericRecoveryController.Version +
                    "; stage=S1; action=INSTALLED; owners=15" +
                    "; recoveryMode=R1_CAREER+S2_SKILLTRAINER; allOtherOwners=DEFERRED" +
                    "; settingsWrites=0; profileWrites=0; build=" +
                    XpCustomizerMod.BuildId);
                Log.Message("[XPC-N1-R2R3] version=" +
                    SettingsNumericRecoveryController.S2Version +
                    "; stage=S2; action=INSTALLED" +
                    "; capability=SKILLTRAINER_SINGLE_MULTIPLIER_ONLY" +
                    "; careerR1=PRESERVED; otherOwners=DEFERRED" +
                    "; settingsWrites=0; profileWrites=0; build=" +
                    XpCustomizerMod.BuildId);
#if DEBUG
                const string upcomingTabs = "Insp/Combat DEBUG_ONLY";
#else
                const string upcomingTabs = "Insp/Combat HIDDEN";
#endif
                Log.Message("[XP Customizer] UI_REVISION=CONS-GWS-TOGGLES-PHASE4BR4-R1-20261103; " +
                    "curveSource=ANCHOR_V2_ONLY; sourceSelector=REMOVED; legacyUI=REMOVED; " +
                    "lowHighModeButtons=underToggle; V2Preview=perSide; ConsCheckboxes=adjacent-right; GwsCheckboxes=adjacent-right; lowColor=GWS-0.87,0.64,0.44; highColor=GWS-0.62,0.80,1.00; " +
                    "ConsReset=threeProfiles-preserveSwitches; ConsFurther=V2-baseTimesMultiplier-plusMthPp; FurtherUi=" + ConsciousnessFurtherLearningSettingsUI.Revision + "; FurtherPreview=CurrentOrCustom-NoSettingsWrites-MentalExcluded; ConsPreview=High12Samples-6ColumnsAdaptive-Low4Unchanged-FurtherPostCapAndAnchor; ConsNumericInput=staged-until-focusout-or-Enter; GWSDefaultSocial=OFF; GWS=V2-variable-anchors; " + upcomingTabs);
                Log.Message("[XP Customizer] UI_REVISION=GWS-R4-HIGH-DIMINISHING-X115-LIMIT-X130-20261103; " +
                    "source=UI+presets; presets=Cons+GWS-ModDefaults-keep-switches-Vanilla-masterOFF; MTH-gameplay=unchanged; Cons=ACTIVE; " +
                    "GWS=ACTIVE; GwsCheckboxes=adjacent-right; SkillGrid=" + GwsSettingsUI.SkillGridRevision + "; defaultExpanded=True; topOfGWS=True; layout=3x4; " +
                    "gwsPreview=Low30+50+65+80-High120+135+140+155+160+175+180+200+300+500+750+1000+selectedAnchors; gwsPreviewLayout=Low1x4+High2x6_NarrowAdaptive; gwsAnchors=editable-30to95+110to1000; gwsNumeric=staged-commit; gwsPreset=HighDiminishing+x1.15-at-180+Limit1.30+Safety2; savedOverrides=preserved; gameplay=V1-formulas-variable-anchor; " + upcomingTabs + "; globalReset=preserved; " +
                    "tabOrder=Custom XP>Learning>Decay>Biotech>Cons>GWS>Insp>Combat; profileEntry=QUICK_PROFILES+MANAGE_BUTTONS; " +
                    "distributionColumns=Share+Vanilla Share+Custom XP+Vanilla XP; distributionHeaderFont=Small; " +
                    "vanillaColor=0.96,0.82,0.48; comparisons=neutral");
#if DEBUG
                // L3-A: once per settings window instance, not on each IMGUI frame.
                // Translation output is diagnostic evidence, not an automatic QA PASS.
                Log.Message("[XPC-L10N-L3A][KEYED_SETTINGS] version=XPC-L10N-L3A-SETTINGS-SHELL-20261112-R1; " +
                    "screen=SETTINGS; keys=23; tabKey=XPC_L3_TabProgression; tabText=" +
                    "XPC_L3_TabProgression".Translate().ToString() + "; build=" + XpCustomizerMod.BuildId);
#endif
            }

            // TabDrawer draws tabs 32 pixels above its base rect.
            // Move the panel down so the tabs remain inside inRect.
            Rect panelRect = new Rect(
                inRect.x,
                inRect.y + TabDrawer.TabHeight,
                inRect.width,
                inRect.height - TabDrawer.TabHeight);

            Widgets.DrawMenuSection(panelRect);

            DrawTabs(panelRect);
            profileNavigation.CancelIfTabChanged(currentTab);

            Rect innerRect =
                panelRect.ContractedBy(10f);


            // ------------------------------------------------------------
            // Fixed global-reset footer
            // ------------------------------------------------------------

            // Debug Biotech D4 gets a separate utility row. The one global
            // install-default reset is centered without changing owned footer space.
            float resetFooterHeight = 30f;
#if DEBUG
            if (currentTab == SettingsTab.Biotech) resetFooterHeight = 68f;
#endif
            Rect resetRect = new Rect(innerRect.x,
                innerRect.yMax - 30f, innerRect.width, 30f);
            float defaultsButtonWidth = Mathf.Min(320f, resetRect.width);
            Rect defaultsResetRect = new Rect(
                resetRect.x + (resetRect.width - defaultsButtonWidth) * 0.5f,
                resetRect.y, defaultsButtonWidth, 30f);

            // Whole-settings tools live in their own owned utility row.
            // Do not draw into Dialog_ModSettings' parent-owned title/X area.
            Rect utilityRect = new Rect(innerRect.x, innerRect.y,
                innerRect.width, 32f);
            Rect quickRect;
            Rect manageRect;
            QuickProfileToolbarLayout.Calculate(utilityRect, out quickRect, out manageRect);
            TooltipHandler.TipRegion(quickRect,
                "XPC_L3_QuickProfilesTip".Translate().ToString());
            if (Widgets.ButtonText(quickRect,
                quickRect.width >= 160f ? "XPC_L3_QuickProfilesFull".Translate().ToString() : "XPC_L3_QuickProfilesShort".Translate().ToString()))
                profileNavigation.Request(ProfileNavigationTarget.Quick, currentTab);
            TooltipHandler.TipRegion(manageRect,
                "XPC_L3_ManageProfilesTip".Translate().ToString());
            if (Widgets.ButtonText(manageRect,
                QuickProfileToolbarLayout.ManageButtonLabel(manageRect.width)))
                profileNavigation.Request(ProfileNavigationTarget.Manage, currentTab);

            Rect contentRect = innerRect;
            contentRect.yMin = utilityRect.yMax + 8f;
            contentRect.yMax = innerRect.yMax - resetFooterHeight - 8f;


            // ------------------------------------------------------------
            // Selected tab
            // ------------------------------------------------------------

            switch (currentTab)
            {
                case SettingsTab.Progression:
                    DrawScrollableTab(
                        contentRect,
                        ref progressionScrollPosition,
                        ref progressionViewHeight,
                        ProgressionSettingsUI.Draw);
                    break;

                case SettingsTab.Learning:
                    DrawScrollableTab(
                        contentRect,
                        ref learningScrollPosition,
                        ref learningViewHeight,
                        LearningSettingsUI.Draw);
                    break;

                case SettingsTab.Cons:
                    DrawScrollableTab(
                        contentRect,
                        ref consScrollPosition,
                        ref consViewHeight,
                        ConsciousnessSettingsUI.Draw);
                    break;

                case SettingsTab.GWS:
                    DrawScrollableTab(contentRect, ref gwsScrollPosition,
                        ref gwsViewHeight, GwsSettingsUI.Draw);
                    break;

#if DEBUG
                case SettingsTab.Insp:
                    DrawScrollableTab(contentRect, ref inspScrollPosition,
                        ref inspViewHeight, DrawInspComingSoon);
                    break;

                case SettingsTab.Combat:
                    DrawScrollableTab(contentRect, ref combatScrollPosition,
                        ref combatViewHeight, DrawCombatComingSoon);
                    break;
#endif

                case SettingsTab.Decay:
                    DrawScrollableTab(contentRect, ref decayScrollPosition,
                        ref decayViewHeight, DecaySettingsUI.Draw);
                    break;

                case SettingsTab.Biotech:
                    DrawScrollableTab(
                        contentRect,
                        ref biotechScrollPosition,
                        ref biotechViewHeight,
                        BiotechSettingsUI.Draw);
                    break;

            }


            // ------------------------------------------------------------
            // Global reset / opt-in live diagnostic in Debug only.
            // Preserve the Release layout and the existing reset behavior.
            // ------------------------------------------------------------
#if DEBUG
            DrawD4LiveAuditRow(innerRect, resetRect);
#endif
            // R4: remove ONLY the old global numeric-reset entry point.
            // R3-A/R3-B policies stay compiled for historical diagnostics,
            // but no longer have a visible button or a public UI path.
            TooltipHandler.TipRegion(defaultsResetRect,
                "XPC_L3_ResetDefaultsTip".Translate().ToString());
            if (Widgets.ButtonText(defaultsResetRect, "XPC_L3_ResetDefaultsLabel".Translate().ToString()))
                resetController.RequestVanillaResetConfirmation();

            profileNavigation.Complete(currentTab);
        }

        // ================================================================
        // Tabs
        // ================================================================

        private void DrawTabs(Rect panelRect)
        {
            tabs.Clear();

            tabs.Add(
                new TabRecord(
                    "XPC_L3_TabProgression".Translate().ToString(),
                    delegate
                    {
                        currentTab =
                            SettingsTab.Progression;
                    },
                    currentTab ==
                    SettingsTab.Progression));

            tabs.Add(
                new TabRecord(
                    "XPC_L3_TabLearning".Translate().ToString(),
                    delegate
                    {
                        currentTab =
                            SettingsTab.Learning;
                    },
                    currentTab ==
                    SettingsTab.Learning));

            // UI display order only. Keep SettingsTab enum values and all
            // handlers intact so existing selection state is unaffected.
            tabs.Add(
                new TabRecord(
                    "XPC_L3_TabDecay".Translate().ToString(),
                    delegate { currentTab = SettingsTab.Decay; },
                    currentTab == SettingsTab.Decay));

            tabs.Add(
                new TabRecord(
                    "XPC_L3_TabBiotech".Translate().ToString(),
                    delegate
                    {
                        currentTab =
                            SettingsTab.Biotech;
                    },
                    currentTab ==
                    SettingsTab.Biotech));

            tabs.Add(
                new TabRecord(
                    "XPC_L3_TabCons".Translate().ToString(),
                    delegate { currentTab = SettingsTab.Cons; },
                    currentTab == SettingsTab.Cons));

            tabs.Add(new TabRecord("XPC_L3_TabGWS".Translate().ToString(),
                delegate { currentTab = SettingsTab.GWS; },
                currentTab == SettingsTab.GWS));
#if DEBUG
            // Unreleased planned features remain discoverable in Debug only.
            // Do not advertise placeholder tabs in Workshop/Release builds.
            tabs.Add(new TabRecord("XPC_L3_TabInsp".Translate().ToString(),
                delegate { currentTab = SettingsTab.Insp; },
                currentTab == SettingsTab.Insp));
            tabs.Add(new TabRecord("XPC_L3_TabCombat".Translate().ToString(),
                delegate { currentTab = SettingsTab.Combat; },
                currentTab == SettingsTab.Combat));
#endif

            TabDrawer.DrawTabs(
                panelRect,
                tabs);
        }


        // ================================================================
        // Generic scroll-view wrapper
        // ================================================================

        private static void DrawScrollableTab(
            Rect outRect,
            ref Vector2 scrollPosition,
            ref float viewHeight,
            Action<Listing_Standard> drawContents)
        {
            Rect viewRect = new Rect(
                0f,
                0f,
                outRect.width - 16f,
                viewHeight);

            Widgets.BeginScrollView(
                outRect,
                ref scrollPosition,
                viewRect);

            Listing_Standard listing =
                new Listing_Standard();

            // Important:
            // never allow Listing_Standard to create another column.
            listing.maxOneColumn = true;

            listing.Begin(viewRect);

            drawContents(listing);

            float contentHeight =
                listing.CurHeight;

            listing.End();

            Widgets.EndScrollView();

            viewHeight =
                Mathf.Max(
                    contentHeight + 20f,
                    outRect.height);
        }

    }
}