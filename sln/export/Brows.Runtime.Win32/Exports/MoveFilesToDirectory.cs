using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class MoveFilesToDirectory : IMoveFilesToDirectory {
        public async Task<bool> Work(IEnumerable<string> files, string directory, IOperationProgress progress, CancellationToken token) {
            var list = files?.Where(file => file != null)?.ToList();
            if (list == null) return false;
            if (list.Count == 0) return false;
            var op = new Win32FileOperation(directory) {
                MoveFiles = list
                    .Select(file => new Win32FileOperation.MoveFile { Path = file })
                    .ToList()
            };
            return await op.Operate(progress, token);
        }
    }
}
