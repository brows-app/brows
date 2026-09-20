using Brows.IPC.Client.ClientStreams;
using Domore.Logs;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IPC.Client.ClientStreamProviders;

/// <summary>
/// This implementation of <see cref="ClientStreamProvider"/> uses a
/// TCP client for network communication.
/// </summary>
internal sealed class TcpStreamProvider : ClientStreamProvider {
    private static readonly ILog Log = Logging.For(typeof(TcpStreamProvider));

    private readonly Lock ClientLocker = new();
    private readonly Lock ConnectLocker = new();

    private bool ClientDisposed;
    private TcpClient Client;
    private Task ConnectTask;

    protected sealed override void Dispose(bool disposing) {
        if (disposing) {
            try {
                lock (ClientLocker) {
                    ClientDisposed = true;
                    Client?.Dispose();
                }
            }
            catch (Exception ex) {
                /*
                 * I don't think we'll ever get into this block,
                 * but we definitely don't want to throw exceptions
                 * from Dispose.
                 */
                Log.Warn(ex);
            }
        }
        base.Dispose(disposing);
    }

    protected sealed override async Task<ClientStream> GetStream(CancellationToken token) {
        if (Client is null) {
            lock (ClientLocker) {
                if (ClientDisposed) {
                    throw new ObjectDisposedException(nameof(TcpStreamProvider));
                }
                if (Client is null) {
                    var
                    client = new TcpClient();
                    client.Client.SetSocketOption(
                        SocketOptionLevel.Socket,
                        SocketOptionName.KeepAlive,
                        KeepAlive);
                    Thread.MemoryBarrier(); // Required for double-checked locking
                    Client = client;
                }
            }
        }
        if (ConnectTask is null) {
            lock (ConnectLocker) {
                if (ConnectTask is null) {
                    /*
                     * .NET Framework's ConnectAsync does not accept a
                     * CancellationToken and returns Task. Modern .NET's
                     * ConnectAsync accepts a CancellationToken and
                     * returns ValueTask, which must be converted via
                     * AsTask() for caching in the ConnectTask field.
                     */
                    var connectTask = Client.ConnectAsync(EndPoint.Address, EndPoint.Port
#if !NETFRAMEWORK
                        , token
#endif
                    )
#if !NETFRAMEWORK
                    .AsTask()
#endif
                    ;
                    Thread.MemoryBarrier(); // Required for double-checked locking
                    ConnectTask = connectTask;
                }
            }
            await ConnectTask;
        }
        return new SingleStream(Client.GetStream());
    }

    public bool KeepAlive { get; }
    public IPEndPoint EndPoint { get; }

    public TcpStreamProvider(IPEndPoint endPoint, bool keepAlive = true) {
        EndPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));
        KeepAlive = keepAlive;
    }

    public sealed override string ToString() {
        return EndPoint.ToString();
    }
}
