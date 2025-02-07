using Domore.Logs;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public abstract class FileProtocolProviderFactory<TProvider> : ProviderFactory<TProvider> where TProvider : Provider {
        private static readonly ILog Log = Logging.For(typeof(FileProtocolProviderFactory<TProvider>));

        protected string Scheme { get; }

        protected FileProtocolProviderFactory(string scheme) {
            Scheme = scheme;
        }

        protected abstract Task<TProvider> CreateFor(Uri uri, IPanel panel, CancellationToken token);

        protected sealed override async Task<TProvider> CreateFor(string id, IPanel panel, CancellationToken token) {
            if (Log.Info()) {
                Log.Info(Log.Join(nameof(CreateFor), id));
            }
            Uri getUri() {
                if (id == null) {
                    return null;
                }
                if (id.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase) == false) {
                    return null;
                }
                var uriCreated = Uri.TryCreate(id, UriKind.Absolute, out var uri);
                if (uriCreated == false) {
                    return null;
                }
                var scheme = uri.Scheme;
                if (scheme != Scheme) {
                    return null;
                }
                var path = uri.AbsolutePath;
                if (Path.IsPathRooted(path) == false) {
                    return null;
                }
                return uri;
            }
            var uri = await Task.Run(getUri, token).ConfigureAwait(false);
            if (uri == null) {
                return null;
            }
            var create = CreateFor(uri, panel, token);
            if (create == null) {
                return null;
            }
            return await create.ConfigureAwait(false);
        }
    }
}
