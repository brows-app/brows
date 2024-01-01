using System;
using System.Collections.Generic;

namespace Brows.SCP {
    internal sealed class SCPStreamSet : EntryStreamSet {
        protected sealed override IEnumerable<IEntryStreamSource> StreamSource() {
            return Collection;
        }

        public IEnumerable<SCPStreamSource> Collection { get; }

        public SCPStreamSet(IEnumerable<SCPStreamSource> collection) {
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }
    }
}
