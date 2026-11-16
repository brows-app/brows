using Brows.Win32;
using System.Threading.Tasks;

namespace Brows.Composition;

internal sealed class OpenFileWith : IOpenFileWith {
    [ImportRequired]
    internal IWin32InteropServices Service { get; set; }

    public async Task<bool> Work(string file, string with, CancellationToken token) {
        await Service.ExecuteDefault(file, with, token);
        return true;
    }
}
