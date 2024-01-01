using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public interface IEntryStreamSource {
        string EntryID { get; }
        string EntryName { get; }
        string RelativePath { get; }
        string SourceFile { get; }
        long StreamLength { get; }
        bool StreamValid { get; }
        Stream Stream();
        Task<IEntryStreamReady> StreamReady(CancellationToken token);
    }
}
