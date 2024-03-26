using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal sealed class FileStreamSet : IEntryStreamSet {
        public IEnumerable<string> Paths { get; }

        public FileStreamSet(IEnumerable<string> paths) {
            Paths = paths ?? throw new ArgumentNullException(nameof(paths));
        }

        IEntryStreamReady IEntryStreamSet.StreamSourceReady() {
            return new EntryStreamReady();
        }

        async IAsyncEnumerable<IEntryStreamSource> IEntryStreamSet.StreamSource([EnumeratorCancellation] CancellationToken token) {
            var paths = Paths.Select(path => new FileStreamSource(path));
            foreach (var path in paths) {
                if (token.IsCancellationRequested) {
                    token.ThrowIfCancellationRequested();
                }
                yield return path;
            }
            await Task.CompletedTask;
        }

        IEnumerable<string> IEntryStreamSet.FileSource() {
            return Paths;
        }
    }
}
