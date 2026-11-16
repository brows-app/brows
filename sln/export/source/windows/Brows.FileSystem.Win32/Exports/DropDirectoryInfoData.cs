using Brows.Win32;
using Brows.Win32.Win32FileOperations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Brows.Exports;

internal sealed class DropDirectoryInfoData : IDropDirectoryInfoData {
    [ImportRequired]
    internal IWin32InteropServices Service { get; set; }

    public Task<bool> Work(DirectoryInfo directoryInfo,
                           IPanelDrop data,
                           IOperationProgress progress,
                           CancellationToken token) {
        if (data is null) {
            throw new ArgumentNullException(nameof(data));
        }
        if (directoryInfo is null) {
            throw new ArgumentNullException(nameof(directoryInfo));
        }
        var
        op = Service.FileOperation(directoryInfo.FullName);
        op.CopyFiles = [.. data.CopyFiles.Select(f => new CopyFile { Path = f })];
        op.MoveFiles = [.. data.MoveFiles.Select(f => new MoveFile { Path = f })];
        return op.Operate(progress, token);
    }
}
