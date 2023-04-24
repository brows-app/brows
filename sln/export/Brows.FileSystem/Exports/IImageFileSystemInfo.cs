using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IImageFileSystemInfo : IExport {
        Task<bool> Work(FileSystemInfo fileSystemInfo, int width, int height, Action<object> set, CancellationToken token);
    }
}
