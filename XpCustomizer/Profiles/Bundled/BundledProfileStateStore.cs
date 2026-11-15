using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace XpCustomizer.Profiles
{
    /// <summary>
    /// BP4: durable, per-user bundled-profile journal, outside the profile repository.
    /// Schema 1 is read without losing BP12 deletion history. Schema 2 records the
    /// current copy ID and distinguishes first-install intent (P) from reset intent (R).
    /// The state file is replaced atomically; malformed/newer versions fail closed.
    /// </summary>
    internal static class BundledProfileStateStore
    {
        private const int MaxStateBytes = 512;
        private const string V1 = @"\A\{""schema"":1,""long"":""([DP-])"",""veryLong"":""([DP-])""\}\n?\z";
        private const string V2 = @"\A\{""schema"":2,""long"":\{""status"":""([DPR-])"",""currentProfileId"":""([^""\\]*)""\},""veryLong"":\{""status"":""([DPR-])"",""currentProfileId"":""([^""\\]*)""\}\}\n?\z";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        internal sealed class State
        {
            internal char Long = '-';
            internal char VeryLong = '-';
            internal string LongId = "";
            internal string VeryLongId = "";
            internal bool WasLegacy;

            internal char GetStatus(string key) { return key == "LONG" ? Long : VeryLong; }
            internal string GetId(string key) { return key == "LONG" ? LongId : VeryLongId; }
            internal void Set(string key, char status, string id)
            {
                if (key == "LONG") { Long = status; LongId = id; }
                else if (key == "VERY_LONG") { VeryLong = status; VeryLongId = id; }
                else throw new ArgumentException("UNKNOWN_BUNDLE_KEY");
            }
        }

        internal static State Read(string marker)
        {
            State state = new State();
            ProfileStoragePaths.GuardRegularFile(marker);
            if (!File.Exists(marker)) return state;
            long count = new FileInfo(marker).Length;
            if (count <= 0 || count > MaxStateBytes) throw new InvalidDataException("BUNDLED_STATE_SIZE");
            byte[] bytes = File.ReadAllBytes(marker);
            if (bytes.Length != count || (bytes.Length >= 3 && bytes[0] == 0xef &&
                bytes[1] == 0xbb && bytes[2] == 0xbf))
                throw new InvalidDataException("BUNDLED_STATE_BOM_OR_CHANGED");
            string value = Utf8.GetString(bytes);
            Match legacy = Regex.Match(value, V1, RegexOptions.CultureInvariant);
            if (legacy.Success)
            {
                state.WasLegacy = true;
                state.Set("LONG", legacy.Groups[1].Value[0],
                    legacy.Groups[1].Value[0] == '-' ? "" : BundledProfileInstaller.LongId);
                state.Set("VERY_LONG", legacy.Groups[2].Value[0],
                    legacy.Groups[2].Value[0] == '-' ? "" : BundledProfileInstaller.VeryLongId);
                return state;
            }
            Match modern = Regex.Match(value, V2, RegexOptions.CultureInvariant);
            if (!modern.Success) throw new InvalidDataException("BUNDLED_STATE_INVALID");
            ValidateSlot(modern.Groups[1].Value[0], modern.Groups[2].Value);
            ValidateSlot(modern.Groups[3].Value[0], modern.Groups[4].Value);
            state.Set("LONG", modern.Groups[1].Value[0], modern.Groups[2].Value);
            state.Set("VERY_LONG", modern.Groups[3].Value[0], modern.Groups[4].Value);
            if (state.LongId.Length > 0 && state.LongId == state.VeryLongId)
                throw new InvalidDataException("BUNDLED_STATE_DUPLICATE_GUID");
            return state;
        }

        private static void ValidateSlot(char status, string id)
        {
            string canonical;
            if (status == '-')
            {
                if (id != "") throw new InvalidDataException("BUNDLED_STATE_UNSTARTED_ID");
            }
            else if ((status != 'P' && status != 'D' && status != 'R') ||
                !ProfileStoragePaths.TryCanonicalId(id, out canonical))
                throw new InvalidDataException("BUNDLED_STATE_SLOT_INVALID");
        }

        internal static void Save(string owner, string marker, State state,
            string key, char status, string id)
        {
            if (state == null) throw new ArgumentNullException("state");
            ValidateSlot(status, id);
            char previousStatus = state.GetStatus(key);
            string previousId = state.GetId(key);
            bool wasLegacy = state.WasLegacy;
            state.Set(key, status, id);
            string stage = null;
            try
            {
                if (state.LongId.Length > 0 && state.LongId == state.VeryLongId)
                    throw new InvalidDataException("BUNDLED_STATE_DUPLICATE_GUID");
                ProfileStoragePaths.GuardDirectoryIfExists(owner);
                if (!Directory.Exists(owner)) Directory.CreateDirectory(owner);
                ProfileStoragePaths.GuardDirectoryIfExists(owner);
                ProfileStoragePaths.GuardRegularFile(marker);
                string text = "{\"schema\":2,\"long\":{\"status\":\"" + state.Long +
                    "\",\"currentProfileId\":\"" + state.LongId +
                    "\"},\"veryLong\":{\"status\":\"" + state.VeryLong +
                    "\",\"currentProfileId\":\"" + state.VeryLongId + "\"}}\n";
                byte[] bytes = Utf8.GetBytes(text);
                if (bytes.Length > MaxStateBytes) throw new InvalidDataException("BUNDLED_STATE_SIZE");
                stage = Path.Combine(owner, ".bundled-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (FileStream file = new FileStream(stage, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None))
                { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                ProfileStoragePaths.GuardRegularFile(marker);
                if (File.Exists(marker)) File.Replace(stage, marker, null);
                else File.Move(stage, marker);
                stage = null;
                state.WasLegacy = false;
            }
            catch
            {
                state.WasLegacy = wasLegacy;
                state.Set(key, previousStatus, previousId);
                throw;
            }
            finally
            {
                if (stage != null)
                { try { if (File.Exists(stage)) File.Delete(stage); } catch (IOException) { } }
            }
        }
    }
}
