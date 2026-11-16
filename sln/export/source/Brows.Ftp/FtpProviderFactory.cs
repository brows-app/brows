using Brows.Url;
using Brows.Exports;
using Domore.Threading.Tasks;
using System;
using System.Threading.Tasks;

namespace Brows;

internal sealed class FtpProviderFactory : FileProtocolProviderFactory<FtpProvider>, IFtpProviderFactory {
    private TaskCache<object> IconCache => field ??= new(async token => {
        var icon = default(object);
        var task = IconService?.Work(set: value => icon = value, token);
        if (task is null) {
            return null;
        }
        var work = await task;
        return work ? icon : null;
    });

    protected sealed override async Task<FtpProvider> CreateFor(Uri uri, IPanel panel, CancellationToken token) {
        var nativeLoaded = await UrlNative.Load(token);
        if (nativeLoaded is false) {
            return null;
        }
        return new FtpProvider(this, uri, icon: await IconCache.Ready(token));
    }

    public IFtpProviderIcon IconService { get; set; }

    public FtpClientCache Client { get; } = new();

    public FtpProviderFactory() : base("ftp") {
    }
}
