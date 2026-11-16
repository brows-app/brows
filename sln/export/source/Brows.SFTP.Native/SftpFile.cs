using Brows.Native;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Brows.SFTP.Native;

internal sealed class SftpFile : NativeType {
    private static readonly Encoding Utf8 = new UTF8Encoding(false);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern IntPtr brows_ssh_SftpFile_create(IntPtr sftp, IntPtr path, uint flags, uint mode);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_SftpFile_ready(IntPtr p, ref BrowsCanceler cancel);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_SftpFile_read(IntPtr p,
                                                      IntPtr buffer,
                                                      nuint capacity,
                                                      ref nuint result,
                                                      ref BrowsCanceler cancel);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_SftpFile_write(IntPtr p,
                                                       IntPtr buffer,
                                                       nuint length,
                                                       ref nuint result,
                                                       ref BrowsCanceler cancel);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_SftpFile_close(IntPtr p, ref BrowsCanceler cancel);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern void brows_ssh_SftpFile_destroy(IntPtr p);

    private static void ValidateBuffer(byte[] buffer, int offset, int count) {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (buffer.Length - offset < count) {
            throw new ArgumentException("Offset and count were out of bounds for the array.");
        }
    }

    protected sealed override IntPtr Create() {
        using var nativePath = new NativeString(Utf8, Path);
        return brows_ssh_SftpFile_create(Sftp.GetHandle(), nativePath.Handle, Flags, Mode);
    }

    protected sealed override void Destroy(IntPtr handle) {
        brows_ssh_SftpFile_destroy(handle);
    }

    internal Sftp Sftp { get; }
    internal string Path { get; }
    internal uint Flags { get; }
    internal uint Mode { get; }

    internal SftpFile(Sftp sftp, string path, uint flags, uint mode) {
        Sftp = sftp ?? throw new ArgumentNullException(nameof(sftp));
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Flags = flags;
        Mode = mode;
    }

    internal void Ready(CancellationToken token) {
        var cancel = BrowsCanceler.From(token);
        Try(() => brows_ssh_SftpFile_ready(GetHandle(), ref cancel));
    }

    internal int Read(byte[] buffer, int offset, int count, CancellationToken token) {
        ValidateBuffer(buffer, offset, count);
        if (count == 0) {
            return 0;
        }
        var pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try {
            var result = default(nuint);
            var cancel = BrowsCanceler.From(token);
            Try(() => brows_ssh_SftpFile_read(
                GetHandle(),
                pin.AddrOfPinnedObject() + offset,
                checked((nuint)count),
                ref result,
                ref cancel));
            return checked((int)result);
        }
        finally {
            pin.Free();
        }
    }

    internal int Write(byte[] buffer, int offset, int count, CancellationToken token) {
        ValidateBuffer(buffer, offset, count);
        if (count == 0) {
            return 0;
        }
        var pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try {
            var result = default(nuint);
            var cancel = BrowsCanceler.From(token);
            Try(() => brows_ssh_SftpFile_write(
                GetHandle(),
                pin.AddrOfPinnedObject() + offset,
                checked((nuint)count),
                ref result,
                ref cancel));
            return checked((int)result);
        }
        finally {
            pin.Free();
        }
    }

    internal void Close(CancellationToken token) {
        var cancel = BrowsCanceler.From(token);
        Try(() => brows_ssh_SftpFile_close(GetHandle(), ref cancel));
    }
}
