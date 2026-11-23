using Brows.Composition;
using Brows.Panels;
using System.Threading.Tasks;

namespace Brows.Providers;

/// <summary>
/// Creates and initializes providers of a particular type for a panel.
/// </summary>
/// <typeparam name="TProvider">
/// The type of provider created by the factory.
/// </typeparam>
public abstract class ProviderFactory<TProvider> : IProviderFactory where TProvider : Provider {
    [ImportRequired]
    internal IImportAgent Import { get; set; }

    /// <summary>
    /// Creates a provider when the identifier is supported by this factory.
    /// </summary>
    /// <param name="id">
    /// The provider identifier to open.
    /// </param>
    /// <param name="panel">
    /// The panel that will host the provider.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The provider, or null when the identifier is not supported.
    /// </returns>
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
