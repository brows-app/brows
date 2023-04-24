using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public static class EntryStreamConsumer {
        public static async Task Consume(this IEntryStreamSet entryStreamSet, EntryStreamConsumingDelegate consuming, IOperationProgress progress, CancellationToken token) {
            if (null == entryStreamSet) throw new ArgumentNullException(nameof(entryStreamSet));
            var set = entryStreamSet;
            using
            var sourcesReady = set.StreamSourceReady();
            var sources = set.StreamSource()?.Where(s => s != null)?.ToList();
            if (sources == null) return;
            if (sources.Count == 0) {
                return;
            }
            if (progress != null) {
                progress.Target?.Add(sources.Count);
            }
            foreach (var source in sources) {
                if (token.IsCancellationRequested) {
                    token.ThrowIfCancellationRequested();
                }
                using
                var streamReady = source.StreamReady();
                var streamValid = source.StreamValid;
                if (streamValid) {
                    if (progress != null) {
                        progress.Info?.Data(source.EntryName);
                    }
                    await using
                    var stream = source.Stream();
                    if (stream != null) {
                        if (consuming != null) {
                            var task = consuming(source, stream, progress, token);
                            if (task != null) {
                                await task;
                            }
                        }
                    }
                }
                if (progress != null) {
                    progress.Add(1);
                }
            }
        }

        public static async Task ConsumeFromMemory(this IEntryStreamSet entryStreamSet, EntryStreamConsumingDelegate consuming, IOperationProgress progress, CancellationToken token) {
            await Consume(entryStreamSet, progress: progress, token: token, consuming: async (source, stream, progress, token) => {
                await using var memory = source.StreamLength <= int.MaxValue
                    ? new MemoryStream(Convert.ToInt32(source.StreamLength))
                    : new MemoryStream();
                await stream.CopyToAsync(memory, token);
                if (consuming != null) {
                    memory.Position = 0;
                    var task = consuming(source, memory, progress, token);
                    if (task != null) {
                        await task;
                    }
                }
            });
        }
    }
}
