using Brows.Native;
using System;
using System.Runtime.InteropServices;

namespace Brows.SFTP.Native;

internal sealed class SftpKnownHost : NativeType {
    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern IntPtr brows_ssh_KnownHost_create();

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern void brows_ssh_KnownHost_destroy(IntPtr p);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern IntPtr brows_ssh_KnownHost_get_key_base64(IntPtr p);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern IntPtr brows_ssh_KnownHost_get_name_plain(IntPtr p);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern KnownHostStatus brows_ssh_KnownHost_get_status(IntPtr p);

    protected sealed override IntPtr Create() {
        return brows_ssh_KnownHost_create();
    }

    protected sealed override void Destroy(IntPtr handle) {
        brows_ssh_KnownHost_destroy(handle);
    }

    internal IntPtr Handle => GetHandle();
    internal string KeyBase64 => Marshal.PtrToStringAnsi(brows_ssh_KnownHost_get_key_base64(GetHandle()));
    internal string NamePlain => Marshal.PtrToStringAnsi(brows_ssh_KnownHost_get_name_plain(GetHandle()));
    internal KnownHostStatus Status => brows_ssh_KnownHost_get_status(GetHandle());
}
