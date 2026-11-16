using Brows.Operations;
using Brows.Win32;
using Brows.Win32.Win32FileOperations;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Composition;

internal sealed class DeleteFilesInDirectory : IDeleteFilesInDirectory {
    [ImportRequired]
    internal IWin32InteropServices Service { get; set; }

    public Task<bool> Work(IEnumerable<string> files,
                                 string directory,
                                 IDeleteFilesInDirectoryOptions options,
                                 IOperationProgress progress,
                                 CancellationToken token) {
        var
        op = Service.FileOperation(directory);
        op.DeleteFiles = files
            ?.Select(name => new DeleteFile { Name = name })
            ?.ToList();
        op.RecycleOnDelete = options?.Unrecoverable == true
            ? false
            : true;
        return op.Operate(progress, token);
    }
}
