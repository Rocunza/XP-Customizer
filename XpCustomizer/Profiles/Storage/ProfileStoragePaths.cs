using System;
using System.Globalization;
using System.IO;
using Verse;

namespace XpCustomizer.Profiles
{
    /// <summary>Only trusted Config-relative/GUID-based names. No DisplayName-derived paths.</summary>
    internal sealed class ProfileStoragePaths
    {
        internal readonly string Root;
        internal string Trash { get { return Path.Combine(Root, "_Trash"); } }
        internal string Backups { get { return Path.Combine(Root, "_Backups"); } }
        internal string LockFile { get { return Path.Combine(Root, ".repository.lock"); } }

        private ProfileStoragePaths(string root)
        { Root = Path.GetFullPath(root); }

        internal static ProfileStoragePaths ForCurrentUser()
        {
            // RimWorld / Verse owns the per-user configuration directory.
            return ForConfigRoot(GenFilePaths.ConfigFolderPath);
        }

        internal static ProfileStoragePaths ForConfigRoot(string configFolder)
        {
            if (String.IsNullOrWhiteSpace(configFolder) || !Path.IsPathRooted(configFolder))
                throw new ArgumentException("Expected an absolute Config path.");
            return new ProfileStoragePaths(Path.Combine(configFolder, "XpCustomizer", "Profiles"));
        }

        internal static ProfileStoragePaths ForIsolatedTestRoot(string disposableRoot)
        {
            if (String.IsNullOrWhiteSpace(disposableRoot) || !Path.IsPathRooted(disposableRoot))
                throw new ArgumentException("Expected absolute isolated test directory.");
            return new ProfileStoragePaths(disposableRoot);
        }

        internal static bool TryCanonicalId(string raw, out string canonical)
        {
            canonical = null;
            Guid parsed;
            if (raw == null || !Guid.TryParseExact(raw, "D", out parsed)) return false;
            canonical = parsed.ToString("D");
            return raw == canonical;
        }

        internal string FileForId(string id)
        {
            string canonical;
            if (!TryCanonicalId(id, out canonical)) throw new ArgumentException("Invalid profile ID.");
            return Path.Combine(Root, canonical + ".json");
        }

        internal string NewTempName()
        { return Path.Combine(Root, ".stage-" + Guid.NewGuid().ToString("N") + ".tmp"); }

        internal string NewBackupName(string id)
        { return Path.Combine(Backups, id + "." + Guid.NewGuid().ToString("N") + ".bak"); }

        internal string NewTrashToken(string id)
        {
            string canonical;
            if (!TryCanonicalId(id, out canonical)) throw new ArgumentException("Invalid profile ID.");
            return canonical + "." + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture) +
                "." + Guid.NewGuid().ToString("N") + ".deleted";
        }

        internal static bool TryParseTrashToken(string token, out string id)
        {
            id = null;
            if (token == null || token.Length > 128 ||
                token.IndexOf(Path.DirectorySeparatorChar) >= 0 ||
                token.IndexOf(Path.AltDirectorySeparatorChar) >= 0) return false;
            string[] parts = token.Split('.');
            if (parts.Length != 4 || parts[3] != "deleted" || parts[2].Length != 32)
                return false;
            long ticks; Guid nonce; string canonical;
            if (!TryCanonicalId(parts[0], out canonical) ||
                !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out ticks) ||
                ticks <= 0 || !Guid.TryParseExact(parts[2], "N", out nonce) ||
                parts[2] != nonce.ToString("N")) return false;
            id = canonical;
            return true;
        }

        internal void GuardDirectories(bool createRoot, bool createTrash, bool createBackups)
        {
            // Reject pre-existing reparse points in *every* path component, including ancestors.
            VerifyDirectoryChain(Root, createRoot);
            if (createTrash) VerifyDirectoryChain(Trash, true);
            if (createBackups) VerifyDirectoryChain(Backups, true);
        }

        private static void VerifyDirectoryChain(string path, bool create)
        {
            string full = Path.GetFullPath(path);
            string basePath = Path.GetPathRoot(full);
            if (String.IsNullOrEmpty(basePath)) throw new IOException("Unrooted directory.");
            string tail = full.Substring(basePath.Length);
            string current = basePath;
            foreach (string part in tail.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, part);
                if (File.Exists(current) && !Directory.Exists(current))
                    throw new IOException("Directory path blocked by file.");
                if (!Directory.Exists(current))
                {
                    if (!create) return;
                    Directory.CreateDirectory(current);
                }
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new UnauthorizedAccessException("Reparse-point directory refused.");
            }
        }

        internal static void GuardDirectoryIfExists(string path)
        { VerifyDirectoryChain(path, false); }

        internal static void GuardRegularFile(string file)
        {
            if (File.Exists(file) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("Reparse-point file refused.");
        }
    }
}
