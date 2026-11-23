using Brows.Providers;

namespace Brows.Composition;

/// <summary>
/// Copies supplied files or stream sources to a provider.
/// </summary>
public interface ICopyProvidedIO : IWorkProvidedIO, IProviderExport {
}
