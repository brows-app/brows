using System;
using System.Threading;

namespace Brows;

internal sealed class SftpClientLease : IDisposable {
    private SftpClientCache Cache { get; }
    internal SftpClient Client { get; }
    private int Disposed;

    internal SftpClientLease(SftpClientCache cache, SftpClient client) {
        Cache = cache ?? throw new ArgumentNullException(nameof(cache));
        Client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref Disposed, 1) == 0) {
            Cache.Release(Client);
        }
    }
}
