using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IMoveFilesToDirectory : IExport {
        Task<bool> Work(IEnumerable<string> files, string directory, IOperationProgress progress, CancellationToken token);
    }
}
