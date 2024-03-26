using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class FileStreamSource : IEntryStreamSource {
        private Stream Stream;

        private FileInfo Info => _Info ??= new(Path);
        private FileInfo _Info;

        public string Path { get; }

        public FileStreamSource(string path) {
            Path = path;
        }

        string IEntryStreamSource.EntryID => Path;
        string IEntryStreamSource.EntryName => Info.Name;
        string IEntryStreamSource.SourceFile => Path;
        string IEntryStreamSource.SourceDirectory => null;
        string IEntryStreamSource.RelativePath => Info.Name;
        long IEntryStreamSource.StreamLength => Info.Length;
        Stream IEntryStreamSource.Stream => Stream;

        async Task<IEntryStreamReady> IEntryStreamSource.StreamReady(CancellationToken token) {
            Stream = Info.Exists
                ? await Task.Run(Info.OpenRead, token)
                : null;
            return new EntryStreamReady();
        }
    }
}
