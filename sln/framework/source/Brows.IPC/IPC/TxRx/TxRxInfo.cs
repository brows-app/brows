using System;
using System.Threading.Tasks;

namespace Brows.IPC.TxRx;

/// <summary>
/// Information and options for transmit/receive operations.
/// </summary>
public sealed record TxRxInfo {
    /// <summary>
    /// Gets or sets a callback invoked after data is serialized.
    /// </summary>
    public Func<string, CancellationToken, Task> OnSerialized { get; init; }

    /// <summary>
    /// Gets or sets a callback invoked before data is deserialized.
    /// </summary>
    public Func<string, CancellationToken, Task> OnDeserializing { get; init; }

    /// <summary>
    /// Gets or sets a callback invoked when an error occurs.
    /// </summary>
    public Func<Exception, CancellationToken, Task> OnError { get; init; }
}
