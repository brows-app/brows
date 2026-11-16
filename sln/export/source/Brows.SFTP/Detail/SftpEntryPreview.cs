using Brows.Providers;

namespace Brows.Detail;

internal sealed class SftpEntryPreview : ProviderDetail {
    protected override ProviderDetail Clone(Provider provider) {
        return provider is SftpProvider sftpProvider
            ? new SftpEntryPreview(sftpProvider)
            : null;
    }

    public SftpEntryPreview(SftpProvider provider) : base(provider) {
    }
}
