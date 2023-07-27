using Brows.SSH;
using Domore.Runtime.Win32;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class SSHEntryInfoIcon : ISSHEntryInfoIcon {
        public async Task<bool> Work(SSHEntryInfo sshEntryInfo, Action<object> set, CancellationToken token) {
            if (null == sshEntryInfo) return false;
            if (null == set) return false;
            switch (sshEntryInfo.Kind) {
                case SSHEntryKind.File:
                    set(await Win32Icon.Load(Path.GetExtension(sshEntryInfo.Name), token));
                    break;
                case SSHEntryKind.Directory:
                    set(await Win32Icon.Load(SHSTOCKICONID.FOLDER, token));
                    break;
                default:
                    set(await Win32Icon.Load(SHSTOCKICONID.DOCNOASSOC, token));
                    break;
            }
            return true;
        }
    }
}
