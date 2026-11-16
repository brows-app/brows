using Brows.Win32;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Exports;
internal sealed class IconTarEntryInfo : IIconTarEntryInfo {
    [ImportRequired]
    internal IWin32WindowsServices Services { get; set; }

    public async Task<object> Icon(TarEntryInfo tarEntryInfo, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(tarEntryInfo);
        if (token.IsCancellationRequested) {
            token.ThrowIfCancellationRequested();
        }
        return tarEntryInfo.Kind switch {
            TarEntryKind.File => await Services.GetIconSource(
                Path.GetExtension(tarEntryInfo.Name.Original),
                FileAttributes.None,
                token),
            TarEntryKind.Path => await Services.GetIconSource(
                Win32StockIcon.Folder,
                token),
            _ => null,
        };
    }
}
