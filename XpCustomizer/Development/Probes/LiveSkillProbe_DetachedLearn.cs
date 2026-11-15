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
        // Curve-5D-D2: actual SkillRecord.Learn() on detached records
        // ================================================================
        // Do not modify the actual saved-game SkillRecord or mod settings.
        // The real Pawn is used only as read-only context for stats and
        // Consciousness, exactly as in D1. The test records are NOT inserted
        // into pawn.skills.skills or any map/world collection.
        // All samples stay at level 3, below level-up, tale, and message
        // thresholds. If another mod patches Learn(), the test SKIPS rather
        // than risking an unknown side effect from someone else's hook.

        private static void RunDetachedLearnProbe(Pawn pawn, SkillRecord liveSkill)
        {
            try
            {
                if (DebugSettings.fastLearning)
                {
                    Log.Warning("[XP Customizer] DEBUG detached Learn probe SKIPPED " +
                                "(Curve-5D-D2): fastLearning enabled.");
                    return;
                }

                MethodBase learnMethod = AccessTools.Method(typeof(SkillRecord),
                    nameof(SkillRecord.Learn));
                if (learnMethod == null)
                {
                    Log.Warning("[XP Customizer] DEBUG detached Learn probe SKIPPED " +
                                "(Curve-5D-D2): SkillRecord.Learn method unavailable.");
                    return;
                }

                // This mod does not patch Learn() itself. With unknown
                // third-party hooks, detached-record safety cannot be
                // promised: avoid executing those hooks on a live Pawn.
                Patches learnHooks = Harmony.GetPatchInfo(learnMethod);
                if (learnHooks != null &&
                    (HasHooks(learnHooks.Prefixes) ||
                     HasHooks(learnHooks.Postfixes) ||
                     HasHooks(learnHooks.Transpilers) ||
                     HasHooks(learnHooks.Finalizers)))
                {
                    Log.Warning("[XP Customizer] DEBUG detached Learn probe SKIPPED " +
                                "(Curve-5D-D2): SkillRecord.Learn has Harmony hooks; " +
                                "unknown side effects cannot be isolated.");
                    return;
                }

                int liveLevel = liveSkill.Level;
                float liveLevelXp = liveSkill.xpSinceLastLevel;
                float liveDailyXp = liveSkill.xpSinceMidnight;
                int liveSkillCount = pawn.skills.skills.Count;
                float cap = LearningCapacity.LearningCapacityService.GetDailyCap(liveSkill);
                AssertFinite(cap, "D2 live daily cap");

                // Test 1: the real game's Learn() must multiply ordinary XP
                // by the Harmony-patched normal learning rate.
                SkillRecord normal = CreateDetachedSkill(pawn, liveSkill);
                float normalRate = normal.LearnRateFactor(false);
                float threshold = SkillRecord.XpRequiredToLevelUpFrom(3);
                AssertFinite(normalRate, "D2 normal learning rate");
                AssertFinite(threshold, "D2 level threshold");
                if (normalRate < 0f || threshold < 1f)
                {
                    throw new InvalidOperationException("D2 invalid rate/threshold.");
                }
                float sampleXp = System.Math.Min(8f,
                    threshold * 0.01f / System.Math.Max(normalRate, 1f));
                if (sampleXp <= 0f)
                {
                    throw new InvalidOperationException("D2 sample XP is nonpositive.");
                }
                float expectedNormal = sampleXp * normalRate;
                normal.Learn(sampleXp, false);
                AssertDetachedLearnResult(normal, expectedNormal,
                    expectedNormal, "ordinary");

                // Test 2: direct XP must use the actual direct learning rate
                // and must not increment the daily learned-XP counter.
                SkillRecord direct = CreateDetachedSkill(pawn, liveSkill);
                float directRate = direct.LearnRateFactor(true);
                AssertFinite(directRate, "D2 direct learning rate");
                if (directRate < 0f)
                {
                    throw new InvalidOperationException("D2 direct rate is negative.");
                }
                float directSample = System.Math.Min(sampleXp,
                    threshold * 0.01f / System.Math.Max(directRate, 1f));
                float expectedDirect = directSample * directRate;
                direct.Learn(directSample, true);
                AssertDetachedLearnResult(direct, expectedDirect,
                    0f, "direct");

                // Test 3: a detached record whose daily XP is above its
                // dynamic cap must take the post-cap LearnRateFactor path.
                // A floating-point cap too large for safe threshold
                // manipulation is deliberately not used for this case.
                bool saturationChecked = false;
                float saturatedRate = 0f;
                if (cap >= 0f && cap <= 1000000f)
                {
                    SkillRecord saturated = CreateDetachedSkill(pawn, liveSkill);
                    float overCapXp = cap + System.Math.Max(64f, cap * 0.01f);
                    saturated.xpSinceMidnight = overCapXp;
                    if (!saturated.LearningSaturatedToday)
                    {
                        throw new InvalidOperationException(
                            "D2 detached record did not saturate above its cap.");
                    }
                    saturatedRate = saturated.LearnRateFactor(false);
                    AssertFinite(saturatedRate, "D2 saturated learning rate");
                    if (saturatedRate < 0f)
                    {
                        throw new InvalidOperationException("D2 saturated rate is negative.");
                    }
                    float satSample = System.Math.Min(sampleXp,
                        threshold * 0.01f / System.Math.Max(saturatedRate, 1f));
                    float expectedSaturated = satSample * saturatedRate;
                    saturated.Learn(satSample, false);
                    AssertDetachedLearnResult(saturated, expectedSaturated,
                        overCapXp + expectedSaturated, "post-cap");
                    saturationChecked = true;
                }

                // Guard: the original, SAVED skill remains untouched. No
                // test record may have been registered in the Pawn tracker.
                if (liveSkill.Level != liveLevel ||
                    liveSkill.xpSinceLastLevel != liveLevelXp ||
                    liveSkill.xpSinceMidnight != liveDailyXp ||
                    pawn.skills.skills.Count != liveSkillCount)
                {
                    throw new InvalidOperationException(
                        "D2 test touched the saved Pawn's skill data.");
                }

                Log.Message("[XP Customizer] DEBUG detached Learn probe passed " +
                    "(Curve-5D-D2): skill=" + liveSkill.def.defName +
                    ", normalXP=" + expectedNormal.ToString("0.####") +
                    ", directXP=" + expectedDirect.ToString("0.####") +
                    ", postCapRate=" + saturatedRate.ToString("0.####") +
                    ", saturationChecked=" + saturationChecked +
                    ", savedSkillUntouched=True. " +
                    "Actual Learn() executed on detached SkillRecords only; " +
                    "no real pawn skill XP changed.");
            }
            catch (Exception e)
            {
                Log.Error("[XP Customizer] DEBUG detached Learn probe FAILED " +
                    "(Curve-5D-D2): " + e);
            }
        }

    }
}
#endif
