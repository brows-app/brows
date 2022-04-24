using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using Threading.Tasks;

    internal static class FileSystemInfoExtension {
        private static readonly FileSystemInfoCanon Canon = new FileSystemInfoCanon();

        public static Task<string> GetCanonicalFullNameAsync(this FileSystemInfo fileSystemInfo, CancellationToken cancellationToken) {
            return Canon.GetCanonicalFullName(fileSystemInfo, cancellationToken);
        }

        public static Task RefreshAsync(this FileSystemInfo fileSystemInfo, CancellationToken cancellationToken) {
            if (null == fileSystemInfo) throw new ArgumentNullException(nameof(fileSystemInfo));
            return Async.Run(cancellationToken, fileSystemInfo.Refresh);
        }

        public static Task<bool> ExistsAsync(this FileSystemInfo fileSystemInfo, CancellationToken cancellationToken) {
            if (null == fileSystemInfo) throw new ArgumentNullException(nameof(fileSystemInfo));
            return Async.Run(cancellationToken, () => fileSystemInfo.Exists);
        }
    }
}
