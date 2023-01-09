using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO {
    using Collections.Generic;
    using Threading.Tasks;

    public static class DirectoryInfoExtension {
        private static async IAsyncEnumerable<FileSystemInfo> ThisDepth(this DirectoryInfo directoryInfo, [EnumeratorCancellation] CancellationToken cancellationToken, IList<DirectoryInfo> directories) {
            if (null == directoryInfo) throw new ArgumentNullException(nameof(directoryInfo));
            var infos = directoryInfo.EnumerateFileSystemInfosAsync(
                searchPattern: "*",
                enumerationOptions: new EnumerationOptions {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false,
                    ReturnSpecialDirectories = false
                },
                enumerableOptions: new DirectoryEnumerableOptions {
                    Delay = new EnumerableDelayOptions {
                        Limit = 10,
                        Milliseconds = 50,
                        Threshold = 100
                    },
                    Mode = EnumerableMode.Channel
                },
                cancellationToken);

            await foreach (var info in infos) {
                if (cancellationToken.IsCancellationRequested) {
                    break;
                }
                if (info is FileInfo file) {
                    yield return file;
                }
                if (info is DirectoryInfo directory) {
                    directories.Add(directory);
                }
            }
        }

        public static async IAsyncEnumerable<FileSystemInfo> RecurseByDepthAsync(this DirectoryInfo directoryInfo, [EnumeratorCancellation] CancellationToken cancellationToken) {
            var directories = new List<DirectoryInfo>();
            await foreach (var item in directoryInfo.ThisDepth(cancellationToken, directories)) {
                yield return item;
            }
            while (directories.Count > 0) {
                var nextDepth = new List<DirectoryInfo>();
                foreach (var directory in directories) {
                    yield return directory;
                    nextDepth.Add(directory);
                }
                directories.Clear();
                foreach (var directory in nextDepth) {
                    await foreach (var info in directory.ThisDepth(cancellationToken, directories)) {
                        yield return info;
                    }
                }
            }
        }

        public static IAsyncEnumerable<FileSystemInfo> EnumerateDirectoriesAsync(this DirectoryInfo directoryInfo, string searchPattern, EnumerationOptions enumerationOptions, DirectoryEnumerableOptions enumerableOptions, CancellationToken cancellationToken) {
            if (null == directoryInfo) throw new ArgumentNullException(nameof(directoryInfo));
            return EnumerableAsync
                .For(
                    factory: () => directoryInfo.EnumerateDirectories(
                        searchPattern: searchPattern,
                        enumerationOptions: enumerationOptions),
                    options: enumerableOptions ?? DirectoryEnumerableOptions.Default)
                .Enumerate(
                    cancellationToken);
        }

        public static IAsyncEnumerable<FileSystemInfo> EnumerateFileSystemInfosAsync(this DirectoryInfo directoryInfo, string searchPattern, EnumerationOptions enumerationOptions, DirectoryEnumerableOptions enumerableOptions, CancellationToken cancellationToken) {
            if (null == directoryInfo) throw new ArgumentNullException(nameof(directoryInfo));
            return EnumerableAsync
                .For(
                    factory: () => directoryInfo.EnumerateFileSystemInfos(
                        searchPattern: searchPattern,
                        enumerationOptions: enumerationOptions),
                    options: enumerableOptions ?? DirectoryEnumerableOptions.Default)
                .Enumerate(
                    cancellationToken);
        }

        public static async Task<DirectoryInfo> TryNewAsync(string path, CancellationToken cancellationToken) {
            return await Async.With(cancellationToken).Run(() => {
                try {
                    return new DirectoryInfo(path);
                }
                catch {
                    return null;
                }
            });
        }

        public static async Task<bool> ExistsAsync(this DirectoryInfo directoryInfo, CancellationToken cancellationToken) {
            if (null == directoryInfo) throw new ArgumentNullException(nameof(directoryInfo));
            return await Async.With(cancellationToken).Run(() => directoryInfo.Exists);
        }

        public static Task<DirectoryInfo> ParentAsync(this DirectoryInfo directoryInfo, CancellationToken cancellationToken) {
            if (null == directoryInfo) throw new ArgumentNullException(nameof(directoryInfo));
            return Async.Run(cancellationToken, () => directoryInfo.Parent);
        }

        public static async IAsyncEnumerable<FileSystemInfo> RecurseInfosAsync(this DirectoryInfo directoryInfo, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (null == directoryInfo) throw new ArgumentNullException(nameof(directoryInfo));
            var infos = directoryInfo.EnumerateFileSystemInfosAsync(
                searchPattern: "*",
                enumerationOptions: new EnumerationOptions {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false,
                    ReturnSpecialDirectories = false
                },
                enumerableOptions: new DirectoryEnumerableOptions {
                    Delay = new EnumerableDelayOptions {
                        Limit = 10,
                        Milliseconds = 50,
                        Threshold = 100
                    },
                    Mode = EnumerableMode.Channel
                },
                cancellationToken);

            await foreach (var info in infos) {
                if (cancellationToken.IsCancellationRequested) {
                    break;
                }
                yield return info;

                if (info is DirectoryInfo directory) {
                    await foreach (var item in directory.RecurseInfosAsync(cancellationToken)) {
                        if (cancellationToken.IsCancellationRequested) {
                            break;
                        }
                        yield return item;
                    }
                }
            }
        }

        public static async IAsyncEnumerable<FileInfo> RecurseFilesAsync(this DirectoryInfo directoryInfo, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (null == directoryInfo) throw new ArgumentNullException(nameof(directoryInfo));
            await foreach (var info in directoryInfo.RecurseInfosAsync(cancellationToken)) {
                if (info is FileInfo file) {
                    yield return file;
                }
            }
        }

        public static async IAsyncEnumerable<DirectoryInfo> RecurseDirectoriesAsync(this DirectoryInfo directoryInfo, [EnumeratorCancellation] CancellationToken cancellationToken) {
            if (null == directoryInfo) throw new ArgumentNullException(nameof(directoryInfo));
            await foreach (var info in directoryInfo.RecurseInfosAsync(cancellationToken)) {
                if (info is DirectoryInfo directory) {
                    yield return directory;
                }
            }
        }
    }
}
