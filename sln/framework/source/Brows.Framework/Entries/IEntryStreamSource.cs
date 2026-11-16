using System.IO;
using System.Threading.Tasks;

namespace Brows.Entries;

public interface IEntryStreamSource {
    string EntryID { get; }
    string EntryName { get; }
    string RelativePath { get; }
    string SourceFile { get; }
    string SourceDirectory { get; }
    /// <summary>
    /// Gets the advertised stream length, or a negative value when the length is unknown.
    /// </summary>
    long StreamLength { get; }
    Stream Stream { get; }
    Task<IEntryStreamReady> StreamReady(CancellationToken token);
}
