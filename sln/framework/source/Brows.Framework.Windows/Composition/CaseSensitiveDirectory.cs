using Brows.Win32;
using System;
using System.Threading.Tasks;

namespace Brows.Composition;

internal sealed class CaseSensitiveDirectory : ICaseSensitiveDirectory {
    [ImportRequired]
    internal IWin32InteropServices Service { get; set; }

    public async Task<bool> Work(string directory, Action<bool> set, CancellationToken token) {
        if (set == null) {
            return false;
        }
        var result = await Service.PathIsCaseSensitive(directory, token);
        set(result);
        return true;
    }
}
