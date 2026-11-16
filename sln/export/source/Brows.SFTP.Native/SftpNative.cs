using Brows.Native;
using System;
using System.Runtime.InteropServices;

namespace Brows.SFTP.Native;

internal sealed class SftpNative : NativeLoader {
    internal const string Library = "brows_ssh.dll";
    internal const CallingConvention Call = CallingConvention.Cdecl;

    [DllImport(Library, CallingConvention = Call)]
    private static extern int brows_ssh_init();

    [DllImport(Library, CallingConvention = Call)]
    private static extern int brows_ssh_exit();

    [DllImport(Library, CallingConvention = Call)]
    private static extern IntPtr brows_ssh_name(int error);

    protected sealed override void HandleLoaded() {
        Try(brows_ssh_init);
    }

    protected sealed override void HandleFreeing() {
        Try(brows_ssh_exit);
    }

    internal static string Name(int error) {
        var name = brows_ssh_name(error);
        return name == IntPtr.Zero
            ? null
            : Marshal.PtrToStringAnsi(name);
    }

    internal SftpNative() : base(Library) {
    }
}
