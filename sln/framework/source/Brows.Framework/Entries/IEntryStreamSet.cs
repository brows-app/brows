using System.Collections.Generic;

namespace Brows.Entries;

/// <summary>
/// Supplies related files and stream sources with a shared preparation lifetime.
/// </summary>
public interface IEntryStreamSet {
    /// <summary>
    /// Prepares resources shared by this set of stream sources.
    /// </summary>
    /// <returns>
    /// An object to dispose after all stream sources have been processed.
    /// </returns>
    IEntryStreamReady StreamSourceReady();

    /// <summary>
    /// Gets file-system paths that can be used directly as transfer sources.
    /// </summary>
    /// <returns>
    /// The source paths, or an empty sequence when the content is available only through streams.
    /// </returns>
    IEnumerable<string> FileSource();

    /// <summary>
    /// Enumerates the stream sources in this set.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// The stream sources to prepare and read.
    /// </returns>
    IAsyncEnumerable<IEntryStreamSource> StreamSource(CancellationToken token);

    /// <summary>
    /// Gets the maximum number of stream sources in this set that a consumer may copy concurrently.
    /// The default value is 1, which copies stream sources one at a time.
    /// </summary>
    int StreamSourceConcurrency => 1;
}
