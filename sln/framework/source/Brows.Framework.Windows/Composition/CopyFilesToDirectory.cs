using Brows.Operations;
using Brows.Win32;
using Brows.Win32.Win32FileOperations;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Composition;

internal sealed class CopyFilesToDirectory : ICopyFilesToDirectory {
    [ImportRequired]
    internal IWin32InteropServices Service { get; set; }

    public async Task<bool> Work(IEnumerable<string> files, string toDirectory,
                                 IOperationProgress progress, CancellationToken token) {
        var list = files?.ToList();
        if (list is null) {
            return false;
        }
        if (list.Count == 0) {
            return false;
        }
        var groups = list.GroupBy(file => Path.GetDirectoryName(file)).ToList();
        var sameDir = true;
        foreach (var group in groups) {
            var same = await Service.PathsAreEquivalent(toDirectory, group.Key, token);
            if (same != true) {
                sameDir = false;
            }
        }
        var
        op = Service.FileOperation(toDirectory);
        op.CopyFiles = [.. list.Select(path => new CopyFile { Path = path })];
        op.PreserveFileExtensions = sameDir;
        op.RenameOnCollision = sameDir;
        return await op.Operate(progress, token);
    }
}
