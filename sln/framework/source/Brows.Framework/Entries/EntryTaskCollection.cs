using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Brows.Entries;

internal sealed class EntryTaskCollection {
    private readonly Dictionary<string, Task> Cache = [];

    public Task<T> Get<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(factory);
        if (token.IsCancellationRequested) {
            return Task.FromCanceled<T>(token);
        }
        /*
         * The lock makes lookup and add a single get-or-add step, so the factory
         * runs at most once per entry. A cached task that completed as canceled
         * or faulted is re-run rather than served again: a dead task would
         * otherwise be returned to every later caller, with any token, forever.
         */
        lock (Cache) {
            if (Cache.TryGetValue(key, out var task)) {
                var taskIsDead = task.IsCanceled || task.IsFaulted;
                if (taskIsDead == false && task is Task<T> task_t) {
                    return task_t;
                }
            }
            var newTask = factory(token);
            Cache[key] = newTask;
            return newTask;
        }
    }
}
