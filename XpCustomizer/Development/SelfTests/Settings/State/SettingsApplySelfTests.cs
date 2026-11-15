#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// R6-P0-C: detached objects only. No live settings singleton, pawn XP,
    /// GeneDef, UI, disk or Scribe access, even on injected failures.
    /// </summary>
    internal static class SettingsApplySelfTests
    {
        internal static void Run()
        {
            int validated = 0, rejected = 0, applied = 0, rolledBack = 0;
            FieldInfo[] fields = SettingsSnapshotService.GetDeclaredSettingsFieldsForAudit();
            Check(fields.Length == SettingsCandidateValidator.ExpectedSettingsFieldCount,
                "field schema drift");
            Check(SettingsCandidateValidator.ComputeFieldFingerprint(fields) ==
                SettingsCandidateValidator.ExpectedFieldNameFingerprint,
                "field fingerprint drift");

            XpCustomizerSettings original = new XpCustomizerSettings();
            SettingsSnapshot baseline = SettingsSnapshotService.Capture(original);
            Check(SettingsCandidateValidator.Validate(original).Accepted, "default rejected");
            validated++;

            XpCustomizerSettings profileSource = new XpCustomizerSettings();
            profileSource.careerXpMultiplier = 8.75f;
            profileSource.skillSpecificXpEnabled = false;
            profileSource.skillSpecificXpMultipliers["FutureUnregisteredSkill"] = 0f;
            profileSource.skilltrainerSpecificMultipliers["Plants"] = 2.25f;
            profileSource.gwsSkillEnabled = new Dictionary<string, bool>(
                StringComparer.OrdinalIgnoreCase);
            profileSource.gwsSkillEnabled["Social"] = true;
            profileSource.careerXpSplitLevels.Clear();
            // Legacy preset may leave lock count different until next Scribe load.
            profileSource.careerXpStageLockedShares.Clear();
            profileSource.consciousnessAnchorSpeed.LowFactor = 0.43f;
            profileSource.consciousnessAnchorCapacity.HighFactor = 1.56f;
            profileSource.consciousnessFurtherV2.LowFactor = 0.67f;
            SettingsSnapshot profile = SettingsSnapshotService.Capture(profileSource);
            XpCustomizerSettings expectedProfile = profile.CreateDetachedCopy();
            Check(SettingsCandidateValidator.Validate(expectedProfile).Accepted,
                "valid inactive/custom/empty career candidate rejected");
            validated++;

            // Mutating the original source after capture cannot affect profile.
            profileSource.careerXpMultiplier = 1f;
            profileSource.skillSpecificXpMultipliers["FutureUnregisteredSkill"] = 12f;

            XpCustomizerSettings root = baseline.CreateDetachedCopy();
            XpCustomizerSettings rootIdentity = root;
            SettingsApplyResult result = SettingsApplyCoordinator.ApplyForTesting(root, profile, -1);
            Check(result.Status == SettingsApplyStatus.APPLY_PASS, "A->B failed: " + result.Reason);
            applied++;
            Check(object.ReferenceEquals(root, rootIdentity), "settings root was replaced");
            CheckEqual(root, expectedProfile, "A->B values");
            Check(!object.ReferenceEquals(root.skillSpecificXpMultipliers,
                expectedProfile.skillSpecificXpMultipliers), "dictionary alias after apply");
            Check(!object.ReferenceEquals(root.consciousnessAnchorSpeed,
                expectedProfile.consciousnessAnchorSpeed), "profile alias after apply");
            Check(root.gwsSkillEnabled.ContainsKey("social"), "dictionary comparer lost");
            Check(root.careerXpSplitLevels.Count == 0 &&
                root.skillSpecificXpMultipliers["FutureUnregisteredSkill"] == 0f,
                "empty career/inactive values lost");

            result = SettingsApplyCoordinator.ApplyForTesting(root, profile, -1);
            Check(result.Status == SettingsApplyStatus.APPLY_PASS, "idempotent apply failed");
            applied++;
            CheckEqual(root, expectedProfile, "idempotent values");
            result = SettingsApplyCoordinator.ApplyForTesting(root, baseline, -1);
            Check(result.Status == SettingsApplyStatus.APPLY_PASS, "B->A failed");
            applied++;
            CheckEqual(root, baseline.CreateDetachedCopy(), "A restored");

            // Invalid candidate tests: no change to any current field, and no
            // change to the original reference objects on a preflight reject.
            XpCustomizerSettings invalid = profile.CreateDetachedCopy();
            invalid.careerXpMultiplier = float.NaN;
            RejectWithoutMutation(root, invalid, "nonfinite scalar"); rejected++;
            invalid = profile.CreateDetachedCopy();
            invalid.decayDailyByLevel[0] = float.PositiveInfinity;
            RejectWithoutMutation(root, invalid, "nonfinite list"); rejected++;
            invalid = profile.CreateDetachedCopy();
            invalid.skillSpecificXpMultipliers["FutureSkill"] = float.NegativeInfinity;
            RejectWithoutMutation(root, invalid, "nonfinite dictionary"); rejected++;
            invalid = profile.CreateDetachedCopy();
            invalid.bookSkillMultipliers = null;
            RejectWithoutMutation(root, invalid, "null dictionary"); rejected++;
            invalid = profile.CreateDetachedCopy();
            invalid.consciousnessFurtherV2 = null;
            RejectWithoutMutation(root, invalid, "null profile"); rejected++;
            invalid = profile.CreateDetachedCopy();
            invalid.consciousnessFurtherV2.LowCurve = 99;
            RejectWithoutMutation(root, invalid, "unknown nested curve"); rejected++;
            invalid = profile.CreateDetachedCopy();
            invalid.gwsHighCurve = -1;
            RejectWithoutMutation(root, invalid, "unknown GWS mode"); rejected++;
            invalid = profile.CreateDetachedCopy();
            invalid.careerXpSplitLevels.Add(22);
            RejectWithoutMutation(root, invalid, "bad split level"); rejected++;
            invalid = profile.CreateDetachedCopy();
            invalid.consciousnessAnchorSettingsVersion = 999;
            RejectWithoutMutation(root, invalid, "future schema"); rejected++;

            result = SettingsApplyCoordinator.ApplyForTesting(root, null, -1);
            Check(result.Status == SettingsApplyStatus.APPLY_REJECTED, "null snapshot accepted");
            rejected++;
            result = SettingsApplyCoordinator.ApplyForTesting(null, profile, -1);
            Check(result.Status == SettingsApplyStatus.APPLY_REJECTED, "null target accepted");
            rejected++;

            // Force an exception after partial and near-complete field writes.
            // All original references (not just equal data) must be restored.
            int[] failurePositions = { 1, 40, fields.Length };
            for (int i = 0; i < failurePositions.Length; i++)
            {
                XpCustomizerSettings before = SettingsSnapshotService.CloneIndependent(root);
                object[] references = CaptureReferences(root, fields);
                result = SettingsApplyCoordinator.ApplyForTesting(root, profile, failurePositions[i]);
                Check(result.Status == SettingsApplyStatus.APPLY_ROLLBACK_PASS &&
                    result.RollbackVerified, "rollback failed at " + failurePositions[i] +
                    ": " + result.Reason);
                rolledBack++;
                CheckEqual(root, before, "rollback value parity");
                CheckReferences(root, fields, references);
            }

            CheckEqual(root, baseline.CreateDetachedCopy(), "test target still original");
            Log.Message("[XPC-R6P0][VALIDATOR_AUDIT] status=PASS; revision=" +
                SettingsCandidateValidator.Revision + "; schemaFields=" + fields.Length +
                "; fingerprint=0x" + SettingsCandidateValidator.ExpectedFieldNameFingerprint.ToString("X8") +
                "; acceptedSamples=" + validated + "; rejectedSamples=" + rejected +
                "; unknownSkillKeys=PRESERVED; emptyCareerSplits=PRESERVED; disabledValues=PRESERVED" +
                "; liveSettingsWrites=0; build=" + XpCustomizerMod.BuildId);
            Log.Message("[XPC-R6P0][APPLY_AUDIT] status=PASS; revision=" +
                SettingsApplyCoordinator.Revision + "; successfulApplies=" + applied +
                "; rootIdentity=PRESERVED; copyIsolation=PASS; presetRoundTrip=PASS" +
                "; externalRefresh=NOT_INCLUDED_P0D; scribeCalls=0; pawnWrites=0" +
                "; liveSettingsWrites=0; build=" + XpCustomizerMod.BuildId);
            Log.Message("[XPC-R6P0][ROLLBACK_AUDIT] status=PASS; faults=" + rolledBack +
                "; partialWriteCounts=1,40," + fields.Length +
                "; valuesRestored=PASS; referenceIdentityRestored=PASS" +
                "; liveSettingsWrites=0; build=" + XpCustomizerMod.BuildId);
        }

        private static void RejectWithoutMutation(XpCustomizerSettings root,
            XpCustomizerSettings invalid, string label)
        {
            FieldInfo[] fields = SettingsSnapshotService.GetDeclaredSettingsFieldsForAudit();
            XpCustomizerSettings previous = SettingsSnapshotService.CloneIndependent(root);
            object[] refs = CaptureReferences(root, fields);
            SettingsApplyResult result = SettingsApplyCoordinator.ApplyForTesting(root,
                SettingsSnapshotService.Capture(invalid), -1);
            Check(result.Status == SettingsApplyStatus.APPLY_REJECTED,
                "invalid candidate accepted: " + label + " status=" + result.Status);
            CheckEqual(root, previous, "reject " + label);
            CheckReferences(root, fields, refs);
        }

        private static object[] CaptureReferences(XpCustomizerSettings settings,
            FieldInfo[] fields)
        {
            object[] originals = new object[fields.Length];
            for (int i = 0; i < fields.Length; i++)
                originals[i] = fields[i].GetValue(settings);
            return originals;
        }

        private static void CheckReferences(XpCustomizerSettings settings,
            FieldInfo[] fields, object[] originals)
        {
            for (int i = 0; i < fields.Length; i++)
                if (!fields[i].FieldType.IsValueType)
                    Check(object.ReferenceEquals(fields[i].GetValue(settings), originals[i]),
                        "original reference not preserved: " + fields[i].Name);
        }

        private static void CheckEqual(XpCustomizerSettings a, XpCustomizerSettings b,
            string label)
        {
            string mismatch;
            Check(SettingsApplyCoordinator.Equivalent(a, b, out mismatch),
                label + ":" + mismatch);
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("[XPC-R6P0C] " + message);
        }
    }
}
#endif
