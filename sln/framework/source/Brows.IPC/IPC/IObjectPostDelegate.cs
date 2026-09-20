using System.Collections.Generic;

namespace Brows.IPC;

/// <summary>
/// Contract for delegates that post events to listeners.
/// </summary>
public interface IObjectPostDelegate {
    /// <summary>
    /// Gets the type resolver used by the delegate.
    /// </summary>
    IObjectTypeResolver TypeResolver { get; }

    /// <summary>
    /// Enumerates over the events posted by the delegate.
    /// </summary>
    /// <param name="token">The cancellation token for the enumeration.</param>
    /// <returns>An enumeration of the events posted by the delegate.</returns>
    IAsyncEnumerable<object> Posts(CancellationToken token);
}
