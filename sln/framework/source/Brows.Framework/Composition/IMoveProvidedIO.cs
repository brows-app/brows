using Brows.Providers;

namespace Brows.Composition;

/// <summary>
/// Moves supplied files or stream sources to a provider.
/// </summary>
public interface IMoveProvidedIO : IWorkProvidedIO, IProviderExport {
}
