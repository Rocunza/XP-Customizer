#if DEBUG
using System;
using System.Text;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// Pure, in-memory checks. Never touches the user's ModSettings XML,
    /// existing config folder, Scribe, actual WriteSettings or profile files.
    /// </summary>
    internal static class ModXmlRoundTripSelfTests
    {
        private static int checks;
        private static int scenarios;
        private static readonly string Prolog =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><SettingsBlock>" +
            "<ModSettings Class=\"XpCustomizer.XpCustomizerSettings\">";
        private const string Epilog = "</ModSettings></SettingsBlock>";

        internal static void Run()
        {
            checks = 0;
            scenarios = 0;
            XpCustomizerSettings expected = new XpCustomizerSettings();
            string selected = "<gwsHighFactor>1.14999998</gwsHighFactor>" +
                "<furtherLearningMentalThresholdStrength>1</furtherLearningMentalThresholdStrength>" +
                "<careerXpMultiplier>1</careerXpMultiplier>" +
                "<careerXpRoundingStep>0</careerXpRoundingStep>";
            expected.gwsHighFactor = 1.15f;
            expected.furtherLearningMentalThresholdStrength = 1f;
            expected.careerXpMultiplier = 1f;
            expected.careerXpRoundingStep = 0;

            ModXmlRoundTripAudit.Snapshot valid = Scan(Prolog + selected + Epilog, expected);
            Case(valid.ValidXml && valid.State == "VALID_XML" && valid.Compared == 4 &&
                valid.Mismatched == 0 && valid.Omitted == 1, "normal selected fields match");
            Case(valid.Sha256.Length == 64 && valid.ByteLength > 0, "byte digest and length");
            Case(ModXmlRoundTripAudit.EvaluateAfterWrite(valid, valid, true) ==
                "PASS_SELECTED_FIELDS", "healthy readback");
            Case(ModXmlRoundTripAudit.EvaluateAfterWrite(valid, valid, false) ==
                "UNCERTAIN_WRITER_EXCEPTION", "writer exception remains uncertain");

            ModXmlRoundTripAudit.Snapshot absent = new ModXmlRoundTripAudit.Snapshot();
            absent.State = "MISSING";
            Case(ModXmlRoundTripAudit.EvaluateAfterWrite(absent, valid, true) ==
                "PASS_SELECTED_FIELDS", "first save of new settings");
            Case(ModXmlRoundTripAudit.EvaluateAfterWrite(null, valid, true) ==
                "PARTIAL_BASELINE_UNVERIFIED", "missing baseline never passed off as full observation");
            Case(ModXmlRoundTripAudit.EvaluateAfterWrite(null, null, false) ==
                "UNCERTAIN_WRITER_EXCEPTION", "missing both evidence and writer failure");

            ModXmlRoundTripAudit.Snapshot noFields = Scan(Prolog + Epilog, expected);
            Case(noFields.ValidXml && noFields.Compared == 0 && noFields.Omitted == 5,
                "default-elided values not invented");
            Case(ModXmlRoundTripAudit.EvaluateAfterWrite(valid, noFields, true) ==
                "NOT_VERIFIED_NO_SCALAR_EVIDENCE", "zero scalar proof not PASS");

            expected.gwsHighFactor = 1.16f;
            ModXmlRoundTripAudit.Snapshot mismatch = Scan(Prolog + selected + Epilog, expected);
            Case(mismatch.State == "FIELD_MISMATCH" && mismatch.MismatchField == "gwsHighFactor",
                "changed live value detects stale XML");
            Case(ModXmlRoundTripAudit.EvaluateAfterWrite(valid, mismatch, true) ==
                "FAIL_READBACK", "stale bytes reject success");
            expected.gwsHighFactor = 1.15f;

            Case(!Scan("<broken", expected).ValidXml, "malformed XML rejected");
            Case(!Scan("<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<!DOCTYPE SettingsBlock [<!ENTITY x SYSTEM 'file:///secret'>]>" +
                "<SettingsBlock><ModSettings Class=\"XpCustomizer.XpCustomizerSettings\" />" +
                "</SettingsBlock>", expected).ValidXml, "DTD blocked");
            Case(!Scan("<SettingsBlock><ModSettings Class=\"Other.ModSettings\" />" +
                "</SettingsBlock>", expected).ValidXml, "foreign mod settings rejected");
            Case(!Scan("<SettingsBlock><ModSettings /><ModSettings />" +
                "</SettingsBlock>", expected).ValidXml, "duplicate settings blocks rejected");
            Case(!Scan("<Unexpected><ModSettings Class=\"XpCustomizer.XpCustomizerSettings\" />" +
                "</Unexpected>", expected).ValidXml, "wrong root rejected");
            ModXmlRoundTripAudit.Snapshot duplicateScalar = Scan(Prolog +
                "<gwsHighFactor>1</gwsHighFactor>" +
                "<gwsHighFactor>1</gwsHighFactor>" + Epilog, expected);
            Case(duplicateScalar.State == "FIELD_MISMATCH" &&
                duplicateScalar.Mismatched > 0,
                "duplicate tracked scalar detected");

            ModXmlRoundTripAudit.Snapshot numeric = Scan(Prolog +
                "<gwsHighFactor>not-a-number</gwsHighFactor>" + Epilog, expected);
            Case(numeric.State == "FIELD_MISMATCH", "invalid numeric evidence rejected");
            Case(Scan(Prolog + "<gwsHighFactor>NaN</gwsHighFactor>" + Epilog,
                expected).Mismatched > 0, "NaN rejected");
            Case(Scan(Prolog + "<careerXpRoundingStep>999</careerXpRoundingStep>" +
                Epilog, expected).Mismatched > 0, "integer mismatch detected");
            Case(Scan(Prolog + "<gwsHighFactor><li>1</li></gwsHighFactor>" +
                Epilog, expected).Mismatched > 0, "nested scalar rejected");
            Case(Scan(Prolog + "<gwsHighFactor>1.14999998</gwsHighFactor>" +
                Epilog, expected).Compared == 1, "one field only is partial selected evidence");

            ModXmlRoundTripAudit.Snapshot oversize = ModXmlRoundTripAudit.InspectBytes(
                new byte[ModXmlRoundTripAudit.MaximumBytes + 1], expected);
            Case(oversize.State == "READ_REJECTED", "oversize in-memory input rejected");
            Case(ModXmlRoundTripAudit.InspectBytes(null, expected).State == "READ_REJECTED",
                "null input rejected");
            Case(ModXmlRoundTripAudit.EvaluateAfterWrite(valid, numeric, true) ==
                "FAIL_READBACK", "invalid scalar result remains a fail");
            Case(ModXmlRoundTripAudit.EvaluateAfterWrite(numeric, valid, true) ==
                "PARTIAL_BASELINE_UNVERIFIED", "bad pre-save state not silently passed");

            Log.Message("[XPC-R6P1D2C3B][MOD_XML_OBSERVER_SELFTEST] status=PASS; revision=" +
                ModXmlRoundTripAudit.Revision + "; scenarios=" + scenarios +
                "; checks=" + checks + "; scope=PURE_MEMORY_XML_ONLY; " +
                "realModXmlReads=0; realModXmlWrites=0; scribeCalls=0; " +
                "vanillaWriteSettings=NOT_CALLED; productionApply=NOT_EXERCISED_BY_THIS_TEST; build=" +
                XpCustomizerMod.BuildId);
        }

        private static ModXmlRoundTripAudit.Snapshot Scan(string xml, XpCustomizerSettings expected)
        {
            return ModXmlRoundTripAudit.InspectBytes(Encoding.UTF8.GetBytes(xml), expected);
        }

        private static void Case(bool passed, string name)
        {
            scenarios++;
            checks++;
            if (!passed)
                throw new InvalidOperationException("[XPC-R6P1D2C3B] " + name);
        }
    }
}
#endif
