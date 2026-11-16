using Brows.Win32;
using System.IO;
using System.Threading.Tasks;

namespace Brows.Exports;

internal sealed class CreateDirectoryInfoFile : ICreateDirectoryInfoFile {
    [ImportRequired]
    internal IWin32InteropServices Service { get; set; }

    public Task<bool> Work(DirectoryInfo directoryInfo,
                           string file,
                           IOperationProgress progress,
                           CancellationToken token) {
        if (directoryInfo != null) {
            var
            op = Service.FileOperation(directoryInfo.FullName);
            op.CreateFiles.Add(new() {
                Name = file,
                Attributes = FileAttributes.Normal
            });
            return op.Operate(progress, token);
        }
        return Task.FromResult(false);
    }
}
