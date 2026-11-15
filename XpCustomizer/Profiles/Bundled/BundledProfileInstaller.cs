using System;
using System.IO;
using System.Text;
using Verse;

namespace XpCustomizer.Profiles
{
    /// <summary>
    /// BP12 first-open seed and BP4 explicitly confirmed Reset restoration.
    /// Never applies gameplay settings, overwrites an existing profile, or touches Trash.
    /// A per-user journal outside Profiles tracks copies and records durable intent
    /// BEFORE Create. Reset-only 'R' intents are never resumed by profile-menu seeding.
    /// </summary>
    internal static class BundledProfileInstaller
    {
        internal const string Revision = "BP4-BUNDLE-RESTORE-V2-20261110";
        internal const string LongId = "4d137a66-6d2f-4dd2-b982-247657c0fb4f";
        internal const string VeryLongId = "e176b260-1ae1-4596-9f05-7768741e25fe";

        private const int MaxSourceBytes = 128 * 1024;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly object Sync = new object();
        private static string configuredModRoot;
        private static bool completedInThisSession;

        private sealed class Preset
        {
            internal readonly string Key;
            internal readonly string File;
            internal readonly string Id;
            internal readonly string Label;
            internal Preset(string key, string file, string id, string label)
            { Key = key; File = file; Id = id; Label = label; }
        }

        private static readonly Preset[] Presets = {
            new Preset("LONG", "long.json", LongId, "Long (x3)"),
            new Preset("VERY_LONG", "verylong.json", VeryLongId, "Very Long (x6)")
        };

        internal sealed class SeedReport
        {
            internal int Created;
            internal int Adopted;
            internal int PreviouslyRemoved;
            internal int AlreadyHandled;
            internal int Failed;
            internal bool Success { get { return Failed == 0; } }
        }

        internal sealed class RestoreReport
        {
            internal int Created;
            internal int Preserved;
            internal int Failed;
            internal bool Success { get { return Failed == 0; } }
        }

        // Configure is deliberately read-only. No writes during game/mod startup.
        internal static void Configure(string modRoot)
        {
            lock (Sync)
            {
                configuredModRoot = modRoot;
                completedInThisSession = false;
                BundledProfileNotesLocalization.InvalidateJournalCache();
            }
        }

        internal static void EnsureForCurrentUser()
        {
            lock (Sync)
            {
                if (completedInThisSession) return;
                if (String.IsNullOrWhiteSpace(configuredModRoot))
                {
                    Log.Warning("[XPC-BP12][BUNDLED_SEED] status=NOT_CONFIGURED; build=" + XpCustomizerMod.BuildId);
                    return;
                }
                try
                {
                    SeedReport report = EnsureAt(configuredModRoot, GenFilePaths.ConfigFolderPath,
                        ProfileFileRepository.ForCurrentUser(), true);
                    completedInThisSession = report.Success;
                    BundledProfileNotesLocalization.InvalidateJournalCache();
                }
                catch (Exception ex)
                {
                    // Seeding is optional: a filesystem failure must not block the profile UI.
                    Log.Warning("[XPC-BP12][BUNDLED_SEED] preset=ALL; status=FAILED_CLOSED; " +
                        "detail=" + ex.GetType().Name + "; build=" + XpCustomizerMod.BuildId);
                }
            }
        }

        // Reuses production path, only with isolated roots/repository in Debug self-tests.
        internal static SeedReport EnsureAt(string modRoot, string configRoot,
            ProfileFileRepository repository, bool logging,
            ProfileStorageFault createFault = ProfileStorageFault.None)
        {
            SeedReport report = new SeedReport();
            lock (Sync)
            {
                try
                {
                    if (String.IsNullOrWhiteSpace(modRoot) ||
                        String.IsNullOrWhiteSpace(configRoot) ||
                        !Path.IsPathRooted(modRoot) || !Path.IsPathRooted(configRoot) ||
                        repository == null) throw new ArgumentException("ROOT_OR_REPOSITORY_INVALID");
                    if (ProfileWriteSessionGate.IsBlocked ||
                        ProfileProductionWriteGate.IsBusy || ProfileProductionWriteGate.IsUncertain)
                        throw new InvalidOperationException("SESSION_WRITE_GATE_BLOCKED");

                    string owner = Path.Combine(Path.GetFullPath(configRoot), "XpCustomizer");
                    string marker = Path.Combine(owner, "BundledProfilesState.json");
                    ProfileStoragePaths.GuardDirectoryIfExists(modRoot);
                    ProfileStoragePaths.GuardDirectoryIfExists(Path.Combine(modRoot, "Presets"));
                    ProfileStoragePaths.GuardDirectoryIfExists(owner);
                    ProfileStoragePaths.GuardRegularFile(marker);
                    BundledProfileStateStore.State state = BundledProfileStateStore.Read(marker); // fail closed

                    foreach (Preset preset in Presets)
                    {
                        try { EnsureOne(preset, modRoot, owner, marker, state,
                            repository, report, logging, createFault); }
                        catch (Exception ex)
                        {
                            report.Failed++;
                            LogAction(logging, preset.Key, "FAILED", ex.GetType().Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    report.Failed++;
                    LogAction(logging, "ALL", "FAILED_CLOSED", ex.GetType().Name);
                }
            }
            return report;
        }

        private static void EnsureOne(Preset preset, string modRoot, string owner, string marker,
            BundledProfileStateStore.State state, ProfileFileRepository repository, SeedReport report, bool logging,
            ProfileStorageFault createFault)
        {
            if (state.GetStatus(preset.Key) == 'R')
            {
                // Reset intent may ONLY be resumed by a subsequent confirmed Reset.
                report.AlreadyHandled++;
                LogAction(logging, preset.Key, "RESET_PENDING_SKIP", "NO_AUTO_RESTORE");
                return;
            }
            if (state.GetStatus(preset.Key) == 'D')
            {
                report.AlreadyHandled++;
                LogAction(logging, preset.Key, "ALREADY_HANDLED", "NO_DISK_WRITE");
                return;
            }
            // Read/validate the bundled source before writing any state or user files.
            ProfileDocument source = LoadSource(modRoot, preset);
            ProfileStorageResult active = repository.Load(preset.Id);
            if (active.Success)
            {
                BundledProfileStateStore.Save(owner, marker, state, preset.Key, 'D', preset.Id);
                report.Adopted++;
                LogAction(logging, preset.Key, "EXISTING_PRESERVED", "ID_MATCH_NO_OVERWRITE");
                return;
            }
            if (active.Code != ProfileStorageCode.NotFound)
                throw new IOException("ACTIVE_NOT_SAFE_" + active.Code);

            // Existing trash proves a prior deletion even if BP2 was first installed later.
            ProfileStorageListing trash = repository.ListTrash();
            if (trash.Code != ProfileStorageCode.Ok)
                throw new IOException("TRASH_LIST_" + trash.Code);
            foreach (ProfileStorageEntry entry in trash.Entries)
            {
                if (entry != null && entry.FileName != null &&
                    entry.FileName.StartsWith(preset.Id + ".", StringComparison.Ordinal))
                {
                    BundledProfileStateStore.Save(owner, marker, state, preset.Key, 'D', preset.Id);
                    report.PreviouslyRemoved++;
                    LogAction(logging, preset.Key, "PRIOR_DELETION_PRESERVED", "TRASH_PRESENT");
                    return;
                }
            }

            if (state.GetStatus(preset.Key) == '-')
                BundledProfileStateStore.Save(owner, marker, state, preset.Key, 'P', preset.Id); // persist intent BEFORE Create

            ProfileStorageResult created = repository.Create(source, createFault);
            ProfileWriteSessionGate.Observe(created.Code);
            if (created.Code == ProfileStorageCode.AlreadyExists)
            {
                // A competing creator may have populated it; never overwrite.
                ProfileStorageResult concurrent = repository.Load(preset.Id);
                if (!concurrent.Success) throw new IOException("CONCURRENT_CREATE_UNVERIFIED");
                BundledProfileStateStore.Save(owner, marker, state, preset.Key, 'D', preset.Id);
                report.Adopted++;
                LogAction(logging, preset.Key, "EXISTING_PRESERVED", "CONCURRENT_CREATE");
                return;
            }
            if (!created.Success || created.Document == null ||
                created.Document.ProfileId != preset.Id || String.IsNullOrEmpty(created.ContentHash))
                throw new IOException("CREATE_" + created.Code);
            BundledProfileStateStore.Save(owner, marker, state, preset.Key, 'D', preset.Id);
            report.Created++;
            LogAction(logging, preset.Key, "CREATED", "PROFILE_JSON_ONLY");
        }


        /// <summary>
        /// BP4 is ONLY invoked after the user expressly confirms the global Reset.
        /// It never runs on startup or as part of automatic menu seeding.
        /// Each missing bundled copy is recreated from the shipped JSON under a new GUID.
        /// </summary>
        internal static RestoreReport RestoreMissingForCurrentUser()
        {
            lock (Sync)
            {
                if (String.IsNullOrWhiteSpace(configuredModRoot))
                {
                    RestoreReport unavailable = new RestoreReport();
                    unavailable.Failed = Presets.Length;
                    LogRestore("ALL", "FAILED", "NOT_CONFIGURED", null, true);
                    return unavailable;
                }
                try
                {
                    RestoreReport report = RestoreAt(configuredModRoot, GenFilePaths.ConfigFolderPath,
                        ProfileFileRepository.ForCurrentUser(), true);
                    // Never let the regular first-open seeder undo an intentional deletion.
                    // Restoring an 'R' intent is exclusively a subsequent confirmed Reset.
                    if (report.Success) completedInThisSession = true;
                    BundledProfileNotesLocalization.InvalidateJournalCache();
                    return report;
                }
                catch (Exception ex)
                {
                    RestoreReport failed = new RestoreReport();
                    failed.Failed = Presets.Length;
                    LogRestore("ALL", "FAILED", ex.GetType().Name, null, true);
                    return failed;
                }
            }
        }

        internal static RestoreReport RestoreAt(string modRoot, string configRoot,
            ProfileFileRepository repository, bool logging,
            ProfileStorageFault createFault = ProfileStorageFault.None)
        {
            RestoreReport report = new RestoreReport();
            lock (Sync)
            {
                try
                {
                    if (String.IsNullOrWhiteSpace(modRoot) ||
                        String.IsNullOrWhiteSpace(configRoot) ||
                        !Path.IsPathRooted(modRoot) || !Path.IsPathRooted(configRoot) ||
                        repository == null) throw new ArgumentException("ROOT_OR_REPOSITORY_INVALID");
                    if (ProfileWriteSessionGate.IsBlocked ||
                        ProfileProductionWriteGate.IsBusy || ProfileProductionWriteGate.IsUncertain)
                        throw new InvalidOperationException("SESSION_WRITE_GATE_BLOCKED");

                    string owner = Path.Combine(Path.GetFullPath(configRoot), "XpCustomizer");
                    string marker = Path.Combine(owner, "BundledProfilesState.json");
                    ProfileStoragePaths.GuardDirectoryIfExists(modRoot);
                    ProfileStoragePaths.GuardDirectoryIfExists(Path.Combine(modRoot, "Presets"));
                    ProfileStoragePaths.GuardDirectoryIfExists(owner);
                    ProfileStoragePaths.GuardRegularFile(marker);
                    BundledProfileStateStore.State state = BundledProfileStateStore.Read(marker);
                    if (state.WasLegacy)
                    {
                        // A confirmed Reset is the explicit schema1 -> schema2
                        // migration point, even when no copies need restoring.
                        BundledProfileStateStore.Save(owner, marker, state, "LONG",
                            state.Long, state.LongId);
                        if (logging)
                            Log.Message("[XPC-BP4][BUNDLED_STATE_MIGRATE] from=1; to=2; " +
                                "status=PASS; profileWrites=0; build=" + XpCustomizerMod.BuildId);
                    }
                    foreach (Preset preset in Presets)
                    {
                        try
                        {
                            if (ProfileWriteSessionGate.IsBlocked ||
                                ProfileProductionWriteGate.IsBusy || ProfileProductionWriteGate.IsUncertain)
                                throw new InvalidOperationException("SESSION_WRITE_GATE_BLOCKED");
                            RestoreOne(preset, modRoot, owner, marker, state, repository,
                                report, logging, createFault);
                        }
                        catch (Exception ex)
                        {
                            report.Failed++;
                            LogRestore(preset.Key, "FAILED", ex.GetType().Name,
                                null, logging);
                        }
                    }
                }
                catch (Exception ex)
                {
                    report.Failed += Presets.Length;
                    LogRestore("ALL", "FAILED_CLOSED", ex.GetType().Name, null, logging);
                }
                if (logging)
                    Log.Message("[XPC-BP4][BUNDLED_RESTORE_SUMMARY] requested=2; created=" +
                        report.Created + "; preserved=" + report.Preserved +
                        "; failed=" + report.Failed +
                        "; autoApply=NEVER; trashWrites=0; settingsWrites=0; build=" +
                        XpCustomizerMod.BuildId);
            }
            return report;
        }

        private static void RestoreOne(Preset preset, string modRoot, string owner, string marker,
            BundledProfileStateStore.State state, ProfileFileRepository repository,
            RestoreReport report, bool logging, ProfileStorageFault createFault)
        {
            char status = state.GetStatus(preset.Key);
            string trackedId = state.GetId(preset.Key);
            if (trackedId.Length != 0)
            {
                ProfileStorageResult tracked = repository.Load(trackedId);
                if (tracked.Success)
                {
                    // A reset intent may have created a file, but crashed before finalizing D.
                    // Never overwrite the surviving file, even when user-edited.
                    if (status != 'D')
                        BundledProfileStateStore.Save(owner, marker, state, preset.Key, 'D', trackedId);
                    report.Preserved++;
                    LogRestore(preset.Key, "EXISTING_PRESERVED", "TRACKED_ID_NO_OVERWRITE",
                        trackedId, logging);
                    return;
                }
                if (tracked.Code != ProfileStorageCode.NotFound)
                    throw new IOException("TRACKED_FILE_UNSAFE_" + tracked.Code);
            }

            // An older original copy can be manually restored while a newer copy
            // was deleted. Adopt the original instead of duplicating it.
            if (trackedId != preset.Id)
            {
                ProfileStorageResult original = repository.Load(preset.Id);
                if (original.Success)
                {
                    BundledProfileStateStore.Save(owner, marker, state, preset.Key, 'D', preset.Id);
                    report.Preserved++;
                    LogRestore(preset.Key, "EXISTING_PRESERVED", "ORIGINAL_ID_ADOPTED",
                        preset.Id, logging);
                    return;
                }
                if (original.Code != ProfileStorageCode.NotFound)
                    throw new IOException("ORIGINAL_FILE_UNSAFE_" + original.Code);
            }

            // Do not change any state before checking that the shipped source
            // can be decoded, validated and imported as detached gameplay settings.
            ProfileDocument source = LoadSource(modRoot, preset);
            string chosen;
            if (status == 'P' || status == 'R')
                chosen = trackedId; // retry the exact previously journaled intent
            else if (status == '-')
                chosen = preset.Id; // first-ever installation uses shipped GUID
            else
                chosen = Guid.NewGuid().ToString("D"); // D => user removed prior copy

            // A trash copy of the chosen ID is not a safe target. It is never
            // moved or purged, and is deliberately left independently restorable.
            ProfileStorageListing trash = repository.ListTrash();
            if (trash.Code != ProfileStorageCode.Ok)
                throw new IOException("TRASH_LIST_" + trash.Code);
            bool collision = false;
            foreach (ProfileStorageEntry entry in trash.Entries)
            {
                if (entry != null && entry.FileName != null &&
                    entry.FileName.StartsWith(chosen + ".", StringComparison.Ordinal))
                { collision = true; break; }
            }
            if (collision) chosen = Guid.NewGuid().ToString("D");
            if (chosen == state.GetId(preset.Key == "LONG" ? "VERY_LONG" : "LONG"))
                chosen = Guid.NewGuid().ToString("D");

            ProfileDocument copy = ProfileJsonCodec.Deserialize(ProfileJsonCodec.Serialize(source));
            copy.ProfileId = chosen;
            copy.CreatedUtc = DateTime.UtcNow;
            copy.UpdatedUtc = copy.CreatedUtc;
            ProfileJsonCodec.Validate(copy);

            // Persist R before Create. A crash after Create but before D leaves
            // R + the same ID, which only another explicit Reset may resume.
            if (status != 'R' || trackedId != chosen)
                BundledProfileStateStore.Save(owner, marker, state, preset.Key, 'R', chosen);

            ProfileStorageResult created = repository.Create(copy, createFault);
            ProfileWriteSessionGate.Observe(created.Code);
            if (created.Code == ProfileStorageCode.AlreadyExists)
            {
                ProfileStorageResult existing = repository.Load(chosen);
                if (!existing.Success) throw new IOException("CONCURRENT_CREATE_UNVERIFIED");
                BundledProfileStateStore.Save(owner, marker, state, preset.Key, 'D', chosen);
                report.Preserved++;
                LogRestore(preset.Key, "EXISTING_PRESERVED", "CONCURRENT_CREATE",
                    chosen, logging);
                return;
            }
            if (!created.Success || created.Document == null ||
                created.Document.ProfileId != chosen || String.IsNullOrEmpty(created.ContentHash))
                throw new IOException("CREATE_" + created.Code);
            BundledProfileStateStore.Save(owner, marker, state, preset.Key, 'D', chosen);
            report.Created++;
            LogRestore(preset.Key, "RESET_CREATED", "ORIGINAL_PRESET_NEW_COPY",
                chosen, logging);
        }

        private static void LogRestore(string preset, string status, string detail,
            string profileId, bool enabled)
        {
            if (!enabled) return;
            string line = "[XPC-BP4][BUNDLED_RESTORE] preset=" + preset +
                "; status=" + status + "; detail=" + detail +
                "; profileId=" + (profileId ?? "NONE") +
                "; autoApply=NEVER; trashWrites=0; build=" + XpCustomizerMod.BuildId;
            if (status.StartsWith("FAILED", StringComparison.Ordinal)) Log.Warning(line);
            else Log.Message(line);
        }

        private static ProfileDocument LoadSource(string root, Preset preset)
        {
            string path = Path.Combine(root, "Presets", preset.File);
            ProfileStoragePaths.GuardRegularFile(path);
            if (!File.Exists(path)) throw new FileNotFoundException("BUNDLED_SOURCE_MISSING");
            long sourceSize = new FileInfo(path).Length;
            if (sourceSize == 0 || sourceSize > MaxSourceBytes)
                throw new InvalidDataException("BUNDLED_SOURCE_SIZE");
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length == 0 || bytes.Length > MaxSourceBytes ||
                (bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf))
                throw new InvalidDataException("BUNDLED_SOURCE_SIZE_OR_BOM");
            ProfileDocument doc = ProfileJsonCodec.Deserialize(StrictUtf8.GetString(bytes));
            if (!String.Equals(doc.ProfileId, preset.Id, StringComparison.Ordinal))
                throw new InvalidDataException("BUNDLED_SOURCE_ID_MISMATCH");
            // Validate the actual candidate gameplay settings as well as the wire schema.
            ProfileMapper.ImportDetached(doc, new XpCustomizerSettings());
            // Display-only normalization; do not change the shipped balancing fields.
            doc.DisplayName = preset.Label;
            ProfileJsonCodec.Validate(doc);
            return doc;
        }

        private static void LogAction(bool enabled, string preset, string status, string detail)
        {
            if (!enabled) return;
            string message = "[XPC-BP12][BUNDLED_SEED] preset=" + preset +
                "; status=" + status + "; detail=" + detail +
                "; settingsWrites=0; scribeCalls=0; geneDefWrites=0; " +
                "automaticApply=NEVER; resetRestore=NOT_INCLUDED_BP12; build=" + XpCustomizerMod.BuildId;
            if (status.StartsWith("FAILED", StringComparison.Ordinal)) Log.Warning(message);
            else Log.Message(message);
        }
    }
}
