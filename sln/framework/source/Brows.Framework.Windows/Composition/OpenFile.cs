using Brows.Win32;
using System.Threading.Tasks;

namespace Brows.Composition;

internal sealed class OpenFile : IOpenFile {
    [ImportRequired]
    internal IWin32InteropServices Service { get; set; }

    public async Task<bool> Work(string file, CancellationToken token) {
        await Service.ExecuteDefault(file, token);
        return true;
    }
}
