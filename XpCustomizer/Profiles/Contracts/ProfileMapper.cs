using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using XpCustomizer.ConsciousnessLearning;
using XpCustomizer.Gws;
using XpCustomizer.BookXp;

namespace XpCustomizer.Profiles
{
    /// <summary>R6-P1-A pure detached mapping; does not call Scribe, write files or apply to live settings.</summary>
    internal static class ProfileMapper
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static readonly string[] CurveKeys = { "speedProfile", "capacityProfile", "furtherProfile" };
#if DEBUG
        private static bool legacyCandidateMigrationLogged;
        private static bool legacyBookCandidateMigrationLogged;
#endif

        internal static ProfileDocument Export(XpCustomizerSettings settings, string name,
            string gameVersion, DateTime utc, string profileId)
        {
            ProfileSchemaV1.VerifyContract();
            if (settings == null) throw Fail("INVALID_VALUE", "null source settings");
            SettingsCandidateValidator.Result valid = SettingsCandidateValidator.Validate(settings);
            if (!valid.Accepted) throw Fail("INVALID_VALUE", valid.Reason);
            ProfileDocument doc = new ProfileDocument();
            doc.FormatId = ProfileSchemaV1.FormatId;
            doc.FormatVersion = ProfileFormatV2.FormatVersion;
            doc.SettingsSchemaVersion = ProfileSchemaV1.SettingsSchemaVersion;
            doc.PortableSchemaFingerprint = ProfileSchemaV1.Fingerprint;
            doc.ProfileId = profileId;
            doc.DisplayName = name;
            doc.CreatedUtc = utc;
            doc.UpdatedUtc = utc;
            doc.SourceBuildId = XpCustomizerMod.BuildId;
            doc.SourceGameVersion = gameVersion;
            doc.Settings = new ProfileSettingsDto();
            foreach (ProfileFieldSpec spec in ProfileSchemaV1.GetFields())
            {
                Dictionary<string, object> section = GetOrAdd(doc.Settings.Sections, spec.Section);
                section.Add(spec.Name, ClonePrimitive(spec.FieldType, spec.SettingField.GetValue(settings)));
            }
            CopyCurveOut(settings.consciousnessAnchorSpeed, "speedProfile", doc.Settings);
            CopyCurveOut(settings.consciousnessAnchorCapacity, "capacityProfile", doc.Settings);
            CopyCurveOut(settings.consciousnessFurtherV2, "furtherProfile", doc.Settings);
            ProfileJsonCodec.Validate(doc);
            return doc;
        }

        internal static XpCustomizerSettings ImportDetached(ProfileDocument doc, XpCustomizerSettings baseline)
        {
            ProfileSchemaV1.VerifyContract();
            ProfileJsonCodec.Validate(doc);
            if (baseline == null) throw Fail("MAPPING_FAILED", "null baseline");
            // Preserve every *nonexported* value, including 1.6 private Scribe
            // migration/version fields. Never inject those from a foreign profile.
            XpCustomizerSettings result = SettingsSnapshotService.CloneIndependent(baseline);
            foreach (ProfileFieldSpec spec in ProfileSchemaV1.GetFields())
            {
                Dictionary<string, object> section = doc.Settings.Sections[spec.Section];
                spec.SettingField.SetValue(result, ClonePrimitive(spec.FieldType, section[spec.Name]));
            }
            result.consciousnessAnchorSpeed = new ConsAnchorV2ProfileSettings();
            result.consciousnessAnchorCapacity = new ConsAnchorV2ProfileSettings();
            result.consciousnessFurtherV2 = new ConsciousnessFurtherV2Settings();
            CopyCurveIn(doc.Settings.Curves["speedProfile"], result.consciousnessAnchorSpeed);
            CopyCurveIn(doc.Settings.Curves["capacityProfile"], result.consciousnessAnchorCapacity);
            CopyCurveIn(doc.Settings.Curves["furtherProfile"], result.consciousnessFurtherV2);
            // Only touch the independent imported candidate. Do NOT run the full
            // GWS settings sanitizer here: review/apply must preserve all other
            // imported curve and dictionary values, and never rewrite the JSON.
            bool canonicalWon;
            bool migrated = GwsLearningService.MigrateLegacyMedicalKey(
                result.gwsSkillEnabled, out canonicalWon);
            // A legacy explicit ON is equivalent to Medicine's implicit ON.
            // Do not erase an explicitly supplied canonical Medicine=true.
            if (migrated && !canonicalWon && result.gwsSkillEnabled["Medicine"])
                result.gwsSkillEnabled.Remove("Medicine");
#if DEBUG
            if (migrated && !legacyCandidateMigrationLogged)
            {
                legacyCandidateMigrationLogged = true;
                Verse.Log.Message("[XPC-GWS-MEDFIX] phase=PROFILE_CANDIDATE; legacyPresent=true" +
                    "; canonicalWon=" + canonicalWon +
                    "; sourceMutated=false; userProfileWritesByMigration=0; build=" +
                    XpCustomizerMod.BuildId);
            }
#endif
            // Independently migrate the Book XP float map on the detached candidate.
            // Never call BookXpService.NormalizeSettings here: Review and Confirm
            // must preserve every other imported setting, and the JSON original.
            bool bookCanonicalWon;
            bool bookMigrated = BookXpService.MigrateLegacyMedicalKey(
                result.bookSkillMultipliers, out bookCanonicalWon);
            // Existing normalized Book XP profiles omit the neutral x1 entry.
            // Only normalize the moved legacy value, not explicit canonical keys.
            if (bookMigrated && !bookCanonicalWon &&
                Mathf.Approximately(BookXpService.NormalizeMultiplier(
                    result.bookSkillMultipliers["Medicine"]), 1f))
                result.bookSkillMultipliers.Remove("Medicine");
#if DEBUG
            if (bookMigrated && !legacyBookCandidateMigrationLogged)
            {
                legacyBookCandidateMigrationLogged = true;
                Verse.Log.Message("[XPC-BOOKXP-MEDFIX] phase=PROFILE_CANDIDATE" +
                    "; legacyPresent=true; canonicalWon=" + bookCanonicalWon +
                    "; sourceMutated=false; userProfileWritesByMigration=0; build=" +
                    XpCustomizerMod.BuildId);
            }
#endif
            SettingsCandidateValidator.Result valid = SettingsCandidateValidator.Validate(result);
            if (!valid.Accepted) throw Fail("INVALID_VALUE", valid.Reason);
            return result;
        }

        internal static object ClonePrimitive(Type type, object val)
        {
            if (val == null) throw Fail("INVALID_VALUE", "null settings field");
            if (type == typeof(bool) || type == typeof(float) || type == typeof(int))
            { if (val.GetType()!=type) throw Fail("SCHEMA_MISMATCH", "primitive type mismatch"); return val; }
            if (type == typeof(Dictionary<string,float>))
            { var d=val as Dictionary<string,float>; if(d==null)throw Fail("SCHEMA_MISMATCH","float map type");return new Dictionary<string,float>(d,d.Comparer); }
            if (type == typeof(Dictionary<string,bool>))
            { var d=val as Dictionary<string,bool>; if(d==null)throw Fail("SCHEMA_MISMATCH","bool map type");return new Dictionary<string,bool>(d,d.Comparer); }
            if (type == typeof(List<int>))
            { var d=val as List<int>; if(d==null)throw Fail("SCHEMA_MISMATCH","int list type");return new List<int>(d); }
            if (type == typeof(List<float>))
            { var d=val as List<float>; if(d==null)throw Fail("SCHEMA_MISMATCH","float list type");return new List<float>(d); }
            throw Fail("SCHEMA_MISMATCH", "unexpected type:"+type.Name);
        }

        private static void CopyCurveOut(object source, string key, ProfileSettingsDto dto)
        {
            if (source == null) throw Fail("INVALID_VALUE", "null curve " + key);
            Dictionary<string, object> fields = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (string name in ProfileSchemaV1.ProfileFieldNames)
            {
                FieldInfo f=source.GetType().GetField(name,Flags);
                if (f==null) throw Fail("SCHEMA_MISMATCH", "missing curve field:"+name);
                fields.Add(name, f.GetValue(source));
            }
            dto.Curves.Add(key,fields);
        }
        private static void CopyCurveIn(Dictionary<string, object> source, object destination)
        {
            foreach (string name in ProfileSchemaV1.ProfileFieldNames)
            {
                FieldInfo f=destination.GetType().GetField(name,Flags);
                object value=source[name];
                if (value==null || value.GetType()!=f.FieldType)
                    throw Fail("SCHEMA_MISMATCH", "curve type:"+name);
                f.SetValue(destination,value);
            }
        }
        internal static Dictionary<string, object> GetOrAdd(
            Dictionary<string, Dictionary<string, object>> sections, string key)
        {
            Dictionary<string, object> values;
            if (!sections.TryGetValue(key,out values))
            { values=new Dictionary<string,object>(StringComparer.Ordinal);sections.Add(key,values); }
            return values;
        }
        private static ProfileContractException Fail(string code,string reason)
        { return new ProfileContractException(code,reason); }
    }
}
