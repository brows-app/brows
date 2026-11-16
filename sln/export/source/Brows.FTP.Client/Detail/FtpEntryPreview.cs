using Brows.Providers;

namespace Brows.Detail;

internal sealed class FtpEntryPreview : ProviderDetail {
    protected override ProviderDetail Clone(Provider provider) {
        return provider is FtpProviderBase ftpProvider
            ? new FtpEntryPreview(ftpProvider)
            : null;
    }

    public FtpEntryPreview(FtpProviderBase provider) : base(provider) {
    }
}
