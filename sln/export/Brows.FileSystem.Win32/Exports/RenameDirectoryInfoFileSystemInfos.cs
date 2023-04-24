using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class RenameDirectoryInfoFileSystemInfos : IRenameDirectoryInfoFileSystemInfos {
        public async Task<bool> Work(DirectoryInfo directoryInfo, IReadOnlyDictionary<string, string> rename, IOperationProgress progress, CancellationToken token) {
            if (directoryInfo == null) return false;
            if (rename == null) return false;
            var op = new Win32FileOperation(directoryInfo.FullName) {
                RenameFiles = rename
                    .Select(item => new Win32FileOperation.RenameFile { NewName = item.Value, OldName = item.Key })
                    .ToList()
            };
            return await op.Operate(progress, token);
        }
    }
}
