using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class CreateDirectoryInfoFile : ICreateDirectoryInfoFile {
        public async Task<bool> Work(DirectoryInfo directoryInfo, string file, IOperationProgress progress, CancellationToken token) {
            if (directoryInfo == null) return false;
            var
            op = new Win32FileOperation(directoryInfo.FullName);
            op.CreateFiles.Add(new() {
                Name = file,
                Attributes = FileAttributes.Normal
            });
            return await op.Operate(progress, token);
        }
    }
}
