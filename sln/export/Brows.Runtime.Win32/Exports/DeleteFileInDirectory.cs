using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class DeleteFileInDirectory : IDeleteFilesInDirectory {
        public async Task<bool> Work(IEnumerable<string> files, string directory, IDeleteFilesInDirectoryOptions options, IOperationProgress progress, CancellationToken token) {
            var op = new Win32FileOperation(directory) {
                DeleteFiles = files?
                    .Select(name => new Win32FileOperation.DeleteFile { Name = name })?
                    .ToList(),
                RecycleOnDelete = options?.Unrecoverable == true
                    ? false
                    : true
            };
            return await op.Operate(progress, token);
        }
    }
}
