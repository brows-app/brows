using Domore.Runtime.Win32;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class IconFileSystemInfo : IIconFileSystemInfo {
        public async Task<bool> Work(FileSystemInfo fileSystemInfo, Action<object> set, CancellationToken token) {
            if (fileSystemInfo == null) return false;
            if (set == null) return false;
            var result = fileSystemInfo is DirectoryInfo
                ? await Win32Icon.Load(SHSTOCKICONID.FOLDER, token)
                : fileSystemInfo is FileInfo
                    ? await Win32Icon.Load(fileSystemInfo.FullName, token)
                    : null;
            if (result == null) {
                return false;
            }
            set(result);
            return true;
        }
    }
}
