using Brows.Composition;
using Brows.Panels;
using System.Threading.Tasks;

namespace Brows.Providers;

/// <summary>
/// Creates a provider for a location or content identifier.
/// </summary>
public interface IProviderFactory : IExport {
    /// <summary>
    /// Creates a provider for an identifier when the factory supports it.
    /// </summary>
    /// <param name="id">
    /// The provider identifier to open.
    /// </param>
    /// <param name="panel">
    /// The panel that will host the provider.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The initialized provider, or null when the identifier is not supported.
    /// </returns>
    Task<IProvider> CreateFor(string id, IPanel panel, CancellationToken cancellationToken);
}
