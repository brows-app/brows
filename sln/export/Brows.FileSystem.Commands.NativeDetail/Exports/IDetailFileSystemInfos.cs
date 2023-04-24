using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IDetailFileSystemInfos : IExport {
        public Task<bool> Work(IEnumerable<FileSystemInfo> fileSystemInfos, IOperationProgress operationProgress, CancellationToken cancellationToken);
    }
}
