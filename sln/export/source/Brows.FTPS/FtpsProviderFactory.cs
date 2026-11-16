using Brows.Exports;
using Brows.Url;
using Brows.Providers;
using Domore.Threading.Tasks;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows;

internal sealed class FtpsProviderFactory : ProviderFactory<FtpsProvider>, IFtpProviderFactory {
    private TaskCache<object> IconCache => field ??= new(async token => {
        var icon = default(object);
        var task = IconService?.Work(set: value => icon = value, token);
        if (task is null) {
            return null;
        }
        var work = await task;
        return work ? icon : null;
    });

    protected sealed override async Task<FtpsProvider> CreateFor(string id, IPanel panel, CancellationToken token) {
        if (!Uri.TryCreate(id, UriKind.Absolute, out var uri)) {
            return null;
        }
        var isFtps = uri.Scheme.Equals("ftps", StringComparison.OrdinalIgnoreCase);
        var isExplicitFtps = uri.Scheme.Equals("ftpes", StringComparison.OrdinalIgnoreCase);
        if (!isFtps && !isExplicitFtps) {
            return null;
        }
        if (!Path.IsPathRooted(uri.AbsolutePath)) {
            return null;
        }
        var nativeLoaded = await UrlNative.Load(token);
        if (nativeLoaded is false) {
            return null;
        }
        return new FtpsProvider(this, uri, icon: await IconCache.Ready(token));
    }

    public IFtpProviderIcon IconService { get; set; }

    public FtpClientCache Client { get; } = new();

    public FtpsProviderFactory() {
    }
}
