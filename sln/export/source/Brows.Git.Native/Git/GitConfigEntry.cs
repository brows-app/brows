using Brows.Native;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Brows.Git; 
public sealed partial class GitConfigEntry : NativeType {
    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])] private static partial void brows_GitConfigEntry_destroy(IntPtr p);
    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])] private static partial uint brows_GitConfigEntry_get_include_depth(IntPtr p);
    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])] private static partial GitConfigLevel brows_GitConfigEntry_get_level(IntPtr p);
    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])] private static partial IntPtr brows_GitConfigEntry_get_name(IntPtr p);
    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])] private static partial IntPtr brows_GitConfigEntry_get_source_kind(IntPtr p);
    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])] private static partial IntPtr brows_GitConfigEntry_get_source_path(IntPtr p);
    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])] private static partial IntPtr brows_GitConfigEntry_get_value(IntPtr p);

    internal IntPtr P { get; }

    internal GitConfigEntry(IntPtr p) {
        P = p;
    }

    protected sealed override nint Create() {
        return P;
    }

    protected sealed override void Destroy(nint handle) {
        brows_GitConfigEntry_destroy(handle);
    }

    public string Name => _Name ??= GitNative.String(brows_GitConfigEntry_get_name(P));
    private string _Name;

    public string Value => _Value ??= GitNative.String(brows_GitConfigEntry_get_value(P));
    private string _Value;

    public string SourceKind => _SourceKind ??= GitNative.String(brows_GitConfigEntry_get_source_kind(P));
    private string _SourceKind;

    public string SourcePath => _SourcePath ??= GitNative.String(brows_GitConfigEntry_get_source_path(P));
    private string _SourcePath;

    public int IncludeDepth => _IncludeDepth ??= Convert.ToInt32(brows_GitConfigEntry_get_include_depth(P));
    private int? _IncludeDepth;

    public GitConfigLevel Level => _Level ??= brows_GitConfigEntry_get_level(P);
    private GitConfigLevel? _Level;
}
