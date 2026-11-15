using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace XpCustomizer.Compatibility
{
    internal sealed class HarmonyModIdentityResolver
    {
        private readonly Dictionary<Assembly, List<ModContentPack>> modByAssembly =
            new Dictionary<Assembly, List<ModContentPack>>();

        internal HarmonyModIdentityResolver()
        {
            // Use the actual assemblies loaded by active mods, never infer a
            // mod from the Harmony owner string alone.
            if (LoadedModManager.RunningModsListForReading == null) return;
            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
            {
                // A malformed third-party content pack must not disable the
                // audit for every other mod. Unknown identities remain honest.
                try
                {
                    if (mod == null || mod.assemblies == null ||
                        mod.assemblies.loadedAssemblies == null) continue;
                    foreach (Assembly assembly in mod.assemblies.loadedAssemblies)
                    {
                        if (assembly == null) continue;
                        List<ModContentPack> candidates;
                        if (!modByAssembly.TryGetValue(assembly, out candidates))
                        {
                            candidates = new List<ModContentPack>();
                            modByAssembly.Add(assembly, candidates);
                        }
                        if (!candidates.Contains(mod)) candidates.Add(mod);
                    }
                }
                catch (Exception)
                {
                    // Retain other assemblies; this mod might be unresolvable.
                }
            }
        }

        internal void Resolve(MethodInfo method, CompatibilityPatchRecord record)
        {
            if (method == null)
            {
                record.IdentityStatus = "PATCH_METHOD_MISSING";
                record.ModName = "UNKNOWN";
                record.PackageId = "UNKNOWN";
                return;
            }

            Assembly assembly = method.Module.Assembly;
            record.AssemblyName = assembly.GetName().Name;
            List<ModContentPack> mods;
            if (!modByAssembly.TryGetValue(assembly, out mods) || mods.Count == 0)
            {
                record.IdentityStatus = "OWNER_ONLY";
                record.ModName = "UNKNOWN";
                record.PackageId = "UNKNOWN";
                return;
            }
            if (mods.Count != 1)
            {
                record.IdentityStatus = "AMBIGUOUS_ASSEMBLY";
                record.ModName = "AMBIGUOUS";
                var ids = new List<string>();
                foreach (ModContentPack mod in mods) ids.Add(mod.PackageId);
                ids.Sort(StringComparer.Ordinal);
                record.PackageId = string.Join(",", ids.ToArray());
                return;
            }
            record.IdentityStatus = "EXACT_ASSEMBLY_MATCH";
            record.ModName = mods[0].Name ?? "<unnamed>";
            record.PackageId = mods[0].PackageId ?? "<no-package-id>";
        }
    }
}
