using System;

namespace Brows.Providers;

/// <summary>
/// Represents a supplementary view owned by a provider.
/// </summary>
public abstract class ProviderDetail : IProviderFocus {
    /// <summary>
    /// Creates an equivalent detail for another provider.
    /// </summary>
    /// <param name="provider">
    /// The provider that will own the copy.
    /// </param>
    /// <returns>
    /// The detail owned by the destination provider, or null when the detail should not carry over.
    /// </returns>
    protected abstract ProviderDetail Clone(Provider provider);

    /// <summary>
    /// Initializes a supplementary view for a provider.
    /// </summary>
    /// <param name="provider">
    /// The provider that owns the detail.
    /// </param>
    protected ProviderDetail(Provider provider) {
        Provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    internal ProviderDetail For(Provider provider) {
        return Clone(provider);
    }

    /// <summary>
    /// Gets the provider that owns this detail.
    /// </summary>
    public Provider Provider { get; }

    bool IProviderFocus.Set() {
        return this is IProviderDetailFocus focus && focus.RequestFocus();
    }

    bool IProviderFocus.Get() {
        return this is IProviderDetailFocus focus && focus.HasKeyboardFocus;
    }
}