using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class CopyFilesToDirectory : ICopyFilesToDirectory {
        public async Task<bool> Work(IEnumerable<string> files, string toDirectory, IOperationProgress progress, CancellationToken token) {
            var list = files?.ToList();
            if (list == null) return false;
            if (list.Count == 0) {
                return false;
            }
            var groups = files.GroupBy(file => Path.GetDirectoryName(file)).ToList();
            var sameDir = await Task.Run(cancellationToken: token, function: () => {
                foreach (var group in groups) {
                    var same = Win32Path.AreSame(toDirectory, group.Key);
                    if (same == false) {
                        return false;
                    }
                }
                return true;
            });
            var op = new Win32FileOperation(toDirectory) {
                CopyFiles = list
                    .Select(path => new Win32FileOperation.CopyFile { Path = path })
                    .ToList(),
                PreserveFileExtensions = sameDir ? true : null,
                RenameOnCollision = sameDir ? true : null
            };
            return await op.Operate(progress, token);
        }
    }
}
