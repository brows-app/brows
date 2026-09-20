using Brows.IPC.Client;
using Domore.Logs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IPC.TxRx;

/// <summary>
/// A proxy for handling transmit and receive operations.
/// </summary>
/// <remarks>
/// Implementations of this class wrap an instance of <see cref="ObjectClient"/>
/// that acts as the agent for TxRx operations.
/// </remarks>
public abstract class TxRxProxy : IDisposable {
    private static readonly ILog Log = Logging.For(typeof(TxRxProxy));

    private readonly Lock ClientLocker = new();
    private readonly List<Exception> ClientErrorList = [];

    private bool ClientInUse;
    private bool ClientDisposed;
    private ObjectClient Client;

    private async Task Use(Func<ObjectClient, CancellationToken, Task> function, CancellationToken token) {
        if (function is null) {
            throw new ArgumentNullException(nameof(function));
        }
        lock (ClientLocker) {
            if (ClientInUse) {
                /*
                 * Protect against reentrancy.
                 */
                throw new InvalidOperationException("Client is already in use.");
            }
            if (Client is null) {
                if (ClientDisposed) {
                    /*
                     * Don't create a client if Dispose has already been called.
                     */
                    throw new ObjectDisposedException(GetType().Name);
                }
                Client = CreateClient();
            }
            ClientInUse = true;
        }
        try {
            var task = function(Client, token);
            if (task is not null) {
                await task;
            }
        }
        finally {
            ClientInUse = false;
        }
    }

    internal async Task TxRx(object obj, TxRxInfo info, CancellationToken token) {
        try {
            await Use(token: token, function: async (client, token) => {
                await client.Call(
                    obj: obj,
                    token: token,
                    onSerialized: info?.OnSerialized,
                    onDeserializing: info?.OnDeserializing);
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
            /*
             * This block doesn't really need to be here, but it's useful
             * for debugging.
             */
            throw;
        }
        catch (/*ClientStreamException*/ Exception ex) {
            if (ex is not ClientStreamException) {
                if (Log.Error()) {
                    Log.Error(ex);
                }
            }
            var onErrorTask = info?.OnError?.Invoke(ex, token);
            if (onErrorTask is not null) {
                await onErrorTask;
            }
            lock (ClientLocker) {
                /*
                 * This is a well-known exception thrown by the client, so we handle it
                 * by requesting a fresh "connection", i.e. a new client.
                 * 
                 * Reset the client by disposing it and removing its reference
                 * from the proxy. The next time it's needed, it'll be recreated
                 * by the descendant type, which may or may not resolve whatever
                 * issue caused this error.
                 */
                var
                client = Client;
                Client = null;
                client?.Dispose();
                ClientErrorList.Add(ex);
            }
            OnClientError(EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets the instance key. The value should be unique among instances in a collection.
    /// </summary>
    protected internal string Key { get; }

    /// <summary>
    /// Initializes the instance.
    /// </summary>
    /// <param name="key">
    /// The instance key. The value should be unique among instances in a collection.
    /// </param>
    protected TxRxProxy(string key) {
        Key = key;
    }

    /// <summary>
    /// When overridden in a derived class, creates the instance of <see cref="ObjectClient"/>
    /// for the proxy.
    /// </summary>
    /// <returns>The instance of <see cref="ObjectClient"/> used by the proxy.</returns>
    protected abstract ObjectClient CreateClient();

    /// <summary>
    /// Releases the resources used by the current instance of the class.
    /// </summary>
    /// <param name="disposing">
    /// A value indicating whether to release both managed and unmanaged resources (<see langword="true"/>), 
    /// or only unmanaged resources (<see langword="false"/>).
    /// </param>
    protected virtual void Dispose(bool disposing) {
        if (disposing) {
            lock (ClientLocker) {
                ClientDisposed = true;
                Client?.Dispose();
            }
        }
    }

    /// <summary>
    /// Raises the <see cref="ClientError"/> event.
    /// </summary>
    /// <param name="e">An <see cref="EventArgs"/> instance containing the event data.</param>
    protected virtual void OnClientError(EventArgs e) {
        ClientError?.Invoke(this, e);
    }

    /// <summary>
    /// Event that is raised when an error is encountered by the client.
    /// </summary>
    public event EventHandler ClientError;

    /// <summary>
    /// Gets a read-only collection of exceptions that represent client errors encountered during operations.
    /// </summary>
    /// <remarks>
    /// This property is thread-safe and provides a snapshot of the current client error state. 
    /// Modifications to the underlying error list are synchronized to ensure consistency.
    /// </remarks>
    public IReadOnlyList<Exception> ClientErrors {
        get {
            lock (ClientLocker) {
                return [.. ClientErrorList];
            }
        }
    }

    /// <summary>
    /// Returns a string representation of the current object.
    /// </summary>
    /// <returns>The value of the <see cref="Key"/> property.</returns>
    public sealed override string ToString() {
        return Key;
    }

    /// <summary>
    /// Releases all resources used by the current instance of the class.
    /// </summary>
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~TxRxProxy() {
        Dispose(false);
    }
}
