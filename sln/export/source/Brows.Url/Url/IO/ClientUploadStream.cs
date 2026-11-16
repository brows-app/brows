using Brows.Url.Extensions;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Brows.Url.IO;

internal sealed class ClientUploadStream : Stream {
    private const int CurlReadFuncAbort = 0x10000000;
    private readonly ClientForUrl Client;
    private readonly CancellationToken Token;
    private readonly long ExpectedLength;
    private readonly Channel<byte[]> Channel = System.Threading.Channels.Channel.CreateBounded<byte[]>(new BoundedChannelOptions(8) {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = true
    });
    private readonly ChannelReader<byte[]> Reader;
    private readonly ChannelWriter<byte[]> Writer;
    private readonly Task Transfer;
    private byte[] Current;
    private int CurrentOffset;
    private long Written;
    private Exception ReadError;
    private bool Disposed;

    private nuint ReadNative(IntPtr buffer, nuint length) {
        if (length == 0) {
            return 0;
        }
        if (Token.IsCancellationRequested) {
            ReadError = new OperationCanceledException(Token);
            return (nuint)CurlReadFuncAbort;
        }
        var total = 0;
        for (;;) {
            var current = Current;
            if (current is not null) {
                var remaining = current.Length - CurrentOffset;
                var available = length - (nuint)total;
                var requested = (int)Math.Min((ulong)remaining, (ulong)available);
                Marshal.Copy(current, CurrentOffset, IntPtr.Add(buffer, total), requested);
                total += requested;
                CurrentOffset += requested;
                if (CurrentOffset == current.Length) {
                    Current = null;
                    CurrentOffset = 0;
                }
                if ((nuint)total == length || total > 0) {
                    return (nuint)total;
                }
                continue;
            }
            if (Reader.TryRead(out current)) {
                Current = current;
                continue;
            }
            try {
                var canRead = Reader.WaitToReadAsync(Token).AsTask().GetAwaiter().GetResult();
                if (canRead) {
                    continue;
                }
            }
            catch (Exception ex) {
                ReadError = ex;
                return (nuint)CurlReadFuncAbort;
            }
            if (ExpectedLength >= 0 && Written != ExpectedLength) {
                ReadError = new EndOfStreamException("The FTP upload source ended before the expected length.");
                return (nuint)CurlReadFuncAbort;
            }
            return (nuint)total;
        }
    }

    private async Task TransferFile(CancellationToken token) {
        Exception transferError = null;
        try {
            var exception = await Task.Run(() => Client.Try(), CancellationToken.None).ConfigureAwait(false);
            if (exception is not null) {
                if (ReadError is not null) {
                    throw new IOException("The FTP upload failed while reading its source.", ReadError);
                }
                if (token.IsCancellationRequested) {
                    throw new OperationCanceledException(token);
                }
                throw exception;
            }
            if (ReadError is not null) {
                throw new IOException("The FTP upload failed while reading its source.", ReadError);
            }
        }
        catch (Exception ex) {
            transferError = ex;
            throw;
        }
        finally {
            Writer.TryComplete(transferError);
        }
    }

    private void ThrowIfDisposed() {
        if (Disposed) {
            throw new ObjectDisposedException(nameof(ClientUploadStream));
        }
    }

    public sealed override bool CanRead => false;
    public sealed override bool CanSeek => false;
    public sealed override bool CanWrite => !Disposed;
    public sealed override long Length => ExpectedLength >= 0 ? ExpectedLength : throw new NotSupportedException();
    public sealed override long Position { get => Written; set => throw new NotSupportedException(); }

    public ClientUploadStream(ClientForUrl client, long length, bool append, CancellationToken token) {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        if (length < -1) {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        ExpectedLength = length;
        Token = token;
        Reader = Channel.Reader;
        Writer = Channel.Writer;
        Client.FtpUpload(length, append);
        Client.OnRead(ReadNative);
        Transfer = TransferFile(token);
    }

    public sealed override void Flush() {
        ThrowIfDisposed();
    }

    public sealed override Task FlushAsync(CancellationToken cancellationToken) {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public sealed override void Write(byte[] buffer, int offset, int count) {
        ValidateBufferArguments(buffer, offset, count);
        WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
    }

    public sealed override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        Token.ThrowIfCancellationRequested();
        if (buffer.Length == 0) {
            return;
        }
        var exceedsExpectedLength = ExpectedLength >= 0 && buffer.Length > ExpectedLength - Written;
        if (exceedsExpectedLength) {
            throw new IOException("The FTP upload source exceeded the expected length.");
        }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(Token, cancellationToken);
        await Writer.WriteAsync(buffer.ToArray(), linked.Token).ConfigureAwait(false);
        Written += buffer.Length;
    }

    public sealed override int Read(byte[] buffer, int offset, int count) {
        throw new NotSupportedException();
    }

    public sealed override long Seek(long offset, SeekOrigin origin) {
        throw new NotSupportedException();
    }

    public sealed override void SetLength(long value) {
        throw new NotSupportedException();
    }

    public sealed override async ValueTask DisposeAsync() {
        if (Disposed) {
            return;
        }
        Disposed = true;
        Writer.TryComplete();
        try {
            await Transfer.ConfigureAwait(false);
        }
        finally {
            Client.Dispose();
            base.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    protected sealed override void Dispose(bool disposing) {
        if (Disposed) {
            return;
        }
        Disposed = true;
        if (disposing) {
            Writer.TryComplete();
            try {
                Transfer.GetAwaiter().GetResult();
            }
            finally {
                Client.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}
