using System.Collections.Generic;

namespace Brows;

internal sealed class SevenZipEntryInfoCollection {
    internal IReadOnlyList<SevenZipEntryInfo> Items { get; }

    internal SevenZipEntryInfoCollection(IEnumerable<SevenZipEntryInfo> items) {
        Items = new List<SevenZipEntryInfo>(items);
    }
}
