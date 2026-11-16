using System;

namespace Brows.Providers;

public abstract class ProviderDetail : IProviderFocus {
    protected abstract ProviderDetail Clone(Provider provider);

    protected ProviderDetail(Provider provider) {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    internal ProviderDetail For(Provider provider) {
        return Clone(provider);
    }

    public Provider Provider { get; }

    bool IProviderFocus.Set() {
        return this is IProviderDetailFocus focus && focus.RequestFocus();
    }

    bool IProviderFocus.Get() {
        return this is IProviderDetailFocus focus && focus.HasKeyboardFocus;
    }
}