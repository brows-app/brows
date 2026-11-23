using Brows.Operations;
using Brows.Providers;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Brows.Composition;

/// <summary>
/// Performs a transfer of supplied files or streams to a provider.
/// </summary>
public interface IWorkProvidedIO {
    /// <summary>
    /// Transfers the supplied files or stream sources to a provider.
    /// </summary>
    /// <param name="io">
    /// The files or stream sources to transfer.
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
    /// True if the transfer succeeded; otherwise, false.
    /// </returns>
    Task<bool> Work(IEnumerable<IProvidedIO> io,
                    IProvider target,
                    IOperationProgress progress,
                    CancellationToken token);
}
