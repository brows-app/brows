using Domore.Logs;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Brows.IPC;

/// <summary>
/// Represents a host that facilitates object-based communication over TCP connections.
/// </summary>
/// <remarks>
/// The <see cref="ObjectCallHost"/> class is responsible for managing TCP connections,  reading and
/// writing objects using an <see cref="IObjectCallDelegate"/>, and coordinating  the lifecycle of the host process. It
/// supports cancellation via a <see cref="CancellationToken"/>  and provides hooks for custom behavior during the start
/// and finish of the host process.
/// </remarks>
public sealed class ObjectCallHost {
    private static readonly ILog Log = Logging.For(typeof(ObjectCallHost));

    private static async Task RxTx(ObjectClient client,
                                   ObjectCallHostRun run,
                                   IObjectCallDelegate d,
                                   CancellationToken token) {
        if (null == d) throw new ArgumentNullException(nameof(d));
        if (null == client) throw new ArgumentNullException(nameof(client));
        var obj = await client.Read(
            token: token,
            onDeserializing: run?.OnDataRead);
        if (Log.Info()) {
            Log.Info($"Object read: {obj}");
        }
        var process = d.Call(obj, token);
        var result = process is null ? null : await process;
        if (Log.Info()) {
            Log.Info($"Writing obj: {result}");
        }
        await client.Write(
            obj: result,
            token: token,
            onSerialized: run?.OnDataWrite);
    }

    private static async Task Accepted(TcpClient tcpClient,
                                       ObjectCallHostRun run,
                                       IObjectCallDelegate d,
                                       CancellationToken token) {
        if (null == d) throw new ArgumentNullException(nameof(d));
        if (null == tcpClient) throw new ArgumentNullException(nameof(tcpClient));
        if (Log.Info()) {
            Log.Info(
                $"TCP client accepted",
                $" Local end point: {tcpClient.Client?.LocalEndPoint}",
                $"Remote end point: {tcpClient.Client?.RemoteEndPoint}");
        }
        using (tcpClient) {
            /*
             * Keep-alive must be set before attempting to use the client.
             */
            tcpClient.Client.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.KeepAlive,
                run?.SocketKeepAlive ?? true);
            var objClient = ObjectClient.From(tcpClient.GetStream(), d.TypeResolver);
            using (objClient) {
                /*
                 * This loop has no explicit break condition. It terminates
                 * when the connection is severed (IOException), when the
                 * token is cancelled (OperationCanceledException), or
                 * when the remote side disconnects (end-of-stream).
                 */
                for (; ; ) {
                    await RxTx(objClient, run, d, token);
                }
            }
        }
    }

    private static async Task Listen(TcpListener listener,
                                     ObjectCallHostRun info,
                                     IObjectCallDelegate d,
                                     CancellationToken token) {
        if (null == listener) throw new ArgumentNullException(nameof(listener));
        if (Log.Info()) {
            Log.Info("Starting TCP listener...");
        }
        listener.Start();
        var state = new ObjectCallHostState((IPEndPoint)listener.LocalEndpoint);
        using var tokenRegistration = token.Register(() => {
            /*
             * Stop the listener upon cancellation. This is probably unnecessary
             * on .NET 5+, because the AcceptTcpClientAsync method accepts a
             * CancellationToken, unlike the method's signature on .NET Framework.
             */
            if (Log.Info()) {
                Log.Info($"Stopping TCP listener...");
            }
            listener.Stop();
        });
        try {
            /*
             * OnStart is usually used to identify the end point that the
             * listener picked if it was allowed to pick any open port
             * (which is the behavior when passing '0' as the port). So
             * the listener obviously needs to be started before invoking
             * it.
             */
            var startTask = info?.OnStart?.Invoke(state, token);
            if (startTask is not null) {
                await startTask;
            }
            if (Log.Info()) {
                Log.Info(
                    $"TCP listener started",
                    $"Local end point: {state.EndPoint}");
            }
            for (; ; )
            {
                if (token.IsCancellationRequested) {
                    token.ThrowIfCancellationRequested();
                }
                var client = default(TcpClient);
                try {
                    client = await listener.AcceptTcpClientAsync(
#if !NETFRAMEWORK
                        token
#endif
                    );
                }
                catch (ObjectDisposedException) {
                    /*
                     * This is thrown on cancellation in .NET Framework.
                     * The behavior may be different on .NET 5+.
                     */
                    break;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) {
                    /*
                     *  This catch doesn't really need to be here, but it's
                     *  useful for debugging.
                     */
                    throw;
                }
                catch (Exception ex) {
                    if (ex is SocketException) {
                        if (token.IsCancellationRequested) {
                            /*
                             * A socket exception seems to be thrown sometimes
                             * upon cancellation. Again, this behavior may be
                             * different on .NET 5+.
                             */
                            break;
                        }
                    }
                    throw;
                }
                if (client is not null) {
                    /*
                     * Each accepted connection is handled concurrently.
                     * The task is intentionally not awaited so the accept
                     * loop can continue receiving new clients. ContinueWith
                     * captures faults for logging; without it, the exception
                     * would be silently swallowed by the discarded task.
                     */
                    async void accept() {
                        try {
                            await Accepted(client, info, d, token);
                        }
                        catch (OperationCanceledException) when (token.IsCancellationRequested) {
                            if (Log.Debug()) {
                                Log.Debug("Canceled during accept");
                            }
                        }
                        catch (Exception ex) {
                            if (Log.Error()) {
                                Log.Error(ex);
                            }
                        }
                    }
                    accept();
                }
            }
        }
        finally {
            try {
                var onFinish = info?.OnFinish?.Invoke(state, default);
                if (onFinish is not null) {
                    await onFinish;
                }
            }
            catch (Exception ex) {
                /*
                 * There's not much we can do if this callback throws
                 * an exception. We definitely don't want to throw in
                 * a finally block, so...
                 */
                Log.Error(ex);
            }
        }
    }

    /// <summary>
    /// Gets the delegate used by the host.
    /// </summary>
    public IObjectCallDelegate Delegate { get; }

    /// <summary>
    /// Crates a new call host.
    /// </summary>
    /// <param name="delegate">The delegate used by the host.</param>
    public ObjectCallHost(IObjectCallDelegate @delegate) {
        Delegate = @delegate;
    }

    /// <summary>
    /// Runs the host process.
    /// </summary>
    /// <param name="info">Information about the run.</param>
    /// <param name="token">The cancellation token for the task.</param>
    /// <returns>A task that completes when the run is finished.</returns>
    public async Task Run(ObjectCallHostRun info, CancellationToken token) {
        var endPoint = info?.EndPoint ?? new IPEndPoint(address: IPAddress.Loopback, port: 0);
        var listener = new TcpListener(endPoint);
        try {
            await Listen(listener, info, Delegate, token);
        }
        finally {
#if !NETFRAMEWORK
            listener.Dispose();
#endif
        }
    }
}
