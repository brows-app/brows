using Brows.Url.Ftp;
using Domore.Runtime.Win32;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class FtpEntryIcon : IFtpEntryIcon {
        public async Task<bool> Work(FtpListingInfo info, Action<object> set, CancellationToken token) {
            if (null == info) return false;
            if (null == set) return false;
            var task = default(Task<object>);
            var flags = info.Flags;
            if (flags.HasFlag(FtpListingInfoFlags.Directory)) {
                task = Win32Icon.Load(SHSTOCKICONID.FOLDER, token);
            }
            if (flags.HasFlag(FtpListingInfoFlags.File)) {
                task = Win32Icon.Load(Path.GetExtension(info.Name), token);
            }
            if (task == null) {
                return false;
            }
            set(await task);
            return true;
        }
    }
}
