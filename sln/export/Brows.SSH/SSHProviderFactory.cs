using Brows.SSHConnection;
using Brows.Exports;
using Brows.SSH;
using Domore.Logs;
using Domore.Threading.Tasks;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class SSHProviderFactory : ProviderFactory<Provider> {
        private static readonly ILog Log = Logging.For(typeof(SSHProviderFactory));
        private static readonly SSHNative Native = new SSHNative();

        private readonly SSHClientCache ClientCache = new();

        private TaskCache<object> IconCache =>
            _IconCache ?? (
            _IconCache = new(async (token) => {
                var icon = default(object);
                var task = ProviderIcon?.Work(set: value => icon = value, token);
                if (task == null) {
                    return null;
                }
                var work = await task;
                if (work == false) {
                    return null;
                }
                return icon;
            }));
        private TaskCache<object> _IconCache;

        protected sealed override async Task<Provider> CreateFor(string id, IPanel panel, CancellationToken token) {
            if (Log.Info()) {
                Log.Info(Log.Join(nameof(CreateFor), id));
            }
            if (id == null) {
                return null;
            }
            if (id.StartsWith("ssh", StringComparison.OrdinalIgnoreCase) == false) {
                return null;
            }
            var uriCreated = Uri.TryCreate(id, UriKind.Absolute, out var uri);
            if (uriCreated == false) {
                return null;
            }
            var scheme = uri.Scheme;
            if (scheme != "ssh") {
                return null;
            }
            var path = uri.AbsolutePath;
            if (Path.IsPathRooted(path) == false) {
                return null;
            }
            var nativeLoaded = await Native.Loaded(token);
            if (nativeLoaded == false) {
                return null;
            }
            var icon = await IconCache.Ready(token);
            var client = await Client(uri, token);
            var connected = client.Connected;
            if (connected) {
                var authenticated = await client.Authenticated(token);
                if (authenticated) {
                    return new SSHProvider(this, uri, icon);
                }
            }
            return new SSHConnectionProvider(this, uri, icon);
        }

        public ISSHProviderIcon ProviderIcon { get; set; }

        public async Task<SSHClient> Client(Uri uri, CancellationToken token) {
            return await ClientCache.GetOrAdd(uri, token);
        }
    }
}
