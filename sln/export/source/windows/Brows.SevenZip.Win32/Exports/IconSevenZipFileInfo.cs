using Brows.Win32;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Exports;
internal sealed class IconSevenZipFileInfo : IIconSevenZipFileInfo {
    [ImportRequired]
    internal IWin32WindowsServices Services { get; set; }

    public async Task<object> Icon(FileInfo sevenZipFileInfo, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(sevenZipFileInfo);
        if (token.IsCancellationRequested) {
            token.ThrowIfCancellationRequested();
        }
        return await Services.GetIconSource(
            sevenZipFileInfo.Extension,
            sevenZipFileInfo.Attributes,
            token);
    }
}
