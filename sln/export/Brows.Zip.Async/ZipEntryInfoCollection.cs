using System.Collections.Generic;

namespace Brows {
    public sealed class ZipEntryInfoCollection {
        public IEnumerable<ZipEntryInfo> Items { get; }

        public ZipEntryInfoCollection(IEnumerable<ZipEntryInfo> items) {
            Items = new List<ZipEntryInfo>(items);
        }
    }
}
