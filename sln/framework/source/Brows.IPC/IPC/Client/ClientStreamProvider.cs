using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IPC.Client;

/// <summary>
/// Base class for types that provide instances of <see cref="ClientStream"/>.
/// </summary>
internal abstract class ClientStreamProvider : IDisposable {
    private readonly Lock StreamLocker = new();

    private ClientStream Stream;
    private Task<ClientStream> StreamTask;

    private void ThrowIfDisposed(Exception innerException) {
        if (Disposed) {
            throw new ObjectDisposedException($"Object disposed: {GetType()?.Name}", innerException);
        }
    }

    private async Task<ClientStream> Ready(CancellationToken token) {
        ThrowIfDisposed(
            innerException: null);
        if (StreamTask is null) {
            lock (StreamLocker) {
                if (StreamTask is null) {
                    var streamTask = GetStream(token);
                    Thread.MemoryBarrier(); // Required for double-checked locking
                    StreamTask = streamTask;
                }
            }
        }
        if (Stream is null) {
            try {
                Stream = await StreamTask;
            }
            catch (Exception ex) {
                ThrowIfDisposed(ex);
                throw new ClientStreamException(this, ex);
            }
        }
        var stream = Stream;
        if (stream is null) {
            throw new InvalidOperationException("Provided stream is null.");
        }
        return stream;
    }

    /// <summary>
    /// When overridden in a derived class, provides an instance of <see cref="ClientStream"/>.
    /// </summary>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>A task that results in the instance of <see cref="ClientStream"/> provided.</returns>
    protected abstract Task<ClientStream> GetStream(CancellationToken token);

    protected virtual void Dispose(bool disposing) {
    }

    public bool Disposed { get; private set; }

    public async Task<T> Use<T>(Func<ClientStream, CancellationToken, Task<T>> function, CancellationToken token) {
        if (function is null) {
            throw new ArgumentNullException(nameof(function));
        }
        var stream = await Ready(token);
        var task = function(stream, token);
        if (task is null) {
            return default;
        }
        try {
            return await task;
        }
        catch (Exception ex) {
            /*
             * If the provider was disposed while the stream operation was
             * in flight, the resulting exception is likely a side-effect
             * of the disposal (e.g. the stream was closed underneath us).
             * Throw ObjectDisposedException instead so callers can
             * distinguish disposal from a genuine stream error.
             */
            ThrowIfDisposed(ex);
            throw;
        }
    }

    public void Dispose() {
        Disposed = true;
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~ClientStreamProvider() {
        Dispose(false);
    }
}
