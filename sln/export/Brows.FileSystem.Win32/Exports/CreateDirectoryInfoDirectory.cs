using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class CreateDirectoryInfoDirectory : ICreateDirectoryInfoDirectory {
        public Task<bool> Work(DirectoryInfo directoryInfo, string directory, IOperationProgress progress, CancellationToken token) {
            if (directoryInfo != null) {
                var
                op = new Win32FileOperation(directoryInfo.FullName);
                op.CreateFiles.Add(new() {
                    Name = directory,
                    Attributes = FileAttributes.Directory
                });
                return op.Operate(progress, token);
            }
            return Task.FromResult(false);
        }
    }
}
