using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public abstract class EntryProviderFactory<TProvider> : IEntryProviderFactory where TProvider : EntryProvider {
        protected abstract Task<TProvider> CreateFor(string id, IPanel panel, CancellationToken token);

        async Task<IEntryProvider> IEntryProviderFactory.CreateFor(string id, IPanel panel, CancellationToken token) {
            var provider = await CreateFor(id, panel, token);
            if (provider == null) {
                return null;
            }
            provider.Panel = panel;
            provider.Import = await Imports.Ready(token);
            await provider.Init(token);
            return provider;
        }
    }
}
