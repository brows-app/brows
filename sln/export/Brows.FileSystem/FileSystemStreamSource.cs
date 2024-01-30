using System;
using System.IO;

namespace Brows {
    internal sealed class FileSystemStreamSource : EntryStreamSource<FileSystemEntry> {
        protected sealed override Stream Stream() {
            if (Entry.Info is FileInfo file) {
                if (file.Exists) {
                    return file.OpenRead();
                }
            }
            return null;
        }

        public sealed override string SourceFile =>
            Entry.ID;

        public sealed override string RelativePath =>
            Entry.Name;

        public sealed override long StreamLength =>
            Entry.Info is FileInfo file
                ? file.Length
                : 0;

        public FileSystemStreamSource(FileSystemEntry entry) : base(entry) {
        }

        public sealed override string ToString() {
            return $"{Entry.Info}";
        }
    }
}
