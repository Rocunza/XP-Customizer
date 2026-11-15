#if DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace XpCustomizer
{
    /// <summary>
    /// B2-C2: bounded read-only observations of Verse.Log's public message queue.
    /// No Harmony, clearing, suppressing or enqueueing of engine messages.
    /// A snapshot takes the engine's own private log lock only while copying the
    /// queue. The entire Scribe action is NEVER executed under that lock.
    /// 
    /// Even a clean result proves only absence of *observed queued* messages:
    /// temporary log suppression, other threads, capped repeats, and Unity's
    /// independent log stream cannot be completely audited this way.
    /// </summary>
    internal static class DetachedLogQueueObserver
    {
        internal const string Revision = "R6P1D2C4B2C2-QUEUE-READONLY-20261107";
        private const BindingFlags PrivateStatics = BindingFlags.NonPublic | BindingFlags.Static;
        private const int ExpectedQueueCapacity = 1000;
        private const int MinimumFreeSlots = 64;

        private static readonly FieldInfo QueueField =
            typeof(Log).GetField("messageQueue", PrivateStatics);
        private static readonly FieldInfo LockField =
            typeof(Log).GetField("logLock", PrivateStatics);
        private static readonly FieldInfo DisablersField =
            typeof(Log).GetField("logDisablers", PrivateStatics);
        private static readonly FieldInfo ReachedLimitField =
            typeof(Log).GetField("reachedMaxMessagesLimit", PrivateStatics);
        private static readonly FieldInfo LoggingErrorField =
            typeof(Log).GetField("currentlyLoggingError", PrivateStatics);

        internal sealed class Item
        {
            internal LogMessage Identity;
            internal string Text;
            internal LogMessageType Type;
            internal int Repeats;

            internal Item(LogMessage source)
            {
                Identity = source;
                Text = source.text;
                Type = source.type;
                Repeats = source.repeats;
            }
        }

        internal sealed class Snapshot
        {
            internal Item[] Entries;
            internal int Capacity;
            internal bool SuppressionFree;
        }

        internal sealed class Result
        {
            internal bool Reliable;
            internal bool QueueAnchorPreserved;
            internal string Reason = "NOT_OBSERVED";
            internal int BeforeCount = -1;
            internal int AfterCount = -1;
            internal int NewErrorEntries;
            internal int NewWarningEntries;
            internal int NewMessageEntries;
            internal int RepeatDelta;
            internal bool RepeatDeltaDetected;
        }

        internal static bool TryCapture(out Snapshot snapshot, out string failure)
        {
            snapshot = null;
            failure = "LOG_OBSERVABILITY_UNCERTAIN";
            try
            {
                if (QueueField == null || LockField == null ||
                    DisablersField == null || ReachedLimitField == null ||
                    LoggingErrorField == null ||
                    QueueField.FieldType != typeof(LogMessageQueue) ||
                    LockField.FieldType != typeof(object) ||
                    DisablersField.FieldType != typeof(int) ||
                    ReachedLimitField.FieldType != typeof(bool) ||
                    LoggingErrorField.FieldType != typeof(bool))
                    return false;
                object gate = LockField.GetValue(null);
                if (gate == null) return false;
                // All built-in Verse.Log enqueue/clear methods use this gate.
                // Direct external mutation of the queue remains outside proof.
                lock (gate)
                {
                    if (!Object.ReferenceEquals(LockField.GetValue(null), gate))
                        return false;
                    LogMessageQueue queue = QueueField.GetValue(null) as LogMessageQueue;
                    if (queue == null) return false;
                    int capacity = queue.maxMessages;
                    if (capacity != ExpectedQueueCapacity) return false;
                    bool stateBefore = (int)DisablersField.GetValue(null) == 0 &&
                        !(bool)ReachedLimitField.GetValue(null) &&
                        !(bool)LoggingErrorField.GetValue(null);
                    if (!stateBefore) return false;
                    Item[] items;
                    if (!TryCopyItems(Log.Messages, capacity, out items))
                        return false;
                    bool stateAfter = (int)DisablersField.GetValue(null) == 0 &&
                        !(bool)ReachedLimitField.GetValue(null) &&
                        !(bool)LoggingErrorField.GetValue(null);
                    if (!stateAfter || !Object.ReferenceEquals(QueueField.GetValue(null), queue) ||
                        queue.maxMessages != capacity)
                        return false;
                    snapshot = new Snapshot
                    {
                        Entries = items,
                        Capacity = capacity,
                        SuppressionFree = true
                    };
                    failure = "NONE";
                    return true;
                }
            }
            catch (Exception)
            {
                // Enumeration/reflection race or engine schema mismatch.
                snapshot = null;
                return false;
            }
        }

        // Shared with synthetic tests to exercise bad enumerators without
        // ever injecting a message into the real Verse queue.
        internal static bool TryCopyItems(IEnumerable<LogMessage> messages,
            int capacity, out Item[] copied)
        {
            copied = null;
            try
            {
                if (messages == null || capacity != ExpectedQueueCapacity)
                    return false;
                List<Item> items = new List<Item>();
                HashSet<LogMessage> identities = new HashSet<LogMessage>();
                foreach (LogMessage msg in messages)
                {
                    if (msg == null || !identities.Add(msg) ||
                        msg.text == null || msg.repeats <= 0 ||
                        msg.repeats >= 99 || !KnownType(msg.type))
                        return false;
                    items.Add(new Item(msg));
                    if (items.Count > capacity - MinimumFreeSlots)
                        return false;
                }
                copied = items.ToArray();
                return true;
            }
            catch (Exception) { return false; }
        }

        internal static Result Analyze(Snapshot before, Snapshot after)
        {
            Result result = new Result();
            if (before != null && before.Entries != null)
                result.BeforeCount = before.Entries.Length;
            if (after != null && after.Entries != null)
                result.AfterCount = after.Entries.Length;
            if (!Healthy(before) || !Healthy(after) ||
                before.Entries.Length == 0 || before.Capacity != after.Capacity ||
                after.Entries.Length < before.Entries.Length)
            {
                result.Reason = "LOG_OBSERVABILITY_UNCERTAIN";
                return result;
            }
            for (int i = 0; i < before.Entries.Length; i++)
            {
                Item a = before.Entries[i];
                Item b = after.Entries[i];
                if (!Object.ReferenceEquals(a.Identity, b.Identity) ||
                    !String.Equals(a.Text, b.Text, StringComparison.Ordinal) ||
                    a.Type != b.Type || b.Repeats < a.Repeats)
                {
                    result.Reason = "LOG_OBSERVABILITY_UNCERTAIN";
                    return result;
                }
            }
            result.QueueAnchorPreserved = true;
            for (int i = 0; i < after.Entries.Length; i++)
            {
                Item item = after.Entries[i];
                int delta = i < before.Entries.Length ?
                    item.Repeats - before.Entries[i].Repeats : item.Repeats;
                if (delta <= 0) continue;
                if (i < before.Entries.Length)
                {
                    result.RepeatDelta += delta;
                    result.RepeatDeltaDetected = true;
                }
                switch (item.Type)
                {
                    case LogMessageType.Error: result.NewErrorEntries += delta; break;
                    case LogMessageType.Warning: result.NewWarningEntries += delta; break;
                    case LogMessageType.Message: result.NewMessageEntries += delta; break;
                    default:
                        result.Reason = "LOG_OBSERVABILITY_UNCERTAIN";
                        return result;
                }
            }
            result.Reliable = true;
            // Any new queued entry may belong to another mod. Never assign
            // causality to the XP mod; the global window is contaminated.
            result.Reason = result.NewErrorEntries != 0 ? "NEW_ERROR_OBSERVED" :
                (result.NewWarningEntries != 0 ? "NEW_WARNING_OBSERVED" :
                (result.NewMessageEntries != 0 ? "NEW_MESSAGE_OBSERVED" : "NONE"));
            return result;
        }

        private static bool Healthy(Snapshot snapshot)
        {
            if (snapshot == null || snapshot.Entries == null ||
                !snapshot.SuppressionFree ||
                snapshot.Capacity != ExpectedQueueCapacity ||
                snapshot.Entries.Length > snapshot.Capacity - MinimumFreeSlots)
                return false;
            HashSet<LogMessage> identities = new HashSet<LogMessage>();
            foreach (Item item in snapshot.Entries)
            {
                if (item == null || item.Identity == null ||
                    !identities.Add(item.Identity) || item.Text == null ||
                    item.Repeats < 1 || item.Repeats >= 99 ||
                    !KnownType(item.Type)) return false;
            }
            return true;
        }

        private static bool KnownType(LogMessageType type)
        {
            return type == LogMessageType.Message || type == LogMessageType.Warning ||
                type == LogMessageType.Error;
        }

        // Test only: snapshots of isolated in-memory messages; does not call
        // Verse.Log.Error / Warning or touch its global queue.
        internal static Snapshot SyntheticSnapshot(int capacity, bool suppressionFree,
            params LogMessage[] messages)
        {
            List<Item> items = new List<Item>();
            foreach (LogMessage message in messages)
                items.Add(message == null ? null : new Item(message));
            return new Snapshot
            {
                Entries = items.ToArray(), Capacity = capacity,
                SuppressionFree = suppressionFree
            };
        }
    }
}
#endif
