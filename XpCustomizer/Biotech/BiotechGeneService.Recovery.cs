using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace XpCustomizer.Biotech
{
    /// <summary>
    /// R6-P0-D3: snapshot the *current* generated skill-gene values and our
    /// ownership bookkeeping before attempting a profile transition.
    /// No general GeneDef rewriting or other-mod state is changed by capture.
    /// </summary>
    public static partial class BiotechGeneService
    {
        internal const string RecoveryRevision = "R6P0D3-BIOTECH-OWNERSHIP-ROLLBACK-20261106";

        private sealed class LiveGeneState
        {
            internal readonly GeneDef Gene;
            internal readonly Aptitude Aptitude;
            internal readonly int Level;
            internal readonly int Complexity;
            internal readonly int Metabolism;
            internal readonly string Description;
            internal readonly object CachedDescription;

            internal LiveGeneState(GeneDef gene)
            {
                Gene = gene;
                Aptitude = gene.aptitudes[0];
                Level = Aptitude.level;
                Complexity = gene.biostatCpx;
                Metabolism = gene.biostatMet;
                Description = gene.description;
                CachedDescription = CachedDescriptionField == null ? null :
                    CachedDescriptionField.GetValue(gene);
            }

            internal void Restore()
            {
                Aptitude.level = Level;
                Gene.biostatCpx = Complexity;
                Gene.biostatMet = Metabolism;
                Gene.description = Description;
                if (CachedDescriptionField != null)
                    CachedDescriptionField.SetValue(Gene, CachedDescription);
            }

            internal bool Matches()
            {
                return Gene != null && Aptitude != null &&
                    !Gene.aptitudes.NullOrEmpty() &&
                    object.ReferenceEquals(Gene.aptitudes[0], Aptitude) &&
                    Aptitude.level == Level && Gene.biostatCpx == Complexity &&
                    Gene.biostatMet == Metabolism && Gene.description == Description &&
                    (CachedDescriptionField == null ||
                     object.Equals(CachedDescriptionField.GetValue(Gene), CachedDescription));
            }
        }

        internal sealed class RollbackSnapshot
        {
            private readonly List<LiveGeneState> genes;
            private readonly Dictionary<GeneDef, OriginalGeneState> ownership;
            private readonly bool valuesApplied;

            private RollbackSnapshot(List<LiveGeneState> current,
                Dictionary<GeneDef, OriginalGeneState> beforeOwnership, bool beforeApplied)
            {
                genes = current;
                ownership = beforeOwnership;
                valuesApplied = beforeApplied;
            }

            internal static RollbackSnapshot Capture()
            {
                List<LiveGeneState> current = new List<LiveGeneState>();
                foreach (GeneDef def in DefDatabase<GeneDef>.AllDefsListForReading)
                {
                    if (GetSkillGeneType(def) == SkillGeneType.None ||
                        def.aptitudes.NullOrEmpty() || def.aptitudes[0] == null ||
                        def.aptitudes[0].skill == null)
                        continue;
                    current.Add(new LiveGeneState(def));
                }
                return new RollbackSnapshot(current,
                    new Dictionary<GeneDef, OriginalGeneState>(OriginalGeneStates), customValuesApplied);
            }

            // D4 read-only equality check. Does not restore or invalidate anything.
            internal int GeneCount { get { return genes.Count; } }
            internal int OwnershipCount { get { return ownership.Count; } }
            internal bool WasApplied { get { return valuesApplied; } }

            internal bool MatchesCurrent(out string reason)
            {
                reason = "NONE";
                try
                {
                    for (int i = 0; i < genes.Count; i++)
                    {
                        if (!genes[i].Matches())
                        {
                            reason = "GENE_MISMATCH:" + i;
                            return false;
                        }
                    }
                    if (customValuesApplied != valuesApplied ||
                        OriginalGeneStates.Count != ownership.Count)
                    {
                        reason = "OWNERSHIP_COUNT_OR_FLAG_MISMATCH";
                        return false;
                    }
                    foreach (KeyValuePair<GeneDef, OriginalGeneState> entry in ownership)
                    {
                        OriginalGeneState now;
                        if (!OriginalGeneStates.TryGetValue(entry.Key, out now) ||
                            !object.ReferenceEquals(now, entry.Value))
                        {
                            reason = "OWNERSHIP_ENTRY_MISMATCH";
                            return false;
                        }
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    reason = "READ_ONLY_VERIFY_EXCEPTION:" + ex.GetType().Name;
                    return false;
                }
            }

            internal bool RestoreAndVerify(out string reason)
            {
                reason = "NONE";
                bool ok = true;
                // Try every GeneDef even if one fails, never claim a partial
                // recovery as success.
                for (int i = 0; i < genes.Count; i++)
                {
                    try { genes[i].Restore(); }
                    catch (Exception ex)
                    {
                        ok = false;
                        reason = "GENE_RESTORE:" + i + ":" + ex.GetType().Name;
                    }
                }
                if (ok)
                {
                    try
                    {
                        OriginalGeneStates.Clear();
                        foreach (KeyValuePair<GeneDef, OriginalGeneState> entry in ownership)
                            OriginalGeneStates.Add(entry.Key, entry.Value);
                        customValuesApplied = valuesApplied;
                    }
                    catch (Exception ex)
                    {
                        ok = false;
                        reason = "OWNERSHIP_RESTORE:" + ex.GetType().Name;
                    }
                }
                // Existing pawns may have seen a transient aptitude value.
                try { DirtyExistingPawnAptitudes(); }
                catch (Exception ex)
                {
                    ok = false;
                    reason = "PAWN_APTITUDE_INVALIDATION:" + ex.GetType().Name;
                }
                try
                {
                    for (int i = 0; i < genes.Count; i++)
                    {
                        if (!genes[i].Matches())
                        {
                            ok = false;
                            reason = "GENE_VERIFY:" + i;
                        }
                    }
                    if (customValuesApplied != valuesApplied || OriginalGeneStates.Count != ownership.Count)
                    {
                        ok = false;
                        reason = "OWNERSHIP_VERIFY:COUNT_OR_FLAG";
                    }
                    foreach (KeyValuePair<GeneDef, OriginalGeneState> entry in ownership)
                    {
                        OriginalGeneState found;
                        if (!OriginalGeneStates.TryGetValue(entry.Key, out found) ||
                            !object.ReferenceEquals(found, entry.Value))
                        {
                            ok = false;
                            reason = "OWNERSHIP_VERIFY:ENTRY";
                        }
                    }
                }
                catch (Exception ex)
                {
                    ok = false;
                    reason = "GENE_VERIFY_EXCEPTION:" + ex.GetType().Name;
                }
                return ok;
            }
        }

        internal static RollbackSnapshot CaptureRollbackSnapshot()
        {
            if (!ModsConfig.BiotechActive)
                throw new InvalidOperationException("BIOTECH_DLC_NOT_ACTIVE");
            return RollbackSnapshot.Capture();
        }

        internal static bool RestoreRollbackSnapshot(RollbackSnapshot snapshot, out string reason)
        {
            if (snapshot == null)
            {
                reason = "NULL_GENE_BACKUP";
                return false;
            }
            return snapshot.RestoreAndVerify(out reason);
        }
    }
}
