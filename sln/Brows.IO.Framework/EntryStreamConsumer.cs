using System;
using System.IO;
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
            var sources = set.StreamSource(token);
            if (sources == null) {
                return;
            }
            await foreach (var source in sources) {
                if (token.IsCancellationRequested) {
                    token.ThrowIfCancellationRequested();
                }
                if (source == null) {
                    continue;
                }
                if (progress != null) {
                    progress.Change(data: source.EntryName);
                }
                using (var streamReady = await source.StreamReady(token)) {
                    await using (var stream = source.Stream) {
                        var task = consuming(source, stream, progress, token);
                        if (task != null) {
                            await task;
                        }
                    }
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
