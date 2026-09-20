using Brows.Runtime.Win32;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Exports;

internal sealed class IconZipEntryInfo : IIconZipEntryInfo {
    public Task<object> Icon(ZipEntryInfo zipEntryInfo, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(zipEntryInfo);
        if (token.IsCancellationRequested) {
            return Task.FromCanceled<object>(token);
        }
        return zipEntryInfo.Kind switch {
            ZipEntryKind.File => Win32Icon.Load(Path.GetExtension(zipEntryInfo.Name.Original), token),
            ZipEntryKind.Path => Win32Icon.Load(SHSTOCKICONID.FOLDER, token),
            _ => Task.FromResult<object>(null),
        };
    }
}
