using Brows.SSH;
using Domore.Runtime.Win32;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class SSHEntryInfoIcon : ISSHEntryInfoIcon {
        public async Task<bool> Work(SSHFileInfo info, Action<object> set, CancellationToken token) {
            if (null == info) return false;
            if (null == set) return false;
            var task = info.Kind switch {
                SSHEntryKind.File => Win32Icon.Load(Path.GetExtension(info.Name), token),
                SSHEntryKind.Directory => Win32Icon.Load(SHSTOCKICONID.FOLDER, token),
                _ => Win32Icon.Load(SHSTOCKICONID.DOCNOASSOC, token)
            };
            set(await task.ConfigureAwait(false));
            return true;
        }
    }
}
