#if DEBUG
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Xml;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// R6-P1-D2-C3-B: Debug-only observations of this mod's existing vanilla
    /// ModSettings XML path. The ONLY production file operations are READS.
    /// Never opens a writer, edits an XML document, calls Scribe, or retries a
    /// failed vanilla save. This is NOT a profile persistence transaction.
    /// </summary>
    internal static class ModXmlRoundTripAudit
    {
        internal const string Revision = "R6P1D2C3B-REAL-MOD-XML-READBACK-20261107";
        internal const string AuditTag = "[XPC-R6P1D2C3B][MOD_XML_ROUNDTRIP_AUDIT]";
        internal const int MaximumBytes = 4 * 1024 * 1024;
        private const string ExpectedSettingsClass = "XpCustomizer.XpCustomizerSettings";
        private static int writeSequence;

        internal sealed class Snapshot
        {
            internal string State = "UNAVAILABLE";
            internal string Reason = "NONE";
            internal long ByteLength = -1;
            internal string Sha256 = "NONE";
            internal int Compared;
            internal int Omitted;
            internal int Mismatched;
            internal string MismatchField = "NONE";
            internal bool ValidXml;
        }

        internal sealed class WriteAttempt
        {
            internal readonly int Number;
            internal readonly string OwnedPath;
            internal readonly Snapshot Before;

            internal WriteAttempt(int number, string ownedPath, Snapshot before)
            {
                Number = number;
                OwnedPath = ownedPath;
                Before = before;
            }
        }

        /// <summary>
        /// Uses the *same* folder identifier + type name + sanitizer from the
        /// inspected RimWorld 1.6 LoadedModManager.GetSettingsFilename source.
        /// It is not a configurable or user-supplied arbitrary file path.
        /// </summary>
        internal static string GetOwnedPath(ModContentPack content)
        {
            if (content == null || String.IsNullOrEmpty(content.FolderName))
                throw new InvalidOperationException("MOD_CONTENT_UNAVAILABLE");

            string configRoot = Path.GetFullPath(GenFilePaths.ConfigFolderPath);
            string name = GenText.SanitizeFilename(
                "Mod_" + content.FolderName + "_" + typeof(XpCustomizerMod).Name + ".xml");
            if (String.IsNullOrEmpty(name) || name.IndexOf('/') >= 0 ||
                name.IndexOf('\\') >= 0 || name == "." || name == "..")
                throw new InvalidOperationException("INVALID_OWNED_FILENAME");

            string path = Path.GetFullPath(Path.Combine(configRoot, name));
            if (!String.Equals(Path.GetDirectoryName(path),
                configRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("OWNERSHIP_PATH_OUTSIDE_CONFIG");
            return path;
        }

        internal static void ObserveStartup(ModContentPack content, XpCustomizerSettings settings)
        {
            try
            {
                Snapshot snap = ReadOwnedFile(GetOwnedPath(content), settings);
                string result = snap.State == "VALID_XML" && snap.Compared > 0 && snap.Mismatched == 0
                    ? "PASS_SELECTED_FIELDS" : "NOT_VERIFIED";
                Emit("STARTUP_AFTER_VANILLA_LOAD", 0, result, null, snap, null);
            }
            catch (Exception ex)
            {
                ObserverFailure("STARTUP_AFTER_VANILLA_LOAD", ex);
            }
        }

        internal static WriteAttempt BeforeVanillaWrite(ModContentPack content)
        {
            try
            {
                int number = ++writeSequence;
                string path = GetOwnedPath(content);
                Snapshot before = ReadOwnedFile(path, null); // Current UI edits are not yet on disk.
                Emit("BEFORE_VANILLA_WRITE", number, "OBSERVED_ONLY", before, null, false);
                return new WriteAttempt(number, path, before);
            }
            catch (Exception ex)
            {
                ObserverFailure("BEFORE_VANILLA_WRITE", ex);
                return null; // Must never block vanilla's existing save.
            }
        }

        /// <summary>
        /// Caller MUST execute in finally, including when base.WriteSettings
        /// throws. The original exception is not caught or converted to PASS.
        /// </summary>
        internal static void AfterVanillaWrite(WriteAttempt attempt,
            XpCustomizerSettings settings, bool vanillaWriterReturnedNormally)
        {
            try
            {
                if (attempt == null)
                {
                    Log.Warning(AuditTag + " phase=AFTER_VANILLA_WRITE; status=NOT_VERIFIED; " +
                        "reason=NO_BASELINE; writerReturned=" + vanillaWriterReturnedNormally +
                        "; liveApply=BLOCKED; build=" + XpCustomizerMod.BuildId);
                    return;
                }

                Snapshot after = ReadOwnedFile(attempt.OwnedPath, settings);
                string result = EvaluateAfterWrite(attempt.Before, after, vanillaWriterReturnedNormally);
                Emit("AFTER_VANILLA_WRITE", attempt.Number, result, attempt.Before, after,
                    vanillaWriterReturnedNormally);
            }
            catch (Exception ex)
            {
                ObserverFailure("AFTER_VANILLA_WRITE", ex);
            }
        }

        internal static string EvaluateAfterWrite(Snapshot before, Snapshot after,
            bool writerReturnedNormally)
        {
            // ScribeSaver can truncate a live XML through FileMode.Create.
            // Even matching bytes after an exception do not prove safety.
            if (!writerReturnedNormally) return "UNCERTAIN_WRITER_EXCEPTION";
            if (after == null || !after.ValidXml || after.Mismatched > 0)
                return "FAIL_READBACK";
            if (after.Compared == 0) return "NOT_VERIFIED_NO_SCALAR_EVIDENCE";
            if (before == null || (before.State != "VALID_XML" && before.State != "MISSING"))
                return "PARTIAL_BASELINE_UNVERIFIED";
            return "PASS_SELECTED_FIELDS";
        }

        private static void Emit(string phase, int attempt, string status,
            Snapshot before, Snapshot after, bool? writerReturned)
        {
            Snapshot current = after ?? before ?? new Snapshot();
            string message = AuditTag + " status=" + status +
                "; revision=" + Revision +
                "; phase=" + phase +
                "; attempt=" + attempt.ToString(CultureInfo.InvariantCulture) +
                "; fileScope=OWN_XP_CUSTOMIZER_MOD_SETTINGS_ONLY" +
                "; beforeState=" + (before == null ? "NOT_OBSERVED" : before.State) +
                "; beforeBytes=" + (before == null ? "NA" : before.ByteLength.ToString(CultureInfo.InvariantCulture)) +
                "; beforeSha256=" + (before == null ? "NA" : before.Sha256) +
                "; readbackState=" + (after == null ? "NOT_OBSERVED" : after.State) +
                "; readbackBytes=" + (after == null ? "NA" : after.ByteLength.ToString(CultureInfo.InvariantCulture)) +
                "; readbackSha256=" + (after == null ? "NA" : after.Sha256) +
                "; comparedSelectedFields=" + current.Compared +
                "; omittedSelectedFields=" + current.Omitted +
                "; mismatchedSelectedFields=" + current.Mismatched +
                "; mismatchField=" + current.MismatchField +
                "; reason=" + current.Reason +
                "; writerReturned=" + (writerReturned.HasValue ? writerReturned.Value.ToString() : "NOT_APPLICABLE") +
                "; normalVanillaWrite=UNCHANGED" +
                "; diagnosticWrites=0; scribeCallsByAudit=0; geneDefWrites=0" +
                "; restartDurability=NOT_VERIFIED; realScribeAtomicity=NOT_PRESENT" +
                "; productionApply=NOT_EXERCISED_BY_THIS_AUDIT; build=" + XpCustomizerMod.BuildId;
            if (status == "FAIL_READBACK" || status == "UNCERTAIN_WRITER_EXCEPTION" ||
                status == "PARTIAL_BASELINE_UNVERIFIED")
                Log.Warning(message);
            else
                Log.Message(message);
        }

        private static void ObserverFailure(string phase, Exception ex)
        {
            // Do not log exception.ToString(): IO exceptions may reveal private
            // filesystem paths. Also do not let diagnostics alter saving behavior.
            Log.Warning(AuditTag + " status=NOT_VERIFIED; revision=" + Revision +
                "; phase=" + phase + "; reason=OBSERVER_EXCEPTION_" +
                ex.GetType().Name + "; diagnosticWrites=0; productionApply=NOT_EXERCISED_BY_THIS_AUDIT; build=" +
                XpCustomizerMod.BuildId);
        }

        private static Snapshot ReadOwnedFile(string path, XpCustomizerSettings expected)
        {
            Snapshot snapshot = new Snapshot();
            if (!File.Exists(path))
            {
                snapshot.State = "MISSING";
                snapshot.Reason = "NO_OWNED_SETTINGS_XML";
                return snapshot;
            }
            try
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                {
                    snapshot.State = "READ_REJECTED";
                    snapshot.Reason = "REPARSE_POINT";
                    return snapshot;
                }
                // Never opens with write permission or follows an in-flight
                // writer intentionally: the vanilla XML saver uses FileShare.None.
                using (FileStream input = new FileStream(path, FileMode.Open,
                    FileAccess.Read, FileShare.Read))
                {
                    if (input.Length > MaximumBytes || input.Length < 0)
                    {
                        snapshot.State = "READ_REJECTED";
                        snapshot.Reason = "SIZE_LIMIT";
                        return snapshot;
                    }
                    byte[] bytes = new byte[(int)input.Length];
                    int offset = 0;
                    while (offset < bytes.Length)
                    {
                        int read = input.Read(bytes, offset, bytes.Length - offset);
                        if (read == 0)
                        {
                            snapshot.State = "READ_UNCERTAIN";
                            snapshot.Reason = "EARLY_END_OF_FILE";
                            return snapshot;
                        }
                        offset += read;
                    }
                    if (input.ReadByte() != -1)
                    {
                        snapshot.State = "READ_UNCERTAIN";
                        snapshot.Reason = "FILE_GREW_DURING_READ";
                        return snapshot;
                    }
                    return InspectBytes(bytes, expected);
                }
            }
            catch (Exception ex)
            {
                snapshot.State = "READ_UNCERTAIN";
                snapshot.Reason = "READ_EXCEPTION_" + ex.GetType().Name;
                return snapshot;
            }
        }

        /// <summary>
        /// Pure in-memory parser, also used by isolated Debug self-tests.
        /// Missing Scribe default-elided fields remain OMITTED, never silently
        /// counted as compared. No full XML text/field values are logged.
        /// </summary>
        internal static Snapshot InspectBytes(byte[] bytes, XpCustomizerSettings expected)
        {
            Snapshot snapshot = new Snapshot();
            if (bytes == null || bytes.Length > MaximumBytes)
            {
                snapshot.State = "READ_REJECTED";
                snapshot.Reason = "NULL_OR_SIZE_LIMIT";
                return snapshot;
            }
            snapshot.ByteLength = bytes.Length;
            using (SHA256 sha = SHA256.Create())
                snapshot.Sha256 = BitConverter.ToString(sha.ComputeHash(bytes))
                    .Replace("-", "").ToLowerInvariant();

            try
            {
                XmlReaderSettings readerSettings = new XmlReaderSettings();
                readerSettings.XmlResolver = null;
                readerSettings.DtdProcessing = DtdProcessing.Prohibit;
                readerSettings.MaxCharactersInDocument = MaximumBytes;
                XmlDocument doc = new XmlDocument();
                doc.XmlResolver = null;
                using (MemoryStream stream = new MemoryStream(bytes, false))
                using (XmlReader reader = XmlReader.Create(stream, readerSettings))
                    doc.Load(reader);

                XmlElement root = doc.DocumentElement;
                if (root == null || root.Name != "SettingsBlock" || root.NamespaceURI.Length != 0)
                    return Reject(snapshot, "UNEXPECTED_ROOT");

                XmlElement payload = null;
                foreach (XmlNode node in root.ChildNodes)
                {
                    if (node.NodeType != XmlNodeType.Element) continue;
                    if (payload != null || node.Name != "ModSettings")
                        return Reject(snapshot, "AMBIGUOUS_SETTINGS_NODE");
                    payload = node as XmlElement;
                }
                if (payload == null || payload.GetAttribute("Class") != ExpectedSettingsClass ||
                    payload.NamespaceURI.Length != 0)
                    return Reject(snapshot, "WRONG_SETTINGS_CLASS");

                snapshot.State = "VALID_XML";
                snapshot.ValidXml = true;
                snapshot.Reason = "OWNED_DOCUMENT_SHAPE_VALID";
                if (expected != null)
                {
                    CheckFloat(payload, "gwsHighFactor", expected.gwsHighFactor, snapshot);
                    CheckFloat(payload, "furtherLearningMentalThresholdStrength",
                        expected.furtherLearningMentalThresholdStrength, snapshot);
                    CheckFloat(payload, "careerXpMultiplier", expected.careerXpMultiplier, snapshot);
                    CheckFloat(payload, "level20ReserveMultiplier",
                        expected.level20ReserveMultiplier, snapshot);
                    CheckInt(payload, "careerXpRoundingStep", expected.careerXpRoundingStep, snapshot);
                    if (snapshot.Mismatched != 0)
                    {
                        snapshot.State = "FIELD_MISMATCH";
                        snapshot.Reason = "SELECTED_SCALAR_MISMATCH";
                    }
                }
            }
            catch (Exception ex)
            {
                snapshot.State = "INVALID_XML";
                snapshot.ValidXml = false;
                snapshot.Reason = "PARSE_EXCEPTION_" + ex.GetType().Name;
            }
            return snapshot;
        }

        private static Snapshot Reject(Snapshot value, string reason)
        {
            value.State = "INVALID_XML";
            value.Reason = reason;
            value.ValidXml = false;
            return value;
        }

        private static string OnlyDirectScalar(XmlElement payload, string name, Snapshot result)
        {
            XmlNode found = null;
            foreach (XmlNode child in payload.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element || child.Name != name) continue;
                if (found != null)
                {
                    result.Mismatched++;
                    result.MismatchField = name;
                    return null;
                }
                found = child;
            }
            if (found == null)
            {
                result.Omitted++;
                return null;
            }
            foreach (XmlNode node in found.ChildNodes)
            {
                if (node.NodeType == XmlNodeType.Element)
                {
                    result.Mismatched++;
                    result.MismatchField = name;
                    return null;
                }
            }
            return found.InnerText;
        }

        private static void CheckFloat(XmlElement payload, string name, float expected, Snapshot result)
        {
            int missingBefore = result.Omitted;
            int mismatchedBefore = result.Mismatched;
            string text = OnlyDirectScalar(payload, name, result);
            if (result.Omitted != missingBefore || result.Mismatched != mismatchedBefore) return;
            float actual;
            result.Compared++;
            if (!Single.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out actual) ||
                Single.IsNaN(actual) || Single.IsInfinity(actual) || !actual.Equals(expected))
            {
                result.Mismatched++;
                result.MismatchField = name;
            }
        }

        private static void CheckInt(XmlElement payload, string name, int expected, Snapshot result)
        {
            int missingBefore = result.Omitted;
            int mismatchedBefore = result.Mismatched;
            string text = OnlyDirectScalar(payload, name, result);
            if (result.Omitted != missingBefore || result.Mismatched != mismatchedBefore) return;
            int actual;
            result.Compared++;
            if (!Int32.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out actual) ||
                actual != expected)
            {
                result.Mismatched++;
                result.MismatchField = name;
            }
        }
    }
}
#endif
