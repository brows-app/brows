using Domore.Runtime.Win32;
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
            bool ret(object obj) {
                set(obj);
                return true;
            }
            var result = await Win32Overlay.Load(fileSystemInfo.FullName, token).ConfigureAwait(false);
            if (result != null) {
                return ret(result);
            }
            if (fileSystemInfo is DirectoryInfo directory) {
                if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                    return ret(await Win32Icon.Load(SHSTOCKICONID.LINK, token).ConfigureAwait(false));
                }
            }
            return false;
        }
    }
}
