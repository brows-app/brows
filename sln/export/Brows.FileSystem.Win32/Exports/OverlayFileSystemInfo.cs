using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class OverlayFileSystemInfo : IOverlayFileSystemInfo {
        public async Task<bool> Work(FileSystemInfo fileSystemInfo, Action<object> set, CancellationToken token) {
            if (fileSystemInfo == null) {
                return false;
            }
            if (set == null) {
                return false;
            }
            var result = await Win32Overlay.Load(fileSystemInfo.FullName, token);
            if (result == null) {
                return false;
            }
            set(result);
            return true;
        }
    }
}
