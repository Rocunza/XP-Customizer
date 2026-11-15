using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace XpCustomizer.Compatibility
{
    internal static class HarmonyPatchDiscovery
    {
        internal const string OwnHarmonyId = "zcn.xpcustomizer";

        internal static CompatibilityAuditSnapshot Capture()
        {
            var snapshot = new CompatibilityAuditSnapshot();
            Dictionary<string, string> expected =
                CompatibilityFeatureCatalog.BuildMap(snapshot.MissingExpectedTargets);
            var targets = new SortedDictionary<string, CompatibilityTargetRecord>(
                StringComparer.Ordinal);

            // All registered methods, including future self-patches not in the
            // old 10-target Debug audit. No Harmony.Patch/Unpatch calls are made.
            foreach (MethodBase method in Harmony.GetAllPatchedMethods())
            {
                if (method == null) continue;
                try
                {
                    Patches info = Harmony.GetPatchInfo(method);
                    if (info == null || !HasOwnPatch(info)) continue;
                    string methodKey = CompatibilityFeatureCatalog.MethodKey(method);
                    CompatibilityTargetRecord target = new CompatibilityTargetRecord();
                    target.Method = methodKey;
                    string features;
                    if (!expected.TryGetValue(methodKey, out features))
                    {
                        features = "UNMAPPED_XPC_TARGET";
                        snapshot.UnmappedTargets.Add(methodKey);
                    }
                    target.Features = features;

                    CaptureKind(target, "Prefix", info.Prefixes);
                    CaptureKind(target, "Postfix", info.Postfixes);
                    CaptureKind(target, "Transpiler", info.Transpilers);
                    CaptureKind(target, "Finalizer", info.Finalizers);
                    target.Patches.Sort((a, b) => string.CompareOrdinal(
                        a.StableKey(), b.StableKey()));
                    targets[methodKey] = target;
                }
                catch (Exception ex)
                {
                    snapshot.CollectionErrors.Add("PATCH_READ_FAILED:" +
                        (method.DeclaringType == null ? "<unknown-type>" :
                            method.DeclaringType.FullName) + "." + method.Name + ":" +
                        ex.GetType().Name);
                }
            }

            // Missing expected methods must not silently vanish merely because
            // GetAllPatchedMethods() only reports registered patches.
            foreach (KeyValuePair<string, string> entry in expected)
            {
                if (!targets.ContainsKey(entry.Key))
                    snapshot.MissingExpectedTargets.Add("EXPECTED_OWN_TARGET_NOT_PATCHED:" + entry.Key);
            }

            // Resolve every patch against active mod *assemblies*, not owner-name guesses.
            var resolver = new HarmonyModIdentityResolver();
            foreach (CompatibilityTargetRecord target in targets.Values)
            {
                foreach (CompatibilityPatchRecord record in target.Patches)
                {
                    // The reflection handle is used only during the audit.
                    try { resolver.Resolve(record.ReflectionMethod, record); }
                    catch (Exception ex)
                    {
                        record.IdentityStatus = "RESOLVE_ERROR:" + ex.GetType().Name;
                        record.ModName = "UNKNOWN";
                        record.PackageId = "UNKNOWN";
                        snapshot.CollectionErrors.Add("IDENTITY_RESOLVE_FAILED:" +
                            record.Owner + ":" + ex.GetType().Name);
                    }
                    record.ReflectionMethod = null;
                    if (record.IsOwn) snapshot.OwnHooks++;
                    else
                    {
                        snapshot.ForeignHooks++;
                        if (record.IdentityStatus == "AMBIGUOUS_ASSEMBLY")
                            snapshot.AmbiguousIdentities++;
                        else if (record.IdentityStatus != "EXACT_ASSEMBLY_MATCH")
                            snapshot.UnknownIdentities++;
                    }
                }
                if (target.ForeignCount > 0) snapshot.ForeignTargets++;
                snapshot.Targets.Add(target);
            }
            snapshot.MissingExpectedTargets.Sort(StringComparer.Ordinal);
            snapshot.UnmappedTargets.Sort(StringComparer.Ordinal);
            snapshot.CollectionErrors.Sort(StringComparer.Ordinal);
            return snapshot;
        }

        private static bool HasOwnPatch(Patches info)
        {
            return AnyOwn(info.Prefixes) || AnyOwn(info.Postfixes) ||
                AnyOwn(info.Transpilers) || AnyOwn(info.Finalizers);
        }

        private static bool AnyOwn(IEnumerable<Patch> patches)
        {
            foreach (Patch patch in patches)
                if (patch.owner == OwnHarmonyId) return true;
            return false;
        }

        private static void CaptureKind(CompatibilityTargetRecord target, string kind,
            IEnumerable<Patch> patches)
        {
            foreach (Patch patch in patches)
            {
                MethodInfo method = patch.PatchMethod;
                var record = new CompatibilityPatchRecord
                {
                    ReflectionMethod = method,
                    Target = target.Method,
                    Owner = patch.owner ?? "<empty>",
                    Kind = kind,
                    PatchMethod = method == null ? "<missing>" :
                        CompatibilityFeatureCatalog.MethodKey(method),
                    AssemblyName = method == null ? "UNKNOWN" : method.Module.Assembly.GetName().Name,
                    Priority = patch.priority,
                    Before = JoinIds(patch.before),
                    After = JoinIds(patch.after),
                    IsOwn = patch.owner == OwnHarmonyId,
                    ModName = "UNKNOWN",
                    PackageId = "UNKNOWN",
                    IdentityStatus = "UNRESOLVED"
                };
                target.Patches.Add(record);
            }
        }

        private static string JoinIds(string[] ids)
        {
            if (ids == null || ids.Length == 0) return "-";
            var copy = new List<string>(ids);
            copy.Sort(StringComparer.Ordinal);
            return string.Join(",", copy.ToArray());
        }
    }
}
