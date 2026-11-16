using Brows.Win32;
using System;
using System.Threading.Tasks;

namespace Brows.Composition;

internal sealed class LinkFile : ILinkFile {
    [ImportRequired]
    internal IWin32InteropServices Service { get; set; }

    async Task<bool> ILinkFile.Work(string file, Action<string> set, CancellationToken token) {
        if (set is null) {
            return false;
        }
        var resolved = await Service.GetLinkPath(file, token);
        if (resolved is null) {
            return false;
        }
        set(resolved);
        return true;
    }
}
