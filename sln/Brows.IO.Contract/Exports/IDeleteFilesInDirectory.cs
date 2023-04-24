using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IDeleteFilesInDirectory : IExport {
        Task<bool> Work(IEnumerable<string> files, string directory, IDeleteFilesInDirectoryOptions options, IOperationProgress progress, CancellationToken token);
    }
}
