using Brows.Composition;

namespace Brows.Providers;

/// <summary>
/// Identifies a service resolved for a particular provider.
/// </summary>
public interface IProviderExport : IExport {
}

/// <summary>
/// Identifies a service associated with a specific provider type.
/// </summary>
/// <typeparam name="TProvider">
/// The provider type with which the service is associated.
/// </typeparam>
public interface IProviderExport<TProvider> : IProviderExport {
}
