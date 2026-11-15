using System;
using System.Collections.Generic;
using System.Reflection;

namespace XpCustomizer.Compatibility
{
    // CA-1: detached, read-only representation. No Harmony or game objects escape the collector.
    internal sealed class CompatibilityPatchRecord
    {
        internal MethodInfo ReflectionMethod; // cleared after identity resolution
        internal string Target;
        internal string Owner;
        internal string Kind;
        internal string PatchMethod;
        internal string AssemblyName;
        internal int Priority;
        internal string Before;
        internal string After;
        internal bool IsOwn;
        internal string ModName;
        internal string PackageId;
        internal string IdentityStatus;

        internal string StableKey()
        {
            return Target + "|" + Owner + "|" + Kind + "|" + PatchMethod + "|" +
                AssemblyName + "|" + Priority + "|" + Before + "|" + After + "|" +
                IdentityStatus + "|" + PackageId + "|" + ModName;
        }
    }

    internal sealed class CompatibilityTargetRecord
    {
        internal string Method;
        internal string Features;
        internal readonly List<CompatibilityPatchRecord> Patches =
            new List<CompatibilityPatchRecord>();

        internal int ForeignCount
        {
            get
            {
                int count = 0;
                foreach (CompatibilityPatchRecord patch in Patches)
                    if (!patch.IsOwn) count++;
                return count;
            }
        }
    }

    internal sealed class CompatibilityAuditSnapshot
    {
        internal readonly List<CompatibilityTargetRecord> Targets =
            new List<CompatibilityTargetRecord>();
        internal readonly List<string> MissingExpectedTargets = new List<string>();
        internal readonly List<string> UnmappedTargets = new List<string>();
        internal readonly List<string> CollectionErrors = new List<string>();
        internal int OwnHooks;
        internal int ForeignHooks;
        internal int ForeignTargets;
        internal int UnknownIdentities;
        internal int AmbiguousIdentities;

        // Deterministic identity for duplicate suppression; do not use GetHashCode().
        internal string StableSignature()
        {
            var parts = new List<string>();
            foreach (CompatibilityTargetRecord target in Targets)
            {
                parts.Add("T:" + target.Method + ":" + target.Features);
                foreach (CompatibilityPatchRecord patch in target.Patches)
                    parts.Add("P:" + patch.StableKey());
            }
            foreach (string missing in MissingExpectedTargets) parts.Add("M:" + missing);
            foreach (string unmapped in UnmappedTargets) parts.Add("U:" + unmapped);
            foreach (string error in CollectionErrors) parts.Add("E:" + error);
            return string.Join("\n", parts.ToArray());
        }
    }
}
