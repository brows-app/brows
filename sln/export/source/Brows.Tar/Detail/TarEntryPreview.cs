using Brows.Providers;

namespace Brows.Detail;

internal sealed class TarEntryPreview : ProviderDetail {
    protected override ProviderDetail Clone(Provider provider) {
        return provider is TarProvider p
            ? new TarEntryPreview(p)
            : null;
    }

    public TarEntryPreview(TarProvider provider) : base(provider) {
    }
}
