using Brows.Operations;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Composition;

public interface IDeleteFilesInDirectory : IExport {
    Task<bool> Work(IEnumerable<string> files, string directory, IDeleteFilesInDirectoryOptions options, IOperationProgress progress, CancellationToken token);
}
