using System.Collections.Generic;
using System.IO.Compression;

namespace Brows.IO.Compression {
    public interface ICompressionCreate {
        string Output { get; }
        IEnumerable<IEntry> Entries { get; }
        IOperationProgress Progress { get; }
        CompressionLevel? Level { get; }
    }
}
