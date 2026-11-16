using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Git;

internal sealed partial class GitRepo : NativeType {
    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int brows_GitRepo_find(IntPtr path, int search, ref IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void brows_GitRepo_destroy(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr brows_GitRepo_get_path(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial GitRepoState brows_GitRepo_get_state(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int brows_GitRepo_config_entry(IntPtr p, IntPtr name, ref IntPtr result);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int brows_GitRepo_head(IntPtr p, ref IntPtr r);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int brows_GitRepo_status(IntPtr p, ref IntPtr status);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void brows_GitRef_destroy(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void brows_GitConfigEntry_destroy(IntPtr p);


    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void brows_GitRepositoryStatus_destroy(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr brows_GitRepositoryStatus_get_work_directory(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr brows_GitRepositoryStatus_get_git_directory(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr brows_GitRepositoryStatus_get_branch(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr brows_GitRepositoryStatus_get_head_id(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr brows_GitRepositoryStatus_get_upstream(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial GitRepoState brows_GitRepositoryStatus_get_state(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int brows_GitRepositoryStatus_get_is_unborn(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int brows_GitRepositoryStatus_get_is_detached(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int brows_GitRepositoryStatus_get_is_bare(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int brows_GitRepositoryStatus_get_upstream_missing(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int brows_GitRepositoryStatus_get_has_ahead(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int brows_GitRepositoryStatus_get_ahead(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int brows_GitRepositoryStatus_get_has_behind(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int brows_GitRepositoryStatus_get_behind(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial nuint brows_GitRepositoryStatus_get_file_count(IntPtr p);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr brows_GitRepositoryStatus_get_file_path(IntPtr p, nuint index);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr brows_GitRepositoryStatus_get_file_original_path(IntPtr p, nuint index);

    [LibraryImport(GitNative.Dll), UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial uint brows_GitRepositoryStatus_get_file_status(IntPtr p, nuint index);

    private readonly Lock Locker = new();
    private bool _Disposed;
    private IntPtr _Native;

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
            brows_GitRepo_destroy(handle);
        }
    }

    protected sealed override void Dispose(bool disposing) {
        lock (Locker) {
            if (!_Disposed) {
                _Disposed = true;
                if (_Native != IntPtr.Zero) {
                    var p = _Native;
                    _Native = IntPtr.Zero;
                    brows_GitRepo_destroy(p);
                }
            }
            base.Dispose(disposing);
        }
    }

    public GitRepoState State {
        get {
            lock (Locker) {
                return brows_GitRepo_get_state(GetHandle());
            }
        }
    }

    public string Path {
        get {
            lock (Locker) {
                var p = GetHandle();
                return _Path ??= GitNative.StringOrEmpty(brows_GitRepo_get_path(p));
            }
        }
    }
    private string _Path;

    private GitRepo(IntPtr p) {
        _Native = p != IntPtr.Zero ? p :
            throw new ArgumentException("A native repository is required.", nameof(p));
    }

    public Task<GitRef> Head(CancellationToken token) {
        return Task.Run(cancellationToken: token, function: () => {
            var r = IntPtr.Zero;
            lock (Locker) {
                if (token.IsCancellationRequested) {
                    token.ThrowIfCancellationRequested();
                }
                Try(p => brows_GitRepo_head(p, ref r), error => GitNative.CreateError(error));
            }
            try {
                if (token.IsCancellationRequested) {
                    token.ThrowIfCancellationRequested();
                }
                return new GitRef(r, Locker);
            }
            catch {
                if (r != IntPtr.Zero) {
                    brows_GitRef_destroy(r);
                }
                throw;
            }
        });
    }

    public Task<GitConfigEntry> ConfigEntry(string name, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(name);
        return Task.Run(cancellationToken: token, function: () => {
            using (var s = GitNative.String(name)) {
                var r = IntPtr.Zero;
                lock (Locker) {
                    if (token.IsCancellationRequested) {
                        token.ThrowIfCancellationRequested();
                    }
                    Try(p => brows_GitRepo_config_entry(p, s.Handle, ref r), error => GitNative.CreateError(error));
                }
                try {
                    if (token.IsCancellationRequested) {
                        token.ThrowIfCancellationRequested();
                    }
                    return new GitConfigEntry(r);
                }
                catch {
                    if (r != IntPtr.Zero) {
                        brows_GitConfigEntry_destroy(r);
                    }
                    throw;
                }
            }
        });
    }

    public Task<GitRepositoryStatus> Status(CancellationToken token) {
        return Task.Run(cancellationToken: token, function: () => {
            var status = IntPtr.Zero;
            lock (Locker) {
                if (token.IsCancellationRequested) {
                    token.ThrowIfCancellationRequested();
                }
                Try(p => brows_GitRepo_status(p, ref status), error => GitNative.CreateError(error));
            }
            try {
                if (token.IsCancellationRequested) {
                    token.ThrowIfCancellationRequested();
                }
                if (status == IntPtr.Zero) {
                    throw new OutOfMemoryException();
                }
                var count = checked((int)brows_GitRepositoryStatus_get_file_count(status));
                var files = new GitFileStatus[count];
                for (var i = 0; i < count; i++) {
                    token.ThrowIfCancellationRequested();
                    var index = checked((nuint)i);
                    files[i] = new GitFileStatus(
                        path: GitNative.StringOrEmpty(brows_GitRepositoryStatus_get_file_path(status, index)),
                        originalPath: GitNative.String(brows_GitRepositoryStatus_get_file_original_path(status, index)),
                        status: (GitStatusFlags)brows_GitRepositoryStatus_get_file_status(status, index));
                }

                var ahead = brows_GitRepositoryStatus_get_has_ahead(status) != 0
                    ? brows_GitRepositoryStatus_get_ahead(status)
                    : default(int?);
                var behind = brows_GitRepositoryStatus_get_has_behind(status) != 0
                    ? brows_GitRepositoryStatus_get_behind(status)
                    : default(int?);

                return new GitRepositoryStatus(
                    workDirectory: GitNative.StringOrEmpty(brows_GitRepositoryStatus_get_work_directory(status)),
                    gitDirectory: GitNative.StringOrEmpty(brows_GitRepositoryStatus_get_git_directory(status)),
                    branch: GitNative.StringOrEmpty(brows_GitRepositoryStatus_get_branch(status)),
                    headId: GitNative.StringOrEmpty(brows_GitRepositoryStatus_get_head_id(status)),
                    upstream: GitNative.StringOrEmpty(brows_GitRepositoryStatus_get_upstream(status)),
                    state: brows_GitRepositoryStatus_get_state(status),
                    isUnborn: brows_GitRepositoryStatus_get_is_unborn(status) != 0,
                    isDetached: brows_GitRepositoryStatus_get_is_detached(status) != 0,
                    isBare: brows_GitRepositoryStatus_get_is_bare(status) != 0,
                    upstreamMissing: brows_GitRepositoryStatus_get_upstream_missing(status) != 0,
                    ahead: ahead,
                    behind: behind,
                    files: files);
            }
            finally {
                if (status != IntPtr.Zero) {
                    brows_GitRepositoryStatus_destroy(status);
                }
            }
        });
    }

    public static Task<GitRepo> Find(string path, bool search, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(path);
        return Task.Run(cancellationToken: token, function: () => {
            var p = IntPtr.Zero;
            try {
                if (token.IsCancellationRequested) {
                    token.ThrowIfCancellationRequested();
                }
                using (var s = GitNative.String(path)) {
                    var error = brows_GitRepo_find(s.Handle, search ? 1 : 0, ref p);
                    if (error != 0) {
                        throw GitNative.CreateError(error);
                    }
                }
                if (token.IsCancellationRequested) {
                    token.ThrowIfCancellationRequested();
                }
                return p != IntPtr.Zero ? new GitRepo(p) : null;
            }
            catch {
                if (p != IntPtr.Zero) {
                    brows_GitRepo_destroy(p);
                }
                throw;
            }
        });
    }
}
