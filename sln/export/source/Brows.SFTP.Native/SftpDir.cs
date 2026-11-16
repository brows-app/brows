using Brows.Native;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Brows.SFTP.Native;

internal sealed class SftpDir : NativeType {
    private static readonly Encoding Utf8 = new UTF8Encoding(false);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern IntPtr brows_ssh_SftpDir_create(IntPtr sftp, IntPtr path);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_SftpDir_ready(IntPtr p, ref BrowsCanceler cancel);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_SftpDir_next(IntPtr p, IntPtr item, ref int end, ref BrowsCanceler cancel);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_SftpDir_close(IntPtr p, ref BrowsCanceler cancel);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern void brows_ssh_SftpDir_destroy(IntPtr p);

    protected sealed override IntPtr Create() {
        using var nativePath = new NativeString(Utf8, Path);
        return brows_ssh_SftpDir_create(Sftp.GetHandle(), nativePath.Handle);
    }

    protected sealed override void Destroy(IntPtr handle) {
        brows_ssh_SftpDir_destroy(handle);
    }

    internal Sftp Sftp { get; }
    internal string Path { get; }

    internal SftpDir(Sftp sftp, string path) {
        Sftp = sftp ?? throw new ArgumentNullException(nameof(sftp));
        Path = path ?? throw new ArgumentNullException(nameof(path));
    }

    internal void Ready(CancellationToken token) {
        var cancel = BrowsCanceler.From(token);
        Try(() => brows_ssh_SftpDir_ready(GetHandle(), ref cancel));
    }

    internal bool Next(SftpItem item, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(item);
        var end = 0;
        var cancel = BrowsCanceler.From(token);
        Try(() => brows_ssh_SftpDir_next(GetHandle(), item.GetHandle(), ref end, ref cancel));
        return end != 0;
    }

    internal void Close(CancellationToken token) {
        var cancel = BrowsCanceler.From(token);
        Try(() => brows_ssh_SftpDir_close(GetHandle(), ref cancel));
    }
}
