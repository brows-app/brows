using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal static class FileSystemInfoExtension {
        private static readonly FileSystemInfoCanon Canon = new FileSystemInfoCanon();

        public static Task<string> GetCanonicalFullNameAsync(this FileSystemInfo fileSystemInfo, CancellationToken cancellationToken) {
            return Canon.GetCanonicalFullName(fileSystemInfo, cancellationToken);
        }
    }
}
