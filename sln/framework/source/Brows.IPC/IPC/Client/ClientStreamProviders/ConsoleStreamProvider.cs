using Brows.IPC.Client.ClientStreams;
using Domore.Logs;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IPC.Client.ClientStreamProviders;

internal sealed class ConsoleStreamProvider : ClientStreamProvider {
    private static readonly ILog Log = Logging.For(typeof(ConsoleStreamProvider));

    private readonly Lock Locker = new();
    private ConsoleStream ConsoleStream;

    protected sealed override void Dispose(bool disposing) {
        if (disposing) {
            try {
                ConsoleStream?.Dispose();
            }
            catch (Exception ex) {
                Log.Warn(ex);
            }
        }
        base.Dispose(disposing);
    }

    protected sealed override Task<ClientStream> GetStream(CancellationToken token) {
        if (ConsoleStream is null) {
            lock (Locker) {
                if (ConsoleStream is null) {
                    var consoleStream = new ConsoleStream();
                    Thread.MemoryBarrier(); // Required for double-checked locking
                    ConsoleStream = consoleStream;
                }
            }
        }
        return token.IsCancellationRequested
            ? Task.FromCanceled<ClientStream>(token)
            : Task.FromResult<ClientStream>(ConsoleStream);
    }
}
