using Brows.Operations;
using Brows.Win32;
using Brows.Win32.Win32FileOperations;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Composition;

internal sealed class MoveFilesToDirectory : IMoveFilesToDirectory {
    [ImportRequired]
    internal IWin32InteropServices Service { get; set; }

    public Task<bool> Work(IEnumerable<string> files, string directory, IOperationProgress progress, CancellationToken token) {
        var list = files?.Where(file => file != null)?.ToList();
        if (list is null) {
            return Task.FromResult(false);
        }
        if (list.Count == 0) {
            return Task.FromResult(false);
        }
        var
        op = Service.FileOperation(directory);
        op.MoveFiles = [.. list.Select(file => new MoveFile { Path = file })];
        return op.Operate(progress, token);
    }
}
