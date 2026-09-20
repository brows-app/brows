using Brows.Operations;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition;

public interface ICopyFilesToDirectory : IExport {
    Task<bool> Work(IEnumerable<string> files, string directory, IOperationProgress progress, CancellationToken token);
}
