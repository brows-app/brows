using System.IO;

namespace Brows {
    internal sealed class FileStreamSource : IEntryStreamSource {
        private FileInfo Info => _Info ??= new(Path);
        private FileInfo _Info;

        public string Path { get; }

        public FileStreamSource(string path) {
            Path = path;
        }

        string IEntryStreamSource.EntryID => Path;
        string IEntryStreamSource.EntryName => Info.Name;
        string IEntryStreamSource.SourceFile => Path;
        string IEntryStreamSource.RelativePath => Info.Name;
        long IEntryStreamSource.StreamLength => Info.Length;
        bool IEntryStreamSource.StreamValid => Info.Exists;
        IEntryStreamReady IEntryStreamSource.StreamReady() => new EntryStreamReady();
        Stream IEntryStreamSource.Stream() => Info.OpenRead();
    }
}
