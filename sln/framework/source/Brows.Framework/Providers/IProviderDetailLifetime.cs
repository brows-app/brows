using System.Threading;
using System.Threading.Tasks;

namespace Brows.Providers;

/// <summary>
/// Provides asynchronous resource cleanup for a provider detail.
/// </summary>
public interface IProviderDetailLifetime {
    /// <summary>
    /// Invalidates pending work and focus requests before cleanup begins.
    /// </summary>
    void Invalidate();

    /// <summary>
    /// Stops resources owned by the detail. Implementations must allow repeated calls.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when the detail resources have been closed.
    /// </returns>
    Task CloseAsync(CancellationToken token);
}