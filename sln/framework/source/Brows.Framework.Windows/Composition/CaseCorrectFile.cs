using Brows.Win32;
using System;
using System.Threading.Tasks;

namespace Brows.Composition;

internal sealed class CaseCorrectFile : ICaseCorrectFile {
    [ImportRequired]
    internal IWin32InteropServices Services { get; set; }

    public async Task<bool> Work(string file, Action<string> set, CancellationToken token) {
        if (set == null) {
            return false;
        }
        var result = await Services.GetStoredPath(file, token);
        set(result);
        return true;
    }
}
