using Brows.Win32;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Exports;

internal sealed class IconZipFileInfo : IIconZipFileInfo {
    [ImportRequired]
    internal IWin32WindowsServices Services { get; set; }

    public async Task<object> Icon(FileInfo zipFileInfo, CancellationToken cancellationToken) {
        return await Services.GetIconSource(Win32StockIcon.ZipFile, cancellationToken);
    }
}
