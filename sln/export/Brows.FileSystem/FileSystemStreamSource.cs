using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class FileSystemStreamSource : EntryStreamSource<FileSystemEntry> {
        protected sealed override async Task<IEntryStreamReady> StreamReady(CancellationToken token) {
            if (Entry.Info is FileInfo file) {
                if (file.Exists) {
                    Stream = await Task.Run(file.OpenRead, token);
                }
            }
            return await base.StreamReady(token);
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
