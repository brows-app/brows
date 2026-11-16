using Brows.Win32;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Exports;
internal sealed class IconTarFileInfo : IIconTarFileInfo {
    [ImportRequired]
    internal IWin32WindowsServices Services { get; set; }

    public async Task<object> Icon(FileInfo tarFileInfo, CancellationToken token) {
        ArgumentNullException.ThrowIfNull(tarFileInfo);
        if (token.IsCancellationRequested) {
            token.ThrowIfCancellationRequested();
        }
        return await Services.GetIconSource(
            tarFileInfo.Extension,
            tarFileInfo.Attributes,
            token);
    }
}
