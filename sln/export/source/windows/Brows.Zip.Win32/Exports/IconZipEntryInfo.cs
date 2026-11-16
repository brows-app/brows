using Brows.Win32;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Exports;

internal sealed class IconZipEntryInfo : IIconZipEntryInfo {
    [ImportRequired]
    internal IWin32WindowsServices Services { get; set; }

    public async Task<object> Icon(ZipEntryInfo zipEntryInfo, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(zipEntryInfo);
        if (token.IsCancellationRequested) {
            token.ThrowIfCancellationRequested();
        }
        return zipEntryInfo.Kind switch {
            ZipEntryKind.File => await Services.GetIconSource(
                Path.GetExtension(zipEntryInfo.Name.Original),
                FileAttributes.None,
                token),
            ZipEntryKind.Path => await Services.GetIconSource(
                Win32StockIcon.Folder,
                token),
            _ => null,
        };
    }
}
