using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO.Compression {
    internal class CompressionInputFileCollection {
        private readonly Dictionary<string, FileInfo> Path;

        public int Count =>
            Path.Count;

        public StringComparer PathComparer { get; }

        public CompressionInputFileCollection(StringComparer pathComparer) {
            PathComparer = pathComparer;
            Path = new Dictionary<string, FileInfo>(PathComparer);
        }

        public Task Add(FileInfo file, CancellationToken cancellationToken) {
            if (null == file) throw new ArgumentNullException(nameof(file));
            var path = file.FullName;
            if (Path.TryGetValue(path, out _) == false) {
                Path[path] = file;
            }
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<(string Name, FileInfo File)> Files([EnumeratorCancellation] CancellationToken cancellationToken) {
            var common = PathExtension.SkipCommonOf(Path.Keys, PathComparer);
            foreach (var item in common) {
                if (cancellationToken.IsCancellationRequested) {
                    break;
                }
                yield return (Name: item.RelativePath, File: Path[item.OriginalPath]);
            }
            await Task.CompletedTask;
        }
    }
}
