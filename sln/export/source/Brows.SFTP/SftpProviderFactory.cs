using Brows.Exports;
using Domore.Logs;
using Domore.Threading.Tasks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class SftpProviderFactory : ProviderFactory<Provider> {
    private static readonly ILog Log = Logging.For(typeof(SftpProviderFactory));
    private static readonly Brows.SFTP.Native.SftpNative Native = new();

    private readonly SftpClientCache ClientCache = new();

    private TaskCache<object> IconCache => field ??= new(async token => {
        object icon = null;
        var task = ProviderIcon?.Work(set: value => icon = value, token);
        if (task is null || !await task) {
            return null;
        }
        return icon;
    });

    protected sealed override async Task<Provider> CreateFor(string id, IPanel panel, CancellationToken token) {
        if (id is null || !id.StartsWith("sftp:", StringComparison.OrdinalIgnoreCase)) {
            return null;
        }
        if (!Uri.TryCreate(id, UriKind.Absolute, out var uri) || !uri.Scheme.Equals("sftp", StringComparison.OrdinalIgnoreCase)) {
            return null;
        }
        if (uri.Host.Length == 0 || !uri.AbsolutePath.StartsWith("/", StringComparison.Ordinal)) {
            return null;
        }
        if (!await Native.Loaded(token)) {
            return null;
        }
        if (Log.Info()) {
            Log.Info(Log.Join(nameof(CreateFor), id));
        }

        var lease = ClientCache.Acquire(uri);
        try {
            var client = lease.Client;
            await client.Connect(token);
            var icon = await IconCache.Ready(token);
            return client.KnownHostState == SftpKnownHostStatus.Match && client.IsAuthenticated
                ? new SftpProvider(this, uri, icon, lease)
                : new SftpConnectionProvider(this, uri, icon, lease);
        }
        catch {
            lease.Dispose();
            throw;
        }
    }

    internal SftpClientLease Acquire(Uri uri, string knownHostsFile = null) {
        return ClientCache.Acquire(uri, knownHostsFile);
    }

    internal ISftpProviderIcon ProviderIcon { get; set; }
}
