using Domore.IO;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Commands;

internal sealed class FindFileTextSource : IStreamText {
    private readonly FileInfo File;
    private readonly long MaximumBytes;

    internal FindFileTextSource(FileInfo file, long maximumBytes) {
        File = file ?? throw new ArgumentNullException(nameof(file));
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumBytes, 1);
        MaximumBytes = maximumBytes;
    }

    public Task<long> StreamLength(CancellationToken token) {
        token.ThrowIfCancellationRequested();
        return Task.FromResult(new FileInfo(File.FullName).Length);
    }

    public Task<IDisposable> StreamReady(CancellationToken token) {
        token.ThrowIfCancellationRequested();
        return Task.FromResult(default(IDisposable));
    }

    public Stream StreamText() {
        var stream = new FileStream(File.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        try {
            if (stream.Length > MaximumBytes) {
                throw new FindContentLimitException();
            }
            return new LimitedStream(stream, MaximumBytes);
        }
        catch {
            stream.Dispose();
            throw;
        }
    }

    internal sealed class LimitedStream : Stream {
        private readonly Stream Source;
        private readonly long MaximumBytes;
        private long BytesRead;
        private bool LimitExceeded;

        private int ReadLength(int requested) {
            /*
             * Read at most one byte beyond the limit to distinguish exact-limit EOF from
             * a file that grew after opening. That byte is never returned to the decoder.
             */
            if (LimitExceeded) {
                throw new FindContentLimitException();
            }
            var remaining = MaximumBytes - BytesRead;
            return remaining >= requested ? requested : (int)remaining + 1;
        }

        private int Count(int count) {
            if (count > MaximumBytes - BytesRead) {
                LimitExceeded = true;
                throw new FindContentLimitException();
            }
            BytesRead += count;
            return count;
        }

        internal LimitedStream(Stream source, long maximumBytes) {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumBytes, 1);
            MaximumBytes = maximumBytes;
        }

        public override bool CanRead => Source.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position {
            get => BytesRead;
            set => throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                Source.Dispose();
            }
            base.Dispose(disposing);
        }

        public override int Read(byte[] buffer, int offset, int count) {
            ArgumentNullException.ThrowIfNull(buffer);
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer) {
            return Count(Source.Read(buffer[..ReadLength(buffer.Length)]));
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) {
            var count = await Source.ReadAsync(buffer[..ReadLength(buffer.Length)], token).ConfigureAwait(false);
            return Count(count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) {
            ArgumentNullException.ThrowIfNull(buffer);
            return ReadAsync(buffer.AsMemory(offset, count), token).AsTask();
        }

        public override void Flush() {
        }

        public override long Seek(long offset, SeekOrigin origin) {
            throw new NotSupportedException();
        }

        public override void SetLength(long value) {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) {
            throw new NotSupportedException();
        }
    }
}
