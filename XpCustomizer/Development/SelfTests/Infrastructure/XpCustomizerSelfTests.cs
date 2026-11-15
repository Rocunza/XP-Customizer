#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.FurtherLearning;
using XpCustomizer.BookXp;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// Lightweight development-time regression tests.
    ///
    /// These tests are compiled and executed only in Debug builds.
    /// They intentionally avoid any external test-framework dependency so
    /// the normal RimWorld mod project can validate pure calculation code
    /// during development.
    ///
    /// A failed assertion throws immediately during mod initialization.
    /// Release builds contain neither the runner call nor this class body.
    /// </summary>
    internal static partial class XpCustomizerSelfTests
    {
        // D2-D1A: manual real runtime profile apply fails closed if ANY startup suite fails.
        internal static bool DebugStartupCompleteAndPassed { get; private set; }
        private const float AnchorTolerance = 0.01f;
        private const float MonotonicTolerance = 0.001f;

        public static void Run()
        {
            SettingsSplitR1SelfTests.Run();
            SettingsWindowSplitSelfTests.Run();
            SkillTooltipSplitSelfTests.Run();
            SkillTooltipServiceSplitSelfTests.Run();
            TestConsciousnessFurtherLearningV2();
            TestTooltipSourceRatioUiR5F3();
            TestTooltipNeutralVisibilityR5F4();
            CareerXpSelfTests.Run();
            CareerXpDistributionSplitSelfTests.Run();
            CareerProgressionSettingsUISplitSelfTests.Run();
            CareerStageBoundaryEditorSplitSelfTests.Run();
            LearningCapacitySelfTests.Run();
            LearningCapacitySplitSelfTests.Run();
            LearningCapacitySettingsUISplitSelfTests.Run();
            BookXpSelfTests.Run();
            GwsSelfTests.Run();
            BiotechUiSelfTests.Run();
            TestFurtherLearningBaseFactorPrecision();
            TestFurtherLearningMentalThresholdCurve();
            ConsciousnessAnchorCurveSelfTests.Run();
            ConsciousnessPhase4BR2SelfTests.Run();
            TestSkilltrainerXpNoSharedMutation();
            TestSkilltrainerSpecificXpS2();
            SettingsPersistenceSelfTests.Run();
            SettingsSnapshotSelfTests.Run();
            SettingsApplySelfTests.Run();
            SettingsRefreshSelfTests.Run();
            SettingsRuntimeApplySelfTests.Run();
            SettingsRuntimeApplySplitSelfTests.Run();
            SettingsRuntimeRecoverySelfTests.Run();
            RuntimeRecoverySessionGateSelfTests.Run();
            SettingsPersistenceGuardSelfTests.Run();
            ProfileApplyPersistenceContractSelfTests.Run();
            ProfileXmlPersistenceEvidenceSelfTests.Run();
            ModXmlRoundTripSelfTests.Run();
            DetachedXmlStageSelfTests.Run();
            DetachedXmlSemanticSelfTests.Run();
            OwnedXmlEvidenceSplitSelfTests.Run();
            DetachedVerseLoadSelfTests.Run();
            DetachedLogQueueSelfTests.Run();
            DetachedNonDefaultVectorSelfTests.Run();
            DetachedTempFileTransactionSelfTests.Run();
            SettingsLiveRecoveryProbe.RunGateSelfTest();
            ProfileSchemaSelfTests.Run();
            ProfileStorageSelfTests.Run();
            ProfileNotesSelfTests.Run();
            Profiles.BundledProfileNotesLocalization.RunSelfAudit();
            BundledProfileBP12SelfTests.Run();
            BundledProfileBP4SelfTests.Run();
            ProfileTrashPurgeSelfTests.Run();
            ProfileManagementUiSelfTests.Run();
            ProfileManagerWindowSelfTests.Run();
            ProfileManagerSplitSelfTests.Run();
            QuickProfileMenuSelfTests.Run();
            ProfileCreationSelfTests.Run();
            ProfileRenameSelfTests.Run();
            ProfileEditorReadinessSelfTests.Run();
            ProfileNumericNavigationN1SelfTests.Run();
            CareerNumericRecoveryR1SelfTests.Run();
            NumericRecoveryR2S1SelfTests.Run();
            SkilltrainerNumericRecoveryS2SelfTests.Run();
            ToggleAlignmentU1SelfTests.Run();
            SettingsUIHelpersSplitSelfTests.Run();
            NumericRowLayoutSelfTests.Run();
            SettingsUIHelpersSafetySelfTests.Run();
            GlobalVanillaResetR1SelfTests.Run();
            GlobalNumericDefaultsR3ASelfTests.Run();
            GlobalNumericResetR3BSelfTests.Run();
            LinearCurveDefaultsL1SelfTests.Run();
            LegacyPresetRetirementH1SelfTests.Run();
            DiagnosticLogSemanticsQ1SelfTests.Run();
            ProfileUiPolishP1SelfTests.Run();
            ProfileOverwritePreflightSelfTests.Run();
            ProfileApplyPreflightSelfTests.Run();
            ProfileApplyGateSelfTests.Run();
            ProfileApplyBridgeSelfTests.Run();
            ProfileDebugLiveApplySelfTests.Run();
            ProfileDebugPersistWriteGateSelfTests.Run();
            ProfileProductionF2SelfTests.Run();
            DecaySelfTests.Run();
            SkillSpecificXpSelfTests.Run();

            Log.Message(
                "[XP Customizer] DEBUG self-tests passed: TOOLTIP-NEUTRAL-R5F4; " +
                "Consciousness Further Learning V2 multiplier/pp/order/default/isolation; Career XP distribution/rounding; " +
                "Learning Capacity V4 Auto/Custom; Book XP V1 pure; GWS V1 pure; Biotech UI R1 numeric pure; Further Learning base precision; Further Learning Mental Threshold curve; " +
                "Cons Anchor V2 pure curves (single source); " +
                "Cons Phase 4B-R2 V2-only gameplay, independent profiles, neutral/off gates, " +
                "preview, presets, Further/GWS isolation, P4BR6 numeric entry validation; " +
                "Skilltrainer XP adjustment pure/no-shared-mutation (S6-A); " +
                "Skilltrainer per-skill payout (SKILLTRAINER-S2); " +
                "Settings migration invariants (R2-A); Cons/GWS preset isolation (R1); Decay pure regression (DECAY-D3); Skill-specific XP (SKILLXP-S1); Settings snapshot isolation/contract (R6-P0-A/B); Settings candidate/apply/rollback (R6-P0-C); UI buffer refresh fan-out/12 Biotech editors (R6-P0-D1); runtime apply dispatch on detached fake effects (R6-P0-D2); runtime data/reference + fake external rollback (R6-P0-D3); D4 read-only live audit after defs, real transitions MANUAL Debug-only; R6-P1-A strict portable profile schema/codec/mapper in-memory only; R6-P1-B isolated temp disk repository/atomic replace/soft delete; R6-P1-C1 read-only Profiles UI state/list/cache/conflict/trash; R6-P1-C1.5 separate Profile Manager window and responsive layout (pure); real IMGUI manual; R6-P1-C1.6 quick-selector read-only menu and stale-file checks (pure + isolated temp); clicks manual; R6-P1-C2-A create-only UI/service with isolated temp persistence; game clicks manual; R6-P1-C2-B1 metadata-only rename with SHA concurrency, backups and session recovery gate (isolated disk; clicks manual); R6-P1-C2-B1.5 quick profile list only, without utility actions/rank numbers (isolated data; FloatMenu manual); R6-P1-C2-B2-A preflight editor readiness/readonly detached candidate, fail closed on untracked numeric focus; no overwrite route; R6-P1-D1 read-only apply review, detached import and dry confirmation only; R6-P1-D2-A1 one-shot consent gate isolated read-only audit; R6-P1-D2-B1 detached apply bridge/fake transaction+rollback audit; R6-P1-D2-B2-A isolated sticky runtime recovery lock; R6-P1-D2-B2-B blocked XML write and settings UI on unrecovered live failure, pure isolated policy audit; R6-P1-D2-C1 read-only persistence boundary contract (pure fake XML evidence, no Scribe); R6-P1-D2-C2 isolated temp XML fault evidence and compensation policy (no real Scribe); R6-P1-D2-C3-B pure XML observer parser tests (real XML readback startup/save handled separately, reload manual; no diagnostic writes); production Apply F2 isolated regression with captured logging; U1 adjacent toggle pure geometry (live IMGUI manual); R1 Vanilla reset confirmation pure one-shot/cancel gate (live IMGUI manual); R3-A global mod numeric values detached preserve/restore/locks/unknown keys (live IMGUI manual); H1 retired preset and independent Cons section-default regression (detached); Q1 legacy-versus-production diagnostic log scope (reflection-only); D2-D1A Debug-only two-click runtime Apply and isolated write latch; D2-D1B Debug-only single owned-XML vanilla writer permit and full raw semantic readback (manual); R6P1C2D isolated Empty Trash purge safety and partial-failure stop (real clicks MANUAL). ");
            DebugStartupCompleteAndPassed = true;
        }

        internal static void AssertTrueForSkillXp(bool condition, string label)
        {
            AssertTrue(condition, "SKILLXP-S1 " + label);
        }

        private static void AssertNear(
            float actual,
            float expected,
            float tolerance,
            string label)
        {
            if (Math.Abs(actual - expected) > tolerance)
            {
                Fail(
                    label + " expected " + expected.ToString("0.###") +
                    " but got " + actual.ToString("0.###") +
                    " (tolerance " + tolerance.ToString("0.###") + ").");
            }
        }


        private static void AssertTrue(
            bool condition,
            string message)
        {
            if (!condition)
            {
                Fail(message);
            }
        }


        private static void Fail(
            string message)
        {
            throw new InvalidOperationException(
                "[XP Customizer] DEBUG self-test failed: " + message);
        }
    }
}
#endif
