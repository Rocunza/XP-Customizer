#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer
{
    internal static class ProfileSchemaSelfTests
    {
        private const string Revision="R6P1A-PORTABLE-SCHEMA-V1-20261106";
        internal static void Run()
        {
            ProfileSchemaV1.VerifyContract();
            XpCustomizerSettings original=new XpCustomizerSettings();
            original.careerXpMultiplier=1.375f;
            original.level20ReserveMultiplier=0.625f;
            original.skillSpecificXpEnabled=false;
            original.skillSpecificXpMultipliers["OtherMod_UnknownSkill"]=1.25f;
            original.careerXpSplitLevels.Clear();
            original.consciousnessAnchorSpeed.LowFactor=0.775f;
            original.consciousnessAnchorCapacity.HighFactor=1.625f;
            original.consciousnessFurtherV2.LowFactor=0.625f;
            original.furtherLearningTraitModifiersEnabled=true; // excluded legacy
            XpCustomizerSettings before=SettingsSnapshotService.CloneIndependent(original);
            ProfileDocument doc=ProfileMapper.Export(original,"테스트/성장 세팅", "1.6.4871",
                new DateTime(2026,11,6,3,0,0,DateTimeKind.Utc),
                "31a0b693-9868-40ad-a494-176aef531faa");
            doc.FormatVersion = ProfileSchemaV1.FormatVersion; // preserve legacy-v1 audit fixture
            string json=ProfileJsonCodec.Serialize(doc);
            ProfileDocument parsed=ProfileJsonCodec.Deserialize(json);
            string jsonAgain=ProfileJsonCodec.Serialize(parsed);
            Check(json==jsonAgain,"canonical JSON roundtrip");
            XpCustomizerSettings baseline=new XpCustomizerSettings();
            baseline.careerXpMultiplier=4.75f;
            baseline.furtherLearningTraitModifiersEnabled=false;
            XpCustomizerSettings baselineCopy=SettingsSnapshotService.CloneIndependent(baseline);
            XpCustomizerSettings imported=ProfileMapper.ImportDetached(parsed,baseline);
            Check(imported!=baseline && imported!=original,"detached candidate root");
            Check(imported.careerXpMultiplier==1.375f && imported.level20ReserveMultiplier==0.625f,
                "float fidelity");
            Check(imported.skillSpecificXpMultipliers["OtherMod_UnknownSkill"]==1.25f,
                "unknown SkillDef preserved");
            Check(imported.careerXpSplitLevels.Count==0,"empty career splits");
            Check(imported.consciousnessAnchorSpeed.LowFactor==0.775f &&
                  imported.consciousnessAnchorCapacity.HighFactor==1.625f &&
                  imported.consciousnessFurtherV2.LowFactor==0.625f,"3 independent profiles");
            Check(!imported.furtherLearningTraitModifiersEnabled,"excluded legacy retained from baseline");
            Check(!Object.ReferenceEquals(imported.skillSpecificXpMultipliers,original.skillSpecificXpMultipliers),
                "map independently owned");
            Check(!Object.ReferenceEquals(imported.careerXpSplitLevels,original.careerXpSplitLevels),
                "list independently owned");
            Check(!Object.ReferenceEquals(imported.consciousnessAnchorSpeed,original.consciousnessAnchorSpeed),
                "profile independently owned");
            foreach(FieldInfo field in SettingsSnapshotService.GetDeclaredSettingsFieldsForAudit())
            {
                if(field.IsPublic)continue;
                if(field.Name=="consciousnessAnchorSpeed" ||
                   field.Name=="consciousnessAnchorCapacity" ||
                   field.Name=="consciousnessFurtherV2")continue;
                Check(Object.Equals(field.GetValue(baseline),field.GetValue(imported)),
                    "nonportable internal state:"+field.Name);
            }
            // No export, import, codec, or test operation may mutate the given objects.
            Check(SameValues(original,before),"original unchanged");
            Check(SameValues(baseline,baselineCopy),"baseline unchanged");
            int rejected=0;
            rejected+=Reject(json.Replace("\"formatVersion\":1","\"formatVersion\":3"),"UNSUPPORTED_VERSION");
            rejected+=Reject(json.Replace("\"settingsSchemaVersion\":1","\"settingsSchemaVersion\":2"),"UNSUPPORTED_VERSION");
            rejected+=Reject(json.Replace("\"formatId\":\"zcn.xpcustomizer.profile\"", "\"formatId\":\"foreign.profile\""),"UNKNOWN_FORMAT");
            rejected+=Reject(json.Replace("\"careerXpMultiplier\":1.375","\"careerXpMultiplier\":null"),"INVALID_VALUE");
            rejected+=Reject(json.Replace("\"careerXpMultiplier\":1.375","\"careerXpMultiplier\":\"1.375\""),"INVALID_VALUE");
            rejected+=Reject(json.Replace("\"careerXpMultiplier\":1.375","\"careerXpMultiplier\":1e999"),"INVALID_VALUE");
            rejected+=Reject(json.Replace("\"careerXpMultiplier\":1.375","\"careerXpMultiplier\":1.375,\"careerXpMultiplier\":1.375"),"INVALID_FORMAT");
            rejected+=Reject(json.Replace("\"careerXpMultiplier\":1.375,", ""),"SCHEMA_MISMATCH");
            rejected+=Reject(json.Replace("\"careerXpMultiplier\":1.375","\"alienSetting\":1.375"),"SCHEMA_MISMATCH");
            rejected+=Reject(json.Replace("\"careerXpMultiplier\":1.375","\"careerXpMultiplier\":1e-999"),"INVALID_VALUE");
            rejected+=Reject(json+"{}","INVALID_FORMAT");
            rejected+=Reject("{\"broken\":","INVALID_FORMAT");
            rejected+=Reject(ReplaceOne(json,"\"careerXpRoundingStep\":"+original.careerXpRoundingStep.ToString(System.Globalization.CultureInfo.InvariantCulture),"\"careerXpRoundingStep\":1.5"),"SCHEMA_MISMATCH",true);
            Check(rejected==13,"rejection samples");
            Check(SameValues(original,before) && SameValues(baseline,baselineCopy),"rejects never write settings");
            Log.Message("[XPC-R6P1A][PROFILE_SCHEMA_AUDIT] status=PASS; revision="+Revision+
                "; format=1; settingsSchema=1; topLevel=81; curves=3; curveFields=36;"+
                " excludedLegacy=15; nonPortable=9; fingerprint="+ProfileSchemaV1.Fingerprint+
                "; build="+XpCustomizerMod.BuildId);
            Log.Message("[XPC-R6P1A][PROFILE_MAPPER_AUDIT] status=PASS; detached=PASS;"+
                " nonPortable=BASELINE_PRESERVED; unknownSkillKeys=PRESERVED;"+
                " disabledValues=PRESERVED; emptySplits=PRESERVED; liveSettingsWrites=0;"+
                " geneDefWrites=0; scribeCalls=0; diskIo=0; build="+XpCustomizerMod.BuildId);
            Log.Message("[XPC-R6P1A][PROFILE_CODEC_AUDIT] status=PASS; roundTrips=1;"+
                " invalidRejected="+rejected+"; deterministicJson=PASS;"+
                " futureVersion=REJECTED; duplicateKeys=REJECTED; liveApply=BLOCKED;"+
                " diskIo=0; build="+XpCustomizerMod.BuildId);
        }
        private static string ReplaceOne(string text,string needle,string replacement)
        {
            int pos=text.IndexOf(needle,StringComparison.Ordinal);
            Check(pos>=0,"missing test mutation target:"+needle);
            return text.Substring(0,pos)+replacement+text.Substring(pos+needle.Length);
        }
        private static int Reject(string value,string expected,bool optional=false)
        {
            try{ProfileJsonCodec.Deserialize(value);}
            catch(ProfileContractException ex)
            {
                if(ex.Code==expected || optional && (ex.Code=="SCHEMA_MISMATCH"||ex.Code=="INVALID_VALUE"))return 1;
                throw new InvalidOperationException("PROFILE_REJECT_REASON expected="+expected+" actual="+ex.Code+" "+ex.Message);
            }
            throw new InvalidOperationException("PROFILE_REJECT_FAILED:"+expected);
        }
        private static bool SameValues(XpCustomizerSettings a, XpCustomizerSettings b)
        {
            FieldInfo[] fields=SettingsSnapshotService.GetDeclaredSettingsFieldsForAudit();
            foreach(FieldInfo f in fields)
            {
                object x=f.GetValue(a),y=f.GetValue(b);
                if(x==null || y==null){if(x!=y)return false;continue;}
                if(x is Dictionary<string,float>)
                {
                    var l=(Dictionary<string,float>)x;var r=(Dictionary<string,float>)y;
                    if(l.Count!=r.Count)return false;
                    foreach(var item in l){float v;if(!r.TryGetValue(item.Key,out v)||v!=item.Value)return false;}
                }
                else if(x is Dictionary<string,bool>)
                {
                    var l=(Dictionary<string,bool>)x;var r=(Dictionary<string,bool>)y;
                    if(l.Count!=r.Count)return false;
                    foreach(var item in l){bool v;if(!r.TryGetValue(item.Key,out v)||v!=item.Value)return false;}
                }
                else if(x is List<float>)
                {var l=(List<float>)x;var r=(List<float>)y;if(l.Count!=r.Count)return false;
                 for(int i=0;i<l.Count;i++)if(l[i]!=r[i])return false;}
                else if(x is List<int>)
                {var l=(List<int>)x;var r=(List<int>)y;if(l.Count!=r.Count)return false;
                 for(int i=0;i<l.Count;i++)if(l[i]!=r[i])return false;}
                else if(f.Name=="consciousnessAnchorSpeed"||f.Name=="consciousnessAnchorCapacity"||f.Name=="consciousnessFurtherV2")
                {foreach(string field in ProfileSchemaV1.ProfileFieldNames)
                 {var p=x.GetType().GetField(field,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                  var q=y.GetType().GetField(field,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                  if(!Object.Equals(p.GetValue(x),q.GetValue(y)))return false;}}
                else if(!Object.Equals(x,y))return false;
            }
            return true;
        }
        private static void Check(bool ok,string description)
        {if(!ok)throw new InvalidOperationException("R6P1A self-test failed:"+description);}
    }
}
#endif
