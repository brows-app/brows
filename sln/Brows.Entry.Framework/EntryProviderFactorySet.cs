using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public sealed class EntryProviderFactorySet {
        public IEnumerable<IEntryProviderFactory> Factories { get; }

        public EntryProviderFactorySet(IEnumerable<IEntryProviderFactory> factories) {
            Factories = new List<IEntryProviderFactory>(factories);
        }

        public async Task<IEntryProvider> CreateFor(string id, IPanel host, CancellationToken cancellationToken) {
            var factories = Factories.Where(factory => factory != null);
            var tasks = factories.Select(f => f.CreateFor(id, host, cancellationToken)).Where(task => task != null).ToList();
            for (; ; ) {
                if (tasks.Count == 0) {
                    return null;
                }
                var task = await Task.WhenAny(tasks);
                var provider = await task;
                if (provider != null) {
                    return provider;
                }
                tasks.Remove(task);
            }
        }
    }
}
