using System;
using System.IO;

namespace Brows {
    internal sealed class FileSystemStreamSource : EntryStreamSource<FileSystemEntry> {
        protected sealed override Stream Stream() {
            var file = Entry.Info as FileInfo;
            if (file == null) throw new InvalidOperationException();
            return file.OpenRead();
        }

        public sealed override string SourceFile =>
            Entry.ID;

        public sealed override string RelativePath =>
            Entry.Name;

        public sealed override long StreamLength =>
            Entry.Info is FileInfo file
                ? file.Length
                : 0;

        public sealed override bool StreamValid =>
            Entry.Info is FileInfo file &&
            file.Exists;

        public FileSystemStreamSource(FileSystemEntry entry) : base(entry) {
        }

        public sealed override string ToString() {
            return $"{Entry.Info}";
        }
    }
}
