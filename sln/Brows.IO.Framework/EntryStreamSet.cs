using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public abstract class EntryStreamSet : IEntryStreamSet {
        protected abstract IEnumerable<IEntryStreamSource> StreamSource();

        protected virtual IEnumerable<string> FileSource() {
            return Array.Empty<string>();
        }

        protected virtual IEntryStreamReady StreamSourceReady() {
            return new EntryStreamReady();
        }

        IEntryStreamReady IEntryStreamSet.StreamSourceReady() => StreamSourceReady();
        IEnumerable<string> IEntryStreamSet.FileSource() => FileSource();

        async IAsyncEnumerable<IEntryStreamSource> IEntryStreamSet.StreamSource([EnumeratorCancellation] CancellationToken token) {
            var streamSource = StreamSource();
            if (streamSource == null) {
                yield break;
            }
            foreach (var source in streamSource) {
                if (token.IsCancellationRequested) {
                    token.ThrowIfCancellationRequested();
                }
                yield return source;
            }
            await Task.CompletedTask;
        }

        public static IEntryStreamSet FromFiles(IEnumerable<string> paths) {
            return new FileStreamSet(paths);
        }
    }
}
