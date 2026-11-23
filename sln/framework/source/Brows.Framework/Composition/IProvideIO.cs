using Brows.Commands;
using Brows.Operations;
using Brows.Providers;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Brows.Composition;

/// <summary>
/// Prepares files or stream sources from a command source for transfer to a provider.
/// </summary>
public interface IProvideIO : IProviderExport {
    /// <summary>
    /// Collects transfer sources for a destination provider.
    /// </summary>
    /// <param name="io">
    /// Receives the prepared files or stream sources.
    /// </param>
    /// <param name="source">
    /// The command source containing the items to transfer.
    /// </param>
    /// <param name="target">
    /// The destination provider.
    /// </param>
    /// <param name="progress">
    /// Receives progress updates for the operation.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// True if the sources were prepared; otherwise, false.
    /// </returns>
    Task<bool> Work(ICollection<IProvidedIO> io,
                    ICommandSource source,
                    IProvider target,
                    IOperationProgress progress,
                    CancellationToken token);
}
