using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Verse;
using XpCustomizer.Profiles;

namespace XpCustomizer.UI
{
    internal sealed partial class ProfileManagementSettingsUI
    {
        private void BuildDetailLines()
        {
            detailRows.Clear();
            ProfileDocument doc = state.Selected;
            if (doc == null) return;
            detailRows.Add(string.Format("XPC_Profile_PreviewName".Translate().ToString(), doc.DisplayName));
            detailRows.Add(string.Format("XPC_Profile_PreviewUpdated".Translate().ToString(), FormatUtc(doc.UpdatedUtc)));
            detailRows.Add(string.Format("XPC_Profile_PreviewCreated".Translate().ToString(), FormatUtc(doc.CreatedUtc)));
            detailRows.Add(string.Format("XPC_Profile_PreviewFormat".Translate().ToString(), doc.FormatVersion, doc.SettingsSchemaVersion));
            detailRows.Add(state.InTrash ? "XPC_Profile_PreviewTrash".Translate().ToString() :
                "XPC_Profile_PreviewVerified".Translate().ToString());
            detailRows.Add(" ");
            detailRows.Add("XPC_Profile_SummaryTitle".Translate().ToString());
            ProfileSettingsDto dto = doc.Settings;
            if (dto != null)
            {
                AddSections(dto.Sections);
                AddSections(dto.Curves);
            }
            detailRows.Add(" ");
            detailRows.Add("XPC_Profile_TechnicalTitle".Translate().ToString());
            detailRows.Add(string.Format("XPC_Profile_PreviewId".Translate().ToString(), doc.ProfileId));
            detailRows.Add(string.Format("XPC_Profile_PreviewGame".Translate().ToString(), doc.SourceGameVersion));
            detailRows.Add(string.Format("XPC_Profile_PreviewBuild".Translate().ToString(), doc.SourceBuildId));
            detailRows.Add(string.Format("XPC_Profile_PreviewFingerprint".Translate().ToString(), doc.PortableSchemaFingerprint));
        }

        private void AddSections(Dictionary<string, Dictionary<string, object>> sections)
        {
            List<string> sectionKeys = new List<string>(sections.Keys);
            sectionKeys.Sort(StringComparer.Ordinal);
            foreach (string key in sectionKeys)
            {
                Dictionary<string, object> values = sections[key];
                detailRows.Add(string.Format("XPC_Profile_SectionCount".Translate().ToString(), key, values.Count));
                List<string> fieldKeys = new List<string>(values.Keys);
                fieldKeys.Sort(StringComparer.Ordinal);
                int count = Math.Min(5, fieldKeys.Count);
                for (int i = 0; i < count; i++)
                {
                    string f = fieldKeys[i];
                    detailRows.Add("  " + f + ": " + ProfileManagementUiState.LocalizedPreviewValue(values[f], CompactValue(values[f])));
                }
                if (count < fieldKeys.Count)
                    detailRows.Add(string.Format("XPC_Profile_AdditionalFields".Translate().ToString(), fieldKeys.Count - count));
            }
        }

        private static string CompactValue(object value)
        {
            if (value == null) return "null";
            if (value is bool) return (bool)value ? "true" : "false";
            if (value is float) return ((float)value).ToString("0.######", CultureInfo.InvariantCulture);
            if (value is int) return ((int)value).ToString(CultureInfo.InvariantCulture);
            IDictionary dict = value as IDictionary;
            if (dict != null) return "map [" + dict.Count + " entries]";
            IList list = value as IList;
            if (list != null) return "list [" + list.Count + " entries]";
            return "(unsupported preview type)";
        }

        private static string FormatUtc(DateTime utc)
        {
            return utc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
        }
    }
}
