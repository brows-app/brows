using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.IPC.TxRx;

internal sealed class TxRxProxyCollection : IDisposable {
    private readonly Dictionary<string, TxRxProxy> Lookup = [];

    private bool Disposed;

    private ObjectDisposedException DisposedException() {
        return new ObjectDisposedException(GetType().Name);
    }

    private void Dispose(bool disposing) {
        if (disposing) {
            lock (Lookup) {
                foreach (var item in Lookup) {
                    item.Value.Dispose();
                }
                Lookup.Clear();
                Disposed = true;
                Provider.ProxiesChanged -= Provider_ProxiesChanged;
            }
        }
    }

    private void Provider_ProxiesChanged(object sender, EventArgs e) {
        if (sender is TxRxProxyProvider obj) {
            Sync(obj.GetProxies());
        }
    }

    private void Sync(IEnumerable<TxRxProxy> proxies) {
        /*
         * This method synchronizes the proxies provided by the provider
         * with the proxies managed by this collection. This method may
         * be called on any thread, so it's important to lock around
         * changes to the lookup.
         */
        var dict = (proxies ?? [])
            .Where(item => item is not null)
            .Distinct()
            .GroupBy(item => item.Key)
            .Select(group => group.First())
            .ToDictionary(proxy => proxy.Key);
        lock (Lookup) {
            if (Disposed) {
                return;
            }
            var curKeys = dict.Keys.Where(Lookup.ContainsKey).ToList();
            var newKeys = dict.Keys.Except(Lookup.Keys).ToList();
            var oldKeys = Lookup.Keys.Except(dict.Keys).ToList();
            foreach (var curKey in curKeys) {
                /*
                 * An item for this key already exists in the collection,
                 * so we're not going to use the one provided.
                 * Dispose it.
                 * Do not add it.
                 */
                dict[curKey].Dispose();
            }
            foreach (var oldKey in oldKeys) {
                /*
                 * This item should no longer exist, isn't needed anymore,
                 * or etc. Dispose it, and remove it from the collection.
                 */
                var item = Lookup[oldKey];
                using (item) {
                    Lookup.Remove(oldKey);
                }
            }
            foreach (var newKey in newKeys) {
                /*
                 * This is a new item that we didn't know about before now,
                 * so add it to the collection.
                 */
                Lookup.Add(newKey, dict[newKey]);
            }
        }
    }

    public TxRxProxyProvider Provider { get; }
    public IObjectTypeResolver TypeResolver { get; }

    public TxRxProxyCollection(TxRxProxyProvider provider, IObjectTypeResolver typeResolver) {
        TypeResolver = typeResolver;
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Provider.ProxiesChanged += Provider_ProxiesChanged;
        Provider.Start();
    }

    public int Count() {
        lock (Lookup) {
            if (Disposed) {
                throw DisposedException();
            }
            return Lookup.Count;
        }
    }

    public async Task TxRx(object obj, TxRxInfo info, CancellationToken token) {
        var proxies = default(List<TxRxProxy>);
        lock (Lookup) {
            if (Disposed) {
                throw DisposedException();
            }
            /*
             * Create a new list so we're not iterating over a list
             * that could potentially change during iteration, e.g.
             * if the provided proxies change during iteration.
             */
            proxies = [.. Lookup.Values];
        }
        await Task.WhenAll(proxies.Select(async proxy => {
            try {
                await proxy.TxRx(obj, info, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
                /*
                 * Swallow cancellation. We'll check if cancellation is requested later.
                 * This should prevent an AggregateException full of OperationCanceledException
                 * from being thrown.
                 */
            }
        }));
        /*
         * Remember when we said we'd check for cancellation later?
         * Now's the time.
         */
        if (token.IsCancellationRequested) {
            token.ThrowIfCancellationRequested();
        }
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~TxRxProxyCollection() {
        Dispose(false);
    }
}
