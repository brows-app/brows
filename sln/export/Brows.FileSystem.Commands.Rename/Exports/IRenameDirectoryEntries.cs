using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IRenameDirectoryEntries : IExport {
        Task<bool> Work(string directory, IReadOnlyDictionary<string, string> entries, IOperationProgress progress, CancellationToken token);
    }
}
