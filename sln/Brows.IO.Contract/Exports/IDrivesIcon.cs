using System;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.Exports {
    public interface IDrivesIcon : IExport {
        Task<bool> Work(IDrives drives, Action<object> set, CancellationToken token);
    }
}
