#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.FurtherLearning;
using Verse;

namespace XpCustomizer
{
    // ================================================================
    // Curve-5D-D1: live saved-game, read-only integration smoke probe
    // ================================================================
    // This DEBUG-only hook runs after a saved game has been loaded.
    // D1 does not modify gameplay settings or skill XP. D2 later runs
    // Learn() ONLY on detached, unsaved SkillRecords.
    [HarmonyPatch(typeof(SavedGameLoaderNow), "LoadGameFromSaveFileNow")]
    internal static class Patch_SavedGameLoaderNow_LiveSkillProbe
    {
        private static void Postfix()
        {
            // Defer until long-event completion so maps, pawns, and their
            // health/skills are ready. This never runs from the mod ctor.
            LongEventHandler.ExecuteWhenFinished(
                () => XpCustomizerLiveSkillProbe.Run());
        }
    }


    internal static partial class XpCustomizerLiveSkillProbe
    {
        public static void Run()
        {
            // S6-B audits loaded Defs independently of whether a live Pawn
            // can be found. The audit reads fields only; it never creates an
            // item, consumes a trainer, writes a Def, or changes settings.
            RunSkilltrainerDefReadOnlyAudit();

            try
            {
                if (Current.Game == null || Find.Maps == null)
                {
                    Log.Warning("[XP Customizer] DEBUG live Pawn read-only probe SKIPPED: " +
                                "no loaded game/maps (Curve-5D-D1).");
                    return;
                }

                Pawn pawn = null;
                SkillRecord skill = null;

                // Read a real, spawned colonist's real SkillRecord.
                // We deliberately NEVER generate or modify a pawn.
                foreach (Map map in Find.Maps)
                {
                    if (map == null || map.mapPawns == null)
                    {
                        continue;
                    }

                    foreach (Pawn candidate in map.mapPawns.FreeColonistsSpawned)
                    {
                        if (candidate == null || candidate.Dead ||
                            candidate.skills == null ||
                            candidate.health == null ||
                            candidate.health.capacities == null)
                        {
                            continue;
                        }

                        foreach (SkillRecord record in candidate.skills.skills)
                        {
                            if (record != null && !record.TotallyDisabled)
                            {
                                pawn = candidate;
                                skill = record;
                                break;
                            }
                        }

                        if (skill != null)
                        {
                            break;
                        }
                    }

                    if (skill != null)
                    {
                        break;
                    }
                }

                if (skill == null)
                {
                    Log.Warning("[XP Customizer] DEBUG live Pawn read-only probe SKIPPED: " +
                                "no spawned colonist with an enabled skill " +
                                "(Curve-5D-D1).");
                    return;
                }

                // Snapshot the mutable fields. No part of this probe writes
                // to these fields or to the global mod settings.
                float beforeDailyXp = skill.xpSinceMidnight;
                float beforeLevelXp = skill.xpSinceLastLevel;
                int beforeLevel = skill.Level;

                float consciousness =
                    pawn.health.capacities.GetLevel(
                        PawnCapacityDefOf.Consciousness);
                float speedFactor =
                    ConsciousnessLearning.ConsciousnessLearningService.GetFactor(pawn);
                float capacityFactor =
                    ConsciousnessLearning.ConsciousnessCapacityService.GetFactor(pawn);
                float liveDailyCap =
                    LearningCapacity.LearningCapacityService.GetDailyCap(skill);

                // These call the actual, Harmony-patched game methods,
                // not our pure calculators. Learn() is NEVER called.
                float liveRate = skill.LearnRateFactor(false);
                float directRate = skill.LearnRateFactor(true);
                bool saturated = skill.LearningSaturatedToday;

                AssertFinite(consciousness, "Consciousness");
                AssertFinite(speedFactor, "Speed factor");
                AssertFinite(capacityFactor, "Capacity factor");
                AssertFinite(liveDailyCap, "Daily capacity");
                AssertFinite(liveRate, "LearnRateFactor(false)");
                AssertFinite(directRate, "LearnRateFactor(true)");

                if (speedFactor < 0f || capacityFactor < 0f ||
                    liveDailyCap < 0f || liveRate < 0f || directRate < 0f)
                {
                    throw new InvalidOperationException(
                        "Negative skill rate / consciousness factor / daily capacity.");
                }

                // Exercise the Pawn -> consciousness -> configured curve
                // route rather than only the pure arithmetic functions.
                if (Math.Abs(speedFactor -
                        ConsciousnessLearning.ConsciousnessLearningService.GetFactor(
                            consciousness)) > 0.0005f ||
                    Math.Abs(capacityFactor -
                        ConsciousnessLearning.ConsciousnessCapacityService.CalculateConfiguredFactor(
                            consciousness, XpCustomizerMod.Settings)) > 0.0005f)
                {
                    throw new InvalidOperationException(
                        "Pawn and settings consciousness paths disagree.");
                }

                // Verify the live saturation Postfix when it MUST replace
                // vanilla's 4000 XP threshold. At exactly 4000 it
                // deliberately passes through vanilla / upstream mods.
                bool saturationChecked = false;
                if (LearningCapacity.LearningCapacityService.IsConfiguredToChangeDailyCap() &&
                    Math.Abs(liveDailyCap -
                        LearningCapacity.LearningCapacityService.VanillaDailyCap) > 0.01f)
                {
                    bool expected = liveDailyCap <= 0f ||
                                    beforeDailyXp > liveDailyCap;
                    if (saturated != expected)
                    {
                        throw new InvalidOperationException(
                            "Live saturation disagrees with the dynamic cap: " +
                            "expected=" + expected + ", got=" + saturated +
                            ", xpToday=" + beforeDailyXp +
                            ", cap=" + liveDailyCap + ".");
                    }
                    saturationChecked = true;
                }

                if (skill.Level != beforeLevel ||
                    skill.xpSinceMidnight != beforeDailyXp ||
                    skill.xpSinceLastLevel != beforeLevelXp)
                {
                    throw new InvalidOperationException(
                        "Probe unexpectedly changed the observed skill XP/level.");
                }

                Log.Message(
                    "[XP Customizer] DEBUG live Pawn read-only probe passed " +
                    "(Curve-5D-D1): skill=" + skill.def.defName +
                    ", consciousness=" + consciousness.ToString("0.###") +
                    ", speedFactor=x" + speedFactor.ToString("0.###") +
                    ", capacityFactor=x" + capacityFactor.ToString("0.###") +
                    ", dailyCap=" + liveDailyCap.ToString("0") +
                    ", rate=" + liveRate.ToString("0.###") +
                    ", directRate=" + directRate.ToString("0.###") +
                    ", saturated=" + saturated +
                    ", saturationAsserted=" + saturationChecked + ". " +
                    "Read-only; actual Learn()/XP gain NOT tested.");

                // SKILLXP-S1: read the actual Harmony-patched instance getter
                // for modified skills without touching saved XP or settings.
                RunSkillSpecificXpGetterReadOnlyAudit(pawn);

                // Curve-5D-D2 uses the same LIVE Pawn as a read-only
                // learning-context provider, but writes only to new,
                // detached SkillRecord objects that are not in pawn.skills.
                // D1 and D2 report independently.
                RunDetachedLearnProbe(pawn, skill);
                // S6-C isolates a tiny direct-XP sample with the CURRENT
                // Skilltrainer multiplier. No item and no DoEffect are used.
                RunSkilltrainerDetachedPayoutBridge(pawn, skill);
                // S5B-A inspects the ACTUAL RimWorld tooltip after D1/D2.
                // It never edits either a SkillRecord or mod settings.
                RunSkillTooltipReadOnlyAudit(pawn, skill);
                // Also inspect a GWS-eligible skill when the feature is ON.
                // The first enabled skill is often Shooting (default GWS OFF).
                if (XpCustomizerMod.Settings != null &&
                    XpCustomizerMod.Settings.gwsEnabled)
                {
                    foreach (SkillRecord record in pawn.skills.skills)
                    {
                        if (record == null || record.TotallyDisabled ||
                            record.def == null || record == skill ||
                            !Gws.GwsLearningService.IsSkillEnabled(
                                XpCustomizerMod.Settings, record.def.defName))
                            continue;
                        RunSkillTooltipReadOnlyAudit(pawn, record);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                // Do not disrupt loading or corrupt the user's save.
                Log.Error("[XP Customizer] DEBUG live Pawn read-only probe FAILED " +
                          "(Curve-5D-D1): " + e);
            }
        }

        private static void RunSkillSpecificXpGetterReadOnlyAudit(Pawn pawn)
        {
            XpCustomizerSettings settings = XpCustomizerMod.Settings;
            if (settings == null || !settings.skillSpecificXpEnabled)
            {
                Log.Message("[XP Customizer] SKILLXP-S1 live getter probe SKIP: disabled.");
                return;
            }

            int modified = 0;
            foreach (SkillRecord record in pawn.skills.skills)
            {
                if (record == null || record.def == null) continue;
                float factor = Progression.SkillSpecificXpService.GetEffectiveMultiplier(
                    record, settings);
                if (Math.Abs(factor - 1f) < 0.00001f) continue;
                float original = SkillRecord.XpRequiredToLevelUpFrom(record.levelInt);
                float expected = original * factor;
                float actual = record.XpRequiredForLevelUp;
                float tolerance = Math.Max(0.02f, Math.Abs(expected) * 0.0001f);
                if (Math.Abs(expected - actual) > tolerance)
                    throw new InvalidOperationException("SKILLXP-S1 getter mismatch for " +
                        record.def.defName + ": expected=" + expected +
                        ", actual=" + actual + ", multiplier=x" + factor);
                modified++;
            }
            Log.Message("[XP Customizer] SKILLXP-S1 live getter probe PASS: " +
                "observedModifiedSkills=" + modified +
                ", pawn=" + pawn.LabelShort +
                "; read-only getter check only, real Learn() threshold unverified.");
        }

        private static SkillRecord CreateDetachedSkill(Pawn pawn, SkillRecord original)
        {
            SkillRecord test = new SkillRecord(pawn, original.def);
            test.Level = 3;
            test.passion = original.passion;
            test.xpSinceLastLevel = 0f;
            test.xpSinceMidnight = 0f;
            // The source Pawn's tracker owns the real record, not this one.
            foreach (SkillRecord record in pawn.skills.skills)
            {
                if (Object.ReferenceEquals(record, test))
                {
                    throw new InvalidOperationException(
                        "D2 detached test record unexpectedly registered with Pawn.");
                }
            }
            return test;
        }

        private static void AssertDetachedLearnResult(SkillRecord record,
            float expectedLevelXp, float expectedDailyXp, string label)
        {
            AssertFinite(record.xpSinceLastLevel, "D2 " + label + " level XP");
            AssertFinite(record.xpSinceMidnight, "D2 " + label + " daily XP");
            // Level is aptitude-adjusted (and may be clamped or disabled).
            // Only levelInt represents the underlying training level that
            // must remain at 3 for this detached, non-level-up XP sample.
            if (record.levelInt != 3 ||
                System.Math.Abs(record.xpSinceLastLevel - expectedLevelXp) >
                    System.Math.Max(0.015f, System.Math.Abs(expectedLevelXp) * 0.002f) ||
                System.Math.Abs(record.xpSinceMidnight - expectedDailyXp) >
                    System.Math.Max(0.02f, System.Math.Abs(expectedDailyXp) * 0.000002f))
            {
                throw new InvalidOperationException(
                    "D2 " + label + " Learn() mismatch: rawLevel=" + record.levelInt +
                    ", displayedLevel=" + record.Level + ", aptitude=" + record.Aptitude +
                    ", disabled=" + record.TotallyDisabled +
                    ", xp=" + record.xpSinceLastLevel + " expected=" + expectedLevelXp +
                    ", daily=" + record.xpSinceMidnight + " expected=" + expectedDailyXp);
            }
        }

        private static void AssertFinite(float value, string label)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new InvalidOperationException(
                    label + " is NaN/Infinity (" + value + ").");
            }
        }
    }
}
#endif
