using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class DropDirectoryInfoData : IDropDirectoryInfoData {
        public Task<bool> Work(DirectoryInfo directoryInfo, IPanelDrop data, IOperationProgress progress, CancellationToken token) {
            if (null == data) throw new ArgumentNullException(nameof(data));
            if (null == directoryInfo) throw new ArgumentNullException(nameof(directoryInfo));
            var op = new Win32FileOperation(directoryInfo.FullName) {
                CopyFiles = data.CopyFiles.Select(f => new Win32FileOperation.CopyFile { Path = f }).ToList(),
                MoveFiles = data.MoveFiles.Select(f => new Win32FileOperation.MoveFile { Path = f }).ToList()
            };
            return op.Operate(progress, token);
        }
    }
}
