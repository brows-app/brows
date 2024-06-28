using Domore.IO;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows {
    internal static class FileSystemEntryExtension {
        public static async Task<bool> Exists(this FileSystemEntry entry, CancellationToken cancellationToken) {
            var info = await FileSystemTask.Existing(entry?.Info?.FullName, cancellationToken).ConfigureAwait(false);
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
