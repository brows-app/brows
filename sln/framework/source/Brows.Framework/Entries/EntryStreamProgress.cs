using Brows.Operations;
using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Entries;

public static class EntryStreamProgress {
    public static Task Copy(this IOperationProgress progress,
                            Stream source,
                            Stream destination,
                            CancellationToken token) {
        return Copy(progress, source, destination, bufferSize: 0, token);
    }

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
