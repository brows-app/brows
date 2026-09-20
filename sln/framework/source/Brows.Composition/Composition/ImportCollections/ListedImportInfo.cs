using System;
using System.Collections.Generic;

namespace Brows.Composition.ImportCollections;

internal sealed class ListedImportInfo : ImportInfo {
    internal sealed override ImportCollectionFactory CollectionFactory() =>
        new ListedImportCollectionFactory(this);

    internal static ListedImportInfo Create(Func<ImportVariables> variables = null,
                                          Func<IEnumerable<IExport>> list = null) {
        return new() {
            List = [.. (list?.Invoke() ?? [])],
            Variables = variables?.Invoke()
        };
    }

    public IReadOnlyList<IExport> List {
        get => field ??= [];
        internal init;
    }
}
