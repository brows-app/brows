using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class FtpDownloadStream : Stream {
    private readonly Stream Inner;
    private readonly byte[] Peeked;
    private readonly int PeekedCount;
    private readonly Action Release;
    private int PeekedOffset;
    private int Released;

    private void ReleaseOnce() {
        var releasing = Interlocked.Exchange(ref Released, 1) == 0;
        if (releasing) {
            Release();
        }
    }

    private int ReadPeeked(Span<byte> buffer) {
        var count = Math.Min(buffer.Length, PeekedCount - PeekedOffset);
        Peeked.AsSpan(PeekedOffset, count).CopyTo(buffer);
        PeekedOffset += count;
        return count;
    }

    protected sealed override void Dispose(bool disposing) {
        if (disposing) {
            try {
                Inner.Dispose();
            }
            finally {
                ReleaseOnce();
            }
        }
        base.Dispose(disposing);
    }

    internal FtpDownloadStream(Stream inner, byte[] peeked, int peekedCount, Action release) {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        Peeked = peeked ?? throw new ArgumentNullException(nameof(peeked));
        ArgumentOutOfRangeException.ThrowIfNegative(peekedCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(peekedCount, peeked.Length);
        PeekedCount = peekedCount;
        Release = release ?? throw new ArgumentNullException(nameof(release));
    }

    public sealed override bool CanRead => true;
    public sealed override bool CanSeek => false;
    public sealed override bool CanWrite => false;
    public sealed override long Length => Inner.Length;

    public sealed override long Position {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public sealed override async ValueTask DisposeAsync() {
        try {
            await Inner.DisposeAsync();
        }
        finally {
            ReleaseOnce();
            await base.DisposeAsync();
        }
    }

    public sealed override void Flush() {
    }

    public sealed override int Read(byte[] buffer, int offset, int count) {
        ValidateBufferArguments(buffer, offset, count);
        return Read(buffer.AsSpan(offset, count));
    }

    public sealed override int Read(Span<byte> buffer) {
        if (PeekedOffset < PeekedCount) {
            return ReadPeeked(buffer);
        }
        if (PeekedCount == 0) {
            return 0;
        }
        return Inner.Read(buffer);
    }

    public sealed override Task<int> ReadAsync(byte[] buffer,
                                               int offset,
                                               int count,
                                               CancellationToken cancellationToken) {
        ValidateBufferArguments(buffer, offset, count);
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public sealed override async ValueTask<int> ReadAsync(Memory<byte> buffer,
                                                          CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        if (PeekedOffset < PeekedCount) {
            return ReadPeeked(buffer.Span);
        }
        if (PeekedCount == 0) {
            return 0;
        }
        return await Inner.ReadAsync(buffer, cancellationToken);
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
