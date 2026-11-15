#if DEBUG
using System;
using System.Text;
using XpCustomizer.Profiles;
using Verse;

namespace XpCustomizer
{
    /// <summary>Pure startup checks. Actual filesystem API is manual DebugAction only.</summary>
    internal static class DetachedTempFileTransactionSelfTests
    {
        // False by default and on any incomplete/throwing startup suite.
        // Read only by the B3-A Debug action; never gates vanilla saves.
        internal static bool StartupPassed { get; private set; }

        internal static void Run()
        {
            StartupPassed = false;
            int checks = 0;
            Action<bool, string> test = delegate(bool success, string label)
            {
                checks++;
                if (!success) throw new InvalidOperationException(
                    "B3A TEMP_COMMIT_SELFTEST: " + label);
            };
            byte[] before = Encoding.UTF8.GetBytes("<x>a</x>");
            byte[] same = Encoding.UTF8.GetBytes("<x>a</x>");
            byte[] after = Encoding.UTF8.GetBytes("<x>b</x>");
            string original = DetachedTempFileTransactionAudit.Sha(before);
            string proposed = DetachedTempFileTransactionAudit.Sha(after);
            test(original.Length == 64, "sha len");
            test(original == DetachedTempFileTransactionAudit.Sha(same), "sha deterministic");
            test(original != proposed, "sha distinguish");
            test(DetachedTempFileTransactionAudit.Sha(null) == "NONE", "null not verified");
            string root = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath());
            string child = System.IO.Path.Combine(root, "xpc_b3a_fixture.xml");
            test(DetachedTempFileTransactionAudit.IsDirectChild(child, root), "direct child accepted");
            test(!DetachedTempFileTransactionAudit.IsDirectChild(root, root), "parent rejected");
            test(!DetachedTempFileTransactionAudit.IsDirectChild(
                System.IO.Path.Combine(root, "nested", "file.xml"), root),
                "nested child rejected");
            test(!DetachedTempFileTransactionAudit.IsDirectChild(
                System.IO.Path.Combine(root, "..", "else.xml"), root),
                "traversal rejected");
            // B3-A-F1 regression: symmetric normalization, root preservation,
            // direct-child scope and malformed paths; no file IO.
            char separator = System.IO.Path.DirectorySeparatorChar;
            string rootWithSlash = root.TrimEnd(separator,
                System.IO.Path.AltDirectorySeparatorChar) + separator;
            string rootWithoutSlash = root.TrimEnd(separator,
                System.IO.Path.AltDirectorySeparatorChar);
            string childWithSlash = child + separator;
            string sibling = System.IO.Path.Combine(root, "..", "xpc_b3a_sibling.xml");
            test(!DetachedTempFileTransactionAudit.IsDirectChild(rootWithSlash, root),
                "identical root trailing rejected");
            test(!DetachedTempFileTransactionAudit.IsDirectChild(root, rootWithSlash),
                "identical root reverse rejected");
            test(!DetachedTempFileTransactionAudit.IsDirectChild(rootWithoutSlash,
                rootWithSlash), "identical root trimmed rejected");
            test(DetachedTempFileTransactionAudit.IsDirectChild(child, rootWithSlash),
                "trailing parent accepts direct child");
            test(DetachedTempFileTransactionAudit.IsDirectChild(childWithSlash, root),
                "trailing child normalized");
            test(!DetachedTempFileTransactionAudit.IsDirectChild(sibling, root),
                "sibling rejected");
            test(!DetachedTempFileTransactionAudit.IsDirectChild(
                System.IO.Path.Combine(root, "nested", "..", "..", "escape.xml"), root),
                "double traversal rejected");
            test(!DetachedTempFileTransactionAudit.IsDirectChild("", root),
                "empty child rejected");
            test(!DetachedTempFileTransactionAudit.IsDirectChild(child, " "),
                "blank parent rejected");
            test(!DetachedTempFileTransactionAudit.IsDirectChild("bad\0path", root),
                "invalid path rejected");
            string volumeRoot = System.IO.Path.GetPathRoot(root);
            test(!DetachedTempFileTransactionAudit.IsDirectChild(volumeRoot, volumeRoot),
                "filesystem root itself rejected");
            test(DetachedTempFileTransactionAudit.IsDirectChild(
                System.IO.Path.Combine(volumeRoot, "xpc_b3a_root_fixture.xml"), volumeRoot),
                "filesystem root direct child accepted");
            // The .NET/Mono runtime need not understand UNC on every platform.
            // Only assert UNC behavior when Windows resolves a share root.
            if (System.IO.Path.DirectorySeparatorChar == '\\')
            {
                const string uncRoot = @"\\xpc-synthetic-host\share\";
                try
                {
                    string resolved = System.IO.Path.GetFullPath(uncRoot);
                    if (String.Equals(System.IO.Path.GetPathRoot(resolved), resolved,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        test(!DetachedTempFileTransactionAudit.IsDirectChild(uncRoot,
                            uncRoot), "UNC share root itself rejected");
                        test(DetachedTempFileTransactionAudit.IsDirectChild(
                            System.IO.Path.Combine(uncRoot, "fixture.xml"), uncRoot),
                            "UNC share root direct child accepted");
                    }
                }
                catch (ArgumentException) { /* Mono does not support this UNC shape. */ }
                catch (NotSupportedException) { /* UNC unsupported on this runtime. */ }
            }
            ProfileXmlPersistenceEvidencePolicy p =
                new ProfileXmlPersistenceEvidencePolicy();
            test(p.Prepare(original, original, proposed, true) ==
                ProfileXmlEvidenceResult.Prepared, "policy prepare");
            test(p.BeginWrite(original) == ProfileXmlEvidenceResult.WriteAllowed,
                "policy begin");
            test(p.ObserveWriterReturn(true, proposed) ==
                ProfileXmlEvidenceResult.WrittenVerified && p.HasVerifiedNewXml,
                "only normal return + readback success");
            ProfileXmlPersistenceEvidencePolicy stale =
                new ProfileXmlPersistenceEvidencePolicy();
            test(stale.Prepare(original, original, proposed, true) ==
                ProfileXmlEvidenceResult.Prepared, "stale fixture prepare");
            test(stale.BeginWrite(proposed) ==
                ProfileXmlEvidenceResult.BlockedChangedTarget && !stale.WriteAttempted,
                "stale prewrite blocks");
            ProfileXmlPersistenceEvidencePolicy broken =
                new ProfileXmlPersistenceEvidencePolicy();
            test(broken.Prepare(original, proposed, proposed, true) ==
                ProfileXmlEvidenceResult.BlockedInvalidEvidence,
                "backup mismatch blocks");
            ProfileXmlPersistenceEvidencePolicy uncertain =
                new ProfileXmlPersistenceEvidencePolicy();
            test(uncertain.Prepare(original, original, proposed, true) ==
                ProfileXmlEvidenceResult.Prepared, "uncertain fixture prepared");
            test(uncertain.BeginWrite(original) == ProfileXmlEvidenceResult.WriteAllowed,
                "uncertain fixture began");
            test(uncertain.ObserveWriteFailure(false, original) ==
                ProfileXmlEvidenceResult.PersistenceUncertain &&
                uncertain.UncertaintyEverObserved, "exception uncertain even old hash");
            test(uncertain.ObserveRestore(true, true, original) ==
                ProfileXmlEvidenceResult.RestoreVerifiedButUncertain &&
                uncertain.DiskStateUncertain, "restore not a pass");
            StartupPassed = true;
            Log.Message("[XPC-R6P1D2C4B3A][TEMP_COMMIT_SELFTEST] status=PASS; checks=" +
                checks + "; scope=PURE_SYNTHETIC_NO_FILE_IO; fileReplaceCalls=0; " +
                "configWrites=0; globalScribeCalls=0; productionApply=NOT_EXERCISED_BY_THIS_TEST; build=" +
                XpCustomizerMod.BuildId);
            Log.Message("[XPC-R6P1D2C4B3AF1][TEMP_PATH_SELFTEST] status=PASS; " +
                "checks=" + checks + "; startupGate=OPEN; pathNormalization=SYMMETRIC; " +
                "rootPreserved=True; osPathCase=" +
                (System.IO.Path.DirectorySeparatorChar == '\\' ?
                    "ORDINAL_IGNORE_CASE" : "ORDINAL") +
                "; actualConfigWrites=0; productionApply=NOT_EXERCISED_BY_THIS_TEST; build=" +
                XpCustomizerMod.BuildId);
        }
    }
}
#endif
