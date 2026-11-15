using System;
using System.Collections.Generic;
using Verse;

namespace XpCustomizer.Compatibility
{
    internal static class CompatibilityAuditReporter
    {
        internal const string Schema = "XPC-COMPAT-AUDIT-V1";
        private const int MaxAutoPatchLines = 80;
        private static string lastSignature;

        // Mod names and Harmony IDs are untrusted external metadata. Keep logs
        // single-line and prevent forged field separators or excessive payloads.
        private static string Clean(string value)
        {
            if (value == null) return "UNKNOWN";
            value = value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ')
                .Replace(';', ',').Replace('=', ':');
            return value.Length > 240 ? value.Substring(0, 240) + "..." : value;
        }

        internal static void Report(CompatibilityAuditSnapshot snapshot,
            string phase, bool forceFull)
        {
            string signature = snapshot.StableSignature();
            if (!forceFull && lastSignature == signature) return;
            lastSignature = signature;

            var owners = new HashSet<string>(StringComparer.Ordinal);
            foreach (CompatibilityTargetRecord target in snapshot.Targets)
                foreach (CompatibilityPatchRecord patch in target.Patches)
                    if (!patch.IsOwn) owners.Add(patch.Owner);

            Log.Message("[XPC-CA][SUMMARY] schema=" + Schema +
                "; build=" + XpCustomizerMod.BuildId +
                "; phase=" + phase +
                "; status=" + (snapshot.CollectionErrors.Count > 0 ? "PARTIAL" :
                    (snapshot.MissingExpectedTargets.Count > 0 ||
                     snapshot.UnmappedTargets.Count > 0 ? "COVERAGE_WARNING" : "COMPLETE")) +
                "; ownTargets=" + snapshot.Targets.Count +
                "; ownHooks=" + snapshot.OwnHooks +
                "; foreignTargets=" + snapshot.ForeignTargets +
                "; foreignHooks=" + snapshot.ForeignHooks +
                "; foreignOwners=" + owners.Count +
                "; unidentified=" + snapshot.UnknownIdentities +
                "; ambiguous=" + snapshot.AmbiguousIdentities +
                "; missingExpected=" + snapshot.MissingExpectedTargets.Count +
                "; unmapped=" + snapshot.UnmappedTargets.Count +
                "; collectionErrors=" + snapshot.CollectionErrors.Count +
                "; evidence=HARMONY_REGISTRATION_ONLY; gameplayWrites=0");

            int printed = 0;
            foreach (CompatibilityTargetRecord target in snapshot.Targets)
            {
                int own = target.Patches.Count - target.ForeignCount;
                Log.Message("[XPC-CA][TARGET] method=" + target.Method +
                    "; features=" + target.Features +
                    "; ownHooks=" + own + "; foreignHooks=" + target.ForeignCount +
                    "; status=" + (target.ForeignCount == 0 ? "OWN_PATCH_ONLY" : "SHARED_PATCH") +
                    "; actualEffect=NOT_VERIFIED");
                foreach (CompatibilityPatchRecord patch in target.Patches)
                {
                    if (patch.IsOwn) continue;
                    if (!forceFull && printed >= MaxAutoPatchLines) continue;
                    printed++;
                    Log.Message("[XPC-CA][FOREIGN] target=" + patch.Target +
                        "; features=" + target.Features +
                        "; owner=" + Clean(patch.Owner) +
                        "; mod=" + Clean(patch.ModName) +
                        "; packageId=" + Clean(patch.PackageId) +
                        "; identity=" + patch.IdentityStatus +
                        "; assembly=" + Clean(patch.AssemblyName) +
                        "; kind=" + patch.Kind +
                        "; method=" + Clean(patch.PatchMethod) +
                        "; priority=" + patch.Priority +
                        "; before=" + Clean(patch.Before) +
                        "; after=" + Clean(patch.After) +
                        "; actualEffect=NOT_VERIFIED");
                }
            }
            if (!forceFull && snapshot.ForeignHooks > printed)
                Log.Warning("[XPC-CA][DETAIL_TRUNCATED] omitted=" +
                    (snapshot.ForeignHooks - printed) +
                    "; rescanWithFullReport=True");
            foreach (string missing in snapshot.MissingExpectedTargets)
                Log.Warning("[XPC-CA][COVERAGE] " + missing);
            foreach (string unmapped in snapshot.UnmappedTargets)
                Log.Warning("[XPC-CA][COVERAGE] UNMAPPED_XPC_TARGET:" + unmapped);
            foreach (string error in snapshot.CollectionErrors)
                Log.Warning("[XPC-CA][COLLECTOR] " + error);

            Log.Message("[XPC-CA][SCOPE] " + CompatibilityFeatureCatalog.OutOfScope +
                "; behaviorConflicts=NOT_DETERMINED; patchMutations=0; " +
                "settingsWrites=0; profileWrites=0; saveWrites=0");
        }
    }
}
