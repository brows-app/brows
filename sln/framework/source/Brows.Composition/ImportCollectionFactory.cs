using System.Threading;
using System.Threading.Tasks;

namespace Brows;

/// <summary>
/// Creates instance of <see cref="ImportCollection"/>.
/// </summary>
internal abstract class ImportCollectionFactory {
    /// <summary>
    /// When overridden in a derived class, creates an instance of <see cref="ImportCollection"/>.
    /// </summary>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>A task that completes with the created instance of <see cref="ImportCollection"/>.</returns>
    public abstract Task<ImportCollection> Create(CancellationToken token);
}
