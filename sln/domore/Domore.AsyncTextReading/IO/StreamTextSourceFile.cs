using System;
using System.IO;

namespace Domore.IO {
    internal sealed class StreamTextSourceFile : StreamTextSource {
        public sealed override long StreamLength =>
            FileInfo.Length;

        public FileInfo FileInfo { get; }

        public StreamTextSourceFile(FileInfo fileInfo) {
            FileInfo = fileInfo ?? throw new ArgumentNullException(nameof(fileInfo));
        }

        public sealed override Stream StreamText() {
            return FileInfo.OpenRead();
        }

        public override string ToString() {
            return FileInfo.ToString();
        }
    }
}
