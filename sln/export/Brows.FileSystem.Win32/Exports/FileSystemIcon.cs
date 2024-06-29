using Domore.Runtime.Win32;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class FileSystemIcon : IFileSystemIcon {
        public async Task<bool> Work(FileSystemInfo info, IFileSystemIconHint hint, Action<object> set, CancellationToken token) {
            if (set == null) return false;
            if (info == null) return false;
            var result = default(object);
            if (info is DirectoryInfo) {
                var icon =
                    hint?.DirectoryOpen == true ? SHSTOCKICONID.FOLDEROPEN :
                    hint?.DirectoryOpen == false ? SHSTOCKICONID.FOLDERBACK :
                    SHSTOCKICONID.FOLDER;
                result = await Win32Icon.Load(icon, token).ConfigureAwait(false);
            }
            else {
                result = await Win32Icon.Load(info.FullName, token).ConfigureAwait(false);
            }
            if (result == null) {
                return false;
            }
            set(result);
            return true;
        }
    }
}
