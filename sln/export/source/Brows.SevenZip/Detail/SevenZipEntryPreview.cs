using Brows.Providers;

namespace Brows.Detail;

internal sealed class SevenZipEntryPreview : ProviderDetail {
    protected override ProviderDetail Clone(Provider provider) {
        return provider is SevenZipProvider p
            ? new SevenZipEntryPreview(p)
            : null;
    }

    public SevenZipEntryPreview(SevenZipProvider provider) : base(provider) {
    }
}
