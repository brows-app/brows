using Brows.Exports;
using Brows.Url.Ftp;
using Domore.Threading.Tasks;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class FtpProviderFactory : FileProtocolProviderFactory<FtpProvider> {
        private TaskCache<object> IconCache => _IconCache ??= new(async (token) => {
            var icon = default(object);
            var task = IconService?.Work(set: value => icon = value, token);
            if (task == null) {
                return null;
            }
            var work = await task.ConfigureAwait(false);
            if (work == false) {
                return null;
            }
            return icon;
        });
        private TaskCache<object> _IconCache;

        protected sealed override async Task<FtpProvider> CreateFor(Uri uri, IPanel panel, CancellationToken token) {
#if DEBUG
            var nativeLoaded = await UrlNative.Load(token).ConfigureAwait(false);
            if (nativeLoaded == false) {
                return null;
            }
            return new FtpProvider(this, uri, icon: await IconCache.Ready(token).ConfigureAwait(false));
#else
            return null;
#endif
        }

        public IFtpProviderIcon IconService { get; set; }

        public FtpClientCache Client => _Client ??= new();
        private FtpClientCache _Client;

        public FtpProviderFactory() : base("ftp") {
        }
    }
}
