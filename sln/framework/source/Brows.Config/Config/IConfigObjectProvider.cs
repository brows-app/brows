using Brows.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Config;

/// <summary>
/// Provides configuration objects.
/// </summary>
public interface IConfigObjectProvider : IExport {
    /// <summary>
    /// Gets the configuration object of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of configuration object to get.</typeparam>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>A task that results in the configuration object for type <typeparamref name="T"/>.</returns>
    Task<T> Get<T>(CancellationToken token) where T : new();
}
