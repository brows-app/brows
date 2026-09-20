using Brows.Native;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Git; 
public sealed partial class GitRef : NativeType {
    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])] private static partial void brows_GitRef_destroy(IntPtr p);
    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])] private static partial IntPtr brows_GitRef_get_name(IntPtr p);
    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])] private static partial int brows_GitRef_compare_remote(IntPtr p, ref GitRepoComparison r);

    internal IntPtr P { get; }
    internal object Locker { get; }

    internal GitRef(IntPtr p, object locker) {
        P = p;
        Locker = locker;
    }

    protected sealed override nint Create() {
        return P;
    }

    protected sealed override void Destroy(nint handle) {
        brows_GitRef_destroy(P);
    }

    public string Name => GitNative.String(brows_GitRef_get_name(P));

    public Task<GitRepoComparison> CompareRemote(CancellationToken token) {
        return Task.Run(cancellationToken: token, function: () => {
            var r = GitRepoComparison.Unknown;
            lock (Locker) {
                Try(p => brows_GitRef_compare_remote(p, ref r));
            }
            return r;
        });
    }
}
