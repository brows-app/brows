using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    using IO;

    internal static class FileSystemEntryExtension {
        public static async Task<bool> Exists(this FileSystemEntry entry, CancellationToken cancellationToken) {
            var info = await FileSystem.InfoExists(entry?.Info?.FullName, cancellationToken);
            if (info is FileInfo) {
                return entry?.Info is FileInfo;
            }
            if (info is DirectoryInfo) {
                return entry?.Info is DirectoryInfo;
            }
            return false;
        }
    }
}
