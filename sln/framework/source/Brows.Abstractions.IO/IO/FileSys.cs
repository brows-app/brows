using Domore.Logs;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO;

internal sealed class FileSys : IFileSys {
    private static readonly ILog Log = Logging.For(typeof(FileSys));

    private static async Task<T> TryThrow<T>(string path,
                                             FileSysArg arg,
                                             Func<string, CancellationToken, Task<T>> function,
                                             CancellationToken token) {
        if (function is null) {
            throw new ArgumentNullException(nameof(function));
        }
        if (arg is null) {
            throw new ArgumentNullException(nameof(arg));
        }
        var resiliency = 0;
        for (; ; ) {
            if (token.IsCancellationRequested) {
                token.ThrowIfCancellationRequested();
            }
            try {
                var task = function(path, token);
                if (task is null) {
                    return default;
                }
                return await task.ConfigureAwait(false);
            }
            catch (Exception ex) {
                if (ex is UnauthorizedAccessException) {
                    if (arg.ThrowOnUnauthorizedAccess) {
                        throw;
                    }
                    return default;
                }
                if (ex is DirectoryNotFoundException or FileNotFoundException) {
                    if (arg.ThrowOnNotFound) {
                        throw;
                    }
                    return default;
                }
                if (ex is IOException) {
                    if (resiliency >= arg.Resiliency) {
                        throw;
                    }
                    var delay = arg.ResiliencyDelay;
                    if (delay > 0) {
                        await Task.Delay(delay, token).ConfigureAwait(false);
                    }
                    resiliency++;
                }
                else {
                    throw;
                }
            }
        }
    }

    private static async Task<T> Handle<T>(string path,
                                           FileSysArg arg,
                                           Func<string, CancellationToken, Task<T>> function,
                                           CancellationToken token) {
        if (arg is null) {
            arg = new();
        }
        try {
            return await TryThrow(path, arg, function, token);
        }
        catch (Exception ex) {
            var errorHandler = arg.ErrorHandler;
            if (errorHandler is null) {
                throw;
            }
            var isCancellation = ex is OperationCanceledException && token.IsCancellationRequested;
            if (isCancellation) {
                if (Log.Debug()) {
                    Log.Debug("FILE-SYSTEM CANCELLATION",
                              ex);
                }
            }
            else {
                if (Log.Warn()) {
                    Log.Warn("FILE-SYSTEM ERROR",
                             ex);
                }
            }
            errorHandler.Handle(new(path, ex), isCancellation);
        }
        return default;
    }

    private static async Task Handle(string path,
                                     FileSysArg arg,
                                     Func<string, CancellationToken, Task> function,
                                     CancellationToken token) {
        if (function is null) {
            throw new ArgumentNullException(nameof(function));
        }
        await Handle(arg: arg, path: path, token: token, function: async (path, token) => {
            var task = function(path, token);
            if (task is not null) {
                await task;
            }
            return default(object);
        });
    }

    public Task<DirectoryInfo> CreateDirectory(string path, FileSysArg arg, CancellationToken token) {
        return Handle(
            arg: arg,
            path: path,
            token: token,
            function: (path, token) => Task.Run(() => Directory.CreateDirectory(path), token));
    }

    public Task<string[]> GetDirectoryFiles(string path, FileSysArg arg, CancellationToken token) {
        return Handle(
            arg: arg,
            path: path,
            token: token,
            function: (path, token) => Task.Run(() => Directory.GetFiles(path), token));
    }

    public Task<string[]> GetDirectoryFiles(string path,
                                            string searchPattern,
                                            SearchOption searchOption,
                                            FileSysArg arg,
                                            CancellationToken token) {
        return Handle(
            arg: arg,
            path: path,
            token: token,
            function: (path, token) => Task.Run(() => Directory.GetFiles(path, searchPattern, searchOption), token));
    }

    public Task<string[]> GetDirectoryDirectories(string path, FileSysArg arg, CancellationToken token) {
        return Handle(
            arg: arg,
            path: path,
            token: token,
            function: (path, token) => Task.Run(() => Directory.GetDirectories(path), token));
    }

    public Task<string[]> GetDirectoryDirectories(string path,
                                                  string searchPattern,
                                                  SearchOption searchOption,
                                                  FileSysArg arg,
                                                  CancellationToken token) {
        return Handle(
            arg: arg,
            path: path,
            token: token,
            function: (path, token) => Task.Run(() => Directory.GetDirectories(path,
                                                                               searchPattern,
                                                                               searchOption),
                                                                               token));
    }

    public Task DeleteDirectory(string path, bool recursive, FileSysArg arg, CancellationToken token) {
        return Handle(
            arg: arg,
            path: path,
            token: token,
            function: (path, token) => Task.Run(() => Directory.Delete(path, recursive), token));
    }

    public Task<string> ReadFileText(string path, FileSysArg arg, CancellationToken token) {
        return Handle(
            arg: arg,
            path: path,
            token: token,
            function: (path, token) => Task.Run(() =>
#if NETFRAMEWORK
                    Task.Run(() => File.ReadAllText(path), token)
#else
                    File.ReadAllTextAsync(path, token)
#endif
            ));
    }

    public Task WriteFileText(string path, string contents, FileSysArg arg, CancellationToken token) {
        return Handle(
            arg: arg,
            path: path,
            token: token,
            function: (path, token) => Task.Run(() =>
#if NETFRAMEWORK
                    Task.Run(() => File.WriteAllText(path, contents), token)
#else
                    File.WriteAllTextAsync(path, contents, token)
#endif
            ));
    }

    public Task DeleteFile(string path, FileSysArg arg, CancellationToken token) {
        return Handle(
            arg: arg,
            path: path,
            token: token,
            function: (path, token) => Task.Run(() => File.Delete(path), token));
    }

    public Task<Stream> OpenWrite(string path, FileSysArg arg, CancellationToken token) {
        return Handle(
            arg: arg,
            path: path,
            token: token,
            function: (path, token) => Task.Run(() => (Stream)File.OpenWrite(path), token));
    }

    public Task<Stream> OpenRead(string path, FileSysArg arg, CancellationToken token) {
        return Handle(
            arg: arg,
            path: path,
            token: token,
            function: (path, token) => Task.Run(() => (Stream)File.OpenRead(path), token));
    }

    public Task<bool> FileExists(string path, FileSysArg arg, CancellationToken token) {
        return Handle(
            arg: arg,
            path: path,
            token: token,
            function: (path, token) => Task.Run(() => File.Exists(path), token));
    }

    public Task<bool> DirectoryExists(string path, FileSysArg arg, CancellationToken token) {
        return Handle(
            arg: arg,
            path: path,
            token: token,
            function: (path, token) => Task.Run(() => Directory.Exists(path), token));
    }

    public Task<FileInfo> FileInfo(string path, FileSysArg arg, CancellationToken token) {
        return Handle(
            arg: arg,
            path: path,
            token: token,
            function: (path, token) => Task.Run(() => {
                var
                fileInfo = new FileInfo(path);
                fileInfo.Refresh();
                return fileInfo;
            }, token));
    }

    public Task<DirectoryInfo> DirectoryInfo(string path, FileSysArg arg, CancellationToken token) {
        return Handle(
            arg: arg,
            path: path,
            token: token,
            function: (path, token) => Task.Run(() => {
                var
                directoryInfo = new DirectoryInfo(path);
                directoryInfo.Refresh();
                return directoryInfo;
            }, token));
    }
}
