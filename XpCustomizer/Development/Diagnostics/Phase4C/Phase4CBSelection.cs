#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.FurtherLearning;
using XpCustomizer.Gws;

namespace XpCustomizer
{
    /// <summary>
    /// RF1: move the unchanged 4C-B selection / snapshot / Harmony safety
    /// routines out of the diagnostic runner. Shares its private state via
    /// a partial class; no gameplay hooks or settings writes are introduced.
    /// </summary>
    internal static partial class Phase4CBDetachedDiagnostics
    {
        private static void CheckDetachedPreflight()
        {
            if (Current.Game == null || Find.CurrentMap == null || Find.Maps == null)
                throw new SkipScenario("NO_LOADED_MAP");
            if (DebugSettings.fastLearning)
                throw new SkipScenario("DEBUG_FAST_LEARNING_ENABLED");
            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            if (settings == null)
                throw new SkipScenario("SETTINGS_UNAVAILABLE");
            if (GwsReadingScope.IsReadingTick)
                throw new SkipScenario("ACTIVE_READING_SCOPE");

            // Keep the R1 detached safety gate: never call Learn() in the
            // presence of foreign/unknown hooks that might mutate real state.
            MethodBase learn = AccessTools.Method(typeof(SkillRecord), nameof(SkillRecord.Learn));
            MethodBase rate = AccessTools.Method(typeof(SkillRecord), nameof(SkillRecord.LearnRateFactor));
            MethodBase saturated = AccessTools.PropertyGetter(typeof(SkillRecord),
                nameof(SkillRecord.LearningSaturatedToday));
            if (learn == null || rate == null || saturated == null)
                throw new SkipScenario("SKILL_METHOD_UNAVAILABLE");
            if (HasAnyPatches(learn))
                throw new SkipScenario("LEARN_HAS_HARMONY_HOOKS");
            string foreign = ForeignPatchOwners(rate) + ForeignPatchOwners(saturated);
            if (!string.IsNullOrEmpty(foreign))
                throw new SkipScenario("FOREIGN_RATE_OR_SATURATION_HOOKS=" + Clean(foreign));

        }

        private static Selection PrepareSelection()
        {
            CheckDetachedPreflight();
            XpCustomizerSettings settings = XpCustomizerMod.Settings;

            var chosen = new Selection();
            chosen.MentalStatAvailable = DefDatabase<StatDef>.GetNamedSilentFail("MentalBreakThreshold") != null;
            StatDef mentalStat = chosen.MentalStatAvailable
                ? DefDatabase<StatDef>.GetNamedSilentFail("MentalBreakThreshold") : null;
            int bestBaseScore = -1;
            float strongestGwsDifference = SmallTolerance;
            float strongestMentalDifference = SmallTolerance;

            foreach (Map map in Find.Maps)
            {
                if (map == null || map.mapPawns == null) continue;
                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    if (pawn == null || pawn.Dead || pawn.skills == null ||
                        pawn.skills.skills == null || pawn.health?.capacities == null)
                        continue;
                    var candidate = new Context { Pawn = pawn, Settings = settings,
                        ForeignRateHookOwners = "NONE" };
                    foreach (SkillRecord skill in pawn.skills.skills)
                    {
                        if (skill == null || skill.def == null || skill.TotallyDisabled) continue;
                        if (candidate.Ordinary == null) candidate.Ordinary = skill;
                        if (GwsLearningService.IsSkillEnabled(settings, skill.def.defName))
                        {
                            if (candidate.GwsOn == null) candidate.GwsOn = skill;
                        }
                        else if (GwsLearningService.IsKnownSkill(skill.def.defName))
                        {
                            if (candidate.GwsOff == null) candidate.GwsOff = skill;
                        }
                    }
                    if (candidate.Ordinary == null) continue;
                    chosen.EligiblePawns++;

                    // A single malformed pawn/stat must not prevent evaluating
                    // other candidates. Failure is recorded, not silently PASSed.
                    try
                    {
                        candidate.Consciousness = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
                        candidate.WorkSpeed = pawn.GetStatValue(StatDefOf.WorkSpeedGlobal);
                        VerifyFinite(candidate.Consciousness, "CANDIDATE_CONS");
                        VerifyFinite(candidate.WorkSpeed, "CANDIDATE_GWS");
                    }
                    catch (Exception)
                    {
                        chosen.CandidateReadErrors++;
                        continue;
                    }

                    int baseScore = (candidate.GwsOn != null ? 1 : 0) +
                        (candidate.GwsOff != null ? 1 : 0);
                    if (baseScore > bestBaseScore)
                    {
                        bestBaseScore = baseScore;
                        chosen.Baseline = candidate;
                    }

                    // GWS and MTH select independently; never adjust pawn stats,
                    // traits, settings, or a saved SkillRecord to force coverage.
                    if (settings.gwsEnabled && candidate.GwsOn != null &&
                        pawn.RaceProps != null && pawn.RaceProps.Humanlike)
                    {
                        chosen.GwsEnabledCandidates++;
                        float computed = GwsLearningService.Calculate(candidate.WorkSpeed, settings);
                        if (float.IsNaN(computed) || float.IsInfinity(computed))
                        {
                            chosen.CandidateReadErrors++;
                        }
                        else
                        {
                            candidate.GwsCandidateFactor = computed;
                            float difference = Math.Abs(computed - 1f);
                            if (difference >= SmallTolerance)
                            {
                                chosen.GwsNonNeutralCandidates++;
                                if (difference > strongestGwsDifference)
                                {
                                    strongestGwsDifference = difference;
                                    chosen.GwsNonNeutral = candidate;
                                }
                            }
                        }
                    }

                    if (settings.furtherLearningMentalThresholdEnabled &&
                        Math.Abs(settings.furtherLearningMentalThresholdStrength) >= SmallTolerance &&
                        mentalStat != null && pawn.RaceProps != null && pawn.RaceProps.Humanlike)
                    {
                        chosen.MentalHumanlikeCandidates++;
                        try
                        {
                            float raw = pawn.GetStatValue(mentalStat);
                            VerifyFinite(raw, "CANDIDATE_MENTAL_THRESHOLD");
                            candidate.MentalThreshold = Mathf.Clamp(raw, 0.01f, 0.50f);
                            candidate.MentalAdjustmentPp = FurtherLearningService.CalculateThresholdAdjustment(
                                candidate.MentalThreshold, settings.furtherLearningMentalThresholdStrength);
                            VerifyFinite(candidate.MentalAdjustmentPp, "CANDIDATE_MENTAL_PP");
                            float difference = Math.Abs(candidate.MentalAdjustmentPp);
                            if (difference >= SmallTolerance)
                            {
                                chosen.MentalNonNeutralCandidates++;
                                if (difference > strongestMentalDifference)
                                {
                                    strongestMentalDifference = difference;
                                    chosen.MentalNonNeutral = candidate;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            chosen.CandidateReadErrors++;
                        }
                    }
                }
            }

            if (chosen.Baseline == null)
                throw new SkipScenario(chosen.EligiblePawns == 0
                    ? "NO_ELIGIBLE_FREE_COLONIST_SKILL"
                    : "NO_READABLE_FREE_COLONIST_SKILL; readErrors=" + chosen.CandidateReadErrors);

            if (!settings.gwsEnabled)
                chosen.GwsSkipReason = "GWS_MASTER_OFF";
            else if (chosen.GwsEnabledCandidates == 0)
                chosen.GwsSkipReason = "NO_GWS_ENABLED_SKILL_IN_ELIGIBLE_PAWNS";
            else
                chosen.GwsSkipReason = "GWS_NONNEUTRAL_PAWN_NOT_FOUND; checked=" +
                    chosen.GwsEnabledCandidates + "; allFactorsNearOne=true";

            if (!settings.furtherLearningMentalThresholdEnabled ||
                Math.Abs(settings.furtherLearningMentalThresholdStrength) < SmallTolerance)
                chosen.MentalSkipReason = "MENTAL_EFFECT_DISABLED";
            else if (!chosen.MentalStatAvailable)
                chosen.MentalSkipReason = "MENTAL_STAT_MISSING";
            else
                chosen.MentalSkipReason = "MTH_NONNEUTRAL_PAWN_NOT_FOUND; checked=" +
                    chosen.MentalHumanlikeCandidates + "; allAdjustmentsNearZero=true";

            AddProtected(chosen, chosen.Baseline);
            AddProtected(chosen, chosen.GwsNonNeutral);
            AddProtected(chosen, chosen.MentalNonNeutral);
            return chosen;
        }

        private static void AddProtected(Selection selection, Context candidate)
        {
            if (candidate == null) return;
            foreach (Context existing in selection.Protected)
                if (System.Object.ReferenceEquals(existing.Pawn, candidate.Pawn))
                {
                    // One pawn can be selected for multiple tests. Use its
                    // already-captured state and the same settings snapshot.
                    candidate.OriginalSkills = existing.OriginalSkills;
                    candidate.SettingsSignature = existing.SettingsSignature;
                    return;
                }
            candidate.OriginalSkills = new List<SkillSnapshot>();
            foreach (SkillRecord skill in candidate.Pawn.skills.skills)
            {
                candidate.OriginalSkills.Add(new SkillSnapshot {
                    Record = skill,
                    Level = skill == null ? 0 : skill.Level,
                    RawLevel = skill == null ? 0 : skill.levelInt,
                    Disabled = skill != null && skill.TotallyDisabled,
                    LevelXp = skill == null ? 0f : skill.xpSinceLastLevel,
                    DailyXp = skill == null ? 0f : skill.xpSinceMidnight,
                    Passion = skill == null ? Passion.None : skill.passion
                });
            }
            candidate.SettingsSignature = Signature(candidate.Settings);
            selection.Protected.Add(candidate);
        }

        private static void CheckProtectedState(Selection selection)
        {
            if (selection == null) return;
            foreach (Context protectedContext in selection.Protected)
                CheckSavedState(protectedContext);
        }

        private static string PawnName(Context context)
        {
            return context == null || context.Pawn == null
                ? "NONE" : Clean(context.Pawn.LabelShort);
        }

        private static bool HasAnyPatches(MethodBase method)
        {
            Patches patches = Harmony.GetPatchInfo(method);
            return patches != null && (HasEntries(patches.Prefixes) ||
                HasEntries(patches.Postfixes) || HasEntries(patches.Transpilers) ||
                HasEntries(patches.Finalizers));
        }
        private static bool HasEntries(IEnumerable<Patch> values)
        {
            if (values == null) return false;
            foreach (Patch value in values) if (value != null) return true;
            return false;
        }
        private static string ForeignPatchOwners(MethodBase method)
        {
            Patches patches = Harmony.GetPatchInfo(method);
            if (patches == null) return "";
            var owners = new HashSet<string>();
            AddForeign(owners, patches.Prefixes);
            AddForeign(owners, patches.Postfixes);
            AddForeign(owners, patches.Transpilers);
            AddForeign(owners, patches.Finalizers);
            return owners.Count == 0 ? "" : string.Join(",", new List<string>(owners).ToArray()) + ";";
        }
        private static void AddForeign(HashSet<string> owners, IEnumerable<Patch> patches)
        {
            if (patches == null) return;
            foreach (Patch patch in patches)
            {
                if (patch == null) continue;
                string owner = patch.owner ?? "UNKNOWN_OWNER";
                if (!string.Equals(owner, OwnHarmonyId, StringComparison.Ordinal))
                    owners.Add(owner);
            }
        }

        private static string Signature(XpCustomizerSettings s)
        {
            // Observe settings but never normalize, rewrite, or serialize them.
            var sb = new StringBuilder();
            sb.Append(s.consciousnessAffectsLearning).Append('|')
                .Append(s.consciousnessAffectsLearningSpeed).Append('|')
                .Append(s.consciousnessAffectsDailyCapacity).Append('|')
                .Append(s.consciousnessAffectsFurtherLearning).Append('|')
                .Append(s.furtherLearningBaseFactor).Append('|')
                .Append(s.furtherLearningMentalThresholdEnabled).Append('|')
                .Append(s.furtherLearningMentalThresholdStrength).Append('|')
                .Append(s.learningCapacityBaseDailyCap).Append('|')
                .Append(s.learningCapacitySourceModifiersEnabled).Append('|')
                .Append(s.gwsEnabled).Append('|').Append(s.gwsLowEnabled).Append('|')
                .Append(s.gwsHighEnabled).Append('|').Append(s.gwsLowFactor).Append('|')
                .Append(s.gwsHighFactor).Append('|').Append(s.gwsLowAnchor).Append('|')
                .Append(s.gwsHighAnchor).Append('|')
                .Append(s.gwsLowCurve).Append('|').Append(s.gwsHighCurve).Append('|')
                .Append(s.gwsLowCurvature).Append('|').Append(s.gwsHighCurvature).Append('|')
                .Append(s.gwsHighDiminishingLimit).Append('|')
                .Append(s.gwsHighTechnicalLimit).Append('|')
                .Append(s.learningCapacitySourceStrength);
            AppendAnchorSignature(sb, s.consciousnessAnchorSpeed);
            AppendAnchorSignature(sb, s.consciousnessAnchorCapacity);
            if (s.consciousnessFurtherV2 != null)
            {
                ConsciousnessFurtherV2Settings f = s.consciousnessFurtherV2;
                sb.Append('|').Append(f.LowEnabled).Append('|').Append(f.HighEnabled)
                    .Append('|').Append(f.LowAnchor).Append('|').Append(f.LowFactor)
                    .Append('|').Append(f.LowCurve).Append('|').Append(f.LowCurvature)
                    .Append('|').Append(f.HighAnchor).Append('|').Append(f.HighFactor)
                    .Append('|').Append(f.HighCurve).Append('|').Append(f.HighCurvature)
                    .Append('|').Append(f.HighDiminishingLimit).Append('|').Append(f.HighSafetyLimit);
            }
            if (s.gwsSkillEnabled != null)
            {
                var keys = new List<string>(s.gwsSkillEnabled.Keys);
                keys.Sort(StringComparer.Ordinal);
                foreach (string key in keys)
                    sb.Append('|').Append(key).Append('=').Append(s.gwsSkillEnabled[key]);
            }
            return sb.ToString();
        }

        private static void AppendAnchorSignature(StringBuilder sb,
            ConsAnchorV2ProfileSettings p)
        {
            if (p == null) { sb.Append("|NULL_ANCHOR"); return; }
            sb.Append('|').Append(p.LowEnabled).Append('|').Append(p.HighEnabled)
                .Append('|').Append(p.LowAnchor).Append('|').Append(p.LowFactor)
                .Append('|').Append(p.LowCurve).Append('|').Append(p.LowCurvature)
                .Append('|').Append(p.HighAnchor).Append('|').Append(p.HighFactor)
                .Append('|').Append(p.HighCurve).Append('|').Append(p.HighCurvature)
                .Append('|').Append(p.HighDiminishingLimit).Append('|').Append(p.HighSafetyLimit);
        }

        private static void CheckSavedState(Context ctx)
        {
            if (ctx == null) return;
            if (!System.Object.ReferenceEquals(ctx.Settings, XpCustomizerMod.Settings) ||
                !string.Equals(ctx.SettingsSignature, Signature(ctx.Settings), StringComparison.Ordinal))
                throw new InvalidOperationException("SETTINGS_SNAPSHOT_MISMATCH");
            if (ctx.Pawn.skills == null || ctx.Pawn.skills.skills == null ||
                ctx.Pawn.skills.skills.Count != ctx.OriginalSkills.Count)
                throw new InvalidOperationException("SAVED_SKILL_COUNT_CHANGED");
            for (int i = 0; i < ctx.OriginalSkills.Count; i++)
            {
                SkillSnapshot previous = ctx.OriginalSkills[i];
                SkillRecord current = ctx.Pawn.skills.skills[i];
                if (!System.Object.ReferenceEquals(current, previous.Record))
                    throw new InvalidOperationException("SAVED_SKILL_REFERENCE_CHANGED=" + i);
                if (current == null) continue;
                if (current.Level != previous.Level ||
                    current.levelInt != previous.RawLevel ||
                    current.TotallyDisabled != previous.Disabled ||
                    current.xpSinceLastLevel != previous.LevelXp ||
                    current.xpSinceMidnight != previous.DailyXp ||
                    current.passion != previous.Passion)
                    throw new InvalidOperationException("SAVED_SKILL_CHANGED=" +
                        (current?.def == null ? i.ToString() : current.def.defName));
            }
        }

    }
}
#endif
