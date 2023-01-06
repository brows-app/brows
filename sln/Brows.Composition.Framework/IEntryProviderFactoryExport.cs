using System.ComponentModel.Composition;

namespace Brows {
    [InheritedExport(typeof(IEntryProviderFactory))]
    public interface IEntryProviderFactoryExport : IEntryProviderFactory {
    }
}
