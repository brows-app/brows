using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IO {
    using Collections.Generic;
    using Threading.Tasks;

    public static class DirectoryAsync {
        public static Task Delete(string path, bool recursive, CancellationToken cancellationToken) {
            return Async.Run(cancellationToken, () => Directory.Delete(path, recursive));
        }

        public static Task<bool> Exists(string path, CancellationToken cancellationToken) {
            return Async.Run(cancellationToken, () => Directory.Exists(path));
        }

        public static async Task<DirectoryInfo> CreateDirectory(string path, CancellationToken cancellationToken) {
            var directoryInfo = await Async.Run(cancellationToken, () => Directory.CreateDirectory(path));
            for (; ; ) {
                var exists = await Exists(path, cancellationToken);
                if (exists) break;
                await Task.Delay(1, cancellationToken);
            }
            return directoryInfo;
        }

        public static IAsyncEnumerable<string> EnumerateFiles(string path, string searchPattern, EnumerationOptions enumerationOptions, EnumerableOptions enumerableOptions, CancellationToken cancellationToken) {
            return EnumerableAsync
                .For(
                    factory: () => Directory.EnumerateFiles(
                        path: path,
                        searchPattern: searchPattern,
                        enumerationOptions: enumerationOptions),
                    options: enumerableOptions ?? DirectoryEnumerableOptions.Default)
                .Enumerate(
                    cancellationToken);
        }

        public static IAsyncEnumerable<string> EnumerateDirectories(string path, string searchPattern, EnumerationOptions enumerationOptions, EnumerableOptions enumerableOptions, CancellationToken cancellationToken) {
            return EnumerableAsync
                .For(
                    factory: () => Directory.EnumerateDirectories(
                        path: path,
                        searchPattern: searchPattern,
                        enumerationOptions: enumerationOptions),
                    options: enumerableOptions ?? DirectoryEnumerableOptions.Default)
                .Enumerate(
                    cancellationToken);
        }
    }
}
