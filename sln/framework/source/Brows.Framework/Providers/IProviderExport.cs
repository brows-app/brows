using Brows.Composition;

namespace Brows.Providers;

public interface IProviderExport : IExport {
}

public interface IProviderExport<TProvider> : IProviderExport {
}
