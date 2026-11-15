using System;

namespace XpCustomizer.Profiles
{
    /// <summary>
    /// R6-P1-C2-C: XP-profile JSON only. Never touches ModSettings, Scribe,
    /// profile Apply, GeneDefs, caches or other mods' files.
    /// All mutations are explicit, one-shot and use repository-verified SHA-256.
    /// </summary>
    internal sealed class ProfileTrashActionService
    {
        internal const string Revision = "R6P1C2C-TRASH-UI-20261108";
        private readonly Func<ProfileFileRepository> repositoryFactory;
        private bool purgeInProgress;

        internal ProfileTrashActionService(Func<ProfileFileRepository> factory)
        {
            if (factory == null) throw new ArgumentNullException("factory");
            repositoryFactory = factory;
        }

        internal static ProfileTrashActionService ForCurrentUser()
        { return new ProfileTrashActionService(delegate { return ProfileFileRepository.ForCurrentUser(); }); }

        internal ProfileStorageResult Delete(string profileId, string expectedHash)
        {
            if (ProfileWriteSessionGate.IsBlocked)
                return new ProfileStorageResult(ProfileStorageCode.RecoveryRequired,
                    detailCode: "SESSION_WRITE_LOCK");
            try
            {
                ProfileStorageResult result = repositoryFactory().SoftDelete(profileId, expectedHash);
                ProfileWriteSessionGate.Observe(result.Code);
                return result;
            }
            catch (Exception)
            {
                return new ProfileStorageResult(ProfileStorageCode.IoError,
                    detailCode: "DELETE_ACTION_FAILURE");
            }
        }

        internal ProfileStorageResult Restore(string trashToken, string expectedHash)
        {
            if (ProfileWriteSessionGate.IsBlocked)
                return new ProfileStorageResult(ProfileStorageCode.RecoveryRequired,
                    detailCode: "SESSION_WRITE_LOCK");
            try
            {
                ProfileStorageResult result = repositoryFactory().Restore(trashToken, expectedHash);
                ProfileWriteSessionGate.Observe(result.Code);
                return result;
            }
            catch (Exception)
            {
                return new ProfileStorageResult(ProfileStorageCode.IoError,
                    detailCode: "RESTORE_ACTION_FAILURE");
            }
        }
        internal ProfileTrashPurgeResult PrepareEmptyTrash()
        {
            if (ProfileWriteSessionGate.IsBlocked || purgeInProgress)
                return new ProfileTrashPurgeResult(ProfileTrashPurgeState.Rejected,
                    ProfileStorageCode.RecoveryRequired, "SESSION_OR_REENTRANT_LOCK",
                    null, 0, 0, -1);
            try { return repositoryFactory().PrepareTrashPurge(); }
            catch (Exception)
            {
                return new ProfileTrashPurgeResult(ProfileTrashPurgeState.Rejected,
                    ProfileStorageCode.IoError, "PREPARE_ACTION_FAILURE", null, 0, 0, -1);
            }
        }

        internal ProfileTrashPurgeResult EmptyTrash(ProfileTrashPurgePlan approved
#if DEBUG
            , int injectBeforeIndex = -1, int injectAfterDeleteIndex = -1
#endif
            )
        {
            if (ProfileWriteSessionGate.IsBlocked || purgeInProgress)
                return new ProfileTrashPurgeResult(ProfileTrashPurgeState.Rejected,
                    ProfileStorageCode.RecoveryRequired, "SESSION_OR_REENTRANT_LOCK", approved,
                    approved == null ? 0 : approved.Count, 0, -1);
            purgeInProgress = true;
            try
            {
                ProfileTrashPurgeResult result = repositoryFactory().PurgeTrash(approved
#if DEBUG
                    , injectBeforeIndex, injectAfterDeleteIndex
#endif
                    );
                ProfileWriteSessionGate.Observe(result.Code);
                return result;
            }
            catch (Exception)
            {
                // An escaped exception cannot prove whether deletion has begun.
                ProfileWriteSessionGate.Observe(ProfileStorageCode.RecoveryRequired);
                return new ProfileTrashPurgeResult(ProfileTrashPurgeState.Uncertain,
                    ProfileStorageCode.RecoveryRequired, "ACTION_EXCEPTION_UNCERTAIN", approved,
                    approved == null ? 0 : approved.Count, 0, -1);
            }
            finally { purgeInProgress = false; }
        }
    }
}
