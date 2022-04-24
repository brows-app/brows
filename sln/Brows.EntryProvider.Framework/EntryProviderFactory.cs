using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public abstract class EntryProviderFactory : IEntryProviderFactory {
        public abstract Task<IEntryProvider> CreateFor(string id, CancellationToken cancellationToken);

        public virtual Task<IEntryProvider> CreateDefault(CancellationToken cancellationToken) {
            return Task.FromResult(default(IEntryProvider));
        }
    }
}
