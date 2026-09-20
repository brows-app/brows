using System.Collections.Concurrent;
using System.Threading;

namespace Brows.Collections;

public abstract class AutoLookup<TKey, TItem> {
    private readonly ConcurrentDictionary<TKey, TItem> Agent = [];

    protected abstract TItem Instantiate(TKey key);

    public TItem this[TKey key] {
        get {
            if (Agent.TryGetValue(key, out var item)) {
                return item;
            }
            lock (Agent) {
                if (Agent.TryGetValue(key, out item)) {
                    return item;
                }
                item = Instantiate(key);
                Thread.MemoryBarrier();
                Agent[key] = item;
            }
            return item;
        }
    }
}
