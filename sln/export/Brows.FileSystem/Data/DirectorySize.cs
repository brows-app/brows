using System.IO;

namespace Brows.Data {
    internal sealed class DirectorySize : DirectoryAggregateData {
        protected sealed override long Data(FileSystemInfo info) {
            return info is FileInfo file ? file.Length : 0;
        }

        public DirectorySize() {
            Width = 100;
            Converter = EntryDataConverter.FileSystemSize;
        }
    }
}
