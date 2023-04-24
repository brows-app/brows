using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IIconFileSystemInfo : IExport {
        Task<bool> Work(FileSystemInfo fileSystemInfo, Action<object> set, CancellationToken token);
    }
}
