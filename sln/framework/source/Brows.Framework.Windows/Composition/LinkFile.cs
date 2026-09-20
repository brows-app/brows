using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition;

internal sealed class LinkFile : ILinkFile {
    async Task<bool> ILinkFile.Work(string file, Action<string> set, CancellationToken token) {
        if (set == null) {
            return false;
        }
        var resolve = await Win32FileLink.Resolve(file, token);
        if (resolve == null) {
            return false;
        }
        set(resolve);
        return true;
    }
}
