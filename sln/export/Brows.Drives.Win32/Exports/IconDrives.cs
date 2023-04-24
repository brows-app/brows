using Domore.Runtime.Win32;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class IconDrives : IIconDrives {
        public async Task<object> Icon(Drives drives, CancellationToken cancellationToken) {
            var stock = SHSTOCKICONID.DESKTOPPC;
            var icon = await Win32Icon.Load(stock, cancellationToken);
            return icon;
        }
    }
}
