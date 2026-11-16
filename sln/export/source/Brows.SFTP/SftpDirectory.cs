using Brows.SFTP.Native;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class SftpDirectory : IDisposable, IAsyncDisposable {
    private readonly SftpClient Client;
    private int Disposed;

    internal SftpDir Agent { get; }

    internal SftpDirectory(SftpClient client, SftpDir agent) {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        Agent = agent ?? throw new ArgumentNullException(nameof(agent));
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref Disposed, 1) != 0) {
            return;
        }
        Client.Close(Agent).GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref Disposed, 1) != 0) {
            return;
        }
        await Client.Close(Agent).ConfigureAwait(false);
    }
}
