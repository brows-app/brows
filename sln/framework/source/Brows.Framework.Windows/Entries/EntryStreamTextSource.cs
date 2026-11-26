using Domore.IO;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Entries;

internal sealed class EntryStreamTextSource : IStreamText {
    private readonly IEntryStreamSource Source;
    private readonly long? ReadLimit;

    internal EntryStreamTextSource(IEntryStreamSource source, long? readLimit = null) {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        if (readLimit < 0) {
            throw new ArgumentOutOfRangeException(nameof(readLimit));
        }
        ReadLimit = readLimit;
    }

    async Task<IDisposable> IStreamText.StreamReady(CancellationToken token) {
        return await Source.StreamReady(token);
    }

    Task<long> IStreamText.StreamLength(CancellationToken token) {
        return Task.FromResult(Source.StreamLength);
    }

    Stream IStreamText.StreamText() {
        var stream = Source.Stream;
        return stream is not null && ReadLimit is long limit ? new BoundedStream(stream, limit) : stream;
    }

    private sealed class BoundedStream : Stream {
        private const string LimitExceeded = "The entry exceeds the text preview byte limit.";
        private readonly Stream Inner;
        private long Remaining;

        private int ReadSize(int requested) {
            if (Remaining < 0) {
                throw new IOException(LimitExceeded);
            }
            /*
             * Reserve one byte to distinguish a complete read at the limit from overflow.
             */
            return requested > Remaining ? (int)(Remaining + 1) : requested;
        }

        private int Consume(int count) {
            Remaining -= count;
            if (Remaining < 0) {
                throw new IOException(LimitExceeded);
            }
            return count;
        }

        public sealed override bool CanRead => Inner.CanRead;
        public sealed override bool CanSeek => false;
        public sealed override bool CanWrite => false;
        public sealed override long Length => throw new NotSupportedException();

        public sealed override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public BoundedStream(Stream inner, long limit) {
            Inner = inner;
            Remaining = limit;
        }

        protected sealed override void Dispose(bool disposing) {
            if (disposing) {
                Inner.Dispose();
            }
            base.Dispose(disposing);
        }

        public sealed override async ValueTask DisposeAsync() {
            await Inner.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        public sealed override int Read(byte[] buffer, int offset, int count) {
            ValidateBufferArguments(buffer, offset, count);
            return Read(buffer.AsSpan(offset, count));
        }

        public sealed override int Read(Span<byte> buffer) {
            return Consume(Inner.Read(buffer[..ReadSize(buffer.Length)]));
        }

        public sealed override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) {
            ValidateBufferArguments(buffer, offset, count);
            return ReadAsync(buffer.AsMemory(offset, count), token).AsTask();
        }

        public sealed override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) {
            var count = await Inner.ReadAsync(buffer[..ReadSize(buffer.Length)], token).ConfigureAwait(false);
            return Consume(count);
        }

        public sealed override void Flush() {
            throw new NotSupportedException();
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
