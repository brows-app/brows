namespace Brows {
    public interface IEntryProviderExport : IExport {
    }

    public interface IEntryProviderExport<TProvider> : IExport<TProvider> where TProvider : IEntryProvider {
    }
}
