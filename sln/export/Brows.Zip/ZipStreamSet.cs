using System.Collections.Generic;

namespace Brows {
    internal sealed class ZipStreamSet : EntryStreamSet {
        protected override IEnumerable<IEntryStreamSource> StreamSource() {
            return Collection;
        }

        public IEnumerable<IEntryStreamSource> Collection { get; }

        public ZipStreamSet(IEnumerable<IEntryStreamSource> collection) {
            Collection = collection;
        }
    }
}
