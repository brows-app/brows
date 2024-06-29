using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class MoveFilesToDirectory : IMoveFilesToDirectory {
        public Task<bool> Work(IEnumerable<string> files, string directory, IOperationProgress progress, CancellationToken token) {
            var list = files?.Where(file => file != null)?.ToList();
            if (list == null) return Task.FromResult(false);
            if (list.Count == 0) return Task.FromResult(false);
            var op = new Win32FileOperation(directory) {
                MoveFiles = list
                    .Select(file => new Win32FileOperation.MoveFile { Path = file })
                    .ToList()
            };
            return op.Operate(progress, token);
        }
    }
}
