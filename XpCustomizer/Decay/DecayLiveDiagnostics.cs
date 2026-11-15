#if DEBUG
using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using XpCustomizer.Compatibility;
using LudeonTK;
using RimWorld;
using Verse;

namespace XpCustomizer.Decay
{
    // DEVELOPMENT DIAGNOSTIC ONLY. Does not create jobs, tick pawns, change XP,
    // mutate settings, or write to saves. One armed Pawn, one Interval per skill.
    internal static class DecayLiveDiagnostics
    {
        internal const string Revision = "DECAY-DP1-LIVE-PROBE-R1-20261113";

        private sealed class SampleRow
        {
            internal SkillRecord Skill;
            internal bool Seen;
            internal int Tick;
            internal int BeforeLevel;
            internal int AfterLevel;
            internal int DisplayLevel;
            internal float BeforeXp;
            internal float AfterXp;
            internal float BaseDaily;
            internal float ConfiguredSkill;
            internal float ActiveSkill;
            internal float MemoryFactor;
            internal float ExpectedDaily;
            internal float ExpectedInterval;
            internal bool Great;
            internal bool Perfect;
            internal bool Anomaly;
            internal string Path = "NOT_SEEN";
            internal float? AppliedCustomLoss;
            internal DecayOverrideReason PolicyReasons;
            internal bool DisabledBefore;
        }

        internal struct BeforeState
        {
            internal bool Capturing;
            internal int Level;
            internal float Xp;
        }

        private static Pawn armedPawn;
        private static Dictionary<SkillRecord, SampleRow> rows;
        private static int armedTick;
        private static string settingsAtArm;

        internal static bool IsArmed { get { return armedPawn != null; } }

        // The DebugAction's ToolMapForPawns passes the actual map-click target.
        // Never read Find.Selector: opening Mod Settings clears its selection.
        internal static void ArmPawn(Pawn pawn)
        {
            if (pawn == null || pawn.skills == null || pawn.skills.skills == null)
            {
                Log.Warning("[XP Customizer][Decay Probe] Invalid target; select a pawn with skills on the map.");
                return;
            }
            // DebugAction targeting may remain active for more than one click.
            // Guard an ongoing capture against accidental replacement.
            if (armedPawn != null)
            {
                Log.Warning("[XP Customizer][Decay Probe] A sample is already armed for " +
                    armedPawn.LabelShortCap + " (" + armedPawn.ThingID +"). " +
                    "Wait for its report, or run 'Print partial decay report' before targeting another pawn.");
                return;
            }
            armedPawn = pawn;
            armedTick = Find.TickManager == null ? -1 : Find.TickManager.TicksGame;
            rows = new Dictionary<SkillRecord, SampleRow>();
            foreach (SkillRecord skill in pawn.skills.skills)
            {
                if (skill != null && skill.def != null && !rows.ContainsKey(skill))
                    rows.Add(skill, new SampleRow { Skill = skill });
            }
            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            settingsAtArm = "levelEnabled=" + (settings != null && settings.decayByLevelEnabled) +
                ", skillEnabled=" + (settings != null && settings.decaySkillMultipliersEnabled) +
                ", greatReduction=" + (settings == null ? "unknown" : settings.greatMemoryDecayReduction.ToString("0.00")) + "%" +
                ", perfectReduction=" + (settings == null ? "unknown" : settings.perfectMemoryDecayReduction.ToString("0.00")) + "%";
            Log.Message("[XP Customizer][Decay Probe] ARMED; revision=" + Revision +
                "; pawn=" + pawn.LabelShortCap + "; id=" + pawn.ThingID +
                "; tick=" + armedTick + "; skills=" + rows.Count + "; " + settingsAtArm +
                ". Press ESC/right-click to leave the vanilla debug targeter; unpause. Each SkillRecord.Interval() is sampled once; " +
                "no XP is granted or removed by the probe. Run 'Print partial decay report' if the pawn never ticks.");
            if (rows.Count == 0) Finish("EMPTY_SKILL_LIST");
        }

        internal static BeforeState Begin(SkillRecord skill)
        {
            BeforeState state = default(BeforeState);
            if (armedPawn == null || skill == null || skill.Pawn != armedPawn || rows == null) return state;
            SampleRow row;
            if (!rows.TryGetValue(skill, out row) || row.Seen) return state;
            state.Capturing = true;
            state.Level = skill.levelInt;
            state.Xp = skill.xpSinceLastLevel;
            row.Tick = Find.TickManager == null ? -1 : Find.TickManager.TicksGame;
            row.BeforeLevel = state.Level;
            row.BeforeXp = state.Xp;
            row.DisabledBefore = skill.TotallyDisabled;
            row.DisplayLevel = skill.Level; // includes aptitude, if any
            row.Anomaly = ModsConfig.AnomalyActive;
            row.Great = skill.Pawn.story != null && skill.Pawn.story.traits != null &&
                skill.Pawn.story.traits.HasTrait(TraitDefOf.GreatMemory);
            row.Perfect = skill.Pawn.story != null && skill.Pawn.story.traits != null &&
                skill.Pawn.story.traits.HasTrait(TraitDefOf.PerfectMemory);
            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            int level = state.Level;
            row.PolicyReasons = DecayOverridePolicy.GetReasons(settings, level,
                skill.def == null ? null : skill.def.defName,
                row.Great, row.Perfect, row.Anomaly);
            row.BaseDaily = level >= 0 && level <= 20 ? SkillDecayService.VanillaDaily[level] : 0f;
            if (settings != null && settings.decayByLevelEnabled && level >= 0 && level <= 20 &&
                settings.decayDailyByLevel != null && settings.decayDailyByLevel.Count == 21)
                row.BaseDaily = UnityEngine.Mathf.Clamp(settings.decayDailyByLevel[level], 0f, SkillDecayService.MaxDaily);
            row.ConfiguredSkill = DecayModifiersService.ConfiguredSkillMultiplier(settings, skill.def.defName);
            row.ActiveSkill = DecayModifiersService.ActiveSkillMultiplier(settings, skill.def.defName);
            row.MemoryFactor = DecayModifiersService.MemoryRemainingFactor(settings, row.Great, row.Perfect, row.Anomaly);
            row.ExpectedDaily = DecayModifiersService.DailyLoss(settings, level, skill.def.defName,
                row.Great, row.Perfect, row.Anomaly);
            row.ExpectedInterval = row.ExpectedDaily / SkillDecayService.IntervalsPerDay;
            return state;
        }

        // Called from the existing production Prefix, only when a probe is armed.
        internal static void NotePath(SkillRecord skill, string path, float? customLoss = null)
        {
            if (armedPawn == null || skill == null || skill.Pawn != armedPawn || rows == null) return;
            SampleRow row;
            if (rows.TryGetValue(skill, out row) && !row.Seen)
            {
                row.Path = path;
                row.AppliedCustomLoss = customLoss;
            }
        }

        internal static void End(SkillRecord skill, BeforeState state)
        {
            if (!state.Capturing || armedPawn == null || rows == null || skill == null) return;
            SampleRow row;
            if (!rows.TryGetValue(skill, out row) || row.Seen) return;
            row.AfterLevel = skill.levelInt;
            row.AfterXp = skill.xpSinceLastLevel;
            row.Seen = true;
            int seen = 0;
            foreach (SampleRow item in rows.Values) if (item.Seen) seen++;
            if (seen == rows.Count) Finish("ALL_SKILLS_OBSERVED");
        }

        internal static void FinishManual()
        {
            if (armedPawn == null)
            {
                Log.Message("[XP Customizer][Decay Probe] No active capture. Use Debug actions > XP Customizer > Probe skill decay (select pawn) first.");
                return;
            }
            Finish("MANUAL_FINISH");
        }

        private static string DetermineVerdict(SampleRow row)
        {
            if (row.Path == "NOT_SEEN") return "INCONCLUSIVE_PREFIX_NOT_OBSERVED";
            if (row.Path.StartsWith("VANILLA-", StringComparison.Ordinal))
                return row.PolicyReasons == DecayOverrideReason.None
                    ? "PASS_THROUGH_PREFIX_CONFIRMED" : "INCONCLUSIVE_CUSTOM_NOT_RUN";
            if (row.Path != "CUSTOM-INTERVAL" || !row.AppliedCustomLoss.HasValue)
                return "INCONCLUSIVE_NO_CUSTOM_INPUT";
            if (row.PolicyReasons == DecayOverrideReason.None)
                return "INCONCLUSIVE_UNEXPECTED_CUSTOM_ROUTE";
            if (row.BeforeLevel != row.AfterLevel)
                return "INCONCLUSIVE_LEVEL_BOUNDARY";
            if (row.DisabledBefore || row.Skill.TotallyDisabled)
                return "INCONCLUSIVE_DISABLED_SKILL";
            float loss = row.AppliedCustomLoss.Value;
            float delta = row.AfterXp - row.BeforeXp;
            float tolerance = Math.Max(0.005f, Math.Abs(loss) * 0.002f);
            return Math.Abs(delta + loss) <= tolerance
                ? "OBSERVED_PAYOUT_MATCH"
                : "OBSERVED_PAYOUT_MISMATCH_OR_OTHER_XP";
        }

        // Read-only owner metadata. Reuse CA-1's assembly-to-mod resolver and
        // collect only once when the user prints a probe report, never per tick.
        private static string IntervalPatchOwners()
        {
            try
            {
                var interval = AccessTools.Method(typeof(SkillRecord), "Interval");
                if (interval == null) return "METHOD_NOT_FOUND";
                string target = CompatibilityFeatureCatalog.MethodKey(interval);
                CompatibilityAuditSnapshot snapshot = HarmonyPatchDiscovery.Capture();
                var owners = new SortedSet<string>(StringComparer.Ordinal);
                foreach (CompatibilityTargetRecord record in snapshot.Targets)
                {
                    if (record.Method != target) continue;
                    foreach (CompatibilityPatchRecord patch in record.Patches)
                        if (!patch.IsOwn)
                            owners.Add(patch.Owner + "[" + patch.ModName + ";" +
                                patch.PackageId + ";" + patch.Kind + "]");
                }
                return owners.Count == 0 ? "NONE" : string.Join(",", new List<string>(owners).ToArray());
            }
            catch (Exception ex)
            {
                return "OWNER_SCAN_UNAVAILABLE:" + ex.GetType().Name;
            }
        }

        private static void Finish(string reason)
        {
            if (armedPawn == null) return;
            StringBuilder sb = new StringBuilder(2400);
            int now = Find.TickManager == null ? -1 : Find.TickManager.TicksGame;
            int seen = 0;
            sb.Append("[XP Customizer][Decay Probe] REPORT; revision=").Append(Revision)
              .Append("; reason=").Append(reason)
              .Append("; pawn=").Append(armedPawn.LabelShortCap)
              .Append("; pawnId=").Append(armedPawn.ThingID)
              .Append("; ticks=").Append(armedTick).Append("->").Append(now)
              .Append("; ").Append(settingsAtArm)
              .Append("; AnomalyActive=").Append(ModsConfig.AnomalyActive)
              .Append("; policy=").Append(DecayOverridePolicy.Revision)
              .Append("; intervalPatchOwners=").Append(IntervalPatchOwners())
              .Append("; columns: Skill | Level raw/display(before->after) | Traits G/P | LevelBase XP/day | Skill configured/active | Memory remaining | Expected XP/day / 200-tick interval | DP1 reasons | Execution path | Actual XP before->after (delta) | verdict");
            foreach (SampleRow row in rows.Values)
            {
                sb.Append("\n  ").Append(row.Skill.def.defName).Append(" | ");
                if (!row.Seen)
                {
                    sb.Append("NOT_SEEN (pawn may be paused, off-map, or XP ticks disabled)");
                    continue;
                }
                seen++;
                sb.Append(row.BeforeLevel).Append("/").Append(row.DisplayLevel)
                  .Append("->").Append(row.AfterLevel)
                  .Append(" | G=").Append(row.Great).Append(",P=").Append(row.Perfect)
                  .Append(" | ").Append(row.BaseDaily.ToString("0.####"))
                  .Append(" | ").Append(row.ConfiguredSkill.ToString("0.####"))
                  .Append("/").Append(row.ActiveSkill.ToString("0.####"))
                  .Append(" | ").Append(row.MemoryFactor.ToString("0.####"))
                  .Append(" | ").Append(row.ExpectedDaily.ToString("0.####"))
                  .Append(" / ").Append(row.ExpectedInterval.ToString("0.######"))
                  .Append(" | ").Append(row.PolicyReasons)
                  .Append(" | ").Append(row.Path);
                if (row.AppliedCustomLoss.HasValue)
                    sb.Append(" (-Learn ").Append(row.AppliedCustomLoss.Value.ToString("0.######")).Append(")");
                sb.Append(" | ").Append(row.BeforeXp.ToString("0.######"))
                  .Append("->").Append(row.AfterXp.ToString("0.######"))
                  .Append(" (delta=").Append((row.AfterXp - row.BeforeXp).ToString("+0.######;-0.######;0"))
                  .Append(", tick=").Append(row.Tick).Append(")");
                if (row.AfterLevel != row.BeforeLevel)
                    sb.Append(" [LEVEL_BOUNDARY: delta includes XP rollover; do NOT compare delta directly to applied loss]");
                sb.Append(" | verdict=").Append(DetermineVerdict(row));
            }
            sb.Append("\n  RESULT observed=").Append(seen).Append("/").Append(rows.Count)
              .Append("; observation only; expected is mod formula, not proof of original execution. " )
              .Append("Actual XP comparison is a sample observation, not proof of a specific patch owner. " )
              .Append("No simulated XP changes were made.");
            Log.Message(sb.ToString());
            armedPawn = null;
            rows = null;
            settingsAtArm = null;
        }
    }

    // RimWorld's own DebugAction targeter handles cursor input and cancellation.
    // This is intentionally not available from the mod settings window.
    public static class DecayDebugActions
    {
        [DebugAction("XP Customizer", "Probe skill decay (select pawn)",
            actionType = DebugActionType.ToolMapForPawns,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ProbeSkillDecay(Pawn pawn)
        {
            DecayLiveDiagnostics.ArmPawn(pawn);
        }

        [DebugAction("XP Customizer", "Print partial decay report",
            actionType = DebugActionType.Action,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void PrintPartialDecayReport()
        {
            DecayLiveDiagnostics.FinishManual();
        }
    }

    // Separate DEBUG-only read-only hooks run around the original/custom Interval.
    // The Prefix is prioritized before XpCustomizer's gameplay Prefix.
    // The Postfix executes even when the gameplay Prefix skips the original method.
    [HarmonyPatch(typeof(SkillRecord), "Interval")]
    internal static class Patch_SkillRecord_Interval_DecayDiagnostics
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(SkillRecord __instance, out DecayLiveDiagnostics.BeforeState __state)
        {
            // Reuse this existing DEBUG-only Interval hook for 4C-C decay
            // classification. No extra Harmony owner or gameplay patch.
            Phase4CCLiveXpCapture.EnterInterval(__instance);
            __state = DecayLiveDiagnostics.Begin(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(SkillRecord __instance, DecayLiveDiagnostics.BeforeState __state)
        {
            try { DecayLiveDiagnostics.End(__instance, __state); }
            finally { Phase4CCLiveXpCapture.LeaveInterval(__instance); }
        }
    }
}
#endif
