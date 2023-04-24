using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IOverlayFileSystemInfo : IExport {
        Task<bool> Work(FileSystemInfo fileSystemInfo, Action<object> set, CancellationToken token);
    }
}
