using Brows.SFTP.Native;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

internal sealed class SftpFileStream : Stream {
    private int Disposed;

    public sealed override bool CanRead { get; }
    public sealed override bool CanSeek => false;
    public sealed override bool CanWrite { get; }
    public sealed override long Length => throw new NotSupportedException();

    public sealed override long Position {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    private SftpClient Client { get; }
    private SftpFile File { get; }

    public SftpFileStream(SftpClient client, SftpFile file, bool canRead, bool canWrite) {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        File = file ?? throw new ArgumentNullException(nameof(file));
        CanRead = canRead;
        CanWrite = canWrite;
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref Disposed) != 0, this);
    }

    protected sealed override void Dispose(bool disposing) {
        if (disposing && Interlocked.Exchange(ref Disposed, 1) == 0) {
            try {
                Client.Close(File).GetAwaiter().GetResult();
            }
            finally {
                base.Dispose(disposing);
            }
            return;
        }
        base.Dispose(disposing);
    }

    public sealed override async ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref Disposed, 1) != 0) {
            await base.DisposeAsync().ConfigureAwait(false);
            return;
        }
        try {
            await Client.Close(File).ConfigureAwait(false);
        }
        finally {
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }

    public sealed override void Flush() {
        ThrowIfDisposed();
    }

    public sealed override int Read(byte[] buffer, int offset, int count) {
        ValidateBufferArguments(buffer, offset, count);
        ThrowIfDisposed();
        if (!CanRead) {
            throw new NotSupportedException();
        }
        if (count == 0) {
            return 0;
        }
        return Client.Read(File, buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
    }

    public sealed override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) {
        ValidateBufferArguments(buffer, offset, count);
        ThrowIfDisposed();
        if (!CanRead) {
            throw new NotSupportedException();
        }
        if (count == 0) {
            return 0;
        }
        return await Client.Read(File, buffer, offset, count, cancellationToken).ConfigureAwait(false);
    }

    public sealed override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (!CanRead) {
            throw new NotSupportedException();
        }
        var copy = new byte[buffer.Length];
        var read = await Client.Read(File, copy, 0, copy.Length, cancellationToken).ConfigureAwait(false);
        copy.AsMemory(0, read).CopyTo(buffer);
        return read;
    }

    public sealed override void Write(byte[] buffer, int offset, int count) {
        ValidateBufferArguments(buffer, offset, count);
        ThrowIfDisposed();
        if (!CanWrite) {
            throw new NotSupportedException();
        }
        if (count == 0) {
            return;
        }
        WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
    }

    public sealed override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) {
        ValidateBufferArguments(buffer, offset, count);
        ThrowIfDisposed();
        if (!CanWrite) {
            throw new NotSupportedException();
        }
        if (count == 0) {
            return;
        }
        var written = 0;
        while (written < count) {
            cancellationToken.ThrowIfCancellationRequested();
            var next = await Client.Write(
                File,
                buffer,
                offset + written,
                count - written,
                cancellationToken).ConfigureAwait(false);
            if (next == 0) {
                throw new IOException("The SFTP server accepted no bytes for a non-empty write.");
            }
            written += next;
        }
    }

    public sealed override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (!CanWrite) {
            throw new NotSupportedException();
        }
        var copy = buffer.ToArray();
        await WriteAsync(copy, 0, copy.Length, cancellationToken).ConfigureAwait(false);
    }

    public sealed override long Seek(long offset, SeekOrigin origin) {
        throw new NotSupportedException();
    }

    public sealed override void SetLength(long value) {
        throw new NotSupportedException();
    }
}
