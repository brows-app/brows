using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class TarArchiveFileEvent {
    private readonly List<Func<CancellationToken, Task>> Tasks = new();
    private readonly Lock Locker = new();

    internal void Clear() {
        lock (Locker) {
            Tasks.Clear();
        }
    }

    internal void Add(Func<CancellationToken, Task> task) {
        ArgumentNullException.ThrowIfNull(task);
        lock (Locker) {
            Tasks.Add(task);
        }
    }

    internal bool Remove(Func<CancellationToken, Task> task) {
        lock (Locker) {
            return Tasks.Remove(task);
        }
    }

    internal Task All(CancellationToken token) {
        var tasks = new List<Func<CancellationToken, Task>>();
        lock (Locker) {
            tasks.AddRange(Tasks);
        }
        return Task.WhenAll(tasks.Select(task => task(token)));
    }
}
