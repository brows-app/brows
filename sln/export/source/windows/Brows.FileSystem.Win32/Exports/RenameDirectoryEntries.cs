using Brows.Win32;
using Brows.Win32.Win32FileOperations;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Exports;

internal sealed class RenameDirectoryEntries : IRenameDirectoryEntries {
    [ImportRequired]
    internal IWin32InteropServices Service { get; set; }

    public async Task<bool> Work(string directory,
                                 IReadOnlyDictionary<string, string> entries,
                                 IOperationProgress progress,
                                 CancellationToken token) {
        if (entries is null) {
            return false;
        }
        if (entries.Count == 0) {
            return false;
        }
        var op = Service.FileOperation(directory);
        op.RenameFiles = [.. entries.Select(entry => new RenameFile { NewName = entry.Value, OldName = entry.Key })];
        return await op.Operate(progress, token);
    }
}
