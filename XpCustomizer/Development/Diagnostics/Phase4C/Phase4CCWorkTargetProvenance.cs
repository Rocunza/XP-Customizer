#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using RimWorld;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// R4-S3 read-only target evidence for work SkillRecord.Learn() calls.
    /// TargetA is the current job target, not proof that this Learn came from
    /// that job's toil. Designations may be removed before the final tick.
    /// No job, plant, designation, Pawn, or SkillRecord is mutated here.
    /// </summary>
    internal static partial class Phase4CCLiveXpCapture
    {
        private sealed class WorkTargetEvidence
        {
            internal string TargetDef = "NONE";
            internal string TargetId = "NONE";
            internal string YieldDef = "NONE";
            internal string Designation = "NONE";
            internal string WorkGiver = "UNAVAILABLE";
            internal string EvidenceStatus = "NO_TARGET";
            internal bool IsPlant;
            internal bool YieldsWood;
        }

        private static readonly FieldInfo WorkGiverDefField =
            typeof(Verse.AI.Job).GetField("workGiverDef",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static WorkTargetEvidence InspectWorkTarget(Pawn pawn)
        {
            var e = new WorkTargetEvidence();
            try
            {
                if (pawn == null || pawn.CurJob == null)
                {
                    e.EvidenceStatus = "NO_JOB";
                    return e;
                }
                // WorkGiver is optional. Reading a missing member must not disable
                // observation or imply that this job originated at the WorkGiver.
                try
                {
                    if (WorkGiverDefField != null)
                    {
                        Def giver = WorkGiverDefField.GetValue(pawn.CurJob) as Def;
                        if (giver != null) e.WorkGiver = Clean(giver.defName);
                    }
                }
                catch (Exception) { e.WorkGiver = "UNAVAILABLE"; }

                Thing thing = pawn.CurJob.targetA.Thing;
                if (thing == null)
                {
                    e.EvidenceStatus = "NO_THING_TARGET_A";
                    return e;
                }
                e.TargetDef = Clean(thing.def == null ? "UNKNOWN" : thing.def.defName);
                e.TargetId = Clean(thing.ThingID);
                e.IsPlant = thing is Plant;
                if (!e.IsPlant || thing.def == null || thing.def.plant == null)
                {
                    e.EvidenceStatus = "NON_PLANT_TARGET_A";
                    return e;
                }
                ThingDef harvested = thing.def.plant.harvestedThingDef;
                e.YieldDef = harvested == null ? "NONE" : Clean(harvested.defName);
                e.YieldsWood = string.Equals(e.YieldDef, "WoodLog", StringComparison.Ordinal);
                Map map = pawn.Map;
                if (map == null || map.designationManager == null)
                {
                    e.EvidenceStatus = "NO_MAP_DESIGNATION_MANAGER";
                    return e;
                }
                bool cut = map.designationManager.DesignationOn(thing, DesignationDefOf.CutPlant) != null;
                bool harvest = map.designationManager.DesignationOn(thing, DesignationDefOf.HarvestPlant) != null;
                e.Designation = cut && harvest ? "BOTH" : cut ? "CutPlant" :
                    harvest ? "HarvestPlant" : "NONE";
                e.EvidenceStatus = cut && harvest ? "AMBIGUOUS_BOTH_DESIGNATIONS" :
                    cut || harvest ? "TARGET_AND_DESIGNATION_OBSERVED" : "DESIGNATION_NOT_OBSERVED";
            }
            catch (Exception ex)
            {
                // Optional provenance failure must not become an XP formula error.
                e.Designation = "UNKNOWN";
                e.EvidenceStatus = "INCONCLUSIVE_READ_" + ex.GetType().Name;
            }
            return e;
        }

        private static string ResolvedWorkKind(string job, WorkTargetEvidence e)
        {
            string fallback = WorkKind(job);
            if (fallback != "HARVEST_CANDIDATE" && fallback != "CUTTING_CANDIDATE")
                return fallback;
            if (e == null || !e.IsPlant ||
                e.EvidenceStatus != "TARGET_AND_DESIGNATION_OBSERVED")
                return fallback;
            // A designation is contextual evidence, not a call-site trace.
            // A job explicitly named CutPlant* but marked HarvestPlant (or vice
            // versa) is inconsistent: preserve the old bucket and log it.
            if (job.IndexOf("CutPlant", StringComparison.OrdinalIgnoreCase) >= 0 &&
                e.Designation == "HarvestPlant")
                return fallback;
            if (e.Designation == "CutPlant")
                return "CUTTING_CANDIDATE";
            if (e.Designation == "HarvestPlant")
                return e.YieldsWood ? "CUTTING_CANDIDATE" : "HARVEST_CANDIDATE";
            return fallback;
        }

        private static string TargetAction(WorkTargetEvidence e)
        {
            if (e == null || e.EvidenceStatus != "TARGET_AND_DESIGNATION_OBSERVED")
                return "UNVERIFIED";
            if (e.Designation == "CutPlant")
                return e.YieldsWood ? "WOOD_CUT_DESIGNATED" : "PLANT_CUT_DESIGNATED";
            if (e.Designation == "HarvestPlant")
                return e.YieldsWood ? "WOOD_YIELD_HARVEST_DESIGNATED" : "PLANT_HARVEST_DESIGNATED";
            return "UNVERIFIED";
        }

        private static void AppendTargetFields(StringBuilder sb, StartState b)
        {
            WorkTargetEvidence e = b.TargetEvidence;
            if (e == null) return;
            sb.Append("; targetA=").Append(e.TargetDef)
                .Append("; targetId=").Append(e.TargetId)
                .Append("; targetIsPlant=").Append(e.IsPlant)
                .Append("; yieldDef=").Append(e.YieldDef)
                .Append("; woodYield=").Append(e.YieldsWood)
                .Append("; targetDesignation=").Append(e.Designation)
                .Append("; targetAction=").Append(TargetAction(e))
                .Append("; workGiver=").Append(e.WorkGiver)
                .Append("; targetEvidence=").Append(e.EvidenceStatus);
        }

        private static void AppendTargetCoverage(StringBuilder sb)
        {
            int captured = 0, woodCut = 0, woodHarvest = 0, plantCut = 0;
            int plantHarvest = 0, unresolved = 0, reclassified = 0;
            int shown = 0;
            var emitted = new HashSet<string>(StringComparer.Ordinal);
            foreach (Sample s in samples)
            {
                StartState b = s.Before;
                if (b.Category != "WORK_CANDIDATE") continue;
                captured++;
                string action = TargetAction(b.TargetEvidence);
                if (action == "WOOD_CUT_DESIGNATED") woodCut++;
                else if (action == "WOOD_YIELD_HARVEST_DESIGNATED") woodHarvest++;
                else if (action == "PLANT_CUT_DESIGNATED") plantCut++;
                else if (action == "PLANT_HARVEST_DESIGNATED") plantHarvest++;
                else unresolved++;
                if (b.Job == "HarvestDesignated" && b.WorkKind == "CUTTING_CANDIDATE")
                    reclassified++;
                // Distinct pawn/job/target/target-action shows contrasting plants
                // and trees even when numerous calls arrive from the same job.
                string key = b.PawnName + "|" + b.Job + "|" +
                    (b.TargetEvidence == null ? "NONE" : b.TargetEvidence.TargetDef) +
                    "|" + action + "|" + b.WorkKind;
                if (shown < 18 && emitted.Add(key))
                {
                    sb.Append("\n[XPC-4CC][WORK_TARGET] pawn=").Append(b.PawnName)
                        .Append("; job=").Append(b.Job)
                        .Append("; skill=").Append(b.Skill)
                        .Append("; resolvedKind=").Append(b.WorkKind)
                        .Append("; nameOnlyKind=").Append(WorkKind(b.Job));
                    AppendTargetFields(sb, b);
                    sb.Append("; attribution=TARGET_DESIGNATION_CONTEXT_NOT_CALLSITE_PROOF");
                    shown++;
                }
            }
            sb.Append("\n[XPC-4CC][WORK_TARGET_COVERAGE] captured=").Append(captured)
                .Append("; woodCutDesignated=").Append(woodCut)
                .Append("; woodYieldHarvestDesignated=").Append(woodHarvest)
                .Append("; plantCutDesignated=").Append(plantCut)
                .Append("; plantHarvestDesignated=").Append(plantHarvest)
                .Append("; unresolved=").Append(unresolved)
                .Append("; harvestJobReclassifiedToCutting=").Append(reclassified)
                .Append("; representativeRows=").Append(shown)
                .Append("; attribution=TARGET_DESIGNATION_CONTEXT_NOT_CALLSITE_PROOF")
                .Append("; notCapturedByQuota=SEE_WORK_KIND_COVERAGE");
        }

        private static void VerifyTargetProvenanceSelfAudit()
        {
            int cases = 0;
            RequireTargetKind("HarvestDesignated", true, true, "HarvestPlant", "CUTTING_CANDIDATE"); cases++;
            RequireTargetKind("HarvestDesignated", true, true, "CutPlant", "CUTTING_CANDIDATE"); cases++;
            RequireTargetKind("HarvestDesignated", true, false, "HarvestPlant", "HARVEST_CANDIDATE"); cases++;
            RequireTargetKind("CutPlantDesignated", true, false, "CutPlant", "CUTTING_CANDIDATE"); cases++;
            RequireTargetKind("HarvestDesignated", true, true, "NONE", "HARVEST_CANDIDATE"); cases++;
            RequireTargetKind("HarvestDesignated", false, true, "HarvestPlant", "HARVEST_CANDIDATE"); cases++;
            RequireTargetKind("HarvestDesignated", true, true, "BOTH", "HARVEST_CANDIDATE"); cases++;
            RequireTargetKind("CutPlantDesignated", true, true, "HarvestPlant", "CUTTING_CANDIDATE"); cases++;
            RequireTargetKind("Mine", true, true, "HarvestPlant", "MINING_CANDIDATE"); cases++;
            RequireTargetKind("FinishFrame", true, true, "CutPlant", "CONSTRUCTION_CANDIDATE"); cases++;
            RequireTargetAction(true, true, "CutPlant", "WOOD_CUT_DESIGNATED"); cases++;
            RequireTargetAction(true, true, "HarvestPlant", "WOOD_YIELD_HARVEST_DESIGNATED"); cases++;
            RequireTargetAction(true, false, "HarvestPlant", "PLANT_HARVEST_DESIGNATED"); cases++;
            RequireTargetAction(true, false, "CutPlant", "PLANT_CUT_DESIGNATED"); cases++;
            RequireTargetAction(true, true, "NONE", "UNVERIFIED"); cases++;
            Log.Message("[XPC-4CC][WORK_TARGET_AUDIT] status=PASS; cases=" + cases +
                "; syntheticOnly=True; noPawnWrites=True; noSettingsWrites=True" +
                "; woodHarvestNotCallsiteProof=True; revision=" + Revision);
        }

        private static WorkTargetEvidence SyntheticEvidence(bool isPlant, bool isWood, string designation)
        {
            return new WorkTargetEvidence {
                IsPlant = isPlant, YieldsWood = isWood, Designation = designation,
                EvidenceStatus = designation == "CutPlant" || designation == "HarvestPlant" ?
                    "TARGET_AND_DESIGNATION_OBSERVED" : "DESIGNATION_NOT_OBSERVED"
            };
        }

        private static void RequireTargetKind(string job, bool plant, bool wood,
            string designation, string expected)
        {
            string actual = ResolvedWorkKind(job, SyntheticEvidence(plant, wood, designation));
            if (actual != expected)
                throw new InvalidOperationException("4C-C R4-S3 work target classifier " + job +
                    "/" + designation + " expected=" + expected + " actual=" + actual);
        }
        private static void RequireTargetAction(bool plant, bool wood,
            string designation, string expected)
        {
            string actual = TargetAction(SyntheticEvidence(plant, wood, designation));
            if (actual != expected)
                throw new InvalidOperationException("4C-C R4-S3 target action " +
                    designation + " expected=" + expected + " actual=" + actual);
        }
    }
}
#endif
