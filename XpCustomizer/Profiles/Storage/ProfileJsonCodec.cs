using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace XpCustomizer.Profiles
{
    /// <summary>
    /// Strict v1 (legacy) + v2 (optional Notes) JSON codec for a fixed XP settings schema.
    /// No CLR type-name deserialization and no dependence on game JSON DLLs.
    /// Input is IN MEMORY ONLY; P1-B owns all disk I/O.
    /// </summary>
    internal static class ProfileJsonCodec
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly string[] RequiredMetadata = {
            "formatId", "formatVersion", "settingsSchemaVersion", "portableSchemaFingerprint",
            "profileId", "displayName", "createdUtc", "updatedUtc", "sourceBuildId",
            "sourceGameVersion", "settings"
        };
        private static readonly string[] V2Metadata = {
            "formatId", "formatVersion", "settingsSchemaVersion", "portableSchemaFingerprint",
            "profileId", "displayName", "createdUtc", "updatedUtc", "sourceBuildId",
            "sourceGameVersion", "notes", "settings"
        };
        private static readonly string[] CurveNames = { "speedProfile", "capacityProfile", "furtherProfile" };

        internal static string Serialize(ProfileDocument doc)
        {
            Validate(doc);
            Dictionary<string, object> obj = new Dictionary<string, object>(StringComparer.Ordinal);
            obj.Add("formatId",doc.FormatId);
            obj.Add("formatVersion",doc.FormatVersion);
            obj.Add("settingsSchemaVersion",doc.SettingsSchemaVersion);
            obj.Add("portableSchemaFingerprint",doc.PortableSchemaFingerprint);
            obj.Add("profileId",doc.ProfileId);
            obj.Add("displayName",doc.DisplayName);
            if (doc.FormatVersion == ProfileFormatV2.FormatVersion) obj.Add("notes", doc.Notes);
            obj.Add("createdUtc",doc.CreatedUtc.ToString("o",Inv));
            obj.Add("updatedUtc",doc.UpdatedUtc.ToString("o",Inv));
            obj.Add("sourceBuildId",doc.SourceBuildId);
            obj.Add("sourceGameVersion",doc.SourceGameVersion);
            Dictionary<string, object> body=new Dictionary<string, object>(StringComparer.Ordinal);
            foreach(var section in doc.Settings.Sections) body.Add(section.Key,section.Value);
            Dictionary<string, object> cons=(Dictionary<string,object>)body["consciousness"];
            // The body is only a temporary map and must not mutate the DTO.
            cons=new Dictionary<string,object>(cons,StringComparer.Ordinal);
            body["consciousness"]=cons;
            foreach(var curve in doc.Settings.Curves) cons.Add(curve.Key,curve.Value);
            obj.Add("settings",body);
            StringBuilder sb=new StringBuilder();
            JsonWrite(sb,obj);
            if(sb.Length > ProfileSchemaV1.MaxJsonChars) throw Fail("INVALID_FORMAT","profile too large");
            return sb.ToString();
        }

        internal static ProfileDocument Deserialize(string json)
        {
            ProfileSchemaV1.VerifyContract();
            if(json==null || json.Length > ProfileSchemaV1.MaxJsonChars)
                throw Fail("INVALID_FORMAT","null/oversized JSON");
            object parsed=new Reader(json).Read();
            Dictionary<string,object> obj=AsObject(parsed,"root");
            // Read the version before enforcing the exact version-specific key set.
            object versionRaw;
            if (!obj.TryGetValue("formatVersion", out versionRaw))
                throw Fail("SCHEMA_MISMATCH", "formatVersion missing");
            int version = GetInt(versionRaw, "formatVersion");
            if (version != ProfileSchemaV1.FormatVersion &&
                version != ProfileFormatV2.FormatVersion)
                throw Fail("UNSUPPORTED_VERSION", "formatVersion");
            RequireKeys(obj, version == ProfileFormatV2.FormatVersion ? V2Metadata : RequiredMetadata, "root");
            ProfileDocument doc=new ProfileDocument();
            doc.FormatId=GetString(obj,"formatId");
            doc.FormatVersion=GetInt(obj["formatVersion"],"formatVersion");
            doc.SettingsSchemaVersion=GetInt(obj["settingsSchemaVersion"],"settingsSchemaVersion");
            doc.PortableSchemaFingerprint=GetString(obj,"portableSchemaFingerprint");
            doc.ProfileId=GetString(obj,"profileId");
            doc.DisplayName=GetString(obj,"displayName");
            doc.Notes=version == ProfileFormatV2.FormatVersion ? GetString(obj,"notes") : "";
            doc.CreatedUtc=GetDate(GetString(obj,"createdUtc"),"createdUtc");
            doc.UpdatedUtc=GetDate(GetString(obj,"updatedUtc"),"updatedUtc");
            doc.SourceBuildId=GetString(obj,"sourceBuildId");
            doc.SourceGameVersion=GetString(obj,"sourceGameVersion");
            // Explicitly reject versions before interpreting their settings payload.
            VerifyMetadata(doc);
            Dictionary<string,object> body=AsObject(obj["settings"],"settings");
            doc.Settings=new ProfileSettingsDto();
            Dictionary<string,List<ProfileFieldSpec>> bySection=SpecsBySection();
            if (body.Count!=bySection.Count) throw Fail("SCHEMA_MISMATCH","section count mismatch");
            foreach(var section in bySection)
            {
                object sectionValue;
                if(!body.TryGetValue(section.Key,out sectionValue)) throw Fail("SCHEMA_MISMATCH","missing section:"+section.Key);
                Dictionary<string,object> values=AsObject(sectionValue,"settings."+section.Key);
                int expected=section.Value.Count+(section.Key=="consciousness"?3:0);
                if(values.Count!=expected) throw Fail("SCHEMA_MISMATCH","wrong keys in:"+section.Key);
                Dictionary<string,object> decoded=ProfileMapper.GetOrAdd(doc.Settings.Sections,section.Key);
                foreach(ProfileFieldSpec spec in section.Value)
                {
                    object raw;
                    if(!values.TryGetValue(spec.Name,out raw)) throw Fail("SCHEMA_MISMATCH","missing field:"+spec.Name);
                    decoded.Add(spec.Name,ConvertValue(raw,spec.FieldType,section.Key+"."+spec.Name));
                }
                if(section.Key=="consciousness")
                {
                    foreach(string curve in CurveNames)
                    {
                        object raw;
                        if(!values.TryGetValue(curve,out raw)) throw Fail("SCHEMA_MISMATCH","missing profile:"+curve);
                        Dictionary<string,object> src=AsObject(raw,curve);
                        RequireKeys(src,ProfileSchemaV1.ProfileFieldNames,curve);
                        Dictionary<string,object> dst=ProfileMapper.GetOrAdd(doc.Settings.Curves,curve);
                        foreach(string field in ProfileSchemaV1.ProfileFieldNames)
                        {
                            Type typ=field.EndsWith("Enabled",StringComparison.Ordinal)?typeof(bool):
                                field.EndsWith("Curve",StringComparison.Ordinal)?typeof(int):typeof(float);
                            dst.Add(field,ConvertValue(src[field],typ,curve+"."+field));
                        }
                    }
                }
            }
            Validate(doc);
            return doc;
        }

        internal static void Validate(ProfileDocument doc)
        {
            ProfileSchemaV1.VerifyContract();
            if(doc==null || doc.Settings==null)throw Fail("INVALID_VALUE","null document/settings");
            VerifyMetadata(doc);
            Dictionary<string,List<ProfileFieldSpec>> sections=SpecsBySection();
            if(doc.Settings.Sections.Count!=sections.Count || doc.Settings.Curves.Count!=3)
                throw Fail("SCHEMA_MISMATCH","wrong section/curve count");
            foreach(var section in sections)
            {
                Dictionary<string,object> values;
                if(!doc.Settings.Sections.TryGetValue(section.Key,out values) || values==null ||
                    values.Count!=section.Value.Count)
                    throw Fail("SCHEMA_MISMATCH","section missing/extra:"+section.Key);
                foreach(ProfileFieldSpec spec in section.Value)
                {
                    object value;
                    if(!values.TryGetValue(spec.Name,out value))throw Fail("SCHEMA_MISMATCH","missing:"+spec.Name);
                    CheckTyped(value,spec.FieldType,section.Key+"."+spec.Name);
                }
            }
            foreach(string curve in CurveNames)
            {
                Dictionary<string,object> fields;
                if(!doc.Settings.Curves.TryGetValue(curve,out fields) || fields==null || fields.Count!=12)
                    throw Fail("SCHEMA_MISMATCH","curve field count:"+curve);
                foreach(string name in ProfileSchemaV1.ProfileFieldNames)
                {
                    object value;
                    if(!fields.TryGetValue(name,out value))throw Fail("SCHEMA_MISMATCH","missing:"+curve+"."+name);
                    Type type=name.EndsWith("Enabled",StringComparison.Ordinal)?typeof(bool):
                        name.EndsWith("Curve",StringComparison.Ordinal)?typeof(int):typeof(float);
                    CheckTyped(value,type,curve+"."+name);
                    if(name.EndsWith("Curve",StringComparison.Ordinal) && ((int)value<0||(int)value>2))
                        throw Fail("INVALID_VALUE","curve mode:"+curve+"."+name);
                }
            }
        }
        private static void VerifyMetadata(ProfileDocument d)
        {
            if(d.FormatId!=ProfileSchemaV1.FormatId)throw Fail("UNKNOWN_FORMAT","formatId");
            if ((d.FormatVersion != ProfileSchemaV1.FormatVersion &&
                d.FormatVersion != ProfileFormatV2.FormatVersion) || d.SettingsSchemaVersion != 1)
                throw Fail("UNSUPPORTED_VERSION", "format/settings schema");
            if (!ProfileFormatV2.ValidNotes(d.Notes) ||
                (d.FormatVersion == ProfileSchemaV1.FormatVersion && d.Notes.Length != 0))
                throw Fail("INVALID_VALUE", "notes");
            if(d.PortableSchemaFingerprint!=ProfileSchemaV1.Fingerprint)throw Fail("SCHEMA_MISMATCH","fingerprint");
            Guid parsed;
            if(d.ProfileId==null || !Guid.TryParseExact(d.ProfileId,"D",out parsed) ||
                d.ProfileId!=parsed.ToString("D"))throw Fail("INVALID_VALUE","profileId");
            if(string.IsNullOrWhiteSpace(d.DisplayName)||d.DisplayName.Length>80)
                throw Fail("INVALID_VALUE","displayName");
            if(d.SourceBuildId==null||d.SourceBuildId.Length>256 ||
               d.SourceGameVersion==null||d.SourceGameVersion.Length>64)
                throw Fail("INVALID_VALUE","source metadata");
            if(d.CreatedUtc.Kind!=DateTimeKind.Utc || d.UpdatedUtc.Kind!=DateTimeKind.Utc ||
                d.UpdatedUtc<d.CreatedUtc)
                throw Fail("INVALID_VALUE","timestamps");
            if(d.CreatedUtc.Year<2000 || d.UpdatedUtc.Year<2000)
                throw Fail("INVALID_VALUE","timestamp year");
        }
        private static Dictionary<string,List<ProfileFieldSpec>> SpecsBySection()
        {
            var result=new Dictionary<string,List<ProfileFieldSpec>>(StringComparer.Ordinal);
            foreach(ProfileFieldSpec spec in ProfileSchemaV1.GetFields())
            { List<ProfileFieldSpec> group; if(!result.TryGetValue(spec.Section,out group))
              {group=new List<ProfileFieldSpec>();result.Add(spec.Section,group);} group.Add(spec); }
            return result;
        }
        private static void CheckTyped(object value,Type type,string path)
        {
            if(value==null)throw Fail("INVALID_VALUE","null:"+path);
            if(type==typeof(bool)||type==typeof(int)||type==typeof(float))
            {
                if(value.GetType()!=type)throw Fail("SCHEMA_MISMATCH","type:"+path);
                if(type==typeof(float) && (float.IsNaN((float)value)||float.IsInfinity((float)value)))
                    throw Fail("INVALID_VALUE","nonfinite:"+path);
                return;
            }
            if(type==typeof(Dictionary<string,float>))
            {
                var d=value as Dictionary<string,float>;
                if(d==null || d.Count>ProfileSchemaV1.MaxCollectionItems)throw Fail("SCHEMA_MISMATCH","dict:"+path);
                foreach(var entry in d)
                    { CheckKey(entry.Key,path);CheckTyped(entry.Value,typeof(float),path+"[]"); }
                return;
            }
            if(type==typeof(Dictionary<string,bool>))
            {
                var d=value as Dictionary<string,bool>;
                if(d==null || d.Count>ProfileSchemaV1.MaxCollectionItems)throw Fail("SCHEMA_MISMATCH","dict:"+path);
                foreach(var entry in d)CheckKey(entry.Key,path);
                return;
            }
            if(type==typeof(List<float>))
            {
                var d=value as List<float>;
                if(d==null || d.Count>ProfileSchemaV1.MaxCollectionItems)throw Fail("SCHEMA_MISMATCH","list:"+path);
                foreach(float v in d)CheckTyped(v,typeof(float),path+"[]");return;
            }
            if(type==typeof(List<int>))
            {
                var d=value as List<int>;
                if(d==null || d.Count>ProfileSchemaV1.MaxCollectionItems)throw Fail("SCHEMA_MISMATCH","list:"+path);
                return;
            }
            throw Fail("SCHEMA_MISMATCH","unexpected type:"+path);
        }
        private static void CheckKey(string key,string path)
        {
            if(string.IsNullOrWhiteSpace(key)||key.Length>256)throw Fail("INVALID_VALUE","invalid dictionary key:"+path);
        }
        private static object ConvertValue(object raw,Type type,string path)
        {
            if(type==typeof(bool)) {if(!(raw is bool))throw Fail("SCHEMA_MISMATCH","bool:"+path);return raw;}
            if(type==typeof(int))return GetInt(raw,path);
            if(type==typeof(float))return GetFloat(raw,path);
            if(type==typeof(Dictionary<string,float>) || type==typeof(Dictionary<string,bool>))
            {
                var data=AsObject(raw,path);
                if(data.Count>ProfileSchemaV1.MaxCollectionItems)throw Fail("INVALID_VALUE","dict limit:"+path);
                if(type==typeof(Dictionary<string,float>))
                {var d=new Dictionary<string,float>(StringComparer.Ordinal);
                 foreach(var entry in data){CheckKey(entry.Key,path);d.Add(entry.Key,GetFloat(entry.Value,path+"."+entry.Key));}return d;}
                else
                {var d=new Dictionary<string,bool>(StringComparer.Ordinal);
                 foreach(var entry in data){CheckKey(entry.Key,path);if(!(entry.Value is bool))throw Fail("SCHEMA_MISMATCH","bool:"+path+"."+entry.Key);d.Add(entry.Key,(bool)entry.Value);}return d;}
            }
            if(type==typeof(List<float>) || type==typeof(List<int>))
            {
                var arr=raw as List<object>;
                if(arr==null || arr.Count>ProfileSchemaV1.MaxCollectionItems)throw Fail("SCHEMA_MISMATCH","list:"+path);
                if(type==typeof(List<float>))
                {var d=new List<float>();foreach(var value in arr)d.Add(GetFloat(value,path+"[]"));return d;}
                else
                {var d=new List<int>();foreach(var value in arr)d.Add(GetInt(value,path+"[]"));return d;}
            }
            throw Fail("SCHEMA_MISMATCH","unsupported:"+path);
        }
        private static int GetInt(object obj,string path)
        {
            if(obj is int)return (int)obj;
            JsonNumber num=obj as JsonNumber;
            int val;
            if(num==null || !int.TryParse(num.Raw,NumberStyles.AllowLeadingSign,Inv,out val) ||
                num.Raw.IndexOf('+')>=0)throw Fail("SCHEMA_MISMATCH","int:"+path);
            return val;
        }
        private static float GetFloat(object obj,string path)
        {
            if(obj is float) { float f=(float)obj;if(float.IsNaN(f)||float.IsInfinity(f))throw Fail("INVALID_VALUE",path);return f; }
            JsonNumber num=obj as JsonNumber;
            float v;
            if(num==null || !float.TryParse(num.Raw,NumberStyles.Float,Inv,out v) || float.IsNaN(v)||float.IsInfinity(v))
                throw Fail("INVALID_VALUE","float:"+path);
            // A nonzero JSON decimal that rounds to Single zero is data loss,
            // not a valid profile value. Explicit 0 and -0 remain permitted.
            if(v==0f && HasNonZeroMantissa(num.Raw))
                throw Fail("INVALID_VALUE","underflow:"+path);
            return v;
        }
        private static bool HasNonZeroMantissa(string numeric)
        {
            foreach(char c in numeric)
            {if(c=='e'||c=='E')break; if(c>='1'&&c<='9')return true;}
            return false;
        }
        private static string GetString(Dictionary<string,object> o,string key)
        {string value=o[key] as string;if(value==null)throw Fail("SCHEMA_MISMATCH","string:"+key);return value;}
        private static DateTime GetDate(string text,string path)
        {
            DateTime parsed;
            if(!text.EndsWith("Z",StringComparison.Ordinal) ||
                !DateTime.TryParseExact(text,"o",Inv,DateTimeStyles.RoundtripKind,out parsed) ||
                parsed.Kind!=DateTimeKind.Utc)throw Fail("INVALID_VALUE","date:"+path);
            return parsed;
        }
        private static Dictionary<string,object> AsObject(object o,string path)
        {var v=o as Dictionary<string,object>;if(v==null)throw Fail("SCHEMA_MISMATCH","object:"+path);return v;}
        private static void RequireKeys(Dictionary<string,object> obj,string[] keys,string path)
        {
            if(obj.Count!=keys.Length)throw Fail("SCHEMA_MISMATCH","key count:"+path);
            foreach(string key in keys)if(!obj.ContainsKey(key))throw Fail("SCHEMA_MISMATCH","missing:"+path+"."+key);
        }
        private static ProfileContractException Fail(string code,string message)
        {return new ProfileContractException(code,message);}

        private static void JsonWrite(StringBuilder sb,object value)
        {
            if(value==null)throw Fail("INVALID_VALUE","JSON null");
            string text=value as string;
            if(text!=null) {WriteString(sb,text);return;}
            if(value is bool) {sb.Append((bool)value?"true":"false");return;}
            if(value is int) {sb.Append(((int)value).ToString(Inv));return;}
            if(value is float)
            {float f=(float)value;if(float.IsNaN(f)||float.IsInfinity(f))throw Fail("INVALID_VALUE","nonfinite JSON");
             // Preserve IEEE-754 negative zero as well as ordinary float bits.
             if(f==0f && BitConverter.ToInt32(BitConverter.GetBytes(f),0)<0)
                 sb.Append("-0.0");
             else sb.Append(f.ToString("R",Inv));return;}
            var generic=value as IDictionary<string,object>;
            if(generic!=null)
            {sb.Append('{');List<string> keys=new List<string>(generic.Keys);keys.Sort(StringComparer.Ordinal);
             bool first=true;foreach(string key in keys)
             {if(!first)sb.Append(',');first=false;WriteString(sb,key);sb.Append(':');JsonWrite(sb,generic[key]);}
             sb.Append('}');return;}
            var floats=value as Dictionary<string,float>;
            if(floats!=null)
            {sb.Append('{');List<string> keys=new List<string>(floats.Keys);keys.Sort(StringComparer.Ordinal);
             for(int i=0;i<keys.Count;i++){if(i>0)sb.Append(',');WriteString(sb,keys[i]);sb.Append(':');JsonWrite(sb,floats[keys[i]]);}sb.Append('}');return;}
            var bools=value as Dictionary<string,bool>;
            if(bools!=null)
            {sb.Append('{');List<string> keys=new List<string>(bools.Keys);keys.Sort(StringComparer.Ordinal);
             for(int i=0;i<keys.Count;i++){if(i>0)sb.Append(',');WriteString(sb,keys[i]);sb.Append(':');JsonWrite(sb,bools[keys[i]]);}sb.Append('}');return;}
            var ints=value as List<int>;
            if(ints!=null){sb.Append('[');for(int i=0;i<ints.Count;i++){if(i>0)sb.Append(',');JsonWrite(sb,ints[i]);}sb.Append(']');return;}
            var fs=value as List<float>;
            if(fs!=null){sb.Append('[');for(int i=0;i<fs.Count;i++){if(i>0)sb.Append(',');JsonWrite(sb,fs[i]);}sb.Append(']');return;}
            throw Fail("SCHEMA_MISMATCH","unsupported JSON value:"+value.GetType().FullName);
        }
        private static void WriteString(StringBuilder sb,string text)
        {
            sb.Append('"');foreach(char c in text)
            {switch(c){case '"':sb.Append("\\\"");break;case '\\':sb.Append("\\\\");break;
              case '\n':sb.Append("\\n");break;case '\r':sb.Append("\\r");break;
              case '\t':sb.Append("\\t");break;
              default:if(c<32 || c=='\u2028'||c=='\u2029')sb.Append("\\u"+((int)c).ToString("x4",Inv));
                      else sb.Append(c);break;}}
            sb.Append('"');
        }

        private sealed class JsonNumber {internal readonly string Raw;internal JsonNumber(string s){Raw=s;}}
        private sealed class Reader
        {
            private readonly string data;
            private int offset;
            private int tokens;
            internal Reader(string json){data=json;}
            internal object Read()
            {Skip();object value=ReadValue(0);Skip();if(offset!=data.Length)throw Fail("INVALID_FORMAT","trailing data");return value;}
            private object ReadValue(int depth)
            {
                if(depth>16 || ++tokens>50000)throw Fail("INVALID_FORMAT","depth/token limit");
                Skip();if(offset>=data.Length)throw Fail("INVALID_FORMAT","truncated JSON");
                char c=data[offset];
                if(c=='{')return ReadObject(depth+1);
                if(c=='[')return ReadArray(depth+1);
                if(c=='"')return ReadString();
                if(c=='t') {Literal("true");return true;}
                if(c=='f') {Literal("false");return false;}
                if(c=='n') {Literal("null");return null;}
                if(c=='-' || (c>='0'&&c<='9'))return ReadNumber();
                throw Fail("INVALID_FORMAT","unexpected token at "+offset);
            }
            private Dictionary<string,object> ReadObject(int depth)
            {
                ++offset;var result=new Dictionary<string,object>(StringComparer.Ordinal);Skip();
                if(Match('}'))return result;
                while(true)
                {
                    Skip();if(offset>=data.Length || data[offset]!='"')throw Fail("INVALID_FORMAT","expected key");
                    string key=ReadString();if(key.Length>256)throw Fail("INVALID_FORMAT","key too long");
                    Skip();Expect(':');
                    object value=ReadValue(depth);
                    if(result.Count>=ProfileSchemaV1.MaxCollectionItems || result.ContainsKey(key))
                        throw Fail("INVALID_FORMAT","duplicate/too many keys:"+key);
                    result.Add(key,value);Skip();if(Match('}'))return result;Expect(',');
                }
            }
            private List<object> ReadArray(int depth)
            {
                ++offset;var result=new List<object>();Skip();if(Match(']'))return result;
                while(true)
                {if(result.Count>=ProfileSchemaV1.MaxCollectionItems)throw Fail("INVALID_FORMAT","array limit");
                 result.Add(ReadValue(depth));Skip();if(Match(']'))return result;Expect(',');}
            }
            private string ReadString()
            {
                Expect('"');StringBuilder sb=new StringBuilder();bool closed=false;
                while(offset<data.Length)
                {
                    char c=data[offset++];if(c=='"'){closed=true;break;}
                    if(c<32)throw Fail("INVALID_FORMAT","unescaped control");
                    if(c=='\\')
                    {
                        if(offset>=data.Length)throw Fail("INVALID_FORMAT","truncated escape");
                        char e=data[offset++];switch(e)
                        {case '"':sb.Append('"');break;case '\\':sb.Append('\\');break;case '/':sb.Append('/');break;
                         case 'b':sb.Append('\b');break;case 'f':sb.Append('\f');break;
                         case 'n':sb.Append('\n');break;case 'r':sb.Append('\r');break;case 't':sb.Append('\t');break;
                         case 'u':
                            if(offset+4>data.Length)throw Fail("INVALID_FORMAT","short unicode escape");
                            int code=0;for(int j=0;j<4;j++){char x=data[offset++];int digit=(x>='0'&&x<='9')?x-'0':(x>='A'&&x<='F')?x-'A'+10:(x>='a'&&x<='f')?x-'a'+10:-1;
                                if(digit<0)throw Fail("INVALID_FORMAT","bad unicode escape");code=(code<<4)|digit;}
                            sb.Append((char)code);break;
                         default:throw Fail("INVALID_FORMAT","bad escape");}
                    }
                    else sb.Append(c);
                    if(sb.Length>8192)throw Fail("INVALID_FORMAT","string too long");
                }
                if(!closed)throw Fail("INVALID_FORMAT","unterminated string");
                string result=sb.ToString();
                for(int i=0;i<result.Length;i++)
                {char c=result[i];if(char.IsHighSurrogate(c))
                   {if(i+1>=result.Length || !char.IsLowSurrogate(result[i+1]))throw Fail("INVALID_FORMAT","unpaired high surrogate");i++;}
                 else if(char.IsLowSurrogate(c))throw Fail("INVALID_FORMAT","unpaired low surrogate");}
                return result;
            }
            private JsonNumber ReadNumber()
            {
                int start=offset;
                if(Match('-') && offset>=data.Length)throw Fail("INVALID_FORMAT","invalid number");
                if(Match('0')) {if(offset<data.Length && char.IsDigit(data[offset]))throw Fail("INVALID_FORMAT","leading zero");}
                else {if(offset>=data.Length || data[offset]<'1'||data[offset]>'9')throw Fail("INVALID_FORMAT","number expected");
                      while(offset<data.Length && data[offset]>='0'&&data[offset]<='9')offset++;}
                if(Match('.'))
                {int at=offset;while(offset<data.Length && data[offset]>='0'&&data[offset]<='9')offset++;
                 if(at==offset)throw Fail("INVALID_FORMAT","decimal without digits");}
                if(Match('e')||Match('E'))
                {if(!Match('+'))Match('-');int at=offset;
                 while(offset<data.Length && data[offset]>='0'&&data[offset]<='9')offset++;
                 if(at==offset)throw Fail("INVALID_FORMAT","exponent without digits");}
                if(offset-start>80)throw Fail("INVALID_FORMAT","number too long");
                return new JsonNumber(data.Substring(start,offset-start));
            }
            private void Literal(string text)
            {if(offset+text.Length>data.Length || string.CompareOrdinal(data,offset,text,0,text.Length)!=0)
                throw Fail("INVALID_FORMAT","bad literal");offset+=text.Length;}
            private void Skip(){while(offset<data.Length && (data[offset]==' '||data[offset]=='\t'||data[offset]=='\n'||data[offset]=='\r'))offset++;}
            private void Expect(char c){if(!Match(c))throw Fail("INVALID_FORMAT","expected "+c+" at "+offset);}
            private bool Match(char c){if(offset<data.Length && data[offset]==c){offset++;return true;}return false;}
        }
    }
}
