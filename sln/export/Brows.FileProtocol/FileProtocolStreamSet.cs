using System;
using System.Collections.Generic;
using System.Threading;

namespace Brows {
    internal sealed class FileProtocolStreamSet<TEntry> : IEntryStreamSet where TEntry : FileProtocolEntry {
        public IEntryStreamReady StreamSourceReady() {
            return null;
        }

        public IEnumerable<string> FileSource() {
            return Array.Empty<string>();
        }

        public IAsyncEnumerable<IEntryStreamSource> StreamSource(CancellationToken token) {
            return Collection;
        }

        public IAsyncEnumerable<FileProtocolStreamSource<TEntry>> Collection { get; }

        public FileProtocolStreamSet(IAsyncEnumerable<FileProtocolStreamSource<TEntry>> collection) {
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }
    }
}
