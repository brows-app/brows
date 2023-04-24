using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface ICreateDirectoryInfoFile : IExport {
        Task<bool> Work(DirectoryInfo directoryInfo, string file, IOperationProgress operationProgress, CancellationToken cancellationToken);
    }
}
