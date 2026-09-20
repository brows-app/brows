using System;
using System.Collections.Generic;
using System.Linq;

namespace Brows.Composition.ImportCollections;

internal sealed class ComposedImportInfo : ImportInfo {
    internal sealed override ImportCollectionFactory CollectionFactory() =>
        new ComposedImportCollectionFactory(this);

    internal static ComposedImportInfo Create(Func<ImportVariables> variables = null,
                                              Func<Dictionary<string, IEnumerable<string>>> path = null,
                                              Func<IEnumerable<IExport>> inject = null) {
        return new() {
            Inject = [.. (inject?.Invoke() ?? [])],
            Path = path?.Invoke()?.ToDictionary(
                item => item.Key,
                item => (item.Value?.ToList() ?? []) as IReadOnlyList<string>),
            Variables = variables?.Invoke()
        };
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Path {
        get => field ??= new Dictionary<string, IReadOnlyList<string>>();
        private init => field = value;
    }

    public IReadOnlyList<IExport> Inject {
        get => field ??= [];
        private init => field = value;
    }
}
