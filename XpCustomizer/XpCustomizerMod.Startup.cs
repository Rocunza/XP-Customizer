using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.Progression;
using XpCustomizer.Biotech;
using XpCustomizer.UI;
using XpCustomizer.BookXp;
using XpCustomizer.Profiles;
using XpCustomizer.Compatibility;

namespace XpCustomizer
{
    public partial class XpCustomizerMod
    {

        // ================================================================
        // Initialization
        // ================================================================

        public XpCustomizerMod(
            ModContentPack content)
            : base(content)
        {
            Settings =
                GetSettings<XpCustomizerSettings>();

            // BP12: configure bundled profile path only; do not write user files on startup.
            BundledProfileInstaller.Configure(content.RootDir);

#if DEBUG
            const string buildConfiguration = "Debug";
#else
            const string buildConfiguration = "Release";
#endif
            Log.Message("[XP Customizer] BUILD_ID=" + BuildId + "; configuration=" +
                buildConfiguration + "; assembly=" + typeof(XpCustomizerMod).Assembly.GetName().Name +
                "; version=" + typeof(XpCustomizerMod).Assembly.GetName().Version +
                "; auditSchema=DECAY-D4-MAP-TARGET+MTH-V1+LC-V4+SKILLTRAINER-S2+BOOKXP-V1+GWS-V1+CONS-P4BR2+CONS-GWS-UI-P4BR4+GWS-SKILLGRID-P4BR5+CONS-NUMERIC-P4BR6+GWS-SOCIAL-DEFAULT-P4BR6+GWS-ANCHOR-V2-R1+CONS-HIGH135-UI-R7+LC-CONS-R7-HF1+BIOTECH-UI-R1+MTH-DEFAULT60-R1+CONS-FURTHER-MULTIPLIER-V1+CONS-FURTHER-PREVIEW-R2+CONS-FURTHER-DEFAULTS-R3+DIAG-4CA-R1-HF1+DIAG-4CB-NONNEUTRAL-COVERAGE-R2+DIAG-4CC-CHILD-MEMORY-DECAY-R2-HF1+DIAG-4CM-APTITUDE-HF1+DIAG-4CRND-PURE-R1+DIAG-4CC-REAL-JOB-PAYOUT-R3+DIAG-4CC-INDEPENDENT-RATE-R4+DIAG-4CC-JOB-SOURCE-COVERAGE-R4S2+DIAG-4CC-WORK-TARGET-R4S3+DIAG-4CC-POSTCAP-SAMPLING-R5A1+DIAG-4C-VERSIONED-REGRESSION-R1+DIAG-4CC-DAILYCAP-R5B1+DIAG-4CC-POSTCAP-FURTHER-R5C1+DIAG-4CC-READINESS-SCAN-R5D1+DIAG-4CC-READING-SOURCE-COVERAGE-R5E2+FURTHER-ORDER-BASE-MTH-CONS-R5F1+FURTHER-TOOLTIP-COMPACT-R5F2+TOOLTIP-STAT-RATIOS-R5F3+TOOLTIP-NEUTRAL-HIDE-R5F4+PROFILE-PREP-SNAPSHOT-R6P0AB+PROFILE-PREP-APPLY-R6P0C+PROFILE-PREP-UI-REFRESH-R6P0D1+PROFILE-PREP-RUNTIME-DISPATCH-R6P0D2+PROFILE-PREP-RUNTIME-RECOVERY-R6P0D3+PROFILE-PREP-LIVE-AUDIT-R6P0D4+PROFILE-PORTABLE-SCHEMA-R6P1A+PROFILE-DISK-STORE-R6P1B+PROFILE-UI-READONLY-R6P1C1+PROFILE-DETACHED-WINDOW-R6P1C15+PROFILE-QUICKMENU-R6P1C16+PROFILE-CREATE-R6P1C2A+PROFILE-RENAME-R6P1C2B1+PROFILE-QUICKMENU-CLEAN-R6P1C2B15+PROFILE-OVERWRITE-PREFLIGHT-R6P1C2B2A+PROFILE-EDITOR-FOCUS-R6P1C2B2A1+PROFILE-DISABLED-DECAY-R6P1C2B2A11+PROFILE-APPLY-REVIEW-R6P1D1+PROFILE-APPLY-REVIEW-FIX2-R6P1D1+PROFILE-APPLY-GATE-R6P1D2A1+PROFILE-APPLY-BRIDGE-R6P1D2B1+RUNTIME-RECOVERY-GATE-R6P1D2B2A+D4-PERSISTENCE-GUARD-R6P1D2B2B+PROFILE-PERSISTENCE-CONTRACT-R6P1D2C1+XML-FAULT-BOUNDARY-R6P1D2C2+MOD-XML-READBACK-R6P1D2C3B+XML-STAGE-ISOLATED-R6P1D2C4B1+XML-STAGE-LEAF-FIX-R6P1D2C4B1F1+XML-SEMANTIC-R6P1D2C4B2AB1+XML-DETACHED-LOAD-R6P1D2C4B2C1+XML-LOG-OBSERVE-R6P1D2C4B2C2+XML-NONDEFAULT-R6P1D2C4B2C3+XML-TEMP-COMMIT-R6P1D2C4B3A+XML-TEMP-PATH-FIX-R6P1D2C4B3AF1+PROFILE-DEBUG-LIVE-R6P1D2D1A+PROFILE-DEBUG-LIVE-REFEQ-FIX-R6P1D2D1AF1+PROFILE-PERSIST-DEBUG-R6P1D2D1B+PROFILE-TRASH-UI-R6P1C2C+PROFILE-TRASH-PURGE-R6P1C2D+PROFILE-OVERWRITE-C2E+PROFILE-LOADED-AUDIT-D2D2A+PROFILE-APPLY-PRODUCTION-D2D3+PROFILE-APPLY-UI-D2D4+PROFILE-LOGGING-F2+PROFILE-NUMERIC-NAV-N1+PROFILE-ACTION-SWAP-S1+UI-TOGGLE-ALIGN-U1+GLOBAL-RESET-CONFIRM-R1+MOD-NUMERIC-DEFAULTS-R3A+HARD-PRESET-RETIRE-H1+DIAGNOSTIC-LOG-SEMANTICS-Q1+Q11-RECOVERY-LATCH-DIAGNOSTIC+P1-REVIEW-UI-READABILITY+P1-HF1-TIMESTAMP-LENGTH-12+R3B-CONFIRMED-DRAFT-DISCARD+L1-CONS-GWS-ALL-LINEAR-DEFAULTS+R4-SINGLE-FULL-MOD-DEFAULTS+R4HF1-RESET-FOCUS-CLEANUP+BP12-BUNDLED-PROFILE-SEED+BP4-RESET-RESTORE-V2+SETTINGS-SPLIT-SSR1+SETTINGS-WINDOW-SW1+SKILL-TOOLTIP-TT1+LC1-SPLIT+RUNTIME-APPLY-RAC1+RUNTIME-APPLY-RAC1-HF1+PROFILE-MANAGER-PM1+PROFILE-MANAGER-UX1+CAREER-BOUNDARY-CBE1+CAREER-PROGRESSION-CPS1+OWN-XML-EVIDENCE-OXE1+LEARNING-CAPACITY-UI-LCUI1+SKILL-TOOLTIP-SERVICE-TT2+SETTINGS-UI-HELPERS-SUH1+SETTINGS-UI-SAFETY-SUH2A-B1+CAREER-XP-DISTRIBUTION-CXD1+CXD1-HF1-TEST-BUILDID-COMPAT+COMPAT-AUDIT-CA1-R1+DECAY-POLICY-DP1-R1+UI-LAYOUT-UL1-R1+UI-LAYOUT-UL1-HF1");

            Log.Message("[XPC-D01][CURVE_DEFAULTS_AUDIT] status=LOADED" +
                "; consSpeedHigh=120/x1.08/Linear/cap2" +
                "; consCapacityHigh=120/x1.08/Linear/cap2" +
                "; consFurtherHigh=120/x1.20/Linear/cap2" +
                "; gwsHigh=500/x1.30/Diminishing/curvature40/limit1.50" +
                "; lowDefaults=UNCHANGED; masterDefaults=UNCHANGED" +
                "; legacyXmlFallbacks=HISTORICAL; profileSchema=UNCHANGED" +
                "; runtimeScribeRoundTrip=NOT_CHECKED; livePawnXp=NOT_CHECKED" +
                "; effectiveConsSpeedHigh=" +
                (Settings.consciousnessAnchorSpeed.HighAnchor * 100f).ToString("0.#") +
                "/x" + Settings.consciousnessAnchorSpeed.HighFactor.ToString("0.###") +
                "; effectiveConsCapacityHigh=" +
                (Settings.consciousnessAnchorCapacity.HighAnchor * 100f).ToString("0.#") +
                "/x" + Settings.consciousnessAnchorCapacity.HighFactor.ToString("0.###") +
                "; effectiveConsFurtherHigh=" +
                (Settings.consciousnessFurtherV2.HighAnchor * 100f).ToString("0.#") +
                "/x" + Settings.consciousnessFurtherV2.HighFactor.ToString("0.###") +
                "; effectiveGwsHigh=" +
                (Settings.gwsHighAnchor * 100f).ToString("0.#") +
                "/x" + Settings.gwsHighFactor.ToString("0.###") +
                "/mode" + Settings.gwsHighCurve +
                "/curvature" + (Settings.gwsHighCurvature * 100f).ToString("0.#") +
                "/limit" + Settings.gwsHighDiminishingLimit.ToString("0.###") +
                "; build=" + BuildId);

#if DEBUG
            // Preserve existing startup checks, but a stale assertion must
            // not prevent mod settings from registering (R7 incident).
            // The new 4C-A runner is manual and isolates failures per suite.
            try
            {
                XpCustomizerSelfTests.Run();
            }
            catch (System.Exception ex)
            {
                Log.Error("[XP Customizer][SELFTEST] DEBUG_STARTUP_TESTS_FAIL; " +
                    "build=" + BuildId + "; furtherChecks=MANUAL_4C_A; " + ex);
            }
#endif

#if DEBUG
            // Read-only selected-field comparison against the already loaded
            // vanilla ModSettings file. Does not write or reload settings.
            ModXmlRoundTripAudit.ObserveStartup(Content, Settings);
#endif

            // D2-D2A: independently compare loaded memory with own disk XML.
            ProfileOwnedXmlEvidence.ObserveStartup(Content, Settings);

            Log.Message(
                "[XP Customizer] Loaded successfully. " +
                "Career XP: x" +
                Settings.careerXpMultiplier.ToString("0.0") +
                ", Level 20 Reserve: x" +
                Settings.level20ReserveMultiplier.ToString("0.0") +
                ", Skilltrainer XP: x" +
                Settings.skilltrainerXpMultiplier.ToString("0.0") +
                ", SkillXP: " + (Settings.skillSpecificXpEnabled ? "ON" : "OFF") +
                " (configured=" + XpCustomizer.Progression.SkillSpecificXpService.GetModifiedCount(Settings) + ")" +
                ", SkilltrainerSkillXP: " + (Settings.skilltrainerSpecificEnabled ? "ON" : "OFF") +
                " (configured=" + XpCustomizer.Skilltrainer.SkilltrainerSpecificXpService.GetModifiedCount(Settings) + ")" +
                ", DecayD3(level=" + Settings.decayByLevelEnabled +
                ", skill=" + Settings.decaySkillMultipliersEnabled +
                ", modified=" + Decay.DecayModifiersService.ModifiedCount(Settings) +
                ", greatReduction=" + Settings.greatMemoryDecayReduction.ToString("0.00") + "%" +
                ", perfectReduction=" + Settings.perfectMemoryDecayReduction.ToString("0.00") + "%" +
                ", memorySwitches=REMOVED)");

            // PNL1: one startup identity line in BOTH Debug and Release.
            // Presentation only; no profile JSON read/write or gameplay changes.
            Log.Message("[XPC-PNL1][BUNDLED_NOTES_L10N] revision=" +
                BundledProfileNotesLocalization.Revision +
                "; status=REGISTERED; official=LONG,VERY_LONG; " +
                "guard=ID_AND_EXACT_ENGLISH_NOTES; " +
                "profileJsonWrites=0; gameplayWrites=0; build=" + BuildId);

            Log.Message("[XP Customizer] CONS_CURVE_ROUTER=" +
                XpCustomizer.ConsciousnessLearning.ConsciousnessCurveDispatcher.Revision +
                "; mode=ANCHOR_V2_ONLY; speed=ANCHOR_V2; capacity=ANCHOR_V2" +
                "; master=" + Settings.consciousnessAffectsLearning +
                "; speedEnabled=" + Settings.consciousnessAffectsLearningSpeed +
                "; capacityEnabled=" + Settings.consciousnessAffectsDailyCapacity +
                "; capacityGate=" + XpCustomizer.ConsciousnessLearning.ConsciousnessCurveDispatcher.HasConfiguredCapacityEffect(Settings) +
                "; speedTooltip=" + XpCustomizer.ConsciousnessLearning.ConsciousnessCurveDispatcher.HasConfiguredSpeedEffect(Settings) +
                "; legacyGameplay=REMOVED; legacyScribe=REMOVED; FurtherLearning=MULTIPLIER_V1; GWS=unchanged");

            Log.Message("[XP Customizer] CONS_ANCHOR_SETTINGS=" +
                XpCustomizer.ConsciousnessLearning.ConsciousnessAnchorV2SettingsStore.Revision +
                "; schema=" + Settings.consciousnessAnchorSettingsVersion +
                "; source=ANCHOR_V2_ONLY" +
                "; speedLow=x" + Settings.consciousnessAnchorSpeed.LowFactor.ToString("0.###") +
                "; speedHigh=x" + Settings.consciousnessAnchorSpeed.HighFactor.ToString("0.###") +
                "; capacityLow=x" + Settings.consciousnessAnchorCapacity.LowFactor.ToString("0.###") +
                "; capacityHigh=x" + Settings.consciousnessAnchorCapacity.HighFactor.ToString("0.###") +
                "; speedHighAnchor=" + (Settings.consciousnessAnchorSpeed.HighAnchor * 100f).ToString("0.#") + "%" +
                "; capacityHighAnchor=" + (Settings.consciousnessAnchorCapacity.HighAnchor * 100f).ToString("0.#") + "%" +
                "; defaultHigh=120%/x1.08/Linear/safety2; consSectionUi=" + ConsciousnessSettingsUI.SectionUiRevision +
                "; speedCurves=" + (XpCustomizer.ConsciousnessLearning.ConsAnchorCurveMode)Settings.consciousnessAnchorSpeed.LowCurve + "/" + (XpCustomizer.ConsciousnessLearning.ConsAnchorCurveMode)Settings.consciousnessAnchorSpeed.HighCurve +
                "; capacityCurves=" + (XpCustomizer.ConsciousnessLearning.ConsAnchorCurveMode)Settings.consciousnessAnchorCapacity.LowCurve + "/" + (XpCustomizer.ConsciousnessLearning.ConsAnchorCurveMode)Settings.consciousnessAnchorCapacity.HighCurve +
                "; speedLowEnabled=" + Settings.consciousnessAnchorSpeed.LowEnabled +
                "; speedHighEnabled=" + Settings.consciousnessAnchorSpeed.HighEnabled +
                "; capacityLowEnabled=" + Settings.consciousnessAnchorCapacity.LowEnabled +
                "; capacityHighEnabled=" + Settings.consciousnessAnchorCapacity.HighEnabled +
                "; UI=" + ConsciousnessAnchorV2SettingsUI.Revision +
                "; S06Split=" + ConsciousnessAnchorV2SettingsUI.SplitStage +
                "; presets=V2-only; runtimePawnXP=notVerified");

            Log.Message("[XP Customizer] CONS_FURTHER_SCHEMA=" +
                XpCustomizer.ConsciousnessLearning.ConsciousnessFurtherV2SettingsStore.Revision +
                "; version=" + Settings.consciousnessFurtherV2Version +
                "; calculation=BASE_PLUS_MTH_PP_TIMES_CONS_R5F1" +
                "; modDefaults=Low80/Linear/x0.80+High120/Linear/x1.20/safety2" +
                "; low=" + (Settings.consciousnessFurtherV2.LowAnchor * 100f).ToString("0.#") +
                "%/x" + Settings.consciousnessFurtherV2.LowFactor.ToString("0.###") +
                "; high=" + (Settings.consciousnessFurtherV2.HighAnchor * 100f).ToString("0.#") +
                "%/x" + Settings.consciousnessFurtherV2.HighFactor.ToString("0.###") +
                "; curveModes=" + (XpCustomizer.ConsciousnessLearning.ConsAnchorCurveMode)Settings.consciousnessFurtherV2.LowCurve + "/" + (XpCustomizer.ConsciousnessLearning.ConsAnchorCurveMode)Settings.consciousnessFurtherV2.HighCurve +
                "; lowEnabled=" + Settings.consciousnessFurtherV2.LowEnabled +
                "; highEnabled=" + Settings.consciousnessFurtherV2.HighEnabled +
                "; legacyPp=INERT; mthPp=UNCHANGED; runtimePawnXP=NOT_VERIFIED");

            Log.Message("[XP Customizer] MTH_SCHEMA=V1; revision=MTH-DEFAULT60-R1-20261104; enabled=" +
                Settings.furtherLearningMentalThresholdEnabled +
                "; strength=" + Settings.furtherLearningMentalThresholdStrength.ToString("0.0") +
                "; modDefault=0.6; vanillaResetStrength=1.0; sectionButtonStrength=0.6; sectionButtonLabel=VANILLA_UNCHANGED; existingXml=preserved" +
                "; base=" + Settings.furtherLearningBaseFactor.ToString("0.000") +
                "; anchor17=+10pp; anchor35=0pp; anchor50=-10pp; " +
                "legacyTraitValues=preserved-inert; GWS=separate-learning-multiplier");

#if DEBUG
            Log.Message("[XPC-GWS-MEDFIX] version=XPC-GWS-MED-001-HOTFIX-20261112-R1" +
                "; canonical=Medicine; legacy=Medical; skillList=12; defaultEnabled=9" +
                "; medicineKnown=" + XpCustomizer.Gws.GwsLearningService.IsKnownSkill("Medicine") +
                "; legacySkillAccepted=" + XpCustomizer.Gws.GwsLearningService.IsKnownSkill("Medical") +
                "; scope=GWS_ONLY; build=" + BuildId);
#endif
            Log.Message("[XP Customizer] GWS_SCHEMA=V2-ANCHORS; enabled=" + Settings.gwsEnabled +
                "; anchorPositions=" + (Settings.gwsLowAnchor * 100f).ToString("0.#") +
                "%/" + (Settings.gwsHighAnchor * 100f).ToString("0.#") + "%" +
                "; low=" + Settings.gwsLowEnabled + "/" + ((XpCustomizer.Gws.GwsCurveMode)Settings.gwsLowCurve) +
                "/x" + Settings.gwsLowFactor.ToString("0.##") +
                "; high=" + Settings.gwsHighEnabled + "/" + ((XpCustomizer.Gws.GwsCurveMode)Settings.gwsHighCurve) +
                "/x" + Settings.gwsHighFactor.ToString("0.##") +
                "; highSafetyLimit=x" + Settings.gwsHighTechnicalLimit.ToString("0.##") +
                "; highDiminishingLimit=x" + Settings.gwsHighDiminishingLimit.ToString("0.##") +
                "; reading=" + Settings.gwsApplyToReading +
                "; direct=excluded; defaultShootingMeleeSocial=OFF" +
                "; effectiveSocial=" + XpCustomizer.Gws.GwsLearningService.IsSkillEnabled(Settings, "Social") +
                "; bookXP=preserved");

            Log.Message("[XP Customizer] LC_SCHEMA=V4; enabled=" +
                Settings.learningCapacitySourceModifiersEnabled +
                "; globalStrength=" +
                (XpCustomizer.LearningCapacity.LearningCapacityService.NormalizeSourceStrength(
                    Settings.learningCapacitySourceStrength) * 100f).ToString("0") +
                "%; formula=vanillaLF*0.20*strength; manual=finalDLC%; " +
                "savedV3=manual-migration; sourcePreview=single-source-Cons100");

#if DEBUG
            Log.Message("[XPC-BOOKXP-MEDFIX] version=XPC-BOOKXP-MED-001-HOTFIX-20261112-R1" +
                "; canonical=Medicine; legacy=Medical; skillList=" +
                XpCustomizer.BookXp.BookXpService.VanillaSkillNames.Length +
                "; medicineKnown=" + XpCustomizer.BookXp.BookXpService.IsVanillaSkill("Medicine") +
                "; legacySkillAccepted=" + XpCustomizer.BookXp.BookXpService.IsVanillaSkill("Medical") +
                "; scope=TEXTBOOK_SKILL_ONLY; build=" + BuildId);
#endif
            Log.Message("[XP Customizer] BOOKXP_SCHEMA=V1; MASTER_SCHEMA=R3; master=" +
                Settings.bookXpEnabled + "; intellectual=" +
                (Settings.bookIntellectualEnabled ? Settings.bookIntellectualXpPerHour.ToString("0.##") + " XP/h" : "OFF") +
                "; quality=" + Settings.bookQualityEnabled +
                "; skill=" + Settings.bookSkillEnabled +
                "; default=vanilla-neutral; existingBooks=runtime-only; levelCaps=untouched");

            Harmony harmony =
                new Harmony(
                    "zcn.xpcustomizer");

            harmony.PatchAll();

#if DEBUG
            // Audit only after patch registration. Failure is loud, but it
            // must not hide the settings window or abort unrelated probes.
            try
            {
                XpCustomizerSelfTests.RunHarmonyPatchAudit();
            }
            catch (System.Exception ex)
            {
                Log.Error("[XP Customizer][4C-A] STARTUP_PATCH_AUDIT_FAIL; " +
                    "build=" + BuildId + "; manualAuditAvailable=True; " + ex);
            }
#endif


            // Biotech implied GeneDefs are generated during loading.
            // Apply our values after RimWorld finishes loading defs.
            LongEventHandler.ExecuteWhenFinished(
                BiotechGeneService.ApplySettings);
#if DEBUG
            // D4 read-only runtime audit AFTER the existing Biotech application.
            // The real GeneDef transition probe is MANUAL ONLY from settings UI.
            LongEventHandler.ExecuteWhenFinished(
                SettingsLiveRecoveryProbe.ReadOnlyAfterDefsLoaded);
#endif

            // CA-1: registration-only Compatibility Audit. Defer until all
            // mod constructors and normal long-event setup have completed.
            // No Harmony, skill, settings, profile or save mutations.
            LongEventHandler.ExecuteWhenFinished(
                CompatibilityAuditCoordinator.RunStartupAudit);

            // Validate S2's skill source against the loaded 1.6 Defs.
            LongEventHandler.ExecuteWhenFinished(
                XpCustomizer.Skilltrainer.SkilltrainerSpecificXpService.LogResolutionAudit);
#if DEBUG
            // Only inspect RimWorld SkillDefs AFTER loading; the constructor's
            // self-tests must not assume that DefDatabase is already populated.
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                int missing = 0;
                foreach (string skillId in XpCustomizer.Gws.GwsLearningService.Skills)
                    if (DefDatabase<SkillDef>.GetNamedSilentFail(skillId) == null) missing++;
                Log.Message("[XPC-GWS-MEDFIX] phase=DEFS_LOADED; skillDefsChecked=12" +
                    "; missing=" + missing + "; medicineResolved=" +
                    (DefDatabase<SkillDef>.GetNamedSilentFail("Medicine") != null) +
                    "; source=GAME_DEFS; build=" + BuildId);
            });
#endif


#if DEBUG
            // Do not query SkillDef during mod construction. RimWorld has to
            // finish defining all skills before this read-only audit runs.
            LongEventHandler.ExecuteWhenFinished(delegate
            {
                int missing = 0;
                foreach (string skillId in XpCustomizer.BookXp.BookXpService.VanillaSkillNames)
                    if (DefDatabase<SkillDef>.GetNamedSilentFail(skillId) == null) missing++;
                Log.Message("[XPC-BOOKXP-MEDFIX] phase=DEFS_LOADED; skillDefsChecked=" +
                    XpCustomizer.BookXp.BookXpService.VanillaSkillNames.Length +
                    "; missing=" + missing + "; medicineResolved=" +
                    (DefDatabase<SkillDef>.GetNamedSilentFail("Medicine") != null) +
                    "; source=GAME_DEFS; build=" + BuildId);
            });
#endif

            float activeTotalXp = 0f;

            for (int level = 0;
                 level < 20;
                 level++)
            {
                activeTotalXp +=
                    SkillRecord
                    .XpRequiredToLevelUpFrom(
                        level);
            }

            Log.Message(
                "[XP Customizer] Active XP curve: " +
                activeTotalXp.ToString("0") +
                " total XP, Level 19 -> 20: " +
                SkillRecord
                    .XpRequiredToLevelUpFrom(19)
                    .ToString("0") +
                " XP, Level 20 reserve: " +
                SkillRecord
                    .XpRequiredToLevelUpFrom(20)
                    .ToString("0") +
                " XP.");
        }

    }
}
