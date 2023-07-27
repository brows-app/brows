using Domore.Runtime.Win32;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class IconZipEntryInfo : IIconZipEntryInfo {
        public async Task<object> Icon(ZipEntryInfo zipEntryInfo, CancellationToken token) {
            if (null == zipEntryInfo) throw new ArgumentNullException(nameof(zipEntryInfo));
            switch (zipEntryInfo.Kind) {
                case ZipEntryKind.File:
                    return await Win32Icon.Load(Path.GetExtension(zipEntryInfo.Name.Original), token);
                case ZipEntryKind.Path:
                    return await Win32Icon.Load(SHSTOCKICONID.FOLDER, token);
                default:
                    return null;
            }
        }
    }
}
