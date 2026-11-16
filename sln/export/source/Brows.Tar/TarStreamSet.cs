using System.Collections.Generic;

namespace Brows;

internal sealed class TarStreamSet : EntryStreamSet {
    protected override IEnumerable<IEntryStreamSource> StreamSource() {
        return Collection;
    }

    public IEnumerable<IEntryStreamSource> Collection { get; }

    public TarStreamSet(IEnumerable<IEntryStreamSource> collection) {
        Collection = collection;
    }
}
