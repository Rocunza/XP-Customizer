using System;
using System.Collections.Generic;

namespace XpCustomizer.Profiles
{
    /// <summary>Portable, detached, non-executable v1 data only. Never stores a ModSettings object.</summary>
    internal sealed class ProfileDocument
    {
        internal string FormatId;
        internal int FormatVersion;
        internal int SettingsSchemaVersion;
        internal string PortableSchemaFingerprint;
        internal string ProfileId;
        internal string DisplayName;
        internal string Notes = "";
        internal DateTime CreatedUtc;
        internal DateTime UpdatedUtc;
        internal string SourceBuildId;
        internal string SourceGameVersion;
        internal ProfileSettingsDto Settings;
    }

    internal sealed class ProfileSettingsDto
    {
        // Only detached bool/int/float/Dictionary<string,float|bool>/List<int|float>
        // are permitted by the field allowlist. Dictionary value objects are not
        // CLR-serialized by type name; the codec validates every key and type.
        internal readonly Dictionary<string, Dictionary<string, object>> Sections =
            new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
        internal readonly Dictionary<string, Dictionary<string, object>> Curves =
            new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
    }

    internal sealed class ProfileContractException : Exception
    {
        internal readonly string Code;
        internal ProfileContractException(string code, string details) : base(details)
        { Code = code; }
    }
}
