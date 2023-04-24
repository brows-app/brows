using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class CreateDirectoryInfoDirectory : ICreateDirectoryInfoDirectory {
        public async Task<bool> Work(DirectoryInfo directoryInfo, string directory, IOperationProgress progress, CancellationToken token) {
            if (directoryInfo == null) return false;
            var
            op = new Win32FileOperation(directoryInfo.FullName);
            op.CreateFiles.Add(new() {
                Name = directory,
                Attributes = FileAttributes.Directory
            });
            return await op.Operate(progress, token);
        }
    }
}
