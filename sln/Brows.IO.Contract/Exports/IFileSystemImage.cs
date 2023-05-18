using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IFileSystemImage : IExport {
        Task<bool> Work(FileSystemInfo info, int width, int height, Action<object> set, CancellationToken token);
    }
}
