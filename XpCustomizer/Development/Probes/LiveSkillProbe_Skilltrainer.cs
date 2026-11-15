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
    // R1-B: split DEBUG-only probe code; original method bodies unchanged.
    internal static partial class XpCustomizerLiveSkillProbe
    {
        // ================================================================
        // S6-B: read-only contract audit against actual loaded Skilltrainer Defs
        // ================================================================
        // This does not call DoEffect() and is NOT proof of real item use.
        // Tests the production calculation using the real Def xpGainAmount
        // while confirming that no shared CompProperties data is modified.
        private static void RunSkilltrainerDefReadOnlyAudit()
        {
            try
            {
                int eligibleDefs = 0;
                int excludedDefs = 0;
                int scenarios = 0;
                int s2Resolved = 0;
                int s2Unresolved = 0;
                int s2NameMismatch = 0;

                foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
                {
                    if (def == null || def.comps == null)
                    {
                        continue;
                    }

                    foreach (CompProperties comp in def.comps)
                    {
                        CompProperties_UseEffect_LearnSkill trainerProps =
                            comp as CompProperties_UseEffect_LearnSkill;
                        if (trainerProps == null)
                        {
                            continue;
                        }

                        // Read the field directly from the shared loaded Def.
                        float original = trainerProps.xpGainAmount;
                        if (float.IsNaN(original) || float.IsInfinity(original))
                        {
                            throw new InvalidOperationException(
                                "S6-B invalid original XP for " + def.defName);
                        }

                        bool supported = def.defName != null &&
                            def.defName.StartsWith("Neurotrainer_", StringComparison.Ordinal);
                        if (!supported)
                        {
                            // Skilltrainer comps with unrelated def names
                            // must be left entirely vanilla even at x2.
                            float unaffected =
                                Skilltrainer.Patch_CompUseEffect_LearnSkill
                                    .CalculateAdjustedXp(original, def.defName, 2f);
                            if (unaffected != original)
                            {
                                throw new InvalidOperationException(
                                    "S6-B unrelated trainer was scaled: " + def.defName);
                            }
                            excludedDefs++;
                            scenarios++;
                        }
                        else
                        {
                            eligibleDefs++;
                            // S2: confirm a REAL skill can be obtained from the
                            // loaded comp, not inferred from a ThingDef suffix.
                            SkillDef targetSkill =
                                Skilltrainer.SkilltrainerSpecificXpService
                                    .GetSkillFromProperties(trainerProps);
                            if (targetSkill == null ||
                                string.IsNullOrEmpty(targetSkill.defName))
                                s2Unresolved++;
                            else
                            {
                                s2Resolved++;
                                if (def.defName != "Neurotrainer_" + targetSkill.defName)
                                    s2NameMismatch++;
                                var simulatedSettings = new XpCustomizerSettings();
                                simulatedSettings.skilltrainerSpecificEnabled = true;
                                Skilltrainer.SkilltrainerSpecificXpService
                                    .SetConfiguredMultiplier(simulatedSettings,
                                        targetSkill.defName, 1.75f);
                                float result =
                                    Skilltrainer.SkilltrainerSpecificXpService.Apply(
                                        original, def.defName,
                                        targetSkill.defName, simulatedSettings);
                                if (!NearSkilltrainerResult(result, original * 1.75f))
                                    throw new InvalidOperationException(
                                        "S2 resolved trainer skill scaling mismatch: " +
                                        def.defName + " -> " + targetSkill.defName);
                            }
                            float neutral =
                                Skilltrainer.Patch_CompUseEffect_LearnSkill
                                    .CalculateAdjustedXp(original, def.defName, 1f);
                            float boosted123 =
                                Skilltrainer.Patch_CompUseEffect_LearnSkill
                                    .CalculateAdjustedXp(original, def.defName, 1.23f);
                            float doubled =
                                Skilltrainer.Patch_CompUseEffect_LearnSkill
                                    .CalculateAdjustedXp(original, def.defName, 2f);

                            if (neutral != original ||
                                !NearSkilltrainerResult(boosted123, original * 1.23f) ||
                                !NearSkilltrainerResult(doubled, original * 2f))
                            {
                                throw new InvalidOperationException(
                                    "S6-B XP mismatch for loaded Def " + def.defName +
                                    ": original=" + original +
                                    ", neutral=" + neutral +
                                    ", x1.23=" + boosted123 +
                                    ", x2=" + doubled);
                            }
                            scenarios += 3;
                        }

                        // No write to the shared CompProperties is permitted.
                        if (trainerProps.xpGainAmount != original)
                        {
                            throw new InvalidOperationException(
                                "S6-B shared Def XP mutated: " + def.defName);
                        }
                    }
                }

                if (eligibleDefs == 0)
                {
                    Log.Warning(
                        "[XP Customizer] DEBUG Skilltrainer Def audit REVIEW " +
                        "(S6-B): no eligible Neurotrainer_ definitions found. " +
                        "Read-only; actual trainer use NOT tested.");
                    return;
                }

                Log.Message(
                    "[XP Customizer] DEBUG Skilltrainer Def audit passed " +
                    "(S6-B): eligibleDefs=" + eligibleDefs +
                    ", excludedDefs=" + excludedDefs +
                    ", scenarios=" + scenarios +
                    ", sharedPropsUntouched=True. " +
                    "S2 resolvedSkillDefs=" + s2Resolved +
                    ", unresolvedSkillDefs=" + s2Unresolved +
                    ", mismatchedItemNames=" + s2NameMismatch +
                    ", skillMemberPresent=" +
                    Skilltrainer.SkilltrainerSpecificXpService.HasSkillResolver + ". " +
                    "Loaded-Def calculation only; actual trainer use NOT tested.");
                if (s2Unresolved > 0)
                    Log.Warning("[XP Customizer] SKILLTRAINER-S2 REVIEW: " +
                        s2Unresolved + " supported trainer Def(s) did not expose an " +
                        "unambiguous SkillDef. Per-skill trainer overrides fail closed " +
                        "for these items; global trainer scaling remains available.");
            }
            catch (Exception e)
            {
                Log.Error(
                    "[XP Customizer] DEBUG Skilltrainer Def audit FAILED " +
                    "(S6-B): " + e);
            }
        }

        private static bool NearSkilltrainerResult(float actual, float expected)
        {
            return !float.IsNaN(actual) && !float.IsInfinity(actual) &&
                !float.IsNaN(expected) && !float.IsInfinity(expected) &&
                Math.Abs(actual - expected) <=
                    Math.Max(0.01f, Math.Abs(expected) * 0.00001f);
        }

        // ================================================================
        // S6-C: controlled Skilltrainer direct-XP payout bridge
        // ================================================================
        // This is an INTEGRATION test of SkillRecord.Learn(direct: true),
        // fed with the production multiplier and a REAL loaded trainer Def.
        // It is NOT a full CompUseEffect_LearnSkill.DoEffect() test.
        // A deliberately tiny fraction of the full trainer XP is used to
        // prevent level-ups and side effects on the saved Pawn. The two
        // test SkillRecords are detached from pawn.skills.skills.
        private static void RunSkilltrainerDetachedPayoutBridge(
            Pawn pawn, SkillRecord liveSkill)
        {
            try
            {
                if (pawn == null || liveSkill == null ||
                    pawn.skills == null ||
                    XpCustomizerMod.Settings == null)
                {
                    Log.Warning("[XP Customizer] DEBUG Skilltrainer payout bridge " +
                        "SKIPPED (S6-C): missing Pawn/skill/settings.");
                    return;
                }
                if (DebugSettings.fastLearning)
                {
                    Log.Warning("[XP Customizer] DEBUG Skilltrainer payout bridge " +
                        "SKIPPED (S6-C): fastLearning enabled.");
                    return;
                }

                MethodBase learnMethod = AccessTools.Method(
                    typeof(SkillRecord), nameof(SkillRecord.Learn));
                if (learnMethod == null)
                {
                    Log.Warning("[XP Customizer] DEBUG Skilltrainer payout bridge " +
                        "SKIPPED (S6-C): SkillRecord.Learn method missing.");
                    return;
                }
                Patches hooks = Harmony.GetPatchInfo(learnMethod);
                if (hooks != null &&
                    (HasHooks(hooks.Prefixes) || HasHooks(hooks.Postfixes) ||
                     HasHooks(hooks.Transpilers) || HasHooks(hooks.Finalizers)))
                {
                    Log.Warning("[XP Customizer] DEBUG Skilltrainer payout bridge " +
                        "SKIPPED (S6-C): SkillRecord.Learn has external hooks.");
                    return;
                }

                string trainerName = null;
                CompProperties_UseEffect_LearnSkill trainerProps = null;
                foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
                {
                    if (def == null || def.comps == null || def.defName == null ||
                        !def.defName.StartsWith("Neurotrainer_", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    foreach (CompProperties comp in def.comps)
                    {
                        CompProperties_UseEffect_LearnSkill found =
                            comp as CompProperties_UseEffect_LearnSkill;
                        if (found != null && found.xpGainAmount > 0f &&
                            !float.IsNaN(found.xpGainAmount) &&
                            !float.IsInfinity(found.xpGainAmount))
                        {
                            trainerName = def.defName;
                            trainerProps = found;
                            break;
                        }
                    }
                    if (trainerProps != null) break;
                }
                if (trainerProps == null)
                {
                    Log.Warning("[XP Customizer] DEBUG Skilltrainer payout bridge " +
                        "SKIPPED (S6-C): no eligible loaded trainer Def.");
                    return;
                }

                float original = trainerProps.xpGainAmount;
                float configured =
                    XpCustomizerMod.Settings.skilltrainerXpMultiplier;
                float adjusted =
                    Skilltrainer.Patch_CompUseEffect_LearnSkill.CalculateAdjustedXp(
                        original, trainerName, configured);
                AssertFinite(adjusted, "S6-C adjusted XP");
                if (adjusted < 0f)
                {
                    throw new InvalidOperationException("S6-C adjusted XP negative.");
                }

                int savedLevel = liveSkill.Level;
                float savedLevelXp = liveSkill.xpSinceLastLevel;
                float savedDailyXp = liveSkill.xpSinceMidnight;
                int savedSkillCount = pawn.skills.skills.Count;

                // Detached records share only their read-only Pawn context.
                // Both have the same level/passion, so direct rate must
                // be equal in the baseline and scaled branches.
                SkillRecord baseline = CreateDetachedSkill(pawn, liveSkill);
                SkillRecord scaled = CreateDetachedSkill(pawn, liveSkill);
                float baselineRate = baseline.LearnRateFactor(true);
                float scaledRate = scaled.LearnRateFactor(true);
                float levelThreshold = SkillRecord.XpRequiredToLevelUpFrom(3);
                AssertFinite(baselineRate, "S6-C baseline direct rate");
                AssertFinite(scaledRate, "S6-C scaled direct rate");
                AssertFinite(levelThreshold, "S6-C level threshold");
                if (baselineRate < 0f || scaledRate < 0f ||
                    Math.Abs(baselineRate - scaledRate) > 0.0001f ||
                    levelThreshold <= 0f)
                {
                    throw new InvalidOperationException(
                        "S6-C unsafe detached direct rate/level threshold.");
                }

                // Never award the full trainer XP here: that could level up
                // a detached SkillRecord and trigger game-wide notifications.
                // Keep the LARGER branch under 8 raw XP and under 1% of
                // the level threshold. The scale is applied to BOTH amounts
                // after using the actual loaded Def and active multiplier.
                double largest = Math.Max(original, adjusted);
                double maxSample = Math.Min(8d,
                    (double)levelThreshold * 0.01d /
                    Math.Max(1d, (double)scaledRate));
                if (largest <= 0d || maxSample <= 0d)
                {
                    throw new InvalidOperationException(
                        "S6-C invalid XP scaling for detached test.");
                }
                double fraction = Math.Min(1d, maxSample / largest);
                float baselineInput = (float)(original * fraction);
                float scaledInput = (float)(adjusted * fraction);
                if (baselineInput <= 0f || scaledInput <= 0f)
                {
                    throw new InvalidOperationException(
                        "S6-C sample too small for float representation.");
                }
                float expectedBaseXp = baselineInput * baselineRate;
                float expectedScaledXp = scaledInput * scaledRate;

                // Exercise the REAL SkillRecord.Learn direct-XP path, but
                // only on these unattached SkillRecord instances.
                baseline.Learn(baselineInput, true);
                scaled.Learn(scaledInput, true);
                AssertDetachedLearnResult(baseline, expectedBaseXp, 0f,
                    "S6-C trainer baseline");
                AssertDetachedLearnResult(scaled, expectedScaledXp, 0f,
                    "S6-C trainer scaled");

                if (trainerProps.xpGainAmount != original ||
                    XpCustomizerMod.Settings.skilltrainerXpMultiplier != configured ||
                    liveSkill.Level != savedLevel ||
                    liveSkill.xpSinceLastLevel != savedLevelXp ||
                    liveSkill.xpSinceMidnight != savedDailyXp ||
                    pawn.skills.skills.Count != savedSkillCount)
                {
                    throw new InvalidOperationException(
                        "S6-C changed shared Props/settings or a saved Pawn skill.");
                }

                Log.Message("[XP Customizer] DEBUG Skilltrainer payout bridge " +
                    "passed (S6-C): def=" + trainerName +
                    ", configured=x" + configured.ToString("0.###") +
                    ", defXp=" + original.ToString("0.###") +
                    ", adjustedFullXp=" + adjusted.ToString("0.###") +
                    ", sampleBaselineXp=" + expectedBaseXp.ToString("0.####") +
                    ", sampleScaledXp=" + expectedScaledXp.ToString("0.####") +
                    ", directDailyXp=0, sharedPropsUntouched=True, " +
                    "savedSkillUntouched=True. Actual detached Learn() only; " +
                    "full DoEffect()/item consumption NOT tested.");
            }
            catch (Exception e)
            {
                Log.Error("[XP Customizer] DEBUG Skilltrainer payout bridge " +
                    "FAILED (S6-C): " + e);
            }
        }

        private static bool HasHooks(IEnumerable<Patch> hooks)
        {
            foreach (Patch hook in hooks)
            {
                return true;
            }
            return false;
        }

    }
}
#endif
