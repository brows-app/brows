using Brows.Composition;
using Brows.Native;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

public sealed partial class GitNative : NativeLoader, IExportAndInit {
    [LibraryImport(Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])] private static partial int brows_git_init();
    [LibraryImport(Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])] private static partial int brows_git_exit();

    internal static string String(IntPtr p) {
        return Marshal.PtrToStringUTF8(p);
    }

    internal static NativeString String(string s) {
        return new NativeString(Encoding.UTF8, s);
    }

    protected sealed override void HandleLoaded() {
        Try(brows_git_init);
    }

    protected sealed override void HandleFreeing() {
        Try(brows_git_exit);
    }

    public new const string Dll = "brows_git.dll";
    public const CallingConvention Call = CallingConvention.Cdecl;

    public GitNative() : base(Dll) {
    }

    Task IExportAndInit.Init(IExportContext context, CancellationToken token) {
        return Loaded(token);
    }
}
