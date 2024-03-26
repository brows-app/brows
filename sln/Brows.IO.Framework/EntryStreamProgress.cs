using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    public static class EntryStreamProgress {
        public static Task Copy(this IOperationProgress progress, Stream source, Stream destination, CancellationToken token) {
            return Copy(progress, source, destination, bufferSize: 0, token);
        }

        public static async Task Copy(this IOperationProgress progress, Stream source, Stream destination, int bufferSize, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(progress);
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(destination);
            var pool = ArrayPool<byte>.Shared;
            var length = source.Length;
            var buffer = default(byte[]);
            if (bufferSize <= 0) {
                bufferSize = 81920;
            }
            if (bufferSize > length) {
                bufferSize = (int)length;
            }
            buffer = pool.Rent(bufferSize);
            progress.Change(
                setProgress: 0,
                setTarget: length,
                kind: OperationProgressKind.FileSize);
            try {
                for (; ; ) {
                    if (token.IsCancellationRequested) {
                        token.ThrowIfCancellationRequested();
                    }
                    var read = await source.ReadAsync(buffer, 0, buffer.Length, token);
                    if (read == 0) {
                        break;
                    }
                    if (token.IsCancellationRequested) {
                        token.ThrowIfCancellationRequested();
                    }
                    await destination.WriteAsync(buffer, 0, read, token);
                    progress.Change(read);
                }
            }
            finally {
                pool.Return(buffer);
            }
            await Task.CompletedTask;
        }
    }
}
