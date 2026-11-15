#if DEBUG
using System;
using XpCustomizer.Compatibility;
using Verse;

namespace XpCustomizer
{
    internal static class CompatibilityAuditSelfTests
    {
        internal static void Verify(CompatibilityAuditSnapshot snapshot)
        {
            // No synthetic Harmony patches or pawn/skill/Def mutations.
            bool expectedCoverage = snapshot.MissingExpectedTargets.Count == 0;
            bool noDuplicateTargets = true;
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (CompatibilityTargetRecord target in snapshot.Targets)
                if (!seen.Add(target.Method)) noDuplicateTargets = false;
            bool consistentCounts = true;
            int owned = 0;
            int foreign = 0;
            foreach (CompatibilityTargetRecord target in snapshot.Targets)
                foreach (CompatibilityPatchRecord record in target.Patches)
                    if (record.IsOwn) owned++; else foreign++;
            consistentCounts = owned == snapshot.OwnHooks && foreign == snapshot.ForeignHooks;

            Log.Message("[XPC-CA][DEBUG_SELFTEST] build=" + XpCustomizerMod.BuildId +
                "; expectedTargetsPresent=" + expectedCoverage +
                "; noDuplicateTargets=" + noDuplicateTargets +
                "; countsConsistent=" + consistentCounts +
                "; ownHooks=" + owned +
                "; status=" + (expectedCoverage && noDuplicateTargets && consistentCounts ?
                    "PASS" : "FAIL") + "; gameplayWrites=0");
        }
    }
}
#endif
