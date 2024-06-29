using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class ThumbnailFileSystemInfo : IThumbnailFileSystemInfo {
        public async Task<bool> Work(FileSystemInfo fileSystemInfo, int width, int height, Action<object> set, CancellationToken token) {
            if (set == null) return false;
            if (fileSystemInfo == null) {
                return false;
            }
            var path = fileSystemInfo.FullName;
            var result = await Win32Thumbnail.GetImageSource(path, width, height, token).ConfigureAwait(false);
            if (result == null) {
                return false;
            }
            set(result);
            return true;
        }
    }
}
