using Brows.Exports;
using Domore.Logs;
using Domore.Threading.Tasks;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class SSHProviderFactory : ProviderFactory<SSHProvider> {
        private static readonly ILog Log = Logging.For(typeof(SSHProviderFactory));

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

        protected sealed override async Task<SSHProvider> CreateFor(string id, IPanel panel, CancellationToken token) {
            if (Log.Info()) {
                Log.Info(Log.Join(nameof(CreateFor), id));
            }
            if (id == null) {
                return null;
            }
            var part = id.Split(new[] { '>' }, count: 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (part.Length < 1) {
                return null;
            }
            var created = Uri.TryCreate(part[0], UriKind.Absolute, out var uri);
            if (created == false) {
                return null;
            }
            var scheme = uri.Scheme;
            if (scheme != "ssh") {
                return null;
            }
            var path = part.Length > 1 ? part[1] : "/";
            if (Path.IsPathRooted(path) == false) {
                return null;
            }
            return new SSHProvider(
                factory: this,
                uri: uri,
                path: path,
                icon: await IconCache.Ready(token),
                password: null);
        }

        public ISSHProviderIcon ProviderIcon { get; set; }
    }
}
