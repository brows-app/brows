using System.Collections.Generic;
using System.IO.Compression;

namespace Brows.IO.Compression {
    public class CompressionCreate : ICompressionCreate {
        public string Output { get; set; }
        public IEnumerable<IEntry> Entries { get; set; }
        public IOperationProgress Progress { get; set; }
        public CompressionLevel? Level { get; set; }
    }
}
