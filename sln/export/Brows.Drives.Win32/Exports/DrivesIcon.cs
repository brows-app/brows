using Domore.Runtime.Win32;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class DrivesIcon : IDrivesIcon {
        public async Task<bool> Work(IDrives drives, Action<object> set, CancellationToken token) {
            if (set == null) {
                return false;
            }
            var stock = SHSTOCKICONID.DESKTOPPC;
            var icon = await Win32Icon.Load(stock, token);
            set(icon);
            return true;
        }
    }
}
