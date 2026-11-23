using System.IO;
using System.Threading.Tasks;

namespace Brows.Entries;

/// <summary>
/// Provides entry identity and a preparation hook for readable stream content.
/// </summary>
public abstract class EntryStreamSource : IEntryStreamSource {
    /// <summary>
    /// Gets or sets the stream available after preparation. Null may represent content without a readable stream.
    /// </summary>
    protected Stream Stream { get; set; }

    /// <summary>
    /// Gets the entry supplying the content.
    /// </summary>
    protected IEntry Entry { get; }

    /// <summary>
    /// Initializes a stream source for an entry.
    /// </summary>
    /// <param name="entry">
    /// The entry supplying the content.
    /// </param>
    protected EntryStreamSource(IEntry entry) {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
    }

    /// <summary>
    /// Prepares the source for reading through its stream.
    /// </summary>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// An object to dispose after reading. The stream must also be disposed when the consumer finishes.
    /// </returns>
    protected virtual Task<IEntryStreamReady> StreamReady(CancellationToken token) {
        return Task.FromResult<IEntryStreamReady>(new EntryStreamReady());
    }

    /// <summary>
    /// Gets the identifier of the entry supplying the content.
    /// </summary>
    public string EntryID => Entry.ID;

    /// <summary>
    /// Gets the name of the entry supplying the content.
    /// </summary>
    public string EntryName => Entry.Name;

    /// <summary>
    /// Gets the advertised stream length in bytes, or a negative value when the length is unknown.
    /// </summary>
    public abstract long StreamLength { get; }

    /// <summary>
    /// Gets the path to use for this content relative to its transfer destination.
    /// </summary>
    public abstract string RelativePath { get; }

    /// <summary>
    /// Gets the source file-system path, or null when the content has no direct file source.
    /// </summary>
    public virtual string SourceFile => null;

    /// <summary>
    /// Gets the directory path represented by this source, or null when no directory is supplied.
    /// </summary>
    public virtual string SourceDirectory => null;

    Stream IEntryStreamSource.Stream =>
        Stream;

    Task<IEntryStreamReady> IEntryStreamSource.StreamReady(CancellationToken token) {
        return StreamReady(token);
    }
}

/// <summary>
/// Provides a stream source with access to a specific entry type.
/// </summary>
/// <typeparam name="TEntry">
/// The entry type supplying the content.
/// </typeparam>
public abstract class EntryStreamSource<TEntry> : EntryStreamSource where TEntry : IEntry {
    /// <summary>
    /// Gets the entry supplying the content.
    /// </summary>
    protected new TEntry Entry { get; }

    /// <summary>
    /// Initializes a stream source for an entry.
    /// </summary>
    /// <param name="entry">
    /// The entry supplying the content.
    /// </param>
    protected EntryStreamSource(TEntry entry) : base(entry) {
        Entry = entry;
    }
}
