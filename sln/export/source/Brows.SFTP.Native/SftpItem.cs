using Brows.Native;
using System;
using System.Runtime.InteropServices;

namespace Brows.SFTP.Native;

internal sealed class SftpItem : NativeType {
    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern IntPtr brows_ssh_SftpItem_create();

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern void brows_ssh_SftpItem_destroy(IntPtr p);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern IntPtr brows_ssh_SftpItem_get_name(IntPtr p);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern uint brows_ssh_SftpItem_get_flags(IntPtr p);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern ulong brows_ssh_SftpItem_get_size(IntPtr p);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern uint brows_ssh_SftpItem_get_uid(IntPtr p);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern uint brows_ssh_SftpItem_get_gid(IntPtr p);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern uint brows_ssh_SftpItem_get_permissions(IntPtr p);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern uint brows_ssh_SftpItem_get_atime(IntPtr p);

    [DllImport(SftpNative.Library, CallingConvention = SftpNative.Call)]
    private static extern uint brows_ssh_SftpItem_get_mtime(IntPtr p);

    protected sealed override IntPtr Create() {
        return brows_ssh_SftpItem_create();
    }

    protected sealed override void Destroy(IntPtr handle) {
        brows_ssh_SftpItem_destroy(handle);
    }

    internal IntPtr Handle => GetHandle();
    internal string Name => Marshal.PtrToStringUTF8(brows_ssh_SftpItem_get_name(GetHandle()));
    internal uint Flags => brows_ssh_SftpItem_get_flags(GetHandle());
    internal ulong Size => brows_ssh_SftpItem_get_size(GetHandle());
    internal uint Uid => brows_ssh_SftpItem_get_uid(GetHandle());
    internal uint Gid => brows_ssh_SftpItem_get_gid(GetHandle());
    internal uint Permissions => brows_ssh_SftpItem_get_permissions(GetHandle());
    internal uint Atime => brows_ssh_SftpItem_get_atime(GetHandle());
    internal uint Mtime => brows_ssh_SftpItem_get_mtime(GetHandle());
}
