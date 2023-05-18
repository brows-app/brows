using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IFileSystemIcon : IExport {
        Task<bool> Work(FileSystemInfo info, IFileSystemIconHint hint, Action<object> set, CancellationToken token);
    }
}
