using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public sealed class ProviderFactorySet {
        private static readonly ILog Log = Logging.For(typeof(ProviderFactorySet));

        public IEnumerable<IProviderFactory> Items { get; }

        public ProviderFactorySet(IEnumerable<IProviderFactory> items) {
            ArgumentNullException.ThrowIfNull(items);
            Items = items.Where(item => item != null).ToList();
        }

        public async Task<IProvider> CreateFor(string id, IPanel host, CancellationToken cancellationToken) {
            var tasks = Items
                .Select(factory => factory.CreateFor(id, host, cancellationToken))
                .Where(task => task != null)
                .ToList();
            for (; ; ) {
                if (tasks.Count == 0) {
                    return null;
                }
                var task = await Task.WhenAny(tasks).ConfigureAwait(false);
                var provider = default(IProvider);
                try {
                    provider = await task.ConfigureAwait(false);
                }
                catch (Exception ex) {
                    if (Log.Error()) {
                        Log.Error(ex);
                    }
                }
                if (provider != null) {
                    return provider;
                }
                tasks.Remove(task);
            }
        }
    }
}
