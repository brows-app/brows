using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class FileSystemImage : IFileSystemImage {
        public async Task<bool> Work(FileSystemInfo info, int width, int height, Action<object> set, CancellationToken token) {
            if (set == null) return false;
            if (info == null) return false;
            var path = info.FullName;
            var result = await Win32Thumbnail.GetImageSource(path, width, height, token);
            if (result == null) {
                return false;
            }
            set(result);
            return true;
        }
    }
}
