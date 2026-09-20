using Brows.Composition;
using Brows.Panels;
using System.Threading.Tasks;

namespace Brows.Providers;

public abstract class ProviderFactory<TProvider> : IProviderFactory where TProvider : Provider {
    [ImportRequired]
    internal IImportAgent Import { get; set; }

    protected abstract Task<TProvider> CreateFor(string id, IPanel panel, CancellationToken token);

    async Task<IProvider> IProviderFactory.CreateFor(string id, IPanel panel, CancellationToken token) {
        var provider = await CreateFor(id, panel, token);
        if (provider is null) {
            return null;
        }
        provider.Panel = panel;
        provider.Import = Import;
        await provider.Init(token);
        return provider;
    }
}
