using Brows.Operations;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Entries;

/// <summary>
/// Reads entry stream sets and manages their preparation and stream lifetimes.
/// </summary>
public static class EntryStreamConsumer {
    /// <summary>
    /// Processes each source in order and disposes its stream and preparation resources afterward.
    /// </summary>
    /// <param name="entryStreamSet">
    /// The stream sources to consume.
    /// </param>
    /// <param name="consuming">
    /// Processes each prepared stream. The stream is valid only until the returned task completes.
    /// </param>
    /// <param name="progress">
    /// Receives progress updates, or null when no progress reporting is needed.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes after all sources have been processed.
    /// </returns>
    public static async Task Consume(this IEntryStreamSet entryStreamSet,
                                     EntryStreamConsumingDelegate consuming,
                                     IOperationProgress progress,
                                     CancellationToken token) {
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

    /// <summary>
    /// Copies each readable source into memory before processing it, disposing streams and preparation resources
    /// afterward.
    /// </summary>
    /// <param name="entryStreamSet">
    /// The stream sources to consume.
    /// </param>
    /// <param name="consuming">
    /// Processes each prepared stream. The stream is valid only until the returned task completes.
    /// </param>
    /// <param name="progress">
    /// Receives progress updates, or null when no progress reporting is needed.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes after all sources have been processed.
    /// </returns>
    public static async Task ConsumeFromMemory(this IEntryStreamSet entryStreamSet,
                                               EntryStreamConsumingDelegate consuming,
                                               IOperationProgress progress,
                                               CancellationToken token) {
        if (consuming is null) {
            throw new ArgumentNullException(nameof(consuming));
        }
        await Consume(entryStreamSet, progress: progress, token: token,
            consuming: async (source, stream, progress, token) => {
                if (stream == null) {
                    var task = consuming(source, stream, progress, token);
                    if (task != null) {
                        await task;
                    }
                }
                else {
                    var capacityIsKnown = source.StreamLength >= 0 && source.StreamLength <= int.MaxValue;
                    await using var memory = capacityIsKnown
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
