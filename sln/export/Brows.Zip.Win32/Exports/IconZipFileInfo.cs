using Domore.Runtime.Win32;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class IconZipFileInfo : IIconZipFileInfo {
        public Task<object> Icon(FileInfo zipFileInfo, CancellationToken cancellationToken) {
            return Win32Icon.Load(SHSTOCKICONID.ZIPFILE, cancellationToken);
        }
    }
}
