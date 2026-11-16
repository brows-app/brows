using Brows.Native;
using System;
using System.Runtime.InteropServices;

namespace Brows.SFTP.Native;

internal sealed class SftpKnownHosts : NativeType {
    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern IntPtr brows_ssh_KnownHosts_create(IntPtr connection);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern void brows_ssh_KnownHosts_destroy(IntPtr p);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_KnownHosts_check(IntPtr p,
                                                         IntPtr file,
                                                         IntPtr name,
                                                         int port,
                                                         IntPtr knownHost);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern int brows_ssh_KnownHosts_trust(IntPtr p,
                                                         IntPtr file,
                                                         IntPtr name,
                                                         int port,
                                                         int replace);

    protected sealed override IntPtr Create() {
        return brows_ssh_KnownHosts_create(Connection.Handle);
    }

    protected sealed override void Destroy(IntPtr handle) {
        brows_ssh_KnownHosts_destroy(handle);
    }

    internal SftpConnection Connection { get; }

    internal SftpKnownHosts(SftpConnection connection) {
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    internal void Check(string file, string name, int port, SftpKnownHost knownHost) {
        ArgumentNullException.ThrowIfNull(knownHost);
        using var nativeFile = NativeGlobalAlloc.String(file, CharSet.Ansi);
        using var nativeName = NativeGlobalAlloc.String(name, CharSet.Ansi);
        Try(() => brows_ssh_KnownHosts_check(
            GetHandle(),
            nativeFile.HGlobal,
            nativeName.HGlobal,
            port,
            knownHost.Handle));
    }

    internal void Trust(string file, string name, int port, bool replace) {
        using var nativeFile = NativeGlobalAlloc.String(file, CharSet.Ansi);
        using var nativeName = NativeGlobalAlloc.String(name, CharSet.Ansi);
        Try(() => brows_ssh_KnownHosts_trust(
            GetHandle(),
            nativeFile.HGlobal,
            nativeName.HGlobal,
            port,
            replace ? 1 : 0));
    }
}

internal enum KnownHostStatus {
    Uninitialized,
    NotFound,
    Match,
    Mismatch,
    Error
}
