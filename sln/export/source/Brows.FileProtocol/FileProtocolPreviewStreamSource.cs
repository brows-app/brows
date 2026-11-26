using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class FileProtocolPreviewStreamSource<TEntry> : FileProtocolStreamSource<TEntry>
where TEntry : FileProtocolEntry {
    private readonly Func<TEntry, CancellationToken, Task<Stream>> Open;
    private readonly long? ReadLimit;

    protected sealed override async Task<IEntryStreamReady> StreamReady(CancellationToken token) {
        var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        try {
            var stream = await Open(Entry, lifetime.Token);
            if (stream is null) {
                throw new IOException("The remote preview stream was not opened.");
            }
            Stream = new PreviewReadStream(stream, ReadLimit, lifetime);
            lifetime = null;
            return await base.StreamReady(token);
        }
        finally {
            lifetime?.Dispose();
        }
    }

    public sealed override long StreamLength { get; }
    public sealed override string RelativePath { get; }

    public FileProtocolPreviewStreamSource(TEntry entry,
                                           string relativePath,
                                           long streamLength,
                                           long? readLimit,
                                           Func<TEntry, CancellationToken, Task<Stream>> open)
    : base(entry) {
        Open = open ?? throw new ArgumentNullException(nameof(open));
        RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
        ReadLimit = readLimit is < 0 ? throw new ArgumentOutOfRangeException(nameof(readLimit)) : readLimit;
        StreamLength = streamLength;
    }

    private sealed class PreviewReadStream : Stream {
        private readonly Lock Locker = new();
        private readonly SemaphoreSlim ReadLocker = new(1, 1);
        private readonly Stream Inner;
        private readonly CancellationTokenSource Lifetime;
        private readonly long? Limit;
        private long ReadLength;
        private Task DisposeTask;
        private int DisposeStarted;

        private Task DisposeInner() {
            lock (Locker) {
                return DisposeTask ??= Task.Run(async () => {
                    await ReadLocker.WaitAsync().ConfigureAwait(false);
                    try {
                        await Inner.DisposeAsync().ConfigureAwait(false);
                    }
                    finally {
                        ReadLocker.Release();
                        ReadLocker.Dispose();
                        Lifetime.Dispose();
                    }
                });
            }
        }

        private void CancelLifetime() {
            try {
                Lifetime.Cancel();
            }
            catch (ObjectDisposedException) {
            }
        }

        public sealed override bool CanRead => Volatile.Read(ref DisposeStarted) == 0;
        public sealed override bool CanSeek => false;
        public sealed override bool CanWrite => false;
        public sealed override long Length => throw new NotSupportedException();
        public sealed override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public PreviewReadStream(Stream inner, long? limit, CancellationTokenSource lifetime) {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            Limit = limit;
            Lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        }

        protected sealed override void Dispose(bool disposing) {
            if (disposing) {
                CancelLifetime();
                Interlocked.Exchange(ref DisposeStarted, 1);
                _ = DisposeInner().ContinueWith(
                    static task => {
                        _ = task.Exception;
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            base.Dispose(disposing);
        }

        public sealed override async ValueTask DisposeAsync() {
            CancelLifetime();
            Interlocked.Exchange(ref DisposeStarted, 1);
            await DisposeInner().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        public sealed override void Flush() {
            throw new NotSupportedException();
        }

        public sealed override int Read(byte[] buffer, int offset, int count) {
            ArgumentNullException.ThrowIfNull(buffer);
            ValidateBufferArguments(buffer, offset, count);
            return ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }

        public sealed override Task<int> ReadAsync(byte[] buffer,
                                                   int offset,
                                                   int count,
                                                   CancellationToken cancellationToken) {
            ArgumentNullException.ThrowIfNull(buffer);
            ValidateBufferArguments(buffer, offset, count);
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        public sealed override async ValueTask<int> ReadAsync(Memory<byte> buffer,
                                                              CancellationToken cancellationToken = default) {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref DisposeStarted) != 0, this);
            cancellationToken.ThrowIfCancellationRequested();
            var limitAlreadyReached = Limit is long limitAtStart && Interlocked.Read(ref ReadLength) >= limitAtStart;
            if (limitAlreadyReached) {
                CancelLifetime();
                return 0;
            }
            if (buffer.Length == 0) {
                return 0;
            }
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(Lifetime.Token, cancellationToken);
            await ReadLocker.WaitAsync(linked.Token).ConfigureAwait(false);
            try {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref DisposeStarted) != 0, this);
                var remaining = Limit is long limit ? limit - ReadLength : long.MaxValue;
                if (remaining <= 0) {
                    CancelLifetime();
                    return 0;
                }
                var read = await Inner.ReadAsync(buffer[..(int)Math.Min(buffer.Length, remaining)], linked.Token)
                    .ConfigureAwait(false);
                ReadLength += read;
                var limitReachedAfterRead = Limit is long readLimit && ReadLength >= readLimit;
                if (limitReachedAfterRead) {
                    CancelLifetime();
                }
                return read;
            }
            finally {
                ReadLocker.Release();
            }
        }

        public sealed override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public sealed override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public sealed override void Write(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }
    }
}
