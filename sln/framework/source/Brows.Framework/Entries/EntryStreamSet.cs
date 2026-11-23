using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Brows.Entries;

/// <summary>
/// Provides a base for a related set of file paths and readable stream sources.
/// </summary>
public abstract class EntryStreamSet : IEntryStreamSet {
    /// <summary>
    /// Supplies the stream sources in this set.
    /// </summary>
    /// <returns>
    /// The stream sources to prepare and read.
    /// </returns>
    protected abstract IEnumerable<IEntryStreamSource> StreamSource();

    /// <summary>
    /// Gets file-system paths that can be used directly as transfer sources.
    /// </summary>
    /// <returns>
    /// The source paths, or an empty sequence when the content is available only through streams.
    /// </returns>
    protected virtual IEnumerable<string> FileSource() {
        return Array.Empty<string>();
    }

    /// <summary>
    /// Prepares resources shared by this set of stream sources.
    /// </summary>
    /// <returns>
    /// An object to dispose after all stream sources have been processed.
    /// </returns>
    protected virtual IEntryStreamReady StreamSourceReady() {
        return new EntryStreamReady();
    }

    IEntryStreamReady IEntryStreamSet.StreamSourceReady() => StreamSourceReady();
    IEnumerable<string> IEntryStreamSet.FileSource() => FileSource();

    async IAsyncEnumerable<IEntryStreamSource>
    IEntryStreamSet.StreamSource([EnumeratorCancellation] CancellationToken token) {
        var streamSource = StreamSource();
        if (streamSource == null) {
            yield break;
        }
        foreach (var source in streamSource) {
            if (token.IsCancellationRequested) {
                token.ThrowIfCancellationRequested();
            }
            yield return source;
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Creates a stream set for file-system paths.
    /// </summary>
    /// <param name="paths">
    /// The file paths to read.
    /// </param>
    /// <returns>
    /// A set that supplies the paths and streams for the files.
    /// </returns>
    public static IEntryStreamSet FromFiles(IEnumerable<string> paths) {
        return new FileStreamSet(paths);
    }
}
