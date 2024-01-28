using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public static class EntryStreamConsumer {
        public static async Task Consume(this IEntryStreamSet entryStreamSet, EntryStreamConsumingDelegate consuming, IOperationProgress progress, CancellationToken token) {
            if (null == entryStreamSet) throw new ArgumentNullException(nameof(entryStreamSet));
            if (null == consuming) throw new ArgumentNullException(nameof(consuming));
            var set = entryStreamSet;
            using
            var sourcesReady = set.StreamSourceReady();
            var sources = set.StreamSource()?.Where(s => s != null)?.ToList();
            if (sources == null) return;
            if (sources.Count == 0) {
                return;
            }
            if (progress != null) {
                progress.Change(addTarget: sources.Count);
            }
            foreach (var source in sources) {
                if (token.IsCancellationRequested) {
                    token.ThrowIfCancellationRequested();
                }
                if (progress != null) {
                    progress.Change(data: source.EntryName);
                }
                using (var streamReady = await source.StreamReady(token)) {
                    await using (var stream = source.Stream()) {
                        var task = consuming(source, stream, progress, token);
                        if (task != null) {
                            await task;
                        }
                    }
                }
                if (progress != null) {
                    progress.Change(1);
                }
            }
        }

        public static async Task ConsumeFromMemory(this IEntryStreamSet entryStreamSet, EntryStreamConsumingDelegate consuming, IOperationProgress progress, CancellationToken token) {
            if (null == consuming) throw new ArgumentNullException(nameof(consuming));
            await Consume(entryStreamSet, progress: progress, token: token, consuming: async (source, stream, progress, token) => {
                if (stream == null) {
                    var task = consuming(source, stream, progress, token);
                    if (task != null) {
                        await task;
                    }
                }
                else {
                    await using var memory = source.StreamLength <= int.MaxValue
                        ? new MemoryStream(Convert.ToInt32(source.StreamLength))
                        : new MemoryStream();
                    await stream.CopyToAsync(memory, token);
                    memory.Position = 0;
                    var task = consuming.Invoke(source, memory, progress, token);
                    if (task != null) {
                        await task;
                    }
                }
            });
        }
    }
}
