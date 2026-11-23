using Brows.Operations;
using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Entries;

/// <summary>
/// Copies stream content while reporting the number of bytes transferred.
/// </summary>
public static class EntryStreamProgress {
    /// <summary>
    /// Copies from the source's current position to its end while reporting byte progress. Leaves both streams open.
    /// </summary>
    /// <param name="progress">
    /// Receives progress updates for the operation.
    /// </param>
    /// <param name="source">
    /// The readable source stream.
    /// </param>
    /// <param name="destination">
    /// The writable destination stream.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when copying finishes.
    /// </returns>
    public static Task Copy(this IOperationProgress progress,
                            Stream source,
                            Stream destination,
                            CancellationToken token) {
        return Copy(progress, source, destination, bufferSize: 0, token);
    }

    /// <summary>
    /// Copies from the source's current position to its end while reporting byte progress. Leaves both streams open.
    /// </summary>
    /// <param name="progress">
    /// Receives progress updates for the operation.
    /// </param>
    /// <param name="source">
    /// The readable source stream.
    /// </param>
    /// <param name="destination">
    /// The writable destination stream.
    /// </param>
    /// <param name="bufferSize">
    /// The requested copy buffer size in bytes, or a nonpositive value to use the default.
    /// </param>
    /// <param name="token">
    /// A token used to cancel the operation.
    /// </param>
    /// <returns>
    /// A task that completes when copying finishes.
    /// </returns>
    public static async Task Copy(this IOperationProgress progress,
                                  Stream source,
                                  Stream destination,
                                  int bufferSize,
                                  CancellationToken token) {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        var pool = ArrayPool<byte>.Shared;
        long length;
        try {
            length = source.Length;
        }
        catch (NotSupportedException) {
            length = -1;
        }
        var lengthIsKnown = length >= 0;
        var buffer = default(byte[]);
        if (bufferSize <= 0) {
            bufferSize = 81920;
        }
        var bufferExceedsLength = length > 0 && bufferSize > length;
        if (bufferExceedsLength) {
            bufferSize = (int)length;
        }
        buffer = pool.Rent(bufferSize);
        progress.Change(
            setProgress: 0,
            setTarget: Math.Max(0, length),
            kind: OperationProgressKind.FileSize);
        try {
            for (; ; ) {
                var read = await source.ReadAsync(buffer, 0, buffer.Length, token);
                if (read == 0) {
                    break;
                }
                await destination.WriteAsync(buffer, 0, read, token);
                progress.Change(addProgress: read, addTarget: lengthIsKnown ? null : read);
            }
        }
        finally {
            pool.Return(buffer);
        }
    }
}
