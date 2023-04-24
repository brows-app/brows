using Domore.Runtime.Win32;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class IconZipEntryInfo : IIconZipEntryInfo {
        public async Task<object> Icon(ZipEntryInfo zipEntryInfo, CancellationToken cancellationToken) {
            if (null == zipEntryInfo) throw new ArgumentNullException(nameof(zipEntryInfo));
            switch (zipEntryInfo.Kind) {
                case ZipEntryKind.File:
                    return await Win32Icon.Load(Path.GetExtension(zipEntryInfo.Name.Original), cancellationToken);
                case ZipEntryKind.Path:
                    return await Win32Icon.Load(SHSTOCKICONID.FOLDER, cancellationToken);
                default:
                    return null;
            }
        }
    }
}
