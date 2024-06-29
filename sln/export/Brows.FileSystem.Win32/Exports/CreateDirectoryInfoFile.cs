using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class CreateDirectoryInfoFile : ICreateDirectoryInfoFile {
        public Task<bool> Work(DirectoryInfo directoryInfo, string file, IOperationProgress progress, CancellationToken token) {
            if (directoryInfo != null) {
                var
                op = new Win32FileOperation(directoryInfo.FullName);
                op.CreateFiles.Add(new() {
                    Name = file,
                    Attributes = FileAttributes.Normal
                });
                return op.Operate(progress, token);
            }
            return Task.FromResult(false);
        }
    }
}
