using System.Collections.Generic;

namespace Brows;

internal sealed class TarEntryInfoCollection {
    internal IReadOnlyList<TarEntryInfo> Items { get; }

    internal TarEntryInfoCollection(IEnumerable<TarEntryInfo> items) {
        Items = new List<TarEntryInfo>(items);
    }
}
