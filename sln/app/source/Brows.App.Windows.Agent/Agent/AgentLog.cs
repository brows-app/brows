using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Brows.Agent;

/*
 * Keeps the most recent log entries of this process, so an agent can read the log of the instance it
 * drives instead of the log file that every instance shares.
 */
internal sealed class AgentLog : ILogSubscription, IDisposable {
    private const int DefaultCapacity = 1000;

    private readonly Lock Locker = new();
    private readonly Queue<AgentLogEntry> Entries = new();

    private long Sequence;
    private bool Subscribed;

    private int Capacity { get; }

    internal AgentLog(int capacity = DefaultCapacity) {
        if (capacity <= 0) {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Use a positive capacity.");
        }
        Capacity = capacity;
    }

    internal void Subscribe() {
        lock (Locker) {
            if (Subscribed) {
                return;
            }
            Subscribed = true;
        }
        Logging.Subscribe(this);
    }

    internal IReadOnlyList<AgentLogEntry> Since(long seq) {
        lock (Locker) {
            return Entries.Where(entry => entry.Seq > seq).ToList();
        }
    }

    internal IReadOnlyList<AgentLogEntry> Errors(long seq) {
        lock (Locker) {
            return Entries.Where(entry => entry.Seq > seq && entry.IsError).ToList();
        }
    }

    internal void Receive(ILogEntry entry) {
        if (entry is null) {
            return;
        }
        var text = string.Join(" / ", entry.LogList ?? Enumerable.Empty<string>());
        var time = entry.LogDate.ToLocalTime();
        lock (Locker) {
            var item = new AgentLogEntry(++Sequence, time, entry.LogSeverity, entry.LogName, text);
            Entries.Enqueue(item);
            while (Entries.Count > Capacity) {
                Entries.Dequeue();
            }
        }
    }

    public void Dispose() {
        lock (Locker) {
            if (!Subscribed) {
                return;
            }
            Subscribed = false;
        }
        Logging.Unsubscribe(this);
    }

    /*
     * The threshold never changes, so there is nothing to raise.
     */
    event EventHandler ILogSubscription.ThresholdChanged {
        add {
        }
        remove {
        }
    }

    LogSeverity ILogSubscription.Threshold(Type type) {
        return LogSeverity.Info;
    }

    void ILogSubscription.Receive(ILogEntry entry) {
        Receive(entry);
    }
}
