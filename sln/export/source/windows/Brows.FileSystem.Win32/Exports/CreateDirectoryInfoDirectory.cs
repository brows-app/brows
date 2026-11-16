using Brows.Win32;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Exports;

internal sealed class CreateDirectoryInfoDirectory : ICreateDirectoryInfoDirectory {
    [ImportRequired]
    internal IWin32InteropServices Service { get; set; }

    public Task<bool> Work(DirectoryInfo directoryInfo,
                           string directory,
                           IOperationProgress progress,
                           CancellationToken token) {
        if (directoryInfo is not null) {
            var
            op = Service.FileOperation(directoryInfo.FullName);
            op.CreateFiles.Add(new() {
                Name = directory,
                Attributes = FileAttributes.Directory
            });
            return op.Operate(progress, token);
        }
        return Task.FromResult(false);
    }
}
