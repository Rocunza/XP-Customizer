using System;
using System.IO;
using Verse;

namespace XpCustomizer
{
    /// <summary>Owned XML path + strict, bounded read. No Scribe, no global edits.</summary>
    internal static class ProfileOwnedXmlEvidence
    {
        internal const string Revision = "R6P1D2D2A-LOADED-XML-READONLY-20261108";
        internal static string GetPath(ModContentPack content)
        {
            if (content == null || String.IsNullOrEmpty(content.FolderName))
                throw new InvalidOperationException("MOD_CONTENT_UNAVAILABLE");
            string root = Path.GetFullPath(GenFilePaths.ConfigFolderPath);
            string name = GenText.SanitizeFilename(
                "Mod_" + content.FolderName + "_" + typeof(XpCustomizerMod).Name + ".xml");
            if (String.IsNullOrEmpty(name) || name == "." || name == ".." ||
                name.IndexOf('/') >= 0 || name.IndexOf('\\') >= 0)
                throw new InvalidOperationException("OWN_XML_NAME_INVALID");
            string path = Path.GetFullPath(Path.Combine(root, name));
            if (!String.Equals(Path.GetDirectoryName(path), root.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("OWN_XML_PATH_NOT_ALLOWED");
            return path;
        }

        internal static byte[] ReadStrict(string path)
        {
            if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("OWN_XML_NOT_REGULAR_FILE");
            using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read))
            {
                if (file.Length <= 0 || file.Length > OwnedXmlScanner.MaximumBytes)
                    throw new InvalidOperationException("OWN_XML_LENGTH_INVALID");
                byte[] bytes = new byte[(int)file.Length];
                int cursor = 0;
                while (cursor < bytes.Length)
                {
                    int read = file.Read(bytes, cursor, bytes.Length - cursor);
                    if (read <= 0) throw new IOException("OWN_XML_SHORT_READ");
                    cursor += read;
                }
                return bytes;
            }
        }

        internal static bool IsScribeIdle()
        {
            return Scribe.mode == LoadSaveMode.Inactive && Scribe.saver != null &&
                Scribe.saver.CurPath == null && !Scribe.saver.savingForDebug;
        }

        // The same pure contract is used by both runtime XML readback and
        // startup evidence. This intentionally does not reload Verse settings
        // or compare a specific saved profile JSON after a new process starts.
        internal static bool MatchesLoaded(byte[] xml, XpCustomizerSettings loaded,
            out int emitted, out int omitted, out int collections, out string failure)
        {
            OwnedXmlScanner.Inspection structure = OwnedXmlScanner.InspectXml(xml, loaded);
            OwnedXmlSemanticContract.Result semantic = OwnedXmlSemanticContract.Inspect(xml, loaded);
            emitted = structure.ComparedScalars;
            omitted = semantic.OmittedVerified;
            collections = structure.VerifiedCollections;
            bool pass = structure.Valid && semantic.Valid &&
                structure.ComparedScalars + semantic.OmittedVerified ==
                    OwnedXmlSemanticContract.ScalarCount &&
                structure.VerifiedCollections == OwnedXmlSemanticContract.CollectionCount &&
                semantic.Collections == OwnedXmlSemanticContract.CollectionCount;
            failure = pass ? "NONE" : structure.Failure + ":" + semantic.Failure;
            return pass;
        }

        internal static void ObserveStartup(ModContentPack content, XpCustomizerSettings loaded)
        {
            try
            {
                byte[] file = ReadStrict(GetPath(content));
                int emitted, omitted, collections;
                string failure;
                bool pass = MatchesLoaded(file, loaded,
                    out emitted, out omitted, out collections, out failure);
                Log.Message("[XPC-R6P1D2D2A][STARTUP_LOADED_SETTINGS_AUDIT] revision=" +
                    Revision + "; phase=STARTUP_AFTER_VANILLA_LOAD; status=" +
                    (pass ? "LOADED_MATCHES_OWN_XML" : "NOT_VERIFIED") +
                    "; xmlSha256=" + OwnedXmlScanner.Sha(file) +
                    "; xmlBytes=" + file.Length +
                    "; emitted=" + emitted +
                    "; omittedDefaults=" + omitted +
                    "; collections=" + collections +
                    "; reason=" + (pass ? "RAW_XML_SEMANTIC_MATCH_NO_ENGINE_RELOAD" :
                        failure) +
                    "; profileJsonComparison=NOT_CHECKED; restartProfileProof=NOT_VERIFIED" +
                    "; writes=0; build=" + XpCustomizerMod.BuildId);
            }
            catch (Exception ex)
            {
                Log.Warning("[XPC-R6P1D2D2A][STARTUP_LOADED_SETTINGS_AUDIT] " +
                    "status=NOT_VERIFIED; reason=" + ex.GetType().Name +
                    "; writes=0; build=" + XpCustomizerMod.BuildId);
            }
        }
    }
}
