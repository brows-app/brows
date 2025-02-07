using Domore.Runtime.Win32;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class FtpProviderIcon : IFtpProviderIcon {
        public async Task<bool> Work(Action<object> set, CancellationToken token) {
            if (null == set) return false;
            set(await Win32Icon.Load(SHSTOCKICONID.FOLDER, token));
            return true;
        }
    }
}
