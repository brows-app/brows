using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IDropDirectoryInfoData : IExport {
        Task<bool> Work(DirectoryInfo directoryInfo, IPanelDrop data, IOperationProgress progress, CancellationToken cancellationToken);
    }
}
