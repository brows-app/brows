using System;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Exports;

internal sealed class FileSystemImage : IFileSystemImage {
    [ImportRequired]
    internal IFileSystemImageSource ImageSource { get; set; }

    public async Task<bool> Work(FileSystemInfo info, int width, int height, Action<object> set, CancellationToken token) {
        if (set is null) {
            return false;
        }
        if (info is null) {
            return false;
        }
        var source = ImageSource ?? throw new InvalidOperationException();
        var result = await source.Get(info, width, height, token);
        if (result is null) {
            return false;
        }
        set(result);
        return true;
    }
}
