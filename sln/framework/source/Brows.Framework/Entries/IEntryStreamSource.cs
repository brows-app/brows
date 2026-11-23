using System.IO;
using System.Threading.Tasks;

namespace Brows.Entries;

/// <summary>
/// Describes entry content that can be opened as a stream for reading or transfer.
/// </summary>
public interface IEntryStreamSource {
    /// <summary>
    /// Gets the identifier of the entry supplying the content.
    /// </summary>
    string EntryID { get; }

    /// <summary>
    /// Gets the name of the entry supplying the content.
    /// </summary>
    string EntryName { get; }

    /// <summary>
    /// Gets the path to use for this content relative to its transfer destination.
    /// </summary>
    string RelativePath { get; }

    /// <summary>
    /// Gets the source file-system path, or null when the content has no direct file source.
    /// </summary>
    string SourceFile { get; }

    /// <summary>
    /// Gets the directory path represented by this source, or null when no directory is supplied.
    /// </summary>
    string SourceDirectory { get; }

    /// <summary>
    /// Gets the advertised stream length, or a negative value when the length is unknown.
    /// </summary>
    long StreamLength { get; }

    /// <summary>
    /// Gets the stream after StreamReady completes, or null for a source without readable content.
    /// The consumer must dispose the stream after reading.
    /// </summary>
    Stream Stream { get; }

    /// <summary>
    /// Prepares the source for reading through its stream.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// An object to dispose after reading. The stream must also be disposed when the consumer finishes.
    /// </returns>
    Task<IEntryStreamReady> StreamReady(CancellationToken token);
}
