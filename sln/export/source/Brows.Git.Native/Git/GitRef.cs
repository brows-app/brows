using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Git;

internal sealed partial class GitRef : NativeType {
    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void brows_GitRef_destroy(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr brows_GitRef_get_name(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int brows_GitRef_compare_remote(IntPtr p, ref GitRepoComparison r);

    private readonly Lock Locker;
    private bool _Disposed;
    private IntPtr _Native;

    internal GitRef(IntPtr p, Lock locker) {
        Locker = locker ?? throw new ArgumentNullException(nameof(locker));
        _Native = p != IntPtr.Zero ? p :
            throw new ArgumentException("A native reference is required.", nameof(p));
    }

    protected sealed override IntPtr Create() {
        lock (Locker) {
            ObjectDisposedException.ThrowIf(_Disposed, this);
            var p = _Native;
            _Native = IntPtr.Zero;
            return p;
        }
    }

    protected sealed override void Destroy(IntPtr handle) {
        lock (Locker) {
            brows_GitRef_destroy(handle);
        }
    }

    protected sealed override void Dispose(bool disposing) {
        lock (Locker) {
            if (!_Disposed) {
                _Disposed = true;
                if (_Native != IntPtr.Zero) {
                    var p = _Native;
                    _Native = IntPtr.Zero;
                    brows_GitRef_destroy(p);
                }
            }
            base.Dispose(disposing);
        }
    }

    public string Name {
        get {
            lock (Locker) {
                return GitNative.StringOrEmpty(brows_GitRef_get_name(GetHandle()));
            }
        }
    }

    public Task<GitRepoComparison> CompareRemote(CancellationToken token) {
        return Task.Run(cancellationToken: token, function: () => {
            var r = GitRepoComparison.Unknown;
            lock (Locker) {
                token.ThrowIfCancellationRequested();
                Try(p => brows_GitRef_compare_remote(p, ref r), error => GitNative.CreateError(error));
            }
            return r;
        });
    }
}
