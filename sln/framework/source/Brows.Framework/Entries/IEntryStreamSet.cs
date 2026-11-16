using System.Collections.Generic;

namespace Brows.Entries;

public interface IEntryStreamSet {
    IEntryStreamReady StreamSourceReady();
    IEnumerable<string> FileSource();
    IAsyncEnumerable<IEntryStreamSource> StreamSource(CancellationToken token);

    /// <summary>
    /// Gets the maximum number of stream sources in this set that a consumer may copy concurrently.
    /// The default value is 1, which copies stream sources one at a time.
    /// </summary>
    int StreamSourceConcurrency => 1;
}
