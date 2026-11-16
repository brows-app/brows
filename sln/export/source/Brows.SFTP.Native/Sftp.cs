using Brows.Native;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Brows.SFTP.Native;

internal sealed class Sftp : NativeType {
    private static readonly Encoding Utf8 = new UTF8Encoding(false);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern IntPtr brows_ssh_Sftp_create(IntPtr connection);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_Sftp_ready(IntPtr p, ref BrowsCanceler cancel);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern void brows_ssh_Sftp_destroy(IntPtr p);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern uint brows_ssh_Sftp_get_last_error(IntPtr p);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern IntPtr brows_ssh_SftpDir_create(IntPtr sftp, IntPtr path);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern IntPtr brows_ssh_SftpFile_create(IntPtr sftp, IntPtr path, uint flags, uint mode);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_Sftp_stat(IntPtr p,
                                                  IntPtr path,
                                                  int lstat,
                                                  IntPtr item,
                                                  ref BrowsCanceler cancel);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_Sftp_mkdir(IntPtr p, IntPtr path, uint mode, ref BrowsCanceler cancel);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_Sftp_unlink(IntPtr p, IntPtr path, ref BrowsCanceler cancel);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_Sftp_rmdir(IntPtr p, IntPtr path, ref BrowsCanceler cancel);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_Sftp_rename(IntPtr p,
                                                    IntPtr source,
                                                    IntPtr destination,
                                                    ref BrowsCanceler cancel);

    protected sealed override IntPtr Create() {
        return brows_ssh_Sftp_create(Connection.Handle);
    }

    protected sealed override void Destroy(IntPtr handle) {
        brows_ssh_Sftp_destroy(handle);
    }

    internal SftpConnection Connection { get; }
    internal uint LastError => brows_ssh_Sftp_get_last_error(GetHandle());

    internal Sftp(SftpConnection connection) {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    internal void Ready(CancellationToken token) {
        var cancel = BrowsCanceler.From(token);
        Try(() => brows_ssh_Sftp_ready(GetHandle(), ref cancel));
    }

    internal SftpDir OpenDirectory(string path) {
        return new SftpDir(this, path);
    }

    internal SftpFile OpenFile(string path, uint flags, uint mode) {
        return new SftpFile(this, path, flags, mode);
    }

    internal SftpMetadata Stat(string path, bool link, CancellationToken token) {
        using var item = new SftpItem();
        using var nativePath = new NativeString(Utf8, path);
        var cancel = BrowsCanceler.From(token);
        Try(() => brows_ssh_Sftp_stat(
            GetHandle(),
            nativePath.Handle,
            link ? 1 : 0,
            item.GetHandle(),
            ref cancel));
        return new SftpMetadata(item.Name, item.Flags, item.Size, item.Uid, item.Gid, item.Permissions, item.Atime, item.Mtime);
    }

    internal void CreateDirectory(string path, uint mode, CancellationToken token) {
        using var nativePath = new NativeString(Utf8, path);
        var cancel = BrowsCanceler.From(token);
        Try(() => brows_ssh_Sftp_mkdir(GetHandle(), nativePath.Handle, mode, ref cancel));
    }

    internal void DeleteFile(string path, CancellationToken token) {
        using var nativePath = new NativeString(Utf8, path);
        var cancel = BrowsCanceler.From(token);
        Try(() => brows_ssh_Sftp_unlink(GetHandle(), nativePath.Handle, ref cancel));
    }

    internal void DeleteDirectory(string path, CancellationToken token) {
        using var nativePath = new NativeString(Utf8, path);
        var cancel = BrowsCanceler.From(token);
        Try(() => brows_ssh_Sftp_rmdir(GetHandle(), nativePath.Handle, ref cancel));
    }

    internal void Rename(string source, string destination, CancellationToken token) {
        using var nativeSource = new NativeString(Utf8, source);
        using var nativeDestination = new NativeString(Utf8, destination);
        var cancel = BrowsCanceler.From(token);
        Try(() => brows_ssh_Sftp_rename(GetHandle(), nativeSource.Handle, nativeDestination.Handle, ref cancel));
    }
}
