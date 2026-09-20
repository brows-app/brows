using Brows.Native;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Git; 
public sealed partial class GitRepo : NativeType {
    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])] private static partial int brows_GitRepo_find(IntPtr path, int search, ref IntPtr p);
    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])] private static partial void brows_GitRepo_destroy(IntPtr p);
    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])] private static partial IntPtr brows_GitRepo_get_path(IntPtr p);
    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])] private static partial GitRepoState brows_GitRepo_get_state(IntPtr p);
    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])] private static partial int brows_GitRepo_config_entry(IntPtr p, IntPtr name, ref IntPtr result);
    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])] private static partial int brows_GitRepo_head(IntPtr p, ref IntPtr r);

    private readonly object Locker = new();

    protected override nint Create() {
        return P;
    }

    protected override void Destroy(nint handle) {
        brows_GitRepo_destroy(handle);
    }

    public GitRepoState State => brows_GitRepo_get_state(GetHandle());

    public string Path => _Path ??= GitNative.String(brows_GitRepo_get_path(P));
    private string _Path;

    private IntPtr P { get; }

    private GitRepo(IntPtr p) {
        P = p;
    }

    public Task<GitRef> Head(CancellationToken token) {
        return Task.Run(cancellationToken: token, function: () => {
            var r = default(IntPtr);
            lock (Locker) {
                Try(p => brows_GitRepo_head(p, ref r));
            }
            return new GitRef(r, Locker);
        });
    }

    public Task<GitConfigEntry> ConfigEntry(string name, CancellationToken token) {
        return Task.Run(cancellationToken: token, function: () => {
            using (var s = GitNative.String(name)) {
                var h = s.Handle;
                var r = default(IntPtr);
                lock (Locker) {
                    Try(p => brows_GitRepo_config_entry(p, h, ref r));
                }
                return new GitConfigEntry(r);
            }
        });
    }

    public static Task<GitRepo> Find(string path, bool search, CancellationToken token) {
        return Task.Run(cancellationToken: token, function: () => {
            using (var s = GitNative.String(path)) {
                var h = s.Handle;
                var p = default(IntPtr);
                var err = brows_GitRepo_find(h, search ? 1 : 0, ref p);
                if (err == 0) {
                    if (p != default) {
                        return new GitRepo(p);
                    }
                    return null;
                }
                throw new NativeErrorException(err);
            }
        });
    }
}
