using System.Collections.Generic;

namespace Brows;

internal sealed class SevenZipStreamSet : EntryStreamSet {
    protected override IEnumerable<IEntryStreamSource> StreamSource() {
        return Collection;
    }

    public IEnumerable<IEntryStreamSource> Collection { get; }

    public SevenZipStreamSet(IEnumerable<IEntryStreamSource> collection) {
        Collection = collection;
    }
}
