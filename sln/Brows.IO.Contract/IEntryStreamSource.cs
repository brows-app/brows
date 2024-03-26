using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IEntryStreamSource {
        string EntryID { get; }
        string EntryName { get; }
        string RelativePath { get; }
        string SourceFile { get; }
        string SourceDirectory { get; }
        long StreamLength { get; }
        Stream Stream { get; }
        Task<IEntryStreamReady> StreamReady(CancellationToken token);
    }
}
