namespace Brows {
    public interface IProviderExport : IExport {
    }

    public interface IProviderExport<TProvider> : IExport<TProvider> where TProvider : IProvider {
    }
}
