using System;
using System.Collections.Generic;
using System.Threading;

namespace Brows.SCP {
    internal sealed class SCPStreamSet : IEntryStreamSet {
        public IEntryStreamReady StreamSourceReady() {
            return null;
        }

        public IEnumerable<string> FileSource() {
            return Array.Empty<string>();
        }

        public IAsyncEnumerable<IEntryStreamSource> StreamSource(CancellationToken token) {
            return Collection;
        }

        public IAsyncEnumerable<SCPStreamSource> Collection { get; }

        public SCPStreamSet(IAsyncEnumerable<SCPStreamSource> collection) {
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }
    }
}
