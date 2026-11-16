using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Brows.Git;

internal sealed partial class GitConfigEntry : NativeType {
    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void brows_GitConfigEntry_destroy(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial uint brows_GitConfigEntry_get_include_depth(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial GitConfigLevel brows_GitConfigEntry_get_level(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr brows_GitConfigEntry_get_name(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr brows_GitConfigEntry_get_source_kind(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr brows_GitConfigEntry_get_source_path(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr brows_GitConfigEntry_get_value(IntPtr p);

    private readonly Lock Locker = new();
    private bool _Disposed;
    private IntPtr _Native;

    internal GitConfigEntry(IntPtr p) {
        _Native = p != IntPtr.Zero ? p :
            throw new ArgumentException("A native config entry is required.", nameof(p));
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
            brows_GitConfigEntry_destroy(handle);
        }
    }

    protected sealed override void Dispose(bool disposing) {
        lock (Locker) {
            if (_Disposed != true) {
                _Disposed = true;
                if (_Native != IntPtr.Zero) {
                    var p = _Native;
                    _Native = IntPtr.Zero;
                    brows_GitConfigEntry_destroy(p);
                }
            }
            base.Dispose(disposing);
        }
    }

    public string Name {
        get {
            lock (Locker) {
                ObjectDisposedException.ThrowIf(_Disposed, this);
                return _Name ??= GitNative.StringOrEmpty(brows_GitConfigEntry_get_name(GetHandle()));
            }
        }
    }
    private string _Name;

    public string Value {
        get {
            lock (Locker) {
                ObjectDisposedException.ThrowIf(_Disposed, this);
                return _Value ??= GitNative.StringOrEmpty(brows_GitConfigEntry_get_value(GetHandle()));
            }
        }
    }
    private string _Value;

    public string SourceKind {
        get {
            lock (Locker) {
                ObjectDisposedException.ThrowIf(_Disposed, this);
                return _SourceKind ??= GitNative.StringOrEmpty(brows_GitConfigEntry_get_source_kind(GetHandle()));
            }
        }
    }
    private string _SourceKind;

    public string SourcePath {
        get {
            lock (Locker) {
                ObjectDisposedException.ThrowIf(_Disposed, this);
                return _SourcePath ??= GitNative.StringOrEmpty(brows_GitConfigEntry_get_source_path(GetHandle()));
            }
        }
    }
    private string _SourcePath;

    public int IncludeDepth {
        get {
            lock (Locker) {
                ObjectDisposedException.ThrowIf(_Disposed, this);
                return _IncludeDepth ??= Convert.ToInt32(brows_GitConfigEntry_get_include_depth(GetHandle()));
            }
        }
    }
    private int? _IncludeDepth;

    public GitConfigLevel Level {
        get {
            lock (Locker) {
                ObjectDisposedException.ThrowIf(_Disposed, this);
                return _Level ??= brows_GitConfigEntry_get_level(GetHandle());
            }
        }
    }
    private GitConfigLevel? _Level;
}
