#if DEBUG
using System;
using System.Collections.Generic;
using Verse;

namespace XpCustomizer
{
    /// <summary>Pure in-memory queue models only; never logs test errors.</summary>
    internal static class DetachedLogQueueSelfTests
    {
        private static int checks;
        private static LogMessage M(LogMessageType type, string text)
        {
            return new LogMessage(type, text, "synthetic");
        }

        private static DetachedLogQueueObserver.Snapshot S(params LogMessage[] messages)
        {
            return DetachedLogQueueObserver.SyntheticSnapshot(1000, true, messages);
        }

        private static void Check(bool valid, string label)
        {
            if (!valid) throw new InvalidOperationException(
                "B2C2_PURE_CASE_FAILED:" + label);
            checks++;
        }

        private static IEnumerable<LogMessage> FailingEnumeration()
        {
            yield return M(LogMessageType.Message, "partial");
            throw new InvalidOperationException("SYNTHETIC_ENUMERATOR_FAILURE");
        }

        internal static void Run()
        {
            checks = 0;
            LogMessage old = M(LogMessageType.Message, "before");
            DetachedLogQueueObserver.Snapshot before = S(old);
            DetachedLogQueueObserver.Result result = DetachedLogQueueObserver.Analyze(before, S(old));
            Check(result.Reliable && result.Reason == "NONE" &&
                result.QueueAnchorPreserved, "unchanged queue is clean");
            Check(result.BeforeCount == 1 && result.AfterCount == 1 &&
                !result.RepeatDeltaDetected, "clean metrics");

            result = DetachedLogQueueObserver.Analyze(before,
                S(old, M(LogMessageType.Message, "info")));
            Check(result.Reliable && result.Reason == "NEW_MESSAGE_OBSERVED" &&
                result.NewMessageEntries == 1, "new informational contamination");
            result = DetachedLogQueueObserver.Analyze(before,
                S(old, M(LogMessageType.Warning, "warning")));
            Check(result.Reliable && result.Reason == "NEW_WARNING_OBSERVED" &&
                result.NewWarningEntries == 1, "new warning");
            result = DetachedLogQueueObserver.Analyze(before,
                S(old, M(LogMessageType.Error, "error")));
            Check(result.Reliable && result.Reason == "NEW_ERROR_OBSERVED" &&
                result.NewErrorEntries == 1, "new engine error");
            result = DetachedLogQueueObserver.Analyze(before,
                S(old, M(LogMessageType.Warning, "warn"), M(LogMessageType.Error, "err")));
            Check(result.Reliable && result.Reason == "NEW_ERROR_OBSERVED" &&
                result.NewWarningEntries == 1, "error priority over warning");

            LogMessage repeatedError = M(LogMessageType.Error, "oldError");
            before = S(old, repeatedError);
            repeatedError.repeats = 2;
            result = DetachedLogQueueObserver.Analyze(before, S(old, repeatedError));
            Check(result.Reliable && result.NewErrorEntries == 1 &&
                result.RepeatDeltaDetected && result.RepeatDelta == 1,
                "existing error repeat counted");
            LogMessage repeatedWarning = M(LogMessageType.Warning, "oldWarning");
            before = S(old, repeatedWarning);
            repeatedWarning.repeats = 3;
            result = DetachedLogQueueObserver.Analyze(before, S(old, repeatedWarning));
            Check(result.Reliable && result.NewWarningEntries == 2 &&
                result.RepeatDelta == 2, "existing warning repeat counted");

            LogMessage capped = M(LogMessageType.Error, "capped");
            capped.repeats = 99;
            Check(!DetachedLogQueueObserver.Analyze(S(old, capped),
                S(old, capped)).Reliable, "repeat cap 99 rejected");
            capped.repeats = 98;
            before = S(old, capped);
            capped.repeats = 99;
            Check(!DetachedLogQueueObserver.Analyze(before,
                S(old, capped)).Reliable, "repeat reaches cap rejected");

            Check(!DetachedLogQueueObserver.Analyze(S(), S()).Reliable,
                "empty unanchored baseline rejected");
            Check(!DetachedLogQueueObserver.Analyze(S(old), S()).Reliable,
                "cleared queue rejected");
            Check(!DetachedLogQueueObserver.Analyze(S(old),
                S(M(LogMessageType.Message, "replacement"))).Reliable,
                "lost identity anchor rejected");
            LogMessage next = M(LogMessageType.Message, "next");
            Check(!DetachedLogQueueObserver.Analyze(S(old, next),
                S(next, old)).Reliable, "queue reordering rejected");
            Check(!DetachedLogQueueObserver.Analyze(S(old),
                S(old, old)).Reliable, "duplicate queue identity rejected");
            Check(!DetachedLogQueueObserver.Analyze(S(old),
                DetachedLogQueueObserver.SyntheticSnapshot(999, true, old)).Reliable,
                "capacity change rejected");
            Check(!DetachedLogQueueObserver.Analyze(S(old),
                DetachedLogQueueObserver.SyntheticSnapshot(1000, false, old)).Reliable,
                "log suppression rejected");
            Check(!DetachedLogQueueObserver.Analyze(null, S(old)).Reliable,
                "capture failure rejected");
            Check(!DetachedLogQueueObserver.Analyze(S(old), null).Reliable,
                "post snapshot failure rejected");
            Check(!DetachedLogQueueObserver.Analyze(S(old), S(old, null)).Reliable,
                "null queue message rejected");
            LogMessage invalidRepeat = M(LogMessageType.Message, "badRepeat");
            invalidRepeat.repeats = 0;
            Check(!DetachedLogQueueObserver.Analyze(S(old),
                S(old, invalidRepeat)).Reliable, "zero repeats rejected");
            invalidRepeat.repeats = 1;
            invalidRepeat.type = (LogMessageType)200;
            Check(!DetachedLogQueueObserver.Analyze(S(old),
                S(old, invalidRepeat)).Reliable, "unknown type rejected");
            LogMessage prior = M(LogMessageType.Message, "prior");
            before = S(prior);
            prior.repeats = 2;
            result = DetachedLogQueueObserver.Analyze(before, S(prior));
            Check(result.Reliable && result.Reason == "NEW_MESSAGE_OBSERVED" &&
                result.RepeatDeltaDetected, "repeat informational message tracked");
            LogMessage messageMutated = M(LogMessageType.Message, "mutable");
            before = S(messageMutated);
            messageMutated.text = "changed";
            Check(!DetachedLogQueueObserver.Analyze(before,
                S(messageMutated)).Reliable, "mutable message text rejected");
            LogMessage repeatsDecrease = M(LogMessageType.Warning, "decreased");
            repeatsDecrease.repeats = 3;
            before = S(repeatsDecrease);
            repeatsDecrease.repeats = 2;
            Check(!DetachedLogQueueObserver.Analyze(before,
                S(repeatsDecrease)).Reliable, "repeat count decrease rejected");

            LogMessage[] nearCapacity = new LogMessage[937];
            nearCapacity[0] = old;
            for (int i = 1; i < nearCapacity.Length; i++)
                nearCapacity[i] = M(LogMessageType.Message, "queue" + i);
            Check(!DetachedLogQueueObserver.Analyze(S(old),
                S(nearCapacity)).Reliable, "near-eviction capacity rejected");
            Check(!DetachedLogQueueObserver.Analyze(S(old),
                DetachedLogQueueObserver.SyntheticSnapshot(1001, true, old)).Reliable,
                "unsupported queue capacity rejected");

            DetachedLogQueueObserver.Item[] unused;
            Check(!DetachedLogQueueObserver.TryCopyItems(
                FailingEnumeration(), 1000, out unused) && unused == null,
                "concurrent-style enumeration exception rejected");
            Check(!DetachedLogQueueObserver.TryCopyItems(null, 1000, out unused),
                "null enumerable rejected");
            Check(DetachedLogQueueObserver.TryCopyItems(
                new LogMessage[] { old }, 1000, out unused) && unused.Length == 1,
                "real copier accepts synthetic normal message");

            Log.Message("[XPC-R6P1D2C4B2C2][LOG_QUEUE_SELFTEST] status=PASS; checks=" +
                checks + "; scope=PURE_SYNTHETIC_MESSAGES; globalLogQueueWrites=0" +
                "; engineScribeCalls=0; liveSettingsWrites=0; productionApply=NOT_EXERCISED_BY_THIS_TEST" +
                "; build=" + XpCustomizerMod.BuildId);
        }
    }
}
#endif
