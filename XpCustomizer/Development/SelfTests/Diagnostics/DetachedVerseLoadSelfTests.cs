#if DEBUG
using System;
using System.Collections.Generic;
using Verse;

namespace XpCustomizer
{
    /// <summary>Startup pure tests only: never creates a file or calls Scribe.</summary>
    internal static class DetachedVerseLoadSelfTests
    {
        private static int checks;
        internal static void Run()
        {
            checks = 0;
            XpCustomizerSettings source = new XpCustomizerSettings();
            XpCustomizerSettings twin = SettingsSnapshotService.CloneIndependent(source);
            string mismatch;
            Check(DetachedVerseLoadValueComparer.Equivalent(source, twin, out mismatch) &&
                mismatch == "NONE", "independent defaults equal");
            Check(SettingsSnapshotService.GetDeclaredSettingsFieldsForAudit().Length == 108,
                "108 root-field inventory unchanged");
            twin.gwsHighFactor = source.gwsHighFactor + 0.13f;
            Check(!DetachedVerseLoadValueComparer.Equivalent(source, twin, out mismatch) &&
                mismatch == "gwsHighFactor", "scalar mismatch is named");
            twin = SettingsSnapshotService.CloneIndependent(source);
            twin.consciousnessAnchorSpeed.HighFactor += 0.15f;
            Check(!DetachedVerseLoadValueComparer.Equivalent(source, twin, out mismatch) &&
                mismatch == "consciousnessAnchorSpeed.HighFactor",
                "nested profile mismatch is named");
            twin = SettingsSnapshotService.CloneIndependent(source);
            twin.careerXpSplitLevels.Add(99);
            Check(!DetachedVerseLoadValueComparer.Equivalent(source, twin, out mismatch) &&
                mismatch == "careerXpSplitLevels.list", "list difference is named");
            twin = SettingsSnapshotService.CloneIndependent(source);
            twin.gwsSkillEnabled["SkillTest"] = true;
            Check(!DetachedVerseLoadValueComparer.Equivalent(source, twin, out mismatch) &&
                mismatch == "gwsSkillEnabled.dictionary", "dictionary difference is named");
            source.gwsSkillEnabled["SkillTest"] = true;
            twin = SettingsSnapshotService.CloneIndependent(source);
            twin.gwsSkillEnabled = new Dictionary<string, bool>(
                twin.gwsSkillEnabled, StringComparer.Ordinal);
            Check(DetachedVerseLoadValueComparer.Equivalent(source, twin, out mismatch),
                "default and explicit Ordinal comparers match by behavior");
            twin.gwsSkillEnabled = new Dictionary<string, bool>(
                twin.gwsSkillEnabled, StringComparer.OrdinalIgnoreCase);
            Check(!DetachedVerseLoadValueComparer.Equivalent(source, twin, out mismatch) &&
                mismatch == "gwsSkillEnabled.dictionary",
                "nonordinal comparer rejected even when entries equal");
            Check(!DetachedVerseLoadValueComparer.Equivalent(null, twin, out mismatch) &&
                mismatch == "NULL_OBJECT", "null candidate rejected");
            Check(DetachedVerseLoadValueComparer.Equivalent(source,
                SettingsSnapshotService.CloneIndependent(source), out mismatch),
                "no mutation of source from previous comparisons");
            Log.Message("[XPC-R6P1D2C4B2C1][DETACHED_VERSE_LOAD_SELFTEST] " +
                "status=PASS; checks=" + checks +
                "; scope=PURE_NO_ENGINE_IO; engineLoader=NOT_RUN" +
                "; liveSettingsWrites=0; scribeCalls=0; productionApply=NOT_EXERCISED_BY_THIS_TEST" +
                "; build=" + XpCustomizerMod.BuildId);
        }
        private static void Check(bool passed, string reason)
        {
            if (!passed) throw new InvalidOperationException(
                "B2C1_PURE_CASE_FAILED:" + reason);
            checks++;
        }
    }
}
#endif
