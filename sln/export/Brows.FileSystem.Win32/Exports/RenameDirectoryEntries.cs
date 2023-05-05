using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    internal sealed class RenameDirectoryEntries : IRenameDirectoryEntries {
        public async Task<bool> Work(string directory, IReadOnlyDictionary<string, string> entries, IOperationProgress progress, CancellationToken token) {
            if (entries == null) return false;
            if (entries.Count == 0) return false;
            var op = new Win32FileOperation(directory) {
                RenameFiles = entries
                    .Select(entry => new Win32FileOperation.RenameFile { NewName = entry.Value, OldName = entry.Key })
                    .ToList()
            };
            return await op.Operate(progress, token);
        }
    }
}
