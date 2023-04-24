using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Data {
    internal abstract class DirectoryAggregateData : FileSystemInfoData<long?> {
        private static readonly string SearchPattern = "*";
        private static readonly EnumerationOptions EnumerationOptions = new EnumerationOptions {
            AttributesToSkip = 0,
            IgnoreInaccessible = true,
            MaxRecursionDepth = int.MaxValue,
            RecurseSubdirectories = true,
            ReturnSpecialDirectories = false
        };

        protected abstract long Data(FileSystemInfo info);

        protected sealed override async Task<long?> GetValue(FileSystemEntry entry, Action<long?> progress, CancellationToken token) {
            if (null == entry) throw new ArgumentNullException(nameof(entry));
            if (null == progress) throw new ArgumentNullException(nameof(progress));
            var info = entry.Info;
            if (info is DirectoryInfo directory) {
                var value = 0L;
                progress(value);
                try {
                    return await Task.Run(cancellationToken: token, function: () => {
                        var items = directory.EnumerateFileSystemInfos(SearchPattern, EnumerationOptions);
                        foreach (var item in items) {
                            token.ThrowIfCancellationRequested();
                            progress(value += Data(item));
                        }
                        return value;
                    });
                }
                catch {
                    progress(null);
                    throw;
                }
            }
            return null;
        }
    }
}
