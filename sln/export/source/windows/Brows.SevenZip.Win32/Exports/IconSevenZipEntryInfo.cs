using Brows.Win32;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Exports;
internal sealed class IconSevenZipEntryInfo : IIconSevenZipEntryInfo {
    [ImportRequired]
    internal IWin32WindowsServices Services { get; set; }

    public async Task<object> Icon(SevenZipEntryInfo sevenZipEntryInfo, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(sevenZipEntryInfo);
        if (token.IsCancellationRequested) {
            token.ThrowIfCancellationRequested();
        }
        return sevenZipEntryInfo.Kind switch {
            SevenZipEntryKind.File => await Services.GetIconSource(
                Path.GetExtension(sevenZipEntryInfo.Name.Original),
                FileAttributes.None,
                token),
            SevenZipEntryKind.Path => await Services.GetIconSource(
                Win32StockIcon.Folder,
                token),
            _ => null,
        };
    }
}
