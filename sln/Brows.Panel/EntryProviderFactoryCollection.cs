using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public class EntryProviderFactoryCollection : IEnumerable<IEntryProviderFactory> {
        private readonly List<IEntryProviderFactory> List;

        public IEnumerable<IEntryProviderFactory> Collection => List;

        public EntryProviderFactoryCollection(IEnumerable<IEntryProviderFactory> collection) {
            List = new List<IEntryProviderFactory>(collection);
        }

        public async Task<IEntryProvider> CreateFor(string id, CancellationToken cancellationToken) {
            foreach (var item in List) {
                var provider = await item.CreateFor(id, cancellationToken);
                if (provider != null) {
                    return provider;
                }
            }
            return null;
        }

        public async Task<IEntryProvider> CreateDefault(CancellationToken cancellationToken) {
            foreach (var item in List) {
                var provider = await item.CreateDefault(cancellationToken);
                if (provider != null) {
                    return provider;
                }
            }
            return null;
        }

        public IEnumerator<IEntryProviderFactory> GetEnumerator() {
            return ((IEnumerable<IEntryProviderFactory>)List).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return ((IEnumerable)List).GetEnumerator();
        }
    }
}
