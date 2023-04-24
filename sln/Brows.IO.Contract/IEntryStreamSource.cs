using System.IO;

namespace Brows {
    public interface IEntryStreamSource {
        string EntryID { get; }
        string EntryName { get; }
        string RelativePath { get; }
        string SourceFile { get; }
        long StreamLength { get; }
        bool StreamValid { get; }
        IEntryStreamReady StreamReady();
        Stream Stream();
    }
}
