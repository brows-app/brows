using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.SevenZip;

internal sealed partial class SevenZipNative : NativeLoader, IExportAndInit {
    [LibraryImport(Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int brows_7z_init();

    [LibraryImport(Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int brows_7z_exit();

    [LibraryImport(Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial nint brows_7z_last_error_message();

    internal static string LastErrorMessage() {
        var p = brows_7z_last_error_message();
        return p == IntPtr.Zero
            ? string.Empty
            : Marshal.PtrToStringUTF8(p) ?? string.Empty;
    }

    internal static SevenZipException CreateError(int error) {
        return new SevenZipException(error, LastErrorMessage());
    }

    protected sealed override void HandleLoaded() {
        Try(brows_7z_init);
    }

    protected sealed override void HandleFreeing() {
        Try(brows_7z_exit);
    }

    public new const string Dll = "brows_7z.dll";
    public const CallingConvention Call = CallingConvention.Cdecl;

    public SevenZipNative() : base(Dll) {
    }

    Task IExportAndInit.Init(IExportContext context, CancellationToken token) {
        return Loaded(token);
    }
}
