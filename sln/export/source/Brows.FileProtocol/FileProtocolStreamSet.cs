using System.Collections.Generic;

namespace Brows;

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
    public int StreamSourceConcurrency { get; }

    public FileProtocolStreamSet(IAsyncEnumerable<FileProtocolStreamSource<TEntry>> collection,
                                 int streamSourceConcurrency = 1) {
        Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        StreamSourceConcurrency = streamSourceConcurrency;
    }
}
