using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition.ImportCollections;

internal sealed class ListedImportCollectionFactory : ImportCollectionFactory {
    public ListedImportInfo Info { get; }

    public ListedImportCollectionFactory(ListedImportInfo info) {
        Info = info ?? throw new ArgumentNullException(nameof(info));
    }

    public override Task<ImportCollection> Create(CancellationToken token) {
        return token.IsCancellationRequested
            ? Task.FromCanceled<ImportCollection>(token)
            : Task.FromResult(new ImportCollection(Info.List));
    }
}
