﻿using Domore.Runtime.Win32;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class IconZipFileInfo : IIconZipFileInfo {
        public async Task<object> Icon(FileInfo zipFileInfo, CancellationToken cancellationToken) {
            return await Win32Icon.Load(SHSTOCKICONID.ZIPFILE, cancellationToken);
        }
    }
}
