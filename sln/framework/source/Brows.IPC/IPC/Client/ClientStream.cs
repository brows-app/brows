using System;
using System.Threading.Tasks;

namespace Brows.IPC.Client;

/// <summary>
/// The base class for client streams that provides methods to
/// read and write text to and from a stream.
/// </summary>
internal abstract class ClientStream : IDisposable {

    /// <summary>
    /// When overridden in a derived class, disposes managed and/or unmanaged
    /// resources used by the stream.
    /// </summary>
    /// <param name="disposing">
    /// True to dispose both managed and unmanaged resources.
    /// False to only dispose unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing) {
    }

    /// <summary>
    /// When overridden in a derived class, reads text content from a stream.
    /// </summary>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>A task that results in the text content read from the stream.</returns>
    public abstract Task<string> Read(CancellationToken token);

    /// <summary>
    /// When overridden in a derived class, writes text content to a stream.
    /// </summary>
    /// <param name="value">The text to be written to the stream.</param>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>A task that completes when the text content has been written to the stream.</returns>
    public abstract Task Write(string value, CancellationToken token);

    /// <summary>
    /// Disposes of managed and unmanaged resources used by the stream.
    /// </summary>
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~ClientStream() {
        Dispose(false);
    }
}
