using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface ICreateDirectoryInfoDirectory : IExport {
        Task<bool> Work(DirectoryInfo directoryInfo, string directory, IOperationProgress progress, CancellationToken cancellationToken);
    }
}
