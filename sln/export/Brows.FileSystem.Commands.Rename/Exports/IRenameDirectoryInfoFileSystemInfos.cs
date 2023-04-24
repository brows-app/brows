using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IRenameDirectoryInfoFileSystemInfos : IExport {
        Task<bool> Work(DirectoryInfo directoryInfo, IReadOnlyDictionary<string, string> rename, IOperationProgress progress, CancellationToken token);
    }
}
