using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows;

internal sealed class ZipEntryValidationStream : Stream {
    private static readonly uint[] CrcTable = CreateCrcTable();

    private readonly Stream Inner;
    private readonly byte[] Probe = new byte[1];
    private readonly long ExpectedLength;
    private readonly uint ExpectedCrc32;

    private uint CrcState = uint.MaxValue;
    private long BytesRead;
    private bool IsValidated;
    private bool IsDisposed;

    /*
     * Validate complete reads, including consumers that stop at the declared length.
     * Partial previews may close early; disposal must not drain an arbitrarily large entry.
     */
    private bool ReachedExpectedLengthAndNeedsValidation => BytesRead == ExpectedLength && IsValidated is false;

    private static uint[] CreateCrcTable() {
        var table = new uint[256];
        for (var i = 0; i < table.Length; i++) {
            var crc = (uint)i;
            for (var bit = 0; bit < 8; bit++) {
                var hasLowBit = (crc & 1) != 0;
                crc = hasLowBit
                    ? 0xedb88320U ^ (crc >> 1)
                    : crc >> 1;
            }
            table[i] = crc;
        }
        return table;
    }

    private void CheckReadable() {
        if (IsDisposed) {
            throw new ObjectDisposedException(nameof(ZipEntryValidationStream));
        }
    }

    private void CheckLength(int read) {
        if (read == 0) {
            if (BytesRead != ExpectedLength) {
                throw new InvalidDataException(
                    $"ZIP entry ended after {BytesRead} bytes; expected {ExpectedLength}.");
            }
            return;
        }

        if (read > ExpectedLength - BytesRead) {
            throw new InvalidDataException(
                $"ZIP entry exceeded its expected length of {ExpectedLength} bytes.");
        }
    }

    private void AddCrc(ReadOnlySpan<byte> buffer) {
        foreach (var value in buffer) {
            var index = (int)((CrcState ^ value) & 0xff);
            CrcState = CrcTable[index] ^ (CrcState >> 8);
        }
    }

    private void ValidateCrc() {
        var actual = ~CrcState;
        if (actual != ExpectedCrc32) {
            throw new InvalidDataException(
                $"ZIP entry CRC-32 mismatch; expected 0x{ExpectedCrc32:x8}, got 0x{actual:x8}.");
        }
        IsValidated = true;
    }

    private void CompleteRead(int read, ReadOnlySpan<byte> buffer) {
        CheckLength(read);
        if (read == 0) {
            ValidateCrc();
            return;
        }

        AddCrc(buffer[..read]);
        BytesRead += read;
    }

    private void ValidateNoExtraBytes(int read) {
        if (read != 0) {
            throw new InvalidDataException(
                $"ZIP entry exceeded its expected length of {ExpectedLength} bytes.");
        }
        ValidateCrc();
    }

    private async ValueTask ValidateNoExtraBytesAsync(CancellationToken token) {
        var read = await Inner.ReadAsync(Probe.AsMemory(), token);
        ValidateNoExtraBytes(read);
    }

    internal ZipEntryValidationStream(Stream stream, long expectedLength, uint expectedCrc32) {
        Inner = stream ?? throw new ArgumentNullException(nameof(stream));
        if (expectedLength < 0) {
            throw new ArgumentOutOfRangeException(nameof(expectedLength));
        }
        ExpectedLength = expectedLength;
        ExpectedCrc32 = expectedCrc32;
    }

    protected override void Dispose(bool disposing) {
        if (IsDisposed == false) {
            IsDisposed = true;
            if (disposing) {
                Inner.Dispose();
            }
        }
        base.Dispose(disposing);
    }

    public override bool CanRead => IsDisposed == false && Inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => ExpectedLength;

    public override long Position {
        get => BytesRead;
        set => throw new NotSupportedException();
    }

    public override void Flush() {
        throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) {
        ValidateBufferArguments(buffer, offset, count);
        CheckReadable();
        if (count == 0) {
            return 0;
        }
        if (IsValidated) {
            return 0;
        }
        var read = Inner.Read(buffer, offset, count);
        CompleteRead(read, buffer.AsSpan(offset, read));
        if (ReachedExpectedLengthAndNeedsValidation) {
            var probeRead = Inner.Read(Probe, 0, Probe.Length);
            ValidateNoExtraBytes(probeRead);
        }
        return read;
    }

    public override int Read(Span<byte> buffer) {
        CheckReadable();
        if (buffer.IsEmpty) {
            return 0;
        }
        if (IsValidated) {
            return 0;
        }
        var read = Inner.Read(buffer);
        CompleteRead(read, buffer[..read]);
        if (ReachedExpectedLengthAndNeedsValidation) {
            var probeRead = Inner.Read(Probe.AsSpan());
            ValidateNoExtraBytes(probeRead);
        }
        return read;
    }

    public override async Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) {
        ValidateBufferArguments(buffer, offset, count);
        CheckReadable();
        if (count == 0) {
            return 0;
        }
        if (IsValidated) {
            return 0;
        }
        var read = await Inner.ReadAsync(buffer, offset, count, cancellationToken);
        CompleteRead(read, buffer.AsSpan(offset, read));
        if (ReachedExpectedLengthAndNeedsValidation) {
            await ValidateNoExtraBytesAsync(cancellationToken);
        }
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
        CheckReadable();
        if (buffer.IsEmpty) {
            return 0;
        }
        if (IsValidated) {
            return 0;
        }
        var read = await Inner.ReadAsync(buffer, cancellationToken);
        CompleteRead(read, buffer.Span[..read]);
        if (ReachedExpectedLengthAndNeedsValidation) {
            await ValidateNoExtraBytesAsync(cancellationToken);
        }
        return read;
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

    public override async ValueTask DisposeAsync() {
        if (IsDisposed == false) {
            IsDisposed = true;
            await Inner.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}
