using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Entries;

internal sealed class EntryTaskCollection {
    private readonly ConcurrentDictionary<string, Task> Cache = [];

    public Task<T> Get<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(factory);
        for (; ; ) {
            if (token.IsCancellationRequested) {
                return Task.FromCanceled<T>(token);
            }
            if (Cache.TryGetValue(key, out var task)) {
                if (task is Task<T> task_t) {
                    return task_t;
                }
            }
            lock (Cache) {
                Cache[key] = factory(token);
            }
        }
    }
}
